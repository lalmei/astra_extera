# AstraExtera

Vintage Story 1.22 mod. Depends on [AstraTerra](https://github.com/lalmei/astraterra) **v0.6.0 or newer** for the sky engine, then replaces Earth's catalog with a **server-authored** procedural sky so every player sees the same heavens.

Worlds are not dropped into a random starfield. The save first draws a Milky Way analog, then a thin-disk location inside that galaxy's habitable annulus, and only keeps sites metal-rich enough for iron cores and ores. A host star and a habitable orbit come next, then the visible sky.

## What is implemented

- Deterministic galaxy + galactocentric location from the Vintage Story world seed
- Galactic habitable zone with `[Fe/H]` floors for iron and ores. Spirals are the common hosts; giant ellipticals are rare and use a spheroid shell outside the dense core instead of a thin disk.
- Playable worlds are Earth analogs: ~1 Rearth, ~1 g, Earth-like bulk iron / core fraction, and Earth-like temperature derived from the host star
- A local system: K/G/F host (M allowed for moons), liquid-water orbit, shepherd giant past the snow line, up to three inner rocky worlds, an optional second gas giant and one or two ice giants beyond it. Every body is Hill-separated from the ones already placed. Moons keep a Roche-safe day under 7 Earth days
- Giants are authored bodies rather than markers: an obliquity, a rotation period and the banding it whips up, a long-lived storm parked between two jets, a ring system in the planet's equatorial plane, and a family of moons outside the rings. Rings carry a composition -- ice, rock and dust, or sooted debris -- which sets how bright and how coloured they read
- Companion planets, leftover comets, and comet-fed meteor showers authored into AstraTerra's sky. Earth's wanderers are replaced, not mixed in.
- On a world that is itself a moon, the sky gets that world's own parent: the giant hangs fixed and tens of degrees wide, phased by the sun, with its sibling moons drifting past it. Vintage Story's moon stands down, since a moon has none of its own
- Save-game persistence of the galaxy, the sampled star catalog, and the local-system sky; the join packet carries all three so clients render the stored sky instead of sampling again
- A visible star catalog sampled from the galaxy's own stellar density, exported in AstraTerra's `star-catalog.v1.json` shape
- `/astraextera galaxy` to inspect the authored site
- **Ctrl+Shift+S** opens the in-game galaxy panel: the same facts, face-on and edge-on figures, all-sky view, habitable zone, full system and companion-planet portraits as the HTML preview.
- The full-system figure compresses distance so the inner orbits stay readable next to an ice giant twenty times further out, and draws bodies at their real radii. The portrait strip below it draws each companion as a disc, with its bands, its storm, its moons, and its rings at the tilt and heading they run.
- Unresolved galactic glow is broken into an equatorial cubemap and drawn behind AstraTerra's star billboards, so the band of light is on the sky rather than only on the preview PNG.
- `make galaxy-preview` writes a random-seed static HTML preview (`dist/galaxy-preview.html`) and opens it. Pass `SEED=42` to pin a known test galaxy.
- `make star-catalog` writes `dist/star-catalog.v1.json` without opening the preview.
- `make celestial-textures` rebuilds the shipped planet, moon and ring textures from the source art: giants from `gas_giants.png`, moons from `image.png`, rings from `ring_assets.zip`. It finds each body on its sheet whether that sheet is on black, on white or on nothing, cuts it out with a circular alpha, strips the filenames some ring renders have burned into them, fills the margin around each body with its own colour so nothing dark bleeds into its rim, and divides out the limb darkening the render baked in -- the mod lights these bodies itself, so a second set of shadows in the texture would fight it. The outputs are committed; the game never runs this step.

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
physically is. Sampling runs once on the server when the save is authored (or when an older save
is loaded without a stored catalog), then the catalog is written next to the placement. Clients
never resample.

## How the sky reaches AstraTerra

AstraTerra loads its shipped Earth catalog from an asset in `AssetsLoaded`, which is long before a
client knows which world it is joining. So AstraExtera lets that load happen, then calls
`AstraTerraModSystem.ReplaceStarCatalog` once the server's sky packet arrives, then
`ReplacePlanetCatalog`, `ReplaceCometCatalog` and `ReplaceMeteorShowers` for the authored
wanderers. The packet already includes those stored catalogs; the client does not run the
sampler. That seam lives in AstraTerra; AstraExtera
compiles against it, preferring a sibling `astra_terra` checkout and falling back to the installed
mod (override with `ASTRA_TERRA`).

Earth's guide groups, sky cultures and deep-sky objects are all keyed to Earth's own star ids and
positions, so they are replaced with empty sets rather than pointed at unrelated stars. Regenerating
deep-sky objects from the galaxy model is still open.

### The sky of a moon

A habitable moon is tidally locked to its giant -- one orbit is one day, which is what gives such a
world a day at all -- and the consequence on the ground is stark. The giant never rises and never
sets. It hangs at one spot, painted with the bands, storm and rings AstraExtera authored for it,
tens of degrees across, and goes through its phases as the sun goes round: full near midnight, dark
at noon. It sits off the meridian rather than on it, because a giant on the sun's noon track would
eclipse the sun every single day.

Sibling moons drift past it at the rate the two orbits beat against each other: an inner one laps
the world and slides one way, an outer one falls behind and slides the other. The sun is the
limiting case of an infinitely distant sibling, going round once a day.

The rings are a line, not an ellipse. A habitable moon is a regular satellite -- it formed in the
disc that became the rings, which is why it is locked at all -- so it orbits inside the ring plane
and sees the rings edge-on, bisecting the planet. Only two things lift them off that line, and both
are fractions of a degree: the orbit's own tilt, and standing away from the world's equator, which
puts you up to one world radius clear of the plane. A giant lying on its side shows a wide ellipse to
anyone else in the system and still shows a line to its own moon, because the moon went over with it.

The giant is a photograph-grade render rather than anything drawn in code: the mod ships twelve gas
giants, sixteen ring systems and fifty-four moons, and picks the one whose own colour sits nearest
the cloud decks the generator authored, so a giant written down as deep blue methane comes back blue.
The ring is resampled onto it -- squashed from the tilt it was drawn at to the tilt this giant has,
rolled to the heading its node runs along, and scaled so its outer edge lands where the authored ring
ends -- with the far half behind the globe and the near half over it.

That face goes to AstraTerra's `ReplaceNearBodies`, which places the body
and lights it -- the terminator is shaded from the sphere normal and the real sun direction rather
than painted in. Vintage Story's own moon is asked to stand down. Only its drawing stops: moonlight,
the phase the calendar reports, and the length of the day are untouched, so the day cycle is the one
the world always had.

A world that is a planet keeps the vanilla moon for now; it has no moons of its own in the model.

A giant's rings reach the sky as brightness and nothing else, because AstraTerra draws every planet
as a point of light: an open sheet of ring ice can add most of a magnitude, an edge-on or sooty ring
adds nothing. A giant's cloud decks set its tint the same way, so the dot in the sky and the portrait
in the panel are the same planet. Moons of a companion giant stay off the planet catalog for the same
reason sibling moons do -- the ephemeris is heliocentric -- so they are recorded and drawn in the
panel rather than rendered.

Companion planets are the bodies already placed in the local system, on Keplerian tracks around this
star. Comets are leftover ice the shepherd giant scattered, with authored return periods and a track
across this world's sky. Each comet leaves a meteor shower where its debris meets the observer's
orbit; a Halley-type comet leaves both nodes. Sibling moons of a habitable moon stay off the planet
catalog: AstraTerra's ephemeris is heliocentric, and those moons would collapse onto the parent.

## Constellations

Catalog ids are assigned by brightness rank, so id 1 is always the world's brightest star. A player's
constellation is stored by AstraTerra as edges between ids, so those ids are a save contract: the
server samples the catalog once and stores it. Clients render that list, which is why a later
sampler change does not scramble figures on an existing world. Regenerating the galaxy itself still
follows the `GalaxyPlacement` schema version.

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
