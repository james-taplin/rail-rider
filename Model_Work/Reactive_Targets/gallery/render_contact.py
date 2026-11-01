"""Diagnostic side view of keyed contact; housing/rats hidden to reveal axle."""
import bpy, math, os
from mathutils import Vector
root=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=os.path.join(root,'rat_gallery.blend'))
scene=bpy.context.scene
prop=bpy.data.collections['EXPORT_GALLERY']
keep={'Common rat support axle','Reset lower hinge','Reset disc stem','Reset_Circle','Reset circle centre'}
for obj in prop.objects:
    if obj.type=='MESH' and obj.name not in keep:obj.hide_render=True
for name in ('Studio floor',):bpy.data.objects[name].hide_render=True
pivot=bpy.data.objects['Reset_Pivot'];pivot.rotation_euler.x=math.radians(-24)
cam=scene.camera;cam.location=(.45,-.077,.095);cam.rotation_euler=(Vector((0,-.077,.095))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=.15
scene.render.resolution_x=900;scene.render.resolution_y=900;scene.cycles.samples=32
scene.render.image_settings.file_format='PNG';scene.render.filepath=os.path.join(root,'reset_contact_side.png')
bpy.ops.render.render(write_still=True)
