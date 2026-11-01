"""Sample the rat sweep and the reset paddle against nearby fixed geometry."""
import bpy, math, os
from mathutils.bvhtree import BVHTree

root=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=os.path.join(root,'rat_gallery.blend'))
deps=bpy.context.evaluated_depsgraph_get()
def bvh(obj):
    ev=obj.evaluated_get(deps)
    mesh=ev.to_mesh()
    mesh.calc_loop_triangles()
    vertices=[ev.matrix_world @ v.co for v in mesh.vertices]
    faces=[tuple(t.vertices) for t in mesh.loop_triangles]
    tree=BVHTree.FromPolygons(vertices,faces,all_triangles=True,epsilon=0)
    ev.to_mesh_clear()
    return tree

fixed={name:bvh(bpy.data.objects[name]) for name in ('Common rat support axle','45 degree rear backstop','Overhanging shroud roof','Front folded lip','Front upper rolled edge')}
collisions=[]
for index in range(1,5):
    pivot=bpy.data.objects['Rat_%02d_Pivot'%index]
    plate=bpy.data.objects['Rat_%02d_Silver'%index]
    for degrees in range(0,-91,-1):
        pivot.rotation_euler.x=math.radians(degrees);deps.update(); moving=bvh(plate)
        for name,tree in fixed.items():
            overlap=moving.overlap(tree)
            if overlap: collisions.append((plate.name,degrees,name,len(overlap)))
    pivot.rotation_euler.x=0
reset=bpy.data.objects['Reset_Pivot']
for degrees in range(-24,6):
    reset.rotation_euler.x=math.radians(degrees);deps.update()
    for name in ('Reset disc stem','Reset_Circle'):
        moving=bvh(bpy.data.objects[name])
        for target,tree in fixed.items():
            overlap=moving.overlap(tree)
            if overlap:collisions.append((name,degrees,target,len(overlap)))
theta=math.radians(-24)
dy=bpy.data.objects['Common rat support axle'].location.y-reset.location.y
dz=bpy.data.objects['Common rat support axle'].location.z-reset.location.z
local_y=math.cos(theta)*dy+math.sin(theta)*dz
nominal_gap=local_y-.0055-.0019
print('Peak analytic stem-to-axle gap: %.6f mm'%(nominal_gap*1000))
if not (0 <= nominal_gap < .00003):raise SystemExit('Peak pose is not in intended near-contact tolerance')
print('CLEARANCE SAMPLED 4 x 91 rat poses and 30 reset poses:', 'PASS' if not collisions else 'FAIL')
for hit in collisions[:30]:print('OVERLAP',hit)
if collisions:raise SystemExit(1)
