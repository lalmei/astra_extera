# AstraExtera Player Guide

AstraExtera gives the current save a generated star field and planetary system. AstraTerra draws
that data and supplies the instruments used to observe it. Both mods must be enabled.

The astronomy belongs to the save. Players joining the same server receive the same stored galaxy,
stars, planets, comets, meteor showers, and nearby moons.

## Get started

1. Install Vintage Story 1.22.2 or newer.
2. Download compatible AstraTerra and AstraExtera release zips. Put both, still zipped, in that game
   instance's `VintagestoryData/Mods` folder, or install them through the game's mod manager. A
   dedicated server and the clients joining it must load both code mods.
3. Enable both mods. AstraExtera requires AstraTerra 0.10.0 or newer.
4. Join a world. The first load can take longer while the server authors and stores the sky.
5. Press **Ctrl+Shift+S** to open the galaxy panel. **Galaxy panel** in the controls menu changes this
   key binding.
6. Press **H**, open **Astronomy**, and choose an instrument. The AstraTerra handbook pages contain
   the recipes and item links.
7. For a first observation, make a Sky Disc and mark a sunrise or sunset. Once you have brass and
   glass, use a telescope and a book to draw a constellation.

The panel is an atlas of the generated setting. It contains face-on and edge-on galaxy views, an
all-sky map, the local system, companion portraits, and the generated facts. `/astraextera galaxy`
prints a compact summary in chat. Neither view identifies the point currently under the crosshair.

## Observing tools

These tools are implemented by AstraTerra. AstraExtera changes the sky they operate on.

### Lie down

Press **Z** to lie on your back and give the sky more of the screen. The binding is remappable.

### Telescope

Hold right click to look through a telescope. Scroll while scoped to change magnification. Middle
click cycles four modes:

- **Observe** changes only the view.
- **Create Constellation** adds a line by dragging with left click from one visible star to another.
- **Inspect Constellation** selects a saved figure so it can be named or renamed.
- **Remove Segment** deletes the line under the pointer.

Creating, naming, or changing a constellation requires a writable normal book in the left hand and
ink and quill in the inventory. A dark, clear sky with open sky overhead is required for drawing.
The brass telescope has five zoom steps. The precision telescope has ten stronger steps and the same
modes.

The telescope does not identify a light automatically. Record it with the Sextant and compare
sightings, or watch whether it moves against the fixed stars.

AstraExtera currently supplies no deep-sky objects, so its sky has no generated nebula, cluster, or
galaxy plates to resolve. Generated companion planets also lack resolved telescope artwork and remain
points of light under magnification. The planet portraits in the galaxy panel are diagrams, not the
telescope view.

### Sextant

Hold right click and center a light to measure it. Middle click cycles the angle and coordinate-grid
displays. While sighting, sneak with a writable book in the left hand and ink and quill in the
inventory to write a dated entry.

An entry records the measured altitude, bearing, brightness, time, and observer position. It does not
record the object's identity. Two entries taken at different times can show whether the light moved
relative to the fixed sky. `.stars sightings` lists recorded sightings, and `.stars classify` helps
compare them.

### Astrolabe

Fit a ruined astrolabe with a brass plate. After dusk, stand under open sky and sneak-hold right click
to cut the plate for the current latitude. A plate used far from that latitude reports the offset and
eventually asks to be recut.

Right click with a written astronomy book to forecast a recorded constellation or identified
wanderer. Middle click changes the target. Scroll changes the hour; sneak-scroll moves by seven days.
The plate fixes the latitude used by the forecast, while the clock follows the current longitude.

### Sky Disc

Form a clay disc and fire it, or craft a metal one. Keep flint or a knife anywhere in the hotbar. At
sunrise or sunset, stand under open sky, sneak, and hold right click for about one second to mark the
sun's position. The first mark binds the disc to that latitude. Continue through the year until the
band reaches an edge and turns back; the disc can then report the year length, latitude, cardinal
direction, and the next solstice.

Right click to read a disc and scroll to turn it. While holding it under the stars, drag with left
click from star to star to cut one connected constellation. Raw clay accepts a figure before firing;
fired clay does not. Metal accepts a figure at any time. A Sky Disc needs no book or ink.

Sneak-right-click a block to place the disc. Right click it with an empty hand to pick it up.

## Constellations and books

AstraExtera's generated catalog contains no inherited Earth constellations or sky culture. A
constellation is a set of lines chosen by a player and stored in a book or Sky Disc.

Hold the book to show its figures in the sky. Give it to another player and the figures travel with
the item. After the first constellation is written, ordinary vanilla book editing is locked, but the
astronomy journal can still be changed through the telescope and `.stars` journal commands.

The fixed stars use numeric IDs assigned in brightness order. The server stores the sampled catalog
so those IDs remain stable on normal reloads. An admin reroll deliberately replaces that catalog;
old constellation lines then connect different stars.

Do not rely on AstraTerra's Earth-template commands such as `.stars build Ori`, or on its prepared
Earth catalog books, in an AstraExtera world. Their HIP star IDs describe AstraTerra's shipped Earth
catalog, not the generated sky.

## What moves, and why

AstraExtera stores fixed stars in **equatorial coordinates**: right ascension and declination. They
are coordinates on an imaginary **celestial sphere** surrounding the observer.

- **Right ascension** is a star's east-west coordinate on that sphere.
- **Declination** is its angle north or south of the celestial equator.
- **Altitude** is how many degrees an object is above the local **horizon**. The zenith is 90°.
- **Azimuth** is the compass bearing around the horizon.
- **Latitude** is the observer's north-south position. It changes which celestial pole is above the
  horizon and which stars can rise.
- **Longitude** is the observer's east-west position. It changes the local hour when AstraTerra's
  longitude-aware sun is enabled.

AstraTerra combines right ascension and declination with latitude and the current local sidereal
angle to obtain altitude and azimuth. **Sidereal motion** is the daily turning of the star field as
the world rotates. A star can therefore be below the horizon now, rise later in the night, appear in
another season, or never rise at the current latitude.

Vintage Story provides latitude from world Z on a realistic-climate world. On other climate modes it
may report one latitude everywhere, so walking north or south does not tilt the sky. The equator is
placed from the world seed rather than fixed at Z = 0, and the latitude pattern repeats across a
large world.

Vintage Story has no native longitude. With AstraTerra's `LongitudeAwareSun` enabled, world X is
mapped to longitude using the world's `polarEquatorDistance`. The sun, daylight, stars, instruments,
and displayed local time then shift together. At the default scale, 90° is 50,000 blocks and one
hour of sky rotation is about 8,300 blocks.

Time affects three different things:

- the hour and longitude turn the whole sky;
- the date changes which part of the sky is up at night and advances companion planets;
- generated comets and showers appear only near their authored return windows.

## Planet worlds and moon worlds

On a **planet world**, the generator provides zero to three moons. Each travels around the sky at a
rate derived from its generated month. A slow moon rises later on successive days; a moon with a
month shorter than the day can move the other way across the sky. A moonless night is a valid result.

On a **moon world**, the playable world is tidally locked to a gas giant. The parent therefore stays
at one hour angle instead of rising and setting. It changes phase with the sun. Its rings appear
almost edge-on because the home moon and rings share the giant's equatorial plane. Sibling moons can
cross in front of the giant, pass behind it, or travel around the sky according to their relative
orbits.

The generated near-body motion is a game model. Planet-world moons use circular paths at fixed
declination. The moon-world system uses circular, nearly coplanar satellite geometry, and the parent
giant is fixed. These are intentional approximations rather than a full gravitational integration.

The ordinary Vintage Story moon disc is hidden in both world types. Vintage Story still supplies
moonlight, the calendar phase, night length, and the rest of the calendar simulation. Setting
AstraTerra's `MoonArt` to `vanilla` does not restore the ordinary disc while AstraExtera's near-body
catalog is active.

## What the generated facts mean

The galaxy panel assigns the home world a radius, gravity, composition, equilibrium temperature,
host star, orbit, and neighbors. These values constrain and describe the generated astronomical
setting. They do not alter blocks, ores, terrain, player gravity, climate, crop behavior, survival
temperature, the sun, day length, year length, or seasons.

The galaxy itself is an analytic Milky-Way-like spiral in most saves and a rare elliptical in some.
The star catalog is a statistical sample from a luminosity function, density model, and dust model;
it is not a list of individually evolved physical star systems. The catalog stops at magnitude 6.5
or the 10,000-star budget, whichever comes first.

Companion planets use Keplerian ellipses without perturbations or precession. Generated comets are
authored sky apparitions with a return period, brightness window, and right-ascension/declination
track. Their meteor showers are scheduled associations, not the result of integrating debris through
an orbital intersection.

## Configuration

AstraExtera has no configuration file in the current release. Its catalog size, limiting magnitude,
galaxy models, and local-system distributions are implementation constants. Server admins can select
another generated result only by rerolling the cosmology.

The visible sky and observing rules come from AstraTerra's `ModConfig/astraterra.json`. The following
settings matter most with AstraExtera:

| AstraTerra key | Default | Effect with AstraExtera | When it applies |
| --- | --- | --- | --- |
| `StarfieldMode` | `astraterra` | Selects AstraTerra stars, both star fields, or Vintage Story stars. It does not disable AstraExtera's generated glow or nearby bodies. | `.stars starfield ...` applies and saves immediately. |
| `SkyGridMode` | `none` | Shows altitude-azimuth, right-ascension/declination, or both coordinate grids. | `.stars sky-grid ...` applies and saves immediately. |
| `SolarSystemArt` | `pixel` | Accepts `pixel` or `photo`, but AstraExtera's generated companions have no disc art, so they remain points in either mode. | `.stars solar-system ...` applies and saves immediately. |
| `MoonArt` | `pixel` | Accepts `pixel`, `photo`, or `vanilla`. AstraExtera supplies separate near-body faces and always hides the ordinary moon disc, so this does not change its generated moons. | `.stars moon ...` applies and saves immediately, but has no visible effect on AstraExtera near bodies. |
| `CalendarDisplay` | `full` | Shows the full date and clock, clock only, or neither in the character panel. | `.stars calendar ...` applies immediately; reopen the panel. |
| `LongitudeAwareSun` | `true` | Makes world X affect the sun, daylight, stars, and instruments. The server's value applies to all players. | Change with the game or server stopped, then restart. |
| `DisplayedClockTime` | `local` | Chooses continuous local solar time, universal world time, or rounded time zones. | Restart after changing it. |
| `MilkyWayBrightness` | `1.0` | Controls AstraTerra's shipped Earth Milky Way only. Set `0.0` to prevent it overlapping AstraExtera's generated glow. Valid range: 0.0 to 2.0. | No command; edit with the client stopped and restart. |
| `StarBrightnessBias` | `1.0` | Scales AstraTerra's star pass. | No command; edit with the client stopped and restart. |
| `GuideStarHighlightStrength` | `1.15` | Scales highlighted guide stars used as drawing anchors. | No command; edit with the client stopped and restart. |
| `DebugMeteorRateMultiplier` | `1.0` | Multiplies meteor spawn frequency for testing. Valid range: 0.0 to 100.0. | No command; edit with the client stopped and restart. |

Valid values for `StarfieldMode` are `astraterra`, `both`, and `vanilla`. Valid values for
`SkyGridMode` are `none`, `horizontal`, `equatorial`, and `both`. `CalendarDisplay` accepts `full`,
`clock`, or `none`; `DisplayedClockTime` accepts `local`, `universal`, or `zones`.

Four keys are present in AstraTerra's generated file but have no effect in its current build:
`SelectionSnapRadiusDeg` (`1.0`), `ShowMinimalHud` (`true`), `ShowReticle` (`true`), and
`DebugGuideStarEmphasisDefault` (`false`). AstraExtera does not consume them either.

`.stars render milkyway off` temporarily disables AstraTerra's Milky Way for the current client
session; it does not disable AstraExtera's glow. `.stars render` choices are diagnostic state and
return to their defaults when the client restarts.

## Rerolling a save's sky

An administrator with `controlserver` can run:

```text
/astraextera reroll
/astraextera reroll 42
```

The first form chooses a random signed 64-bit seed. The second is repeatable. The server immediately
stores and broadcasts the replacement galaxy, star field, and local sky. It preserves Vintage
Story's terrain and world-generation seed.

Use `/astraextera galaxy` before and after a reroll to record the seed. Supplying the current seed is
a successful no-op. Connected clients update without rejoining, although the galaxy panel must be
reopened to inspect the new result.

Rerolling is destructive to the meaning of existing star IDs. Back up the save or accept that old
constellation shapes and star names will point elsewhere.

## When the sky looks wrong

| Symptom | Current explanation or check |
| --- | --- |
| The galaxy panel says it is waiting for the server | The client has not received or decoded the saved-sky packet. Check that the server loaded AstraExtera and that both sides use compatible mod versions. |
| Two galactic bands overlap | AstraTerra's Earth Milky Way and AstraExtera's generated glow are separate passes. Set AstraTerra `MilkyWayBrightness` to `0.0` and restart the client. |
| `.stars starfield vanilla` still leaves a band or nearby moon | The command controls AstraTerra's star pass. AstraExtera's glow and near bodies are separate renderers/catalogs. There is no complete AstraExtera visual toggle. |
| A fixed giant never rises or sets | The playable world is a tidally locked moon. The fixed parent is intentional. |
| There is no moon | A planet world can be generated without one. The ordinary Vintage Story moon is still hidden. |
| Moonlight or the calendar phase disagrees with the visible moons | Vintage Story still controls moonlight and calendar phase; AstraExtera replaces only the visible disc and nearby bodies. |
| A companion planet remains a point in the telescope | AstraExtera exports orbital and photometric data but no telescope disc or moon artwork for generated companion planets. |
| No deep-sky smudges resolve in a telescope | The generated star catalog intentionally has an empty deep-sky-object list. |
| An Earth-template constellation is misshapen | The template's HIP IDs belong to AstraTerra's Earth catalog. Draw a new constellation against the generated sky. |
| The sky does not change after walking north or south | The world climate may report a constant latitude. Use `.stars debug` to inspect the latitude AstraTerra receives. |
| An object is never visible | It may be below the horizon at this latitude, up only at another hour or season, or outside a comet's return window. Check the date, time, latitude, sky exposure, weather, and brightness. |
| Old constellations changed after an admin command | A cosmology reroll kept the book data but replaced the catalog behind its star IDs. |

Automated tests do not replace an in-game visual check. If a procedural star or near body is absent
despite the expected time and geometry, record the cosmology seed, world coordinates, date and hour,
AstraExtera and AstraTerra versions, and relevant log messages when reporting the problem.
