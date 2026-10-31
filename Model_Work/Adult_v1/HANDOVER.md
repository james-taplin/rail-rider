# Rail-Rider adult model review

Status: actual Blender model and review GLB built, not installed or promoted to release Models. James requested previews before final build. The 0.1.0 release assets are unchanged. Current work incorporates his review corrections: centred smaller logo, irregular grain, rounded stock, fitted buttplate, receiver-slot attachment and a flatter double-loop lever.

## Deliverables

- `rail_rider_adult.blend`: packed editable model, materials, preview lights/camera and lever-cycle action.
- `rail_rider_adult_preview.glb`: runtime-compatible GLB, no baked animation clip. Existing RifleController owns motion.
- `build_adult.py`: reproducible geometry, texture-material authoring, export, static renders and sampled sweep.
- `render_motion.py`: actual mesh cocking preview and closeups.
- `renders/`: shooter side, recessed logo, receiver, sights, open lever, plain far side, fitted buttplate, pivot slot closed/open and illustrative carry framing.
- `geometry_audit.json`, `export_validation.json`, `verify_export.py`: validation evidence.
- `beech_basecolor.png`, `beech_normal.png`: 2048px atlas, also embedded in the GLB. `woodcut_height.png` and `woodcut_mask.png` are authoring maps, not additional runtime requirements.
- `logo_approved_mask.png`, `beech_source.png`: image-generation inputs saved locally.
- `UnityRenderCheck/` and `unity_checks/`: isolated built-in Standard shader renderer test using a copy of the project loader. This is Unity 6000.6.2f1, NOT Derail Valley running in game.

## Current asset contract

Metres, +Y up, +Z muzzle. Length 0.933 m. 22,680 triangles, 21 meshes, seven materials. This exceeds the historical brief's 12k/2-material targets; separate finishes deliberately distinguish wood, barrel, receiver, lever, hardware and sights per James's current request. Each mesh has one primitive/material; hierarchy is flat and scales are applied for the current loader.

Required names preserved: Lever, RearSight, FrontSight, Muzzle, Chamber, RackDock. Marker coordinates are in export_validation.json. Lever pivot is now (0, -0.022, -0.042), inside the receiver's underside slot. Game animation remains local X from 0 to -70 degrees, already selected by Factories. No controller rewrite or new animation system is required. The Blender preview action is presentation only; it does not override gameplay timing.

The lever follows the adult side photograph and the 1938B exploded drawing (part 98L): one closed rear loop and an open scallop that forms the trigger opening against the receiver. This is a visual reconstruction. Its head sits inside the receiver mortise, and fixed pin heads show the attachment on the receiver sides. Trigger is centred laterally; geometry checks include it, not just the stock.

The buttplate's front perimeter is generated from the stock's exact end section. Export validation measured zero gap at the shared perimeter. It is a thin dark metal buttplate, not the previous oversized slab.

## Logo and wood

Approved identity: simplified completely side-on locomotive, larger flowing RailRider cursive. Logo reduced from the first mesh pass, moved upward/back onto the broad butt, centred along that panel's sloping axis with clear margins. Art exists on GLB +X only, mapping to Unity -X through the current loader. Do not rename StockVisual to Stock: the loader has a legacy UV swap for Stock.

The striped procedural wood was rejected by James. It has been replaced with an irregular generated stained-beech grain source. Stock cross-sections are genuinely rounded, not merely flat geometry with smooth shading. The nominal recess is 0.20mm of virtual normal-map relief with rounded shoulders; it does not cut silhouette geometry. Highlight/shadow response is dynamic, not baked into base colour. Mipmaps alone do not produce this effect.

## Minimal integration changes already made

`GltfLoader.cs`: load glTF normalTexture as linear data; pack it for Windows built-in AG/RG-or-AG normal decoding; bind _BumpMap/_BumpScale, enable _NORMALMAP; rebuild tangents after position and UV conversion. Warn if the inherited material shader lacks _BumpMap. Existing colour and metallic/roughness handling retained. Original file saved as GltfLoader.before.txt.

`RailRider.csproj`: exclude Model_Work/**/*.cs so the isolated Unity test scripts are not compiled into the mod.

No game files, release GLB, inventory icons, gameplay controller, ballistics changed. Do not run the existing recursive package-copy script against candidate files inside Models: this revision deliberately lives in Model_Work to avoid accidental promotion.

## Verified / limitations

- Release C# build succeeded with zero warnings/errors against installed game assemblies after the integration changes.
- All 21 source meshes: closed/manifold, consistent edge winding and positive signed volume.
- Lever sampled at every degree from 0 through -70: no triangle overlap with any fixed mesh, including Trigger. Sampled check, not continuous collision proof.
- Export: required nodes, metre bounds, applied scale, flat hierarchy, one primitive per mesh, stock tangents and embedded normal map verified.
- Rear/front markers share the sight line; rendered view is taken 14cm behind rear marker and shows an unobstructed notch/front blade.
- Isolated Unity test: Standard shader receives the normal map, keyword and tangents. Normal-on/off renders verify relief lighting. Shooter-facing logo reads correctly in the loader's coordinate conversion.
- Controlled culling A/B: CURRENT mirror-X plus reversed indices renders the intended external marked face; removing reversal exposes the wrong-facing/plain surfaces. Earlier handover winding hypothesis is NOT supported by this test. Keep current winding conversion and outward asset normals; do not invert geometry or duplicate faces.
- Unity test version is newer than the game's runtime, and its lighting is only a test setup. Actual in-game FOV, lighting, carry/cock framing, rack fit, player-view clearance and runtime shader variants remain untested. The illustrative carry image uses a camera assumption; it is not proof that the whole stock logo is within every game's FOV.
- Final packaging/install awaits the requested visual review and in-game validation. The generated review GLB is a candidate, not a release claim.

## Image source prompts

Built-in image_gen was used, not API/CLI fallback. Logo prompt: extract/reproduce the approved v3 upper logo as a crisp black transparent production mask, retaining completely side-on locomotive, three wheels/rod, steam flourish and large cursive RailRider; no wood, captions, shadows or relief. Wood prompt: a continuous square dark walnut-stained European beech base-colour texture, fine irregular horizontal fibres/pores and restrained natural variation, flat illumination, no logo, plank seams, knots, cracks or repeated stripes. Actual texture maps and model previews are authored/rendered in Blender from these sources.

Normal packing was checked against the installed Unity editor's UnityStandardUtils.cginc and UnityCG.cginc. Related primary source: https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl .

Current review: raised Dad's is centred from actual glyph bounds on both lever faces. Four-line receiver inscription is separate and exclusive to the shooter face, using embedded base colour and recessed normal maps on the existing shell. Opposite face and edges sample plain metal. Exact copy including EXCERCISE is in make_receiver_stamp.ps1. Carry and cock positions in Factories.cs were adjusted to (.13,-.12,.82) and (.10,-.09,.80) and previewed using the saved 50-degree FOV; these are model previews, not game screenshots.
Release update: James approved the preview. Promoted the identical GLB to Models/rail_rider_rifle.glb and built Rail-Rider 0.1.0-beta Adult Model.zip. The archived rifle and compiled DLL were hash-verified. Installation and in-game testing are pending.
Audio review after model release: Audio/ contains the selected dry rifle, glass, terrain and brick sounds plus yard cans and rolling stock. Numbered WAV takes vary shake and impact playback without consecutive repeats. Audio_Work/Audition/Mixed_repeat_hits.wav is the audition. Brick routing currently uses collider hierarchy or physics material names. The existing release ZIP predates this audio mix.
Approved audio release: built Rail-Rider 0.1.0-beta Adult Model and Sounds.zip. Verified all 40 archived WAVs, the approved GLB and the release DLL against source hashes. In-game playback and brick recognition remain untested.
Rack release: Rail-Rider 0.1.0-beta Rifle Rack and Sounds.zip. The rack uses the existing GLB loader and native gadget snap. All four screws are on the metal hooks. Model_Work/Rack/build_rack.py checks parked rifle mesh clearance and renders loaded and empty views. In-cab placement still needs playtesting.
Current rack backing is unfinished pine, using Model_Work/Rack/unfinished_pine.png with roughness 0.89. Release: Rail-Rider 0.1.0-beta Pine Rack and Sounds.zip.
