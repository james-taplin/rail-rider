import bpy, math, os
from mathutils.bvhtree import BVHTree
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
p=r'C:\Users\james\Desktop\Derail Valley Mods\Mods (code)\RailRider\Models\rail_rider_rifle.glb'; bpy.ops.import_scene.gltf(filepath=p)
lev=bpy.data.objects['Lever']; lev.rotation_mode='XYZ'; dg=bpy.context.evaluated_depsgraph_get()
def tree(o):
 ev=o.evaluated_get(dg); me=ev.to_mesh(); verts=[ev.matrix_world@v.co for v in me.vertices]; faces=[tuple(p.vertices) for p in me.polygons]; t=BVHTree.FromPolygons(verts,faces); ev.to_mesh_clear(); return t
others=[o for o in bpy.context.scene.objects if o.type=='MESH' and o!=lev]
for deg in range(0,71,5):
 lev.rotation_euler[0]=-math.radians(deg); bpy.context.view_layer.update(); t=tree(lev); hits=[]
 for o in others:
  n=len(t.overlap(tree(o)))
  if n: hits.append((o.name,n))
 print('SWEEP',deg,hits)

