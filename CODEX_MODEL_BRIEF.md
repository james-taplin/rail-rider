# Brief: Rail-Rider rifle model (replace the blockout)

## Context
Rail-Rider is a Derail Valley mod: a lever-action BB plinking rifle, a clear nod to the Daisy Red Ryder (1938-style).
The player holds it floating in the lower right of the first-person view (no hands are modelled), raises it to look
down the sights, and works the lever before every shot. The current model (`Models/rail_rider_rifle_placeholder.glb`)
is stacked boxes and cylinders and looks like a machine gun turned inside out. Replace it with something that reads
instantly as a classic wooden-stocked, blued-steel lever-action BB gun.

You are building the model only. Do not edit the C# in this folder. Claude owns the integration code and will
adapt the loader to your file; flag anything in the contract below you cannot meet.

## What it must read as
- Daisy Red Ryder silhouette: slim blued-steel barrel, short wooden forearm under the barrel (no magazine tube,
  no bayonet lug, no rails, no pistol grip furniture), cast-metal receiver, slightly drooped straight wooden stock
  with a gentle wrist, plain metal buttplate, trigger with a simple trigger guard that is also the **cocking lever**
  (the classic big finger-loop lever that swings forward and down).
- Overall length about 0.90 m, barrel about 0.28 m of that visible past the receiver. Real proportions beat
  exaggeration; the in-game feel is small, light, slightly toy-like.
- Warm walnut/stained-beech wood with visible grain, dark blued steel with a soft sheen, a little brass or nickel
  accent is fine. Aged, loved, not factory-new. Stylised-realistic, consistent with Derail Valley's look (clean
  surfaces, moderate texture detail, nothing noisy).
- Brand art (the existing `Models/rail_rider_brand_plate.png`, steam train + "Rail-Rider" woodburn) goes on the
  **left side of the stock only** (the side the player sees). The right side stays plain wood. Bake it into the
  stock texture or use a decal mesh; either is fine, but no art on the far side.

## Hard contract (gameplay depends on this)
Units metres. Export a single `rail_rider_rifle.glb` (glTF 2.0, binary) plus textures embedded or beside it.
Use standard glTF axes: **+Y up, muzzle pointing +Z**, rifle right side toward +X when viewed from behind
(i.e. a normal right-handed glTF, barrel toward +Z). Origin at the middle of the receiver. Scale 1:1.

Named nodes, exactly these names:
| Node | Type | Requirement |
|---|---|---|
| `Lever` | mesh node | **Origin on the lever's pivot pin** (under the receiver). Rotating it about local **X** swings the loop forward and down; the game rotates it by up to 70 degrees. Its full travel must clear the stock, trigger and the player's view without intersecting the body. The whole lever, including the loop, is one node. |
| `RearSight` | empty | Centre of the rear sight notch, at the **top** of the leaf, exactly on the sight line. |
| `FrontSight` | empty | Top of the front blade/bead, exactly on the same sight line. |
| `Muzzle` | empty | Centre of the barrel opening. |
| `Chamber` | empty | On the bore axis a little ahead of the receiver, behind `Muzzle`. |
| `RackDock` | empty | Where the rack's hooks should take the weight: roughly the balance point; axes aligned with the model (+Z along the barrel). |
| everything else | meshes | Any names; split into logical parts (stock, forearm, barrel, receiver, sights, trigger, buttplate...). One lever, nothing else moves. |

Sights are the point of the gun: the camera sits about 14 cm behind `RearSight` looking along the sight line, so
the rear notch (a proper V or U notch, not a solid plate) and a clear front blade must be crisp and readable from
there, with the receiver top and stock comb low and out of the line of sight. No scope mounts, no rails.

## Technical
- 6k-12k triangles total, single UV set, 1-2 materials max (wood, metal) with 1024-2048 px textures. PBR
  metal-rough (base colour, normal, ORM) preferred; if you can't, base colour + normal only is fine.
- Smooth shading with hard edges only where real (sights, receiver cuts). No flat-shaded boxes.
- Collision is handled in code, so no collider meshes are needed.
- Check it from three views before delivering: left side (this is how it sits in the player's lower right
  while carried), from behind along the sight line, and with the lever fully open.

## Deliverables
In `Models/` in this folder:
- `rail_rider_rifle.glb`
- textures if not embedded
- three preview PNGs (left side carried, behind-sights view, lever open)
- a short note listing the nodes and anything that deviates from the contract

Do not touch the installed game or the Mods folder. Claude will integrate and test in game.
