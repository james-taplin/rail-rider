import bpy,os,math
from mathutils import Vector,Matrix
P=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=os.path.join(P,'rail_rider_adult.blend'))
s=bpy.context.scene;c=s.camera;d=c.data;s.render.resolution_x=1600;s.render.resolution_y=900;s.cycles.samples=24
d.type='PERSP';d.sensor_fit='VERTICAL';d.sensor_height=24;d.lens=24/(2*math.tan(math.radians(50)/2));d.clip_start=.005
def B(p):return Vector((p[0],-p[2],p[1]))
def shot(name,position,euler,frame):
    s.frame_set(frame)
    r=Matrix.Rotation(math.radians(euler[1]),3,'Y')@Matrix.Rotation(math.radians(euler[0]),3,'X')@Matrix.Rotation(math.radians(euler[2]),3,'Z')
    p=r.transposed()@(-Vector(position));f=r.transposed()@Vector((0,0,1));u=r.transposed()@Vector((0,1,0));p.x=-p.x;f.x=-f.x;u.x=-u.x
    c.location=B(p);forward=B(f);up=B(u);right=forward.cross(up).normalized();up=right.cross(forward).normalized()
    c.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler()
    s.render.filepath=os.path.join(P,'renders',name+'.png');bpy.ops.render.render(write_still=True)
shot('11_original_carry_50FOV',(.18,-.20,.70),(6,-10,0),1)
shot('12_review_carry_50FOV',(.13,-.12,.82),(6,-10,0),1)
shot('13_review_cock_50FOV',(.10,-.09,.80),(12,-6,38),30)
