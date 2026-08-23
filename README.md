# AstraExtera

Vintage Story 1.22 mod. Depends on [AstraTerra](https://github.com/lalmei/astraterra) for the sky engine, then replaces Earth's catalog with a **server-authored** procedural sky so every player sees the same heavens.

Worlds are not dropped into a random starfield. The save first draws a Milky Way analog, then a thin-disk location inside that galaxy's habitable annulus, and only keeps sites metal-rich enough for iron cores and ores. Stellar systems and the visible sky come after that placement.

## What is implemented

- Deterministic galaxy + galactocentric location from the Vintage Story world seed
- Galactic habitable zone with `[Fe/H]` floors for iron and ores. Spirals are the common hosts; giant ellipticals are rare and use a spheroid shell outside the dense core instead of a thin disk.
- Playable worlds are Earth analogs: ~1 R⊕, ~1 g, Earth-like bulk iron / core fraction, and Earth-like temperature (climate may change later)
- Save-game persistence and a join packet so clients share the server placement
- A visible star catalog sampled from the galaxy's own stellar density, exported in AstraTerra's `star-catalog.v1.json` shape
- `/astraextera galaxy` to inspect the authored site
- **Ctrl+Shift+S** opens the in-game galaxy panel: the same facts, face-on and edge-on figures, and all-sky view as the HTML preview
- `make galaxy-preview` writes a random-seed static HTML preview (`dist/galaxy-preview.html`) and opens it. Pass `SEED=42` to pin a known test galaxy.
- `make star-catalog` writes `dist/star-catalog.v1.json` without opening the preview.

## How the star count is decided

Nothing picks a number of stars. For each bin of a solar-neighborhood luminosity function the sampler
marches outward along ~200 sight lines, accumulating dust extinction, and stops where a star of that
brightness would drop below the eye's limit. The visible count is that luminosity function integrated
against the local stellar density inside those horizons, so it follows the location on its own:

| Seed | Site | Naked-eye stars | Catalog |
| --- | --- | --- | --- |
| 42 | R = 12.5 kpc, \|z\| = 402 pc | 2,364 | 2,297, full limit mag 6.5 |
| 1234 | R = 7.5 kpc, \|z\| = 48 pc | 18,062 | brightest 10,000, to mag 6.0 |

Earth sees roughly 9,100 stars at magnitude 6.5, which is the calibration anchor. Where the sky is
crowded the render budget runs out before the eye does, so the catalog becomes a brightest-first
slice and the remainder stays as unresolved glow in the Milky Way band -- which is what that band
physically is. Sampling a world costs 8-18 ms, once, when the placement arrives.

## How the sky reaches AstraTerra

AstraTerra loads its shipped Earth catalog from an asset in `AssetsLoaded`, which is long before a
client knows which world it is joining. So AstraExtera lets that load happen, then calls
`AstraTerraModSystem.ReplaceStarCatalog` once the server's placement packet arrives, which swaps the
catalog and drops AstraTerra's cached star projections. That seam lives in AstraTerra; AstraExtera
compiles against it, preferring a sibling `astra_terra` checkout and falling back to the installed
mod (override with `ASTRA_TERRA`).

Earth's guide groups, sky cultures and deep-sky objects are all keyed to Earth's own star ids and
positions, so they are replaced with empty sets rather than pointed at unrelated stars. Regenerating
deep-sky objects from the galaxy model is still open.

## Constellations

Catalog ids are assigned by brightness rank, so id 1 is always the world's brightest star. A player's
constellation is stored by AstraTerra as edges between ids, so those ids are a save contract: the same
seed must always number the same stars. That holds while the sampler and `GalaxyPlacement`
schema version are unchanged, which is why the schema version is what gates regeneration.

`guide-stars.v1.json` is exported empty on purpose. A procedurally authored sky inherits no
constellations; the brightest 58 stars are flagged as guides to serve as naming anchors, and the
figures are the players' to invent.

Each world also draws its own celestial pole direction, since there is no reason for a planet's spin
axis to line up with its galaxy. That angle decides whether the band of light wheels overhead through
the night or sits near the horizon.

## Build

```bash
make test
make build
make package
make deploy
```

Enable **AstraTerra** and **AstraExtera** together. AstraExtera will not load without AstraTerra.
