import bpy, math, random, os
from mathutils import Vector
from math import sin, cos, pi
D = os.path.dirname(os.path.abspath(__file__))
random.seed(14)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for a in bpy.data.actions: bpy.data.actions.remove(a)
# Real dimensions: approximately 167 mm diameter by 194 mm high.
R=.0835; H=.194

def mat(name, color, metal=0, rough=.5):
 m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1); p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=rough
 return m
steel=mat('Tinplate | rubbed silver',(.46,.49,.47),.82,.36)
darksteel=mat('Creases and oxidised steel',(.19,.205,.19),.65,.57)
paint=mat('Railway green | aged enamel',(.105,.19,.135),.25,.68)
residue=mat('Dried green paint | matte',(.075,.125,.077),.05,.92)
rust=mat('Oxide freckles',(.23,.115,.055),.35,.82)
# A compact original image texture goes into the GLB, subtle mottled enamel rather than clean flat colour.
img=bpy.data.images.new('Enamel mottling - original',width=128,height=128,alpha=True)
pix=[]
for y in range(128):
 for x in range(128):
  a=random.uniform(-.018,.018) + .009*sin(x*.12)*sin(y*.18)
  pix.extend((max(.01,.105+a),max(.01,.19+a),max(.01,.135+a),1))
img.pixels[:]=pix; img.filepath_raw=os.path.join(D,'enamel.png'); img.file_format='PNG'; img.save(); img.pack()
p=paint.node_tree.nodes.get('Principled BSDF'); t=paint.node_tree.nodes.new('ShaderNodeTexImage'); t.image=img; paint.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color'])

def mesh(name,verts,faces,material,parent=None):
 me=bpy.data.meshes.new(name); me.from_pydata(verts,[],faces); me.update(); ob=bpy.data.objects.new(name,me); bpy.context.collection.objects.link(ob); ob.data.materials.append(material); ob.parent=parent
 # UV mapping from cylindrical angle and height
 uv=me.uv_layers.new(name='UVMap')
 for poly in me.polygons:
  for li in poly.loop_indices:
   v=me.vertices[me.loops[li].vertex_index].co
   uv.data[li].uv=((math.atan2(v.y,v.x)/(2*pi))%1, (v.z/H)%1)
 return ob

def cyl(name,r,depth,z,material,parent=None,loc=None,vertices=64):
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=loc or (0,0,z)); o=bpy.context.object; o.name=name; o.data.materials.append(material); o.parent=parent
 bevel=o.modifiers.new('Soft manufactured edge','BEVEL'); bevel.width=.001; bevel.segments=2
 o.modifiers.new('Weighted normals','WEIGHTED_NORMAL'); return o

def tor(name,major,minor,z,material,parent=None):
 bpy.ops.mesh.primitive_torus_add(major_radius=major,minor_radius=minor,major_segments=64,minor_segments=10,location=(0,0,z)); o=bpy.context.object; o.name=name; o.data.materials.append(material); o.parent=parent
 for f in o.data.polygons:f.use_smooth=True
 return o

def wire(name,points,r,material,parent=None):
 c=bpy.data.curves.new(name,'CURVE'); c.dimensions='3D'; c.resolution_u=2; c.bevel_depth=r; c.bevel_resolution=3; sp=c.splines.new('POLY'); sp.points.add(len(points)-1)
 for p,co in zip(sp.points,points): p.co=(*co,1)
 o=bpy.data.objects.new(name,c); bpy.context.collection.objects.link(o); o.data.materials.append(material); o.parent=parent; return o

def empty(name):
 o=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(o); return o
root=empty('Tin_Rock_Pivot')
# open thin-walled sheet-metal sides, separately exposed metal inside.
N=96
for name,r,material,reverse in [('Enamel outside',R,paint,False),('Visible tin interior',R-.0018,steel,True)]:
 v=[]
 for z in (.003,H-.004):
  v.extend([(r*cos(2*pi*i/N),r*sin(2*pi*i/N),z) for i in range(N)])
 f=[]
 for i in range(N):
  j=(i+1)%N; f.append((i,j,N+j,N+i) if not reverse else (j,i,N+i,N+j))
 o=mesh(name,v,f,material,root)
 for p in o.data.polygons:p.use_smooth=True
cyl('Interior bottom',R-.0018,.002,.004,steel,root)
cyl('Dried paint in bottom',R-.004,.0006,.0055,residue,root)
tor('Rolled upper outside seam',R-.0002,.0025,H-.003,steel,root)
tor('Upper inner press-fit lip',R-.009,.0015,H-.004,steel,root)
tor('Bottom rolled seam',R-.0005,.002,.003,steel,root)
# Flat annular shoulder surrounding the opening.
v=[]
for r in (R-.001,R-.010):v.extend([(r*cos(2*pi*i/N),r*sin(2*pi*i/N),H-.004) for i in range(N)])
f=[(i,(i+1)%N,N+(i+1)%N,N+i) for i in range(N)]
mesh('Pressed top ring',v,f,steel,root)
# Lid has its own pivot and is seated in the intact pose.
lid=empty('Lid_Release_Pivot'); lid.parent=root; lid.location=(0,0,H-.001)
cyl('Separate press-fit lid',R-.009,.003,0,steel,lid)
tor('Lid rolled bead',R-.011,.0011,.002,steel,lid)
tor('Lid stamped inner ring',R-.018,.00055,.002,steel,lid)
# A thin old paint stain on lid is a true separate mesh.
cyl('Lid paint stain',.017,.00025,.0017,residue,lid,loc=(.025,-.016,.0017),vertices=40)
# Two fixed bail ears. Their sockets are oriented toward the sides.
for side in (-1,1):
 x=side*(R+.001)
 bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,location=(x,0,.153)); o=bpy.context.object; o.name=('Left' if side<0 else 'Right')+' bail lug'; o.scale=(.0035,.010,.013); o.data.materials.append(steel); o.parent=root
 bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=10,location=(x+side*.003,0,.153)); o=bpy.context.object; o.name=('Left' if side<0 else 'Right')+' bail pivot'; o.scale=(.002,.003,.003); o.data.materials.append(darksteel); o.parent=root
pts=[]
for i in range(65):
 t=pi*i/64; pts.append((R*cos(t),-.102*sin(t),.153-.065*sin(t)))
wire('Wire bail | folded to rear',pts,.00145,steel,root)
# Dried paint trails draped over the upper edge and down the outer wall.
for k,(ang,length,width) in enumerate([(1.0,.061,.010),(1.27,.035,.006),(2.25,.049,.008),(4.8,.024,.006),(5.1,.042,.005)]):
 vv=[]; seg=16
 for j in range(seg+1):
  t=j/seg; z=H-.005-length*t; a=ang+.007*sin(t*9+k); w=width*(1-.43*t)*(.8+.2*sin(t*15+k))
  for s in (-1,1):
   aa=a+s*w/R/2; vv.append(((R+.00035)*cos(aa),(R+.00035)*sin(aa),z))
 ff=[(2*j,2*j+1,2*j+3,2*j+2) for j in range(seg)]
 mesh('Dried paint run %02d'%k,vv,ff,residue,root)
# Small exposed steel and oxide chips, modelled as irregular surface patches.
for i in range(38):
 a=random.uniform(0,2*pi); z=random.uniform(.014,H-.015); w=random.uniform(.001,.004); h=random.uniform(.001,.0035)
 vv=[]
 for da,dz in [(-w,-h),(w*.8,-h*.6),(w,h*.8),(-w*.5,h)]:
  aa=a+da/R; vv.append(((R+.0005)*cos(aa),(R+.0005)*sin(aa),z+dz))
 mesh('Paint chip %02d'%i,vv,[(0,1,2,3)],steel if i%3 else rust,root)
# Key actions separately and expose them as glTF clips via NLA tracks.
def action_keys(obj,name,keys):
 for frame,loc,rot in keys:
  obj.location=loc; obj.rotation_euler=rot; obj.keyframe_insert(data_path='location',frame=frame); obj.keyframe_insert(data_path='rotation_euler',frame=frame)
 act=obj.animation_data.action; act.name=name
 track=obj.animation_data.nla_tracks.new(); track.name=name; track.strips.new(name,1,act); obj.animation_data.action=None
side=[]
for frame,angle in [(1,0),(6,.16),(12,-.11),(18,.07),(24,-.04),(30,.018),(38,0)]:
 side.append((frame,(0,0,R*abs(sin(angle))),(0,angle,0)))
action_keys(root,'Side_Hit_Rock',side)
action_keys(lid,'Lid_Hit_Release',[(1,(0,0,H-.001),(0,0,0)),(8,(0,0,H+.052),(.22,-.20,0)),(16,(0,.19,H+.055),(.48,-.28,.1)),(27,(0,.21,.018),(.12,-.08,.16)),(34,(0,.21,.003),(0,0,.16)),(40,(0,.21,.003),(0,0,.16))])
# Static intact transforms before exporting; clips restore this at start.
root.location=(0,0,0);root.rotation_euler=(0,0,0);lid.location=(0,0,H-.001);lid.rotation_euler=(0,0,0)
bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=40;bpy.context.scene.render.fps=24
# Save source before presentation set dressing, keeping editable mesh objects and keyframes.
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(D,'discarded_paint_tin.blend'))
# Export only the model. Exporting NLA tracks means the two clips can replay independently.
bpy.ops.object.select_all(action='DESELECT')
for o in list(bpy.context.scene.objects):
 if o.type in {'MESH','CURVE','EMPTY'}: o.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.gltf(filepath=os.path.join(D,'discarded_paint_tin.glb'),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_nla_strips=True)
# Create a presentation render independently of the GLB.
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.mesh.primitive_plane_add(size=2,location=(0,0,-.002)); floor=bpy.context.object; floor.name='Presentation floor'; floor.data.materials.append(mat('Presentation backdrop',(.12,.14,.13),0,.82))
world=bpy.context.scene.world; world.color=(.18,.18,.18)
def aim(o,point): o.rotation_euler=(Vector(point)-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(.48,-.66,.41)); cam=bpy.context.object; aim(cam,(0,0,.09)); cam.data.type='ORTHO'; cam.data.ortho_scale=.39; bpy.context.scene.camera=cam
for loc,energy,size in [((.1,-.4,.65),12,.5),((-.4,-.1,.35),8,.4),((0,.4,.5),10,.35)]:
 bpy.ops.object.light_add(type='AREA',location=loc); l=bpy.context.object; l.data.energy=energy; l.data.shape='DISK'; l.data.size=size; aim(l,(0,0,.09))
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.samples=32; sc.render.resolution_x=960; sc.render.resolution_y=960; sc.render.resolution_percentage=100
sc.view_settings.view_transform='AgX'; sc.frame_set(1); sc.render.filepath=os.path.join(D,'tin_still.png'); bpy.ops.render.render(write_still=True)
# Companion sequence: each reaction starts in the intact pose.
cam.data.ortho_scale=.55; aim(cam,(0,.06,.11)); sc.render.resolution_x=400; sc.render.resolution_y=400; sc.cycles.samples=6
frames_dir=os.path.join(D,'preview_frames'); os.makedirs(frames_dir,exist_ok=True)
n=0
for which in ('side','lid'):
 root.animation_data.nla_tracks[0].mute=(which!='side')
 lid.animation_data.nla_tracks[0].mute=(which!='lid')
 for f in (1,4,7,10,13,16,19,22,25,28,32,38):
  sc.frame_set(f); sc.render.filepath=os.path.join(frames_dir,'frame_%02d.png'%n); bpy.ops.render.render(write_still=True); n+=1
print('TIN_BUILD_COMPLETE')



