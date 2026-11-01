"""Locate the tangency between the reset stem and support axle at peak hit."""
import bpy, math, os
from mathutils.bvhtree import BVHTree
root=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=os.path.join(root,'rat_gallery.blend'))
deps=bpy.context.evaluated_depsgraph_get()
def tree_and_vertices(obj):
    ev=obj.evaluated_get(deps); mesh=ev.to_mesh(); mesh.calc_loop_triangles()
    vertices=[ev.matrix_world@v.co for v in mesh.vertices]
    tree=BVHTree.FromPolygons(vertices,[tuple(t.vertices) for t in mesh.loop_triangles],all_triangles=True)
    ev.to_mesh_clear();return tree,vertices
reset=bpy.data.objects['Reset_Pivot'];reset.rotation_euler.x=math.radians(-24)
axle,vertices=tree_and_vertices(bpy.data.objects['Common rat support axle'])
for i in range(0,121):
    y=-.096+i*.0001;reset.location.y=y;deps.update()
    stem,sv=tree_and_vertices(bpy.data.objects['Reset disc stem'])
    disc,dv=tree_and_vertices(bpy.data.objects['Reset_Circle'])
    collisions=len(stem.overlap(axle))+len(disc.overlap(axle))
    d=min([axle.find_nearest(v)[3] for v in sv+dv]+[stem.find_nearest(v)[3] for v in vertices])
    if -.094<=y<=-.089:print(f'{y:.5f} overlap={collisions} vertex_gap={d*1000:.3f}mm')
