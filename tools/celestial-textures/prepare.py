#!/usr/bin/env python3
"""Turn the supplied planet artwork into the celestial textures the mod ships.

The artwork arrives as one contact sheet of gas giants and example moons, and a set of
ring renders. Neither is usable as a texture as delivered: the giants and moons sit on a
black sheet with no alpha, and half the ring renders have their own filename burned into
the picture. This crops each body out, cuts a circular alpha, flattens the limb darkening
the render baked in -- the mod lights these bodies itself, from wherever the sun actually
is, so a second set of shadows painted into the texture would fight it -- and writes a
manifest of what each texture turned out to be, so the mod can match a texture to the
giant it authored rather than picking blind.

Run it with `make celestial-textures`. It is a build-time step: the outputs are committed
and the game never sees this script.
"""

from __future__ import annotations

import argparse
import json
import math
import zipfile
from collections import deque
from dataclasses import dataclass, asdict
from pathlib import Path

import numpy as np
from PIL import Image

GIANT_SIZE = 512
MOON_SIZE = 64

# A moon drawn on its own arrives far larger than a cell of the contact sheet, and is worth
# keeping at more than sheet size. Still small: a sibling moon is under a degree in the sky.
BIG_MOON_SIZE = 128
RING_SIZE = 512

# How far a pixel must sit from the sheet's background colour to count as part of a body.
BODY_THRESHOLD = 12

# The sheet's own captions: any blob smaller than this is lettering, not a world.
MIN_BODY_PIXELS = 900

# How many of each the mod ships. More is only more variety, and each one costs its own space.
MAX_GIANTS = 12
MAX_MOONS = 54

# Where in the spread of measured radii a body's true edge is taken to be.
RADIUS_PERCENTILE = 12

# Depths tried when telling bodies apart, in pixels.
EROSION_LADDER = (2, 6, 12, 20, 30)

# How far the limb may be divided back up when the baked shading is flattened. The floor is
# deliberately high: pushing a dark rim back up by more than half again amplifies whatever
# faint structure is in it into a fan of spokes. Anything shaded harder than this is not
# corrected at all -- it is past the point of being colour, and gets continued from inside.
MIN_VIGNETTE = 0.70

# Margin left around a cut-out body, as a multiple of its radius.
DISC_MARGIN = 1.06

# How much of the disc is the render's own colour. The rest is its antialiased fringe, where
# the picture has already faded into its background, and is continued from just inside so that
# nothing dark is left to bleed into the rim.
LIMB_TRUST = 0.985


@dataclass
class TextureRecord:
    id: str
    file: str
    kind: str
    red: float
    green: float
    blue: float
    # The body's own disc as a fraction of the texture's half-width; the rest is the margin
    # that keeps a resampled rim from touching the edge.
    disc_fraction: float = 1.0


@dataclass
class RingRecord(TextureRecord):
    # Where the ring sits in its own texture, so the mod can scale it to the ring it authored.
    outer_radius_fraction: float = 0.0
    inner_radius_fraction: float = 0.0
    baked_openness: float = 0.0


def body_mask(sheet: np.ndarray, alpha: np.ndarray | None = None) -> np.ndarray:
    """Which pixels of a contact sheet are a body rather than the paper it sits on.

    Sheets arrive on black, on white, or on nothing at all, depending on what drew them.
    An alpha channel, when there is a real one, says so outright and is believed. Otherwise
    the background is read off the corners -- no sheet yet has had a planet in one -- and a
    pixel counts as a body when it is far enough from that colour.
    """
    if alpha is not None and alpha.min() < 20 and (alpha < 20).mean() > 0.02:
        return alpha > 128

    height, width, _ = sheet.shape
    patch = max(2, min(height, width) // 100)
    corners = np.concatenate([
        sheet[:patch, :patch].reshape(-1, 3),
        sheet[:patch, -patch:].reshape(-1, 3),
        sheet[-patch:, :patch].reshape(-1, 3),
        sheet[-patch:, -patch:].reshape(-1, 3),
    ])
    background = np.median(corners, axis=0)
    distance = np.abs(sheet.astype(float) - background).sum(axis=2)
    return distance > BODY_THRESHOLD * 3


def components(mask: np.ndarray, min_pixels: int) -> list[tuple[int, int, int, int]]:
    """Bounding boxes of the connected blobs in `mask`, largest first."""
    height, width = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    found: list[tuple[int, int, int, int, int]] = []
    for y in range(height):
        for x in range(width):
            if not mask[y, x] or seen[y, x]:
                continue
            queue = deque([(y, x)])
            seen[y, x] = True
            count = 0
            min_x = max_x = x
            min_y = max_y = y
            while queue:
                cy, cx = queue.popleft()
                count += 1
                min_x, max_x = min(min_x, cx), max(max_x, cx)
                min_y, max_y = min(min_y, cy), max(max_y, cy)
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)):
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < height and 0 <= nx < width and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        queue.append((ny, nx))
            if count >= min_pixels:
                found.append((count, min_x, min_y, max_x, max_y))
    found.sort(key=lambda blob: blob[0], reverse=True)
    return [(x0, y0, x1, y1) for _, x0, y0, x1, y1 in found]


def bleed_edges(rgb: np.ndarray, inside: np.ndarray, passes: int = 10) -> np.ndarray:
    """Push the body's own colour out into the transparent margin around it.

    The sheet's background is black, and resizing an image with straight alpha mixes the
    colour channels without regard to it -- so a rim pixel ends up part planet, part
    background, and the planet comes out with a dark line drawn round it. The game's own
    texture filtering does the same thing again at draw time. Neither can be stopped, but
    both stop mattering once there is no black out there to mix in: the margin is filled
    with the colour of the nearest lit pixel, and stays fully transparent while it does it.
    """
    filled = inside.copy()
    out = rgb.copy()
    out[~filled] = 0.0
    for _ in range(passes):
        if filled.all():
            break
        total = np.zeros_like(out)
        count = np.zeros(filled.shape, dtype=float)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)):
            shifted = np.roll(np.roll(out, dy, axis=0), dx, axis=1)
            valid = np.roll(np.roll(filled, dy, axis=0), dx, axis=1)
            total += shifted * valid[..., None]
            count += valid
        grow = (~filled) & (count > 0)
        out[grow] = total[grow] / count[grow, None]
        filled |= grow
    return out


def erode(mask: np.ndarray, radius: int) -> np.ndarray:
    """Shrink a mask by a few pixels, to snap the threads between things that touch."""
    out = mask.copy()
    for _ in range(radius):
        shrunk = out.copy()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            shrunk &= np.roll(np.roll(out, dy, axis=0), dx, axis=1)
        out = shrunk
    return out


def disc_at(mask: np.ndarray, centre_y: float, centre_x: float) -> tuple[float, float, float]:
    """Fit the circle around a centre by walking outward until the mask ends.

    Rays rather than a bounding box, because a sheet's own gutters and captions can touch a
    planet and stretch its box; the median over hundreds of directions ignores the few that
    run off down a gutter.
    """
    height, width = mask.shape
    reach = int(math.hypot(height, width))
    radii = []
    for step in range(360):
        angle = step * math.pi / 180.0
        dy, dx = math.sin(angle), math.cos(angle)
        last = 0.0
        for r in range(1, reach):
            y = int(round(centre_y + dy * r))
            x = int(round(centre_x + dx * r))
            if not (0 <= y < height and 0 <= x < width) or not mask[y, x]:
                break
            last = r
        radii.append(last)

    # Well down the spread, not the median. A clean disc gives the same radius in every
    # direction, and the only thing that can lengthen a ray is the sheet's own paper still
    # attached to the body -- contamination only ever adds, so the truth sits near the bottom.
    # A sheet with paper down one whole side of a planet can spoil a third of its rays.
    return centre_x, centre_y, float(np.percentile(radii, RADIUS_PERCENTILE))


def read_singles(pattern: str, limit: int) -> list[tuple[np.ndarray, tuple[float, float, float]]]:
    """Renders of one body each, in name order.

    A body drawn on its own arrives several times the size of a cell on a contact sheet and
    needs no separating from its neighbours, so these are always preferred. Supply a full set
    and the sheet stops being read for that kind at all.
    """
    found: list[tuple[np.ndarray, tuple[float, float, float]]] = []
    for path in sorted(Path().glob(pattern))[:limit]:
        rgba = np.array(Image.open(path).convert("RGBA"))
        bodies = find_bodies(body_mask(rgba[..., :3], rgba[..., 3]))
        if not bodies:
            print(f"  skipped {path}: nothing in it looks like a body")
            continue
        found.append((rgba[..., :3], max(bodies, key=lambda circle: circle[2])))
    return found


def read_sheet(path: Path) -> tuple[np.ndarray, list[tuple[float, float, float]]]:
    """A sheet's colour, and the circle around every body on it."""
    rgba = np.array(Image.open(path).convert("RGBA"))
    sheet = rgba[..., :3]
    return sheet, find_bodies(body_mask(sheet, rgba[..., 3]))


def find_bodies(mask: np.ndarray) -> list[tuple[float, float, float]]:
    """Every body on a sheet, as a fitted circle.

    Sheets are not always cut cleanly. One leaves opaque strips of its own paper between the
    planets, welding all twelve into a single blob, and eroding the mask is what snaps those
    threads. But erode too far and a sheet's small moons vanish altogether, so the amount is
    not a constant: each depth is tried and the one that finds the most bodies wins, which is
    a light touch on a clean sheet and a heavy one on a welded sheet.

    The erosion is only ever used to tell bodies apart. Each one's real edge is then found by
    walking outward from it across the original mask.
    """
    best: list[tuple[int, int, int, int]] = []
    for depth in EROSION_LADDER:
        found = components(erode(mask, depth), MIN_BODY_PIXELS)
        if len(found) > len(best):
            best = found

    return [disc_at(mask, (y0 + y1) / 2.0, (x0 + x1) / 2.0) for x0, y0, x1, y1 in best]


def extend_limb(rgb: np.ndarray, distance: np.ndarray, trust: float) -> np.ndarray:
    """Continue the disc's colour straight outward along each radius.

    Everything past `trust` is taken from the last pixel that was believed on the same line
    out of the centre. Spreading it sideways instead -- growing the mask a pixel at a time --
    leaves a faint octagon of spokes around the rim, because eight neighbours are not a
    circle. Radii are, and a globe's own bands run along them.
    """
    height, width, _ = rgb.shape
    centre_y = (height - 1) / 2.0
    centre_x = (width - 1) / 2.0
    ys, xs = np.mgrid[0:height, 0:width]
    offset_y = ys - centre_y
    offset_x = xs - centre_x

    pull = np.where(distance > trust, trust / np.maximum(distance, 1e-6), 1.0)
    source_y = np.clip(np.round(centre_y + offset_y * pull).astype(int), 0, height - 1)
    source_x = np.clip(np.round(centre_x + offset_x * pull).astype(int), 0, width - 1)
    return rgb[source_y, source_x]


def cut_disc(
    sheet: np.ndarray,
    circle: tuple[float, float, float],
    size: int,
) -> tuple[Image.Image, tuple[float, float, float]]:
    """Crop one body, give it a circular alpha, and divide out its baked limb darkening."""
    centre_x, centre_y, radius = circle

    # A square window a little wider than the body, so the rim keeps a transparent margin.
    half = radius * DISC_MARGIN
    left = int(round(centre_x - half))
    top = int(round(centre_y - half))
    span = int(round(half * 2))
    crop = np.zeros((span, span, 3), dtype=float)
    src = sheet[max(0, top):top + span, max(0, left):left + span].astype(float)
    crop[: src.shape[0], : src.shape[1]] = src

    ys, xs = np.mgrid[0:span, 0:span]
    distance = np.hypot(xs - (span - 1) / 2.0, ys - (span - 1) / 2.0) / radius

    # The render lit these spheres from the front and let the limb fall off. Measure that
    # falloff as a function of radius and divide it back out, so what is left is the body's
    # own colour and the mod can light it for itself.
    luminance = crop.sum(axis=2) / 3.0
    profile = np.ones(24)
    for step in range(24):
        band = (distance >= step / 24.0) & (distance < (step + 1) / 24.0)
        if band.any():
            profile[step] = luminance[band].mean()
    profile /= max(profile[:6].mean(), 1e-6)

    # Only the last sliver is reconstructed. Continuing a limb inward-out over any real width
    # smears the bands it crosses into a fan of wedges, which is worse than the shading it was
    # trying to remove; a render lit too hard for the gentle correction below simply keeps some
    # of its own limb darkening, and the mod's darkening lands on top of it.
    trust = LIMB_TRUST

    profile = np.clip(profile, MIN_VIGNETTE, 4.0)
    correction = np.interp(np.clip(distance, 0, 0.999) * 24.0, np.arange(24), profile)
    flattened = np.clip(crop / correction[..., None], 0, 255)

    alpha = np.clip((1.0 - distance) * radius * 1.5 + 0.5, 0.0, 1.0) * 255.0
    bled = extend_limb(flattened, distance, trust)
    rgba = np.dstack([bled, alpha]).astype(np.uint8)
    image = Image.fromarray(rgba, "RGBA").resize((size, size), Image.LANCZOS)

    inner = distance < 0.82
    mean = crop[inner].reshape(-1, 3).mean(axis=0) / 255.0
    return image, (float(mean[0]), float(mean[1]), float(mean[2]))


def strip_caption(alpha: np.ndarray) -> np.ndarray:
    """Blank the filename some of the ring renders have burned into their lower margin."""
    rows = (alpha > 25).sum(axis=1)
    filled = np.nonzero(rows)[0]
    if len(filled) == 0:
        return alpha

    previous = filled[0]
    for row in filled[1:]:
        if row - previous > 3 and row > alpha.shape[0] * 0.6:
            cleaned = alpha.copy()
            cleaned[row:] = 0
            return cleaned
        previous = row
    return alpha


def prepare_ring(path: Path, size: int) -> tuple[Image.Image, RingRecord]:
    source = np.array(Image.open(path).convert("RGBA"))
    alpha = strip_caption(source[..., 3])
    source = source.copy()
    source[..., 3] = alpha

    ys, xs = np.nonzero(alpha > 25)
    centre_x, centre_y = xs.mean(), ys.mean()
    x = xs - centre_x
    y = ys - centre_y

    # The ring is drawn as an ellipse at some angle of its own. Find that angle so the
    # texture can be written flat, and the mod can then roll and squash it to the ring it
    # actually authored.
    covariance = np.cov(np.vstack([x, y]))
    values, vectors = np.linalg.eigh(covariance)
    major = vectors[:, np.argmax(values)]
    roll = math.atan2(major[1], major[0])

    rotated = Image.fromarray(source, "RGBA").rotate(
        math.degrees(roll),
        resample=Image.BICUBIC,
        center=(centre_x, centre_y),
        translate=(source.shape[1] / 2 - centre_x, source.shape[0] / 2 - centre_y),
    )
    flat = np.array(rotated)
    ys, xs = np.nonzero(flat[..., 3] > 25)

    # Centre on the ellipse itself rather than on its centroid: these rings are lopsided --
    # one has a moonlet train along one side -- and a centroid pulls toward the heavy side,
    # which would leave the planet sitting off-centre inside its own rings.
    left, right = xs.min(), xs.max()
    top, bottom = ys.min(), ys.max()
    half_width = max((right - left) / 2.0, 1.0)
    half_height = max((bottom - top) / 2.0, 1.0)
    ellipse_x = (left + right) / 2.0
    ellipse_y = (top + bottom) / 2.0

    # The hole in the middle: half the widest run of empty pixels along the long axis.
    row = flat[int(round(ellipse_y)), :, 3]
    lit = np.nonzero(row > 25)[0]
    inner = 0.0
    if len(lit) > 1:
        widest = int(np.diff(lit).max())
        if widest > 2:
            inner = (widest / 2.0) / half_width

    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    scale = (size * 0.98) / (half_width * 2)
    scaled = rotated.resize(
        (max(1, int(round(flat.shape[1] * scale))), max(1, int(round(flat.shape[0] * scale)))),
        Image.LANCZOS,
    )
    canvas.alpha_composite(
        scaled,
        (
            int(round(size / 2 - ellipse_x * scale)),
            int(round(size / 2 - ellipse_y * scale)),
        ),
    )

    pixels = np.array(canvas)
    lit_mask = pixels[..., 3] > 25
    mean = pixels[..., :3][lit_mask].reshape(-1, 3).mean(axis=0) / 255.0
    record = RingRecord(
        id=path.stem,
        file=f"{path.stem}.png",
        kind="ring",
        red=float(mean[0]),
        green=float(mean[1]),
        blue=float(mean[2]),
        disc_fraction=1.0,
        outer_radius_fraction=0.49,
        inner_radius_fraction=round(float(inner) * 0.49, 4),
        baked_openness=round(float(half_height / half_width), 4),
    )
    return canvas, record


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sheet", type=Path, default=Path("gas_giants.png"))
    parser.add_argument(
        "--giant-files",
        default="gas_giant*_*.png",
        help="Glob of single-planet renders, each one giant. These are preferred over the sheet.",
    )
    parser.add_argument(
        "--moon-files",
        default="moon*_*.png",
        help="Glob of single-moon renders, each one moon. These are preferred over the sheet.",
    )
    parser.add_argument("--moon-sheet", type=Path, default=Path("image.png"))
    parser.add_argument("--rings", type=Path, default=Path("ring_assets.zip"))
    parser.add_argument("--out", type=Path, default=Path("assets/astraextera/textures/celestial"))
    parser.add_argument("--manifest", type=Path, default=Path("assets/astraextera/config/celestial-textures.json"))
    arguments = parser.parse_args()

    arguments.out.mkdir(parents=True, exist_ok=True)
    arguments.manifest.parent.mkdir(parents=True, exist_ok=True)
    records: list[TextureRecord] = []

    # Giants and moons come off different sheets: the giants were redrawn larger and on their
    # own, while the moons are still on the original contact sheet they arrived with.
    giant_sheet, giant_circles = read_sheet(arguments.sheet)
    moon_sheet, moon_circles = (
        (giant_sheet, giant_circles)
        if arguments.moon_sheet == arguments.sheet
        else read_sheet(arguments.moon_sheet)
    )

    # A render of one planet on its own beats a cell of a contact sheet -- it arrives several
    # times the size and needs no separating -- so those are taken first and the sheet only
    # makes up the numbers. Hand over twelve of them and the sheet stops being used at all.
    sheet_giants = sorted(
        (circle for circle in giant_circles if circle[2] > 75),
        key=lambda circle: (int(circle[1]) // 200, circle[0]),
    )
    giants = read_singles(arguments.giant_files, MAX_GIANTS)
    giants += [(giant_sheet, circle) for circle in sheet_giants][: MAX_GIANTS - len(giants)]
    sheet_moons = sorted(
        (circle for circle in moon_circles if 15 < circle[2] <= 75),
        key=lambda circle: (int(circle[1]) // 60, circle[0]),
    )
    moons = read_singles(arguments.moon_files, MAX_MOONS)
    moons += [(moon_sheet, circle) for circle in sheet_moons][: MAX_MOONS - len(moons)]

    for index, (pixels, circle) in enumerate(giants, start=1):
        image, colour = cut_disc(pixels, circle, GIANT_SIZE)
        name = f"gas-giant-{index:02d}"
        image.save(arguments.out / f"{name}.png", optimize=True)
        records.append(
            TextureRecord(name, f"{name}.png", "giant", *[round(c, 4) for c in colour], round(1.0 / DISC_MARGIN, 4))
        )

    for index, (pixels, circle) in enumerate(moons, start=1):
        image, colour = cut_disc(pixels, circle, BIG_MOON_SIZE if circle[2] >= 64 else MOON_SIZE)
        name = f"moon-{index:02d}"
        image.save(arguments.out / f"{name}.png", optimize=True)
        records.append(
            TextureRecord(name, f"{name}.png", "moon", *[round(c, 4) for c in colour], round(1.0 / DISC_MARGIN, 4))
        )

    with zipfile.ZipFile(arguments.rings) as archive:
        extracted = Path("/tmp/astraextera-rings")
        extracted.mkdir(parents=True, exist_ok=True)
        archive.extractall(extracted)

    for index, path in enumerate(sorted(extracted.glob("*.png")), start=1):
        image, record = prepare_ring(path, RING_SIZE)
        name = f"ring-{index:02d}"
        record.id = name
        record.file = f"{name}.png"
        record.red, record.green, record.blue = (round(record.red, 4), round(record.green, 4), round(record.blue, 4))
        image.save(arguments.out / f"{name}.png", optimize=True)
        records.append(record)

    manifest = {
        "schema_version": 1,
        "textures": [asdict(record) for record in records],
    }
    arguments.manifest.write_text(json.dumps(manifest, indent=2) + "\n")
    print(
        f"wrote {len(giants)} giants, {len(moons)} moons and "
        f"{len(records) - len(giants) - len(moons)} rings to {arguments.out}"
    )


if __name__ == "__main__":
    main()
