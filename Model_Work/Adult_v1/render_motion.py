import bpy,os,math
from mathutils import Vector,Matrix
P=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=os.path.join(P,'rail_rider_adult.blend'))
s=bpy.context.scene;c=s.camera;d=c.data
def B(p):return Vector((p[0],-p[2],p[1]))
def pose(pos,target,ortho):
    c.location=B(pos);c.rotation_euler=(B(target)-c.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=ortho
def shot(name):
    s.render.filepath=os.path.join(P,'renders',name+'.png');bpy.ops.render.render(write_still=True)
s.frame_set(1);s.cycles.samples=32
pose((.18,.025,-.57),(0,-.075,-.442),.16);shot('07_fitted_buttplate')
pivot=bpy.data.objects['Lever'].location; pivot_model=(pivot.x,pivot.z,-pivot.y)
pose((.20,pivot_model[1]-.11,pivot_model[2]+.09),pivot_model,.13);shot('08_pivot_slot_closed')
s.frame_set(30);shot('09_pivot_slot_open')
# Actual non-VR carry transform from Factories/RifleRefs, mapped back to glTF.
s.frame_set(1)
r=Matrix.Rotation(math.radians(-10),3,'Y')@Matrix.Rotation(math.radians(6),3,'X')
p=r.transposed()@(-Vector((.18,-.20,.70)));f=r.transposed()@Vector((0,0,1));u=r.transposed()@Vector((0,1,0))
p.x=-p.x;f.x=-f.x;u.x=-u.x
c.location=B(p);forward=B(f);up=B(u);right=forward.cross(up).normalized();up=right.cross(forward).normalized()
c.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler();d.type='PERSP';d.lens=24;d.clip_start=.005
shot('10_player_carry_pose')
os.makedirs(os.path.join(P,'frames'),exist_ok=True)
s.render.resolution_x=1200;s.render.resolution_y=600;s.cycles.samples=12
pose((.78,-.012,-.12),(0,-.075,-.077),.52)
for idx,frame in enumerate(range(1,67,2)):
    s.frame_set(frame);s.render.filepath=os.path.join(P,'frames','lever_%03d.png'%idx);bpy.ops.render.render(write_still=True)
print('MOTION_RENDERS_COMPLETE')
