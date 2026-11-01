# Rail-Rider (Derail Valley plinking rifle + cab rack) 0.3.0 beta

A Red Ryder-style lever-action BB rifle, a buyable cab rifle rack, six generic cans and a breakable green glass bottle.
Needs Unity Mod Manager and Custom Item Mod 0.2.0 (`custom_item_mod`). Beta: tested in game by the author only; see `README_BETA.txt` for what is still unproven.

## Build and package
`powershell -File build.ps1` builds Release and assembles `build_out\RailRider\` (DLL, Info.json, `Models`, `Audio`). It never installs.
Install = copy that folder to `<Derail Valley>\Mods\RailRider` with the game closed, and delete any stale `*.cache` there.
The compiled DLL references the game's assemblies via `DVRoot` (default `B:\SteamLibrary\steamapps\common\Derail Valley`); pass `-DVRoot` to `build.ps1` to change it.

## Layout
- `*.cs` - the mod. `Factories.cs` builds the items, `RifleController.cs` is the rifle state machine, `Ballistics.cs` the live BB, `Scatter.cs` the litter, `Props.cs` the cans and bottle.
- `Models/` - shipped art: `rail_rider_rifle.glb`, `rail_rider_rack.glb`, `rail_rider_inventory.png`, `icons/`.
- `Audio/` - shipped sounds (16-bit mono WAV). Any can be replaced by the player.
- `Model_Work/`, `Audio_Work/`, `Models/*.py` - Codex's Blender and audio source scripts for the shipped art and sounds.
- `CODEX_*_BRIEF.md` - briefs given to Codex for the model and icons.

## Controls (UMM panel can rebind)
Y raise/lower, U cock, left click fire, Shift while aiming = subtle zoom. F8 debug spawn (rifle + targets), Shift+F8 rack, Ctrl+F8 rat gallery, F7 one of each prop.

## Design
- Items are built in code (no asset bundle) and injected into Custom Item Mod's item list.
- Model contract is `RifleRefs` (Lever, RearSight, FrontSight, Muzzle, Chamber, RackDock named nodes in the GLB).
- Rack = native `GadgetWithSnapPoint` + `SnapPointGadget` (LongCylinder). DV's own snap code handles parenting and save/load of the rifle-on-rack link.
- Cocked state is saved in the rifle's save data (`rrCocked`). Props and scatter are created with `BelongsToPlayer = false`, so they are never saved.
- BB is a live projectile (gravity, drag, travel time), zeroed for 15 m.

## Licence
MIT, see `LICENSE`. Requires Custom Item Mod by Tostiman, which has its own licence and is not included here.

## Shop prices (career base prices)
Scaled against DV's own General Store items, read from the game's scene data: Hammer $300 (stock 10), Soldering Gun $5,000 (stock 3).

| Item | Price | In hammers | In soldering guns |
|---|---|---|---|
| Rail-Rider rifle | $1,500 | 5 | 0.3 |
| Rail-Rider rifle rack | $600 | 2 | 0.12 |
| Rail-Rider rat gallery | $900 | 3 | 0.18 |

Cans and bottles are never sold (litter only); their listed prices ($5, $8) are unused.
