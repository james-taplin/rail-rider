import bpy, math, os
root=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=os.path.join(root,'rat_gallery.blend'))
scene=bpy.context.scene
scene.render.resolution_x=720; scene.render.resolution_y=480; scene.render.resolution_percentage=100
scene.cycles.samples=8
scene.render.fps=10
scene.frame_start=1; scene.frame_end=64
for i in range(1,5):
    o=bpy.data.objects['Rat_%02d_Pivot'%i]
    # A fresh temporary action for the companion render; original source is not overwritten.
    o.animation_data.action=None
    points=[(1,0)]
    if i in (1,3): points.extend([(5+(i-1)*2,0),(8+(i-1)*2,-math.pi/2),(19,-math.pi/2),(22,0)])
    else: points.append((22,0))
    start=27+(i-1)*4
    points.extend([(start,0),(start+3,-math.pi/2),(48,-math.pi/2),(52,0),(64,0)])
    for frame,angle in points:
        o.rotation_euler.x=angle; o.keyframe_insert(data_path='rotation_euler',frame=frame)
reset=bpy.data.objects['Reset_Pivot']; reset.animation_data.action=None
for frame,angle in [(1,0),(2,-24),(3,5),(4,0),(19,0),(20,-24),(21,5),(22,0),(48,0),(49,-24),(50,5),(52,0),(64,0)]:
    reset.rotation_euler.x=math.radians(angle); reset.keyframe_insert(data_path='rotation_euler',frame=frame)
scene.render.image_settings.file_format='PNG'
scene.render.filepath=os.path.join(root,'preview_frames','frame_')
bpy.ops.render.render(animation=True)
