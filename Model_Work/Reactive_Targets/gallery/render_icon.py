# Transparent 3/4 inventory render of the rat gallery. Run: blender -b -P render_icon.py
import bpy, os
from mathutils import Vector
root=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=os.path.join(root,'rat_gallery.blend'))
scene=bpy.context.scene
keep=bpy.data.collections.get('EXPORT_GALLERY')
for ob in scene.objects:
    if ob.type=='MESH' and (keep is None or ob.name not in keep.all_objects): ob.hide_render=True; ob.hide_viewport=True
scene.render.film_transparent=True
scene.render.resolution_x=900; scene.render.resolution_y=900; scene.render.resolution_percentage=100
cam=scene.camera
cam.data.type='PERSP'; cam.data.lens=60
cam.location=(-.62,-.95,.42)
cam.rotation_euler=(Vector((0,0,.1))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=os.path.join(root,'icon_raw.png')
bpy.ops.render.render(write_still=True)
