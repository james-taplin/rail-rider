import bpy, os
from mathutils import Vector
root=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=os.path.join(root,'rat_gallery.blend'))
scene=bpy.context.scene
scene.camera.location=(0,-1.2,.11)
scene.camera.rotation_euler=(Vector((0,0,.11))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
scene.camera.data.ortho_scale=.58
scene.render.resolution_x=1200; scene.render.resolution_y=600; scene.render.resolution_percentage=100
scene.render.filepath=os.path.join(root,'rat_gallery_front.png')
bpy.ops.render.render(write_still=True)
