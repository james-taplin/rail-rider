# Reactive targets — model review

Status: two standalone review candidates. James paused further work on the paint tin. The gallery front is labelled `DAD'S RESET-ABLE RAT REVUE`. No changes to the mod, game, runtime Models, shop, scatter or release packages are part of this work. They have not been approved for integration or tested in Derail Valley.

## Open the interactive viewer

Double-click `Open viewer.cmd`. Leave its console window open while reviewing; it starts a server bound to this PC's loopback address and opens the browser. If the browser does not open, go to <http://127.0.0.1:8765/viewer/>. The launcher uses the existing Codex Node runtime if present, otherwise `node` on PATH. If port 8765 is occupied by another service, set `REACTIVE_TARGETS_PORT` to an available port before launching and use the URL printed in the console.

Use the Paint tin and Rat gallery tabs to switch. Drag to orbit, wheel to zoom and right-drag to pan; Reset camera restores the initial view. Select either tin reaction to replay it from intact. On the gallery, click an individual rat or its button to drop it, and click the round disc or its button to hit reset. The disc and stem deflect away from the incoming shot, meet the support pole and rebound, then only the fallen rats rise. Sound plays after the first target click; the Sound button mutes or unmutes it. Each rising rat contributes its own overlapping clatter. The model is freely orbitable above, below and behind. For best target selection, look into the open front.

## Files

- `tin/discarded_paint_tin.blend` and `.glb`: editable source and metre-scale exported model. `tin_still.png` and `tin_reactions.gif` are companion previews. GLB clips: `Side_Hit_Rock`, `Lid_Hit_Release`.
- `gallery/rat_gallery.blend` and `.glb`: editable source and metre-scale exported model. `rat_gallery_still.png`, `rat_gallery_front.png` and `rat_gallery_animation.gif` show the latest hooded design. Four named pivots each have Down and Up clips; `Reset_Pivot` has its own `Reset_Hit` clip. See `gallery/README.md` for dimensions and export details.
- `sounds/` contains original procedural PCM WAV candidates: four rat falls, a related reset ting–chhk, a separate quieter pole-contact tick and four layerable rat returns. `gallery_sound_audition.wav` compares an empty reset, one rat and four rats; see the sound README for details and level measurements.
- `REFERENCES.md` records source references, design choices and estimated dimensions. Original geometry and textures were made for these candidates.

## Current decision and checks

The gallery was revised for James's review: rat body centres and reset centre are approximately level; the housing has a roof, end caps, bottom, rear casing and 45° internal backstop, leaving the front open. A shared axle supports the individual rat hinges. The full reset paddle, including its stem, hinges away from the shot, reaches the axle and rebounds even with no rats down. Its contact is timed to precede the fallen rat Up clips. The first sound designs were rejected as too hollow and wooden; revised candidates use sharper metallic hits. Following further feedback, each rat return has three variably pitched contacts over 0.16 seconds, and all sounds have a short metallic housing response. These remain subject to listening review. There is no simulated internal reset mechanism.

Both exported GLBs were opened in the local browser viewer. The tin's lid release and side-hit replay were checked, including returning to a seated lid after release. For the revised gallery, direct rat and reset selection, partial and full resets, and orbiting behind and underneath were checked. After the support and paddle revision, the viewer played the loaded fall, reset and return sound files for one and four rats, and the mute control was checked. Blender render frames show the complete paddle tilting; the gallery source records degree-sampled clearance checks. These do not constitute continuous collision or physics tests. The WAV mix was measured for headroom but has not been subjectively auditioned on the game's audio system.

The viewer is a model-review tool using the actual exported meshes, embedded materials and animation clips. It tracks target states in the browser; these interactions do not constitute game behaviour. Runtime loader compatibility, in-game scale/lighting and collision behaviour remain unverified. Keep these candidates in Model_Work pending review.
