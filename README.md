# AstraExtera

Vintage Story 1.22 mod. Depends on [AstraTerra](https://github.com/lalmei/astraterra) for the sky engine, then replaces Earth's catalog with a **server-authored** procedural sky so every player sees the same heavens.

Worlds are not dropped into a random starfield. The save first draws a Milky Way analog, then a thin-disk location inside that galaxy's habitable annulus, and only keeps sites metal-rich enough for iron cores and ores. Stellar systems and the visible sky come after that placement.

## What is implemented

- Deterministic galaxy + galactocentric location from the Vintage Story world seed
- Galactic habitable zone with `[Fe/H]` floors for iron and ores
- Save-game persistence and a join packet so clients share the server placement
- `/astraextera galaxy` to inspect the authored site

## Build

```bash
make test
make build
make package
make deploy
```

Enable **AstraTerra** and **AstraExtera** together. AstraExtera will not load without AstraTerra.
