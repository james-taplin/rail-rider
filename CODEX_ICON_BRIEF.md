# Brief: Rail-Rider inventory icons (rack, six cans, bottle)

## Context
Rail-Rider is a Derail Valley mod (BB plinking rifle). You already made the rifle's inventory icon
(`Models/rail_rider_inventory.png`, a 512x512 transparent render of the closed-lever model, angled ~3/4 view,
soft lighting, no background, no text). The rest of the mod's items currently have flat colour squares as
placeholders. Make proper icons for them in the same style so the inventory and shop look consistent.

You are making icons only. Do not edit the C# in this folder, the rifle model, or the installed game. Claude will
hook the files up and test in game.

## Items (8 icons)
Build each as a simple 3D model in Blender (your usual route), render it, and export a transparent PNG. Dimensions
and colours below match the in-game props so the icon looks like the item.

| File | Item | What it is |
|---|---|---|
| `icon_rack.png` | Rail-Rider Rifle Rack | wooden plate about 0.70 x 0.10 m with two small wooden hooks at x = +/-0.25 m, wall-mounted; warm dark wood (about 0.35, 0.22, 0.12). Show it from the front, slightly angled and slightly from above. Optional: the rifle resting on it is NOT wanted, show the rack alone. |
| `icon_can_red.png` | Can - Red | generic 330 ml aluminium can, 66 mm diameter x 116 mm tall, rim/base in plain aluminium. Body red (0.72, 0.09, 0.09), a plain white band around the middle (about 40 mm tall). |
| `icon_can_green.png` | Can - Green | same can, body green (0.12, 0.55, 0.20), band yellow-green (0.90, 0.95, 0.30). |
| `icon_can_orange.png` | Can - Orange | same can, body orange (0.90, 0.48, 0.08), band white. |
| `icon_can_blue.png` | Can - Blue | same can, body blue (0.10, 0.28, 0.72), band pale blue (0.80, 0.90, 1.00). |
| `icon_can_silver.png` | Can - Silver | same can, plain bare aluminium body (0.74, 0.76, 0.80), band darker grey (0.45, 0.47, 0.50). |
| `icon_can_black.png` | Can - Black | same can, body near black (0.07, 0.07, 0.08), band gold (0.85, 0.70, 0.20). |
| `icon_bottle_green.png` | Green Glass Bottle | green glass bottle, no label: 62 mm diameter body about 160 mm tall, rounded shoulder, 26 mm neck about 90 mm long, small lip; total about 260 mm. Translucent green glass (about 0.10, 0.55, 0.22) with believable glass highlights and a hint of the background showing through. |

## Style
- Match `rail_rider_inventory.png`: transparent background, one object, angled 3/4 view, soft studio lighting,
  gentle contact-free shadow or none, no text, no frame, no border.
- Cans are generic and unbranded: NO logos, NO text, NO barcodes. Just colour bands. A little bevel and rim detail
  is welcome, and the same camera angle across all six cans so they read as a set (cans upright, slightly tilted
  toward the camera, top ellipse visible).
- Keep objects centred and filling about 80% of the frame with equal margins, so they sit well in a square
  inventory tile. (The rifle icon is long and thin; cans/bottle are tall, so tall subjects should fill the frame
  height.)
- Stylised-realistic, clean surfaces, moderate detail; the game's own item icons are clean renders on transparency.

## Technical
- 512 x 512 px, RGBA PNG, transparent background (premultiplied-alpha fringing removed: no dark or white halo around
  edges when composited over a grey tile).
- Save into `Models/icons/` in this folder with exactly the file names above.
- Claude generates the greyed "item missing" variant in code from your PNGs, so deliver only the normal one.

## Deliverables
- The eight PNGs in `Models/icons/`.
- Your build/render script next to them (as for the rifle) so they can be revised.
- A short note listing anything that deviates from the table (for example if you changed a colour for legibility).

Do not touch the installed game or the Mods folder.
