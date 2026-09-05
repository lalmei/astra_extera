# AstraExtera Developer Guide

This guide describes the current code contract. AstraExtera authors one procedural astronomical
setting per save and hands its renderable catalogs to AstraTerra. It does not modify Vintage Story's
terrain or physical world simulation.

## System boundary

AstraExtera owns:

- deterministic galaxy, observer-site, home-world, host-star, and local-system generation;
- server save data and the join packet;
- conversion of the stored sky into AstraTerra catalog records;
- generated near-body artwork and motion inputs;
- the unresolved galactic-glow renderer;
- the galaxy facts command and inspection panel.

AstraTerra owns:

- equatorial-to-horizontal projection using game time and observer position;
- fixed-star, planet, comet, meteor, constellation, deep-sky, and near-body rendering;
- the longitude-aware sun and displayed local time;
- telescope, Sextant, Astrolabe, Sky Disc, constellation journal, and handbook mechanics;
- the public catalog-replacement methods used by AstraExtera.

Vintage Story still owns the terrain seed, blocks, ores, climate, calendar, day and year lengths,
seasons, sun base motion, moonlight, and calendar moon phase. Generated world facts in
`EarthAnalogWorld` are metadata and authoring constraints; no code applies them to those game
systems.

## Startup and data flow

[`AstraExteraModSystem`](../src/AstraExtera/AstraExteraModSystem.cs) runs at order `0.6`.

1. On the server, `StartServerSide` creates `GalaxyServerSync`, registers the `astraextera.galaxy`
   network channel, hooks `SaveGameLoaded` and `PlayerJoin`, and registers `/astraextera`.
2. `SaveGameLoaded` asks `GalaxySkyStore` to resolve the three stored records. Missing records are
   authored and saved. A stale placement schema causes all three records to be rebuilt.
3. `PlayerJoin` sends one `GalaxyPlacementPacket` containing the placement, star field, and local
   sky. A reroll stores a complete replacement and broadcasts the same packet.
4. On each client, `GalaxyClientSync` decodes the packet into a `GalaxySky`.
5. `AstraTerraSkyBridge.Publish` builds AstraTerra catalogs for stars, planets, comets, meteor
   showers, and near bodies. It publishes each catalog once per cosmology seed.
6. `GalaxyGlowRenderer.Apply` builds and uploads the generated galactic-glow cubemap. The galaxy
   panel reads the same decoded `GalaxySky`.

Clients never run the star sampler or local-sky authoring code during normal play. This is the
multiplayer consistency boundary: the server stores the result, and clients render that result.

## Source map

| Area | Entry points | Responsibility |
| --- | --- | --- |
| Bootstrap | [`AstraExteraModSystem.cs`](../src/AstraExtera/AstraExteraModSystem.cs) | Side-specific startup and disposal. |
| Persistence and network | [`Sync/GalaxyServerSync.cs`](../src/AstraExtera/Sync/GalaxyServerSync.cs), [`Sync/GalaxyClientSync.cs`](../src/AstraExtera/Sync/GalaxyClientSync.cs) | Resolve save records, transmit a snapshot, decode it, and publish it. |
| Galaxy placement | [`Galaxy/GalaxyGenerator.cs`](../src/AstraExtera/Galaxy/GalaxyGenerator.cs), [`Galaxy/GalaxyModels.cs`](../src/AstraExtera/Galaxy/GalaxyModels.cs) | Galaxy morphology, galactocentric location, home world, local system, and celestial orientation. |
| Physical constraints | [`Galaxy/MetallicityModel.cs`](../src/AstraExtera/Galaxy/MetallicityModel.cs), [`Galaxy/EarthAnalog.cs`](../src/AstraExtera/Galaxy/EarthAnalog.cs), [`Galaxy/LocalSystem.cs`](../src/AstraExtera/Galaxy/LocalSystem.cs) | Metallicity gates, Earth-analog metadata, stellar relations, habitable orbit, companion spacing, rings, and moons. |
| Visible stars | [`Galaxy/StarFieldSampler.cs`](../src/AstraExtera/Galaxy/StarFieldSampler.cs), [`Galaxy/StellarLuminosityFunction.cs`](../src/AstraExtera/Galaxy/StellarLuminosityFunction.cs) | Statistical sampling through density and dust to a naked-eye limit and render budget. |
| Coordinate frame | [`Galaxy/CelestialOrientation.cs`](../src/AstraExtera/Galaxy/CelestialOrientation.cs), [`Galaxy/ObserverFrame.cs`](../src/AstraExtera/Galaxy/ObserverFrame.cs) | Generated celestial pole and transformations between galactic and equatorial coordinates. |
| Local visible catalogs | [`Galaxy/LocalSystemSky.cs`](../src/AstraExtera/Galaxy/LocalSystemSky.cs), [`Sync/LocalSystemSkyExport.cs`](../src/AstraExtera/Sync/LocalSystemSkyExport.cs) | Observer orbit, companion elements, comet apparitions, shower schedules, and AstraTerra record conversion. |
| Nearby bodies | [`Galaxy/NearSky.cs`](../src/AstraExtera/Galaxy/NearSky.cs), [`Sync/NearBodyExport.cs`](../src/AstraExtera/Sync/NearBodyExport.cs) | Parent giant and moon geometry, then AstraTerra near-body records. |
| Artwork | [`Client/CelestialTextureLibrary.cs`](../src/AstraExtera/Client/CelestialTextureLibrary.cs), [`Galaxy/CelestialFaceComposer.cs`](../src/AstraExtera/Galaxy/CelestialFaceComposer.cs), [`Client/BodyFacePainter.cs`](../src/AstraExtera/Client/BodyFacePainter.cs) | Load source faces, choose them deterministically, composite rings, and produce lit-disc inputs. |
| Galactic glow | [`Galaxy/GalaxySkyView.cs`](../src/AstraExtera/Galaxy/GalaxySkyView.cs), [`Galaxy/SkyCubemap.cs`](../src/AstraExtera/Galaxy/SkyCubemap.cs), [`Client/GalaxyGlowRenderer.cs`](../src/AstraExtera/Client/GalaxyGlowRenderer.cs) | Integrate unresolved light, reproject it, and render a six-face sky cubemap. |
| Inspection | [`Galaxy/GalaxyFacts.cs`](../src/AstraExtera/Galaxy/GalaxyFacts.cs), [`Client/GalaxyPanelDialog.cs`](../src/AstraExtera/Client/GalaxyPanelDialog.cs), [`Commands/GalaxyServerCommands.cs`](../src/AstraExtera/Commands/GalaxyServerCommands.cs) | Shared facts, diagrams, hotkey panel, and server commands. |

## Deterministic authoring

`GalaxySky.Author(seed)` has three outputs:

- `GalaxyPlacement`: galaxy, site, world kind, Earth-analog facts, local system, and celestial
  orientation;
- `StarField`: stars in galactic coordinates plus sampling totals and limits;
- `LocalSystemSky`: observer orbit, companion planets, comet apparitions, and meteor showers.

`SplitMix64` drives generation. Subsystems mix the cosmology seed with their own constants, which
keeps a deterministic stream from depending on the game process or client timing. Do not replace
these streams with `Random.Shared` inside authoring code. `Random.Shared` is used only to select a new
seed when an admin omits one from the reroll command.

The initial cosmology seed is Vintage Story's terrain world seed. Once saved, it is independent. A
reroll changes the cosmology seed but not the terrain seed. When a valid older `GalaxyPlacement` is
rejected only because its schema is stale, `GalaxySkyStore` preserves its cosmology seed while
reauthoring the sky.

### Galaxy and observer site

`GalaxyGenerator` chooses an unbarred spiral, barred spiral, or rare elliptical analytic model. The
spirals use disks, bulges, density-wave arms, metallicity gradients, and a habitable annulus. An
elliptical uses a Sersic-like spheroid and a habitable shell outside its dense core. This is a
statistical galaxy model, not an N-body or cosmological simulation.

The site must satisfy the metallicity rules used for an iron-bearing Earth analog. The generated
location records local density, metallicity, and relative supernova rate. These values feed later
authoring and the panel; they do not rewrite Vintage Story ore generation.

`EarthAnalog` samples bulk world facts. `LocalSystem.Sample` then chooses a main-sequence host,
habitable orbit, and companion layout. Its mass-luminosity, main-sequence lifetime, habitable-zone,
snow-line, Roche-limit, Hill-sphere, and separation formulas are explicit approximations. The
generated surface and equilibrium temperatures are not Vintage Story climate values.

A terrestrial-planet world can receive zero to three home moons. A terrestrial-moon world is chosen
inside a parent giant's regular moon family, with the home moon constrained to a 0.4-to-7-Earth-day
orbit. Other giant moons can have periods as long as 120 Earth days; planet-world moons use a
2-to-90-Earth-day range subject to the home world's Hill sphere.

### Star field

`StarFieldSampler` integrates the binned `StellarLuminosityFunction` along 192 directions and 72
radial cells from 0.5 pc to 30 kpc. Each sight line uses the generated stellar density and cumulative
dust extinction. The default apparent-magnitude limit is 6.5. A Poisson draw turns the expected
weight in each luminosity bin into individual stars.

Stars receive a deterministic total order: apparent magnitude, galactic longitude, then galactic
latitude. The sampler keeps the brightest 10,000 when a crowded site exceeds the render budget. That
ordering is a save contract because `StarCatalogExport` assigns `Hip = index + 1`, and constellation
books store edges between those values.

`StarFieldCodec.Quantize` applies the same float precision used by the binary save format before a new
field enters live state. The server and client therefore calculate against the stored precision, not
against a higher-precision transient result.

The integrated light left after resolving stars is computed separately by `GalaxySkyView`. It is a
line-of-sight density and dust integral, not the sum of discarded `VisibleStar` records.

### Local system, comets, and showers

`LocalSystemSky` converts every companion into an `AuthoredPlanet` with fixed eccentricity,
inclination, node, perihelion, and initial mean longitude. Only mean longitude advances. Export sets
all other per-century rates to zero, so there are no perturbations or precession.

Each save receives two to four `AuthoredComet` records. A record is an apparition model: period,
first perihelion, visibility half-width, brightness curve, tail length, and equatorial path keyframes.
It is not a physical orbit integrated from state vectors.

Every comet receives one shower at one end of its authored path. Comets with periods of at least 45
world years receive a second. Peak solar longitude is derived from the authored perihelion phase,
and the radiant comes from a path endpoint. The parent-comet association is stored, but no debris
particles or orbital intersections are integrated.

## Coordinates and time

Sampled stars begin in **galactic coordinates** relative to the generated galaxy. Longitude zero
points toward the galactic center; latitude zero is the galactic midplane. `ObserverFrame` performs
the spatial density integration from the generated site.

`CelestialOrientation` gives the save a generated celestial pole and zero point. It rotates each
galactic unit vector into **equatorial coordinates**:

- right ascension, the angle around the celestial equator;
- declination, the angle north or south of that equator.

`StarCatalogExport` stores those equatorial coordinates in the AstraTerra catalog. AstraTerra later
obtains observer latitude from Vintage Story's world-Z mapping and longitude from world X when its
longitude-aware sun is active. Given latitude `phi`, declination `delta`, and hour angle `H`, the
altitude `h` follows:

```text
sin(h) = sin(phi) sin(delta) + cos(phi) cos(delta) cos(H)
H = local sidereal angle - right ascension
```

Azimuth comes from the corresponding horizontal projection. The local sidereal angle uses Vintage
Story total days, `DaysPerYear`, `HoursPerDay`, and AstraTerra's observer longitude. AstraExtera's
glow renderer calls the same AstraTerra coordinate helpers so its band remains registered behind the
star billboards.

World X has no longitude meaning in the Vintage Story API by itself. AstraTerra maps it using the
same `polarEquatorDistance` scale that Vintage Story uses for latitude and shifts the sun through a
calendar delegate when `LongitudeAwareSun` is enabled. A compatibility mod that also shifts the sun
can double-apply longitude; that integration belongs at the AstraTerra boundary.

Near bodies use **hour angle** rather than right ascension. A moon-world parent has a fixed hour
angle, while orbiting siblings use either a flat angular rate or `NearBodyOrbit` for bounded motion
about the parent. Planet-world moons use circular motion at fixed declination. This is deliberately
separate from the heliocentric planet ephemeris.

## Persistence and serialization

The save contains three blobs:

| Key | Format | Compatibility behavior |
| --- | --- | --- |
| `astraextera:galaxy-placement.v1` | JSON from `GalaxyPlacementCodec` | The decoded `SchemaVersion` must equal `GalaxyPlacement.CurrentSchemaVersion` (currently 7). A mismatch reauthors all three records with the stored cosmology seed. |
| `astraextera:star-field.v1` | GZip-compressed binary from `StarFieldCodec` | The binary header version must equal `StarFieldCodec.CurrentSchemaVersion` (currently 1). Missing or invalid data is resampled from a current placement. |
| `astraextera:local-sky.v1` | JSON from `LocalSystemSkyCodec` | The payload does not encode or validate a schema field. Missing or invalid data is reauthored from a current placement. `LocalSystemSky.SchemaVersion` is used only when exporting AstraTerra catalogs. |

Load failures are logged and treated as missing data. With a current placement, only the missing star
field or local sky is regenerated. With a missing or stale placement, the complete sky is rebuilt.
There is no field-by-field migration layer.

`GalaxyPlacementPacket` carries the same three encoded payloads. This duplicates encoded bytes rather
than protobuf-serializing every model type, so save and network decoding use the same codecs.

The repository has no machine-readable JSON Schema files for these payloads or the texture manifest.
The C# records, codecs, and round-trip tests are the source of truth. A numeric field called
`schema_version` is not by itself a migration contract; only the validation paths described above
currently enforce one.

Changing a generator without raising the placement schema preserves old placements. Changing only
star sampling does not affect a save that already has a stored field. Schema changes therefore need
an explicit decision about whether an existing sky remains valid. Raising the placement schema
rewrites constellation ID meaning because the new star field is completely resampled.

## Catalog handoff to AstraTerra

`AstraTerraSkyBridge.Publish` performs a full client-side replacement after the server packet arrives:

| AstraTerra method | AstraExtera input |
| --- | --- |
| `ReplaceStarCatalog` | Brightness-ranked fixed stars; empty guide groups, sky cultures, and deep-sky objects. The first 58 stars are flagged as guide anchors. |
| `ReplacePlanetCatalog` | Observer orbit and every generated companion planet. |
| `ReplaceCometCatalog` | Generated apparition records. |
| `ReplaceMeteorShowers` | Generated shower records. |
| `ReplaceNearBodies` | Parent giant and siblings on a moon world, or home moons on a planet world; `HidesVanillaMoon` is always true. |

The call is guarded by `publishedSeed`. A second packet carrying the same seed is ignored even if its
other content differs. Normal rerolls use a new seed, so this is safe for the implemented path. Code
that mutates a sky in place under the same seed has no supported refresh operation.

The live star handoff constructs `StarCatalogEntry` records directly. `StarCatalogExport.ToJson` and
`EmptyGuideGroupsJson` are tool/test helpers and do not write assets during play.

### Current server/client mismatch

AstraExtera calls the replacement methods only from its client bridge. AstraTerra also reads its star
catalog on the server for constellation validation, prepared-book creation, and `/stars` services.
Consequently, a server can retain AstraTerra's shipped Earth catalog while clients render the
procedural catalog.

This is an implementation discrepancy, not a supported split-brain contract. It can affect prepared
Earth books, `.stars build`, server validation, and commands that resolve HIP IDs. Code that changes
this must arrange a server-side AstraTerra replacement from the stored field without asking a client
to author data. Until then, integrations should not treat those server services as authoritative for
the procedural sky.

## Rendering

### Fixed stars, planets, and comets

AstraTerra renders the replacement stars and companion planets as celestial billboards. Naked-eye
companion planets are points with generated photometric values and tints. `LocalSystemSkyExport` does
not supply AstraTerra `Disc` or `Moons` data, so telescope magnification does not resolve generated
companions into panel-style discs.

AstraTerra owns the star and wanderer brightness curves, horizon fade, daylight gating, moonlight
response, and instrument exposure checks. AstraExtera supplies catalog magnitudes and colors but does
not add another visibility policy for those objects. Its galactic glow is the exception described
below.

The replacement star catalog has no deep-sky objects. No telescope plates are generated. Guide-star
groups and sky cultures are also empty; the `IsGuideStar` flag on the first 58 stars supplies drawing
anchors, not named figures.

### Galactic glow

`GalaxySkyView` integrates a 720 by 360 equirectangular glow map in galactic coordinates. The client
reprojects it into equatorial coordinates, converts it to six 256-pixel cube faces, and renders 12
subdivisions per face. `GalaxyGlowRenderer` runs at opaque order `0.2`, after Vintage Story's night
sky (`0.1`) and before AstraTerra's stars (`0.3`). Depth is disabled for the sky pass, while later
terrain still occludes it.

The glow opacity follows natural daylight. It does not read AstraTerra's `StarfieldMode`,
`MilkyWayBrightness`, moon-brightness response, or `.stars render` flags. AstraTerra's own Earth Milky
Way is not removed by catalog replacement, so both bands draw by default unless its
`MilkyWayBrightness` is set to `0.0`. This is a known rendering integration gap.

`GalaxySkyView` caches one glow array by cosmology seed and returns clones. The panel and sky renderer
share that expensive result. `CelestialTextureLibrary` separately caches decoded body textures by
manifest ID. Both caches assume that data under a seed or asset ID is immutable.

### Near bodies and the moon

`NearSky.Author` produces geometry records. `NearBodyExport` chooses client assets and converts them
to AstraTerra `NearBodyEntry` records. `BodyFacePainter` and `CelestialFaceComposer` composite the
selected planet, ring, and moon images before AstraTerra applies per-frame lighting and overlap.

`NearBodyCatalog.HidesVanillaMoon` is always true, including on planet worlds generated with no moon.
AstraTerra's `MoonArt` selection cannot restore the Vintage Story disc while this catalog is active.
Only the drawing is hidden: Vintage Story moonlight and calendar phase continue.

The generated parent and moon motion is circular/coplanar and does not include orbital perturbations.
Home moons keep a fixed declination instead of a full inclined monthly path. Those limits should be
preserved in player documentation unless the model changes.

## Assets and handbook

[`assets/astraextera/config/celestial-textures.json`](../assets/astraextera/config/celestial-textures.json)
lists 12 giant, 54 moon, and 16 ring images under `assets/astraextera/textures/celestial/`. The loader
uses snake-case JSON names and selects art deterministically by authored color and seed. It validates
missing and nonsquare images at load time, but does not currently reject an unexpected manifest
`schema_version`. If an image cannot be used, body painting falls back to a flat generated disc.

`make celestial-textures` runs `tools/celestial-textures/prepare.py` against the repository's source
art. The generated PNGs and manifest are build inputs and are committed. This tool is not part of
game startup.

The AstraExtera handbook page is an asset in AstraTerra's `astraterra` category. Its visible strings
live in `assets/astraextera/lang/en.json`. Keep item instructions in AstraTerra's handbook; the
AstraExtera page should explain only how the generated sky changes those instruments and which
limitations apply.

## Configuration

AstraExtera has no config class or mod configuration file. `StarFieldOptions` contains authoring
defaults for tools and tests, but no runtime loader exposes them to players or servers. The texture
manifest is an asset index, not player configuration.

Rendering, coordinates, clock display, and instrument settings belong to AstraTerra. In particular:

- `LongitudeAwareSun` and `DisplayedClockTime` affect the coordinate/time pipeline and require a
  restart after file edits;
- `MilkyWayBrightness` controls only AstraTerra's Earth band;
- `StarfieldMode` controls AstraTerra's star renderer, not AstraExtera's glow or near bodies;
- `MoonArt` and `SolarSystemArt` do not replace AstraExtera-authored near-body faces or add telescope
  discs to its generated companions.

Do not add an AstraExtera setting to documentation until it has a runtime load path and a defined
client/server owner.

## Extension and integration contract

### Supported API

AstraExtera exposes no supported public extension API. Its records and helpers are `public` largely
for cross-namespace use and tests; their namespaces, constructors, serialization shapes, and behavior
may change with the implementation. There is no catalog registry, merge hook, generated-state query,
event, invalidation method, or JSON drop-in directory in AstraExtera.

The supported integration seam is AstraTerra's `AstraTerraModSystem`:

```csharp
var astraTerra = api.ModLoader.GetModSystem<AstraTerraModSystem>();
if (astraTerra is null)
{
    return;
}

var replaced = astraTerra.ReplaceStarCatalog(myCompleteCatalog);
astraTerra.ReplacePlanetCatalog(myCompletePlanetCatalog);
astraTerra.ReplaceCometCatalog(myCompleteCometCatalog);
astraTerra.ReplaceMeteorShowers(myCompleteShowers);
astraTerra.ReplaceNearBodies(myCompleteNearBodyCatalog);
```

These calls replace complete catalogs. They do not append or merge. There is no separate invalidation
step; AstraTerra pushes a replacement into consumers that already exist. `ReplaceStarCatalog` alone
returns `false`, which means AstraTerra did not have a usable catalog to replace.

For stars, replace on both server and client because server constellation services also read the
catalog. Run after AstraTerra has loaded its assets and built the relevant side. Preserve stable HIP
IDs across reloads because books store them as save data.

Multiple replacement mods are **last writer wins**. AstraTerra provides no ownership or composition
protocol. A mod that loads after AstraExtera can replace its catalogs; a mod that loads before it can
be overwritten when the server packet arrives. Set an explicit mod dependency/order and document
which mod owns each complete catalog.

### Adding content to AstraExtera's generated sky

There is no supported additive route. A mod may build complete AstraTerra replacement catalogs of its
own, but it cannot query AstraExtera's current generated `GalaxySky` through a stable API and append
one body. Depending on `GalaxyClientSync`, `GalaxySky`, the save keys, or the packet type is depending
on internal implementation.

If additive generated content becomes a product requirement, add an explicit versioned contract
instead of asking consumers to reflect over the current mod system. That contract needs:

- a side and lifecycle point where the stored `GalaxySky` is available;
- immutable query records separated from internal generator records;
- catalog composition and ID ownership rules;
- server/client agreement for constellation validation;
- a refresh event or revision value stronger than the current seed-only guard.

Until such a contract exists, do not advertise star-catalog replacement, catalog addition,
astronomical-state queries, or cache invalidation as AstraExtera APIs.

### Compatibility boundaries

- A mod that replaces any AstraTerra catalog competes with AstraExtera for that complete catalog.
- A mod that changes `IGameCalendar.OnGetSolarSphericalCoords` must coordinate with AstraTerra's
  longitude-aware wrapper or longitude can be applied twice.
- A mod that changes Vintage Story latitude changes the projection of both AstraTerra stars and
  AstraExtera glow because both read the calendar latitude delegate.
- A mod that expects the vanilla moon renderer will lose its visible disc while AstraExtera's near
  catalog is active, although the calendar state remains.
- Books and Sky Discs persist AstraTerra star IDs independently of AstraExtera's save blobs. A reroll
  changes their meaning without migrating the items.

## Build, tools, and tests

The repository targets `net10.0` and pins SDK `10.0.100`. `AstraExtera.csproj` resolves Vintage Story
from `VINTAGE_STORY`, defaulting to `/Applications/Vintage Story.app`. It resolves AstraTerra from
`ASTRA_TERRA`, then a sibling `../astra_terra` build, then the installed macOS Mods path. The
AstraTerra reference is not copied into the package because it is a separate required mod.

```bash
make test
make build
make package
```

`make package` includes `LICENSE`, `modinfo.json`, and all AstraExtera assets. `make deploy` removes an
older deployed AstraExtera folder/zip and copies the package into `MODS_DIR`; use it only when that is
the intended local installation.

Generation and documentation tools:

```bash
make galaxy-preview SEED=42
make star-catalog SEED=42
make celestial-textures
make moddb-preview
make moddb-copy
```

The preview and live panel share facts and geometry helpers, which reduces drift but does not prove
the in-game renderer. Tests cover deterministic generation, scientific constraints, persistence,
binary codecs, AstraTerra export, near-body geometry, cubemap mapping, commands, art selection, and
bootstrap packaging. Changes to rendering or interaction still need a restarted client and a real
world at representative latitudes, longitudes, times, and both observer-world kinds.

Start a subsystem change at its authoring type, then follow the stored record into the export and
renderer boundary. For example:

- star density: `StarFieldSampler` -> `StarFieldCodec` -> `StarCatalogExport` -> AstraTerra star pass;
- companion orbit: `LocalSystem` -> `LocalSystemSky` -> `LocalSystemSkyExport` -> AstraTerra planet
  ephemeris;
- parent/moon motion: `NearSky` -> `NearBodyExport` -> AstraTerra near-body renderer;
- galactic band: `GalaxyGenerator` density/dust -> `GalaxySkyView` -> `SkyCubemap` ->
  `GalaxyGlowRenderer`;
- save compatibility: model/codec -> `GalaxySkyStore.Resolve` -> server packet -> client decoder.

Update the player guide whenever a model change alters something visible, an interaction condition,
or the meaning of the galaxy panel. Update the placement schema only after deciding how existing
saves and constellation IDs should behave.

### Manual runtime checks

After a rendering, networking, catalog, or coordinate change, verify a restarted game rather than
stopping at `make test`:

1. Load a planet-world cosmology. Confirm `/astraextera galaxy` reports its seed and catalog counts,
   and that the panel's moon count agrees with the visible near bodies at a dark hour.
2. Load a moon-world cosmology. Confirm the parent stays fixed, phases with the sun, and orders
   sibling occlusion correctly.
3. Observe at the equator and in both hemispheres. Confirm the fixed stars and generated glow keep
   registration while latitude changes.
4. Travel far enough east or west to make longitude visible. Confirm the sun, fixed stars, glow,
   Sextant, Astrolabe, and displayed local time agree.
5. Join from a second client. Confirm both clients receive identical catalog counts and panel facts.
6. Reroll to an explicit seed. Confirm connected clients update, the result survives a server
   restart, and the terrain seed remains unchanged.
7. Check `StarfieldMode`, both Milky Way passes, `MoonArt`, telescope zoom, constellation drawing,
   Sextant records, and Sky Disc marks against the limitations in the player guide.

Record the cosmology seed, observer world kind, coordinates, date, hour, mod versions, and relevant
log lines for any failed visual check. The automated suite does not currently provide this evidence.
