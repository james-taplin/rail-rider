import bpy, math, os
from mathutils import Vector, Quaternion
from math import pi
OUT=os.path.dirname(os.path.abspath(__file__))
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene
scene.render.engine='CYCLES'; scene.cycles.samples=64
scene.cycles.use_denoising=True
scene.render.resolution_x=512; scene.render.resolution_y=512; scene.render.resolution_percentage=100
scene.render.film_transparent=True; scene.cycles.film_transparent_glass=True; scene.cycles.film_transparent_roughness=.25; scene.render.image_settings.file_format='PNG'; scene.render.image_settings.color_mode='RGBA'; scene.render.image_settings.color_depth='8'
scene.view_settings.view_transform='AgX'
world=bpy.data.worlds.new('Soft studio'); scene.world=world; world.use_nodes=True
world.node_tree.nodes['Background'].inputs['Color'].default_value=(.32,.32,.32,1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value=.7

def mat(name,c,metal=0,rough=.38,transmission=0):
 m=bpy.data.materials.new(name); m.diffuse_color=(*c,1); m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*c,1); p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=rough; p.inputs['Transmission Weight'].default_value=transmission
 if transmission: p.inputs['IOR'].default_value=1.46
 return m
alum=mat('Brushed aluminium',(.74,.76,.80),.75,.27)
rimdark=mat('Recessed aluminium',(.43,.45,.47),.72,.32)
wood=mat('Warm dark wood',(.35,.22,.12),0,.43)
# Subtle directional timber grain, no printed marks.
n=wood.node_tree.nodes; l=wood.node_tree.links; p=n.get('Principled BSDF'); tex=n.new('ShaderNodeTexNoise'); tex.inputs['Scale'].default_value=5.0; tex.inputs['Detail'].default_value=2.; tex.inputs['Roughness'].default_value=.65
coord=n.new('ShaderNodeTexCoord'); mapping=n.new('ShaderNodeVectorMath'); mapping.operation='MULTIPLY'; mapping.inputs[1].default_value=(1.2,18,25)
l.new(coord.outputs['Generated'],mapping.inputs[0]); l.new(mapping.outputs['Vector'],tex.inputs['Vector']); ramp=n.new('ShaderNodeValToRGB'); ramp.color_ramp.elements[0].color=(.18,.095,.042,1); ramp.color_ramp.elements[1].color=(.36,.21,.105,1); l.new(tex.outputs['Fac'],ramp.inputs['Fac']); l.new(ramp.outputs['Color'],p.inputs['Base Color'])
colors={'red':((.72,.09,.09),(1,1,1)), 'green':((.12,.55,.20),(.90,.95,.30)), 'orange':((.90,.48,.08),(1,1,1)), 'blue':((.10,.28,.72),(.80,.90,1)), 'silver':((.74,.76,.80),(.45,.47,.50)), 'black':((.07,.07,.08),(.85,.70,.20))}
glass=mat('Green bottle glass',(.10,.55,.22),0,.12,.82)
camd=bpy.data.cameras.new('Camera'); cam=bpy.data.objects.new('Camera',camd); bpy.context.collection.objects.link(cam); scene.camera=cam; camd.type='ORTHO'
lights=[]
for name in ('Key','Fill','Rim'):
 d=bpy.data.lights.new(name,'AREA'); o=bpy.data.objects.new(name,d); bpy.context.collection.objects.link(o); d.shape='DISK'; lights.append(o)
def aim(obj,target): obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
def setup(center,scale,campos,ortho,roll=0):
 cam.location=campos; aim(cam,center); camd.ortho_scale=ortho
 cam.rotation_euler=(cam.rotation_euler.to_quaternion() @ Quaternion((0,0,1),math.radians(roll))).to_euler()
 for o,rel,powr,size in zip(lights,[(-3,-4,6),(4,-1,3),(1,5,4)],[65,30,45],[4.5,3.5,3]):
  o.location=Vector(center)+Vector(rel)*scale; aim(o,center); o.data.energy=powr*scale*scale; o.data.size=size*scale

def clear_objects():
 for o in list(bpy.data.objects):
  if o.type=='MESH': bpy.data.objects.remove(o,do_unlink=True)
def smooth(o):
 for p in o.data.polygons: p.use_smooth=True

def cyl(name,r,depth,z,material,vertices=80):
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=(0,0,z)); o=bpy.context.object; o.name=name; o.data.materials.append(material); smooth(o)
 b=o.modifiers.new('Rounded edge','BEVEL'); b.width=min(.0007,depth*.15); b.segments=3; o.modifiers.new('Weighted normals','WEIGHTED_NORMAL'); return o

def lathe(name,profile,materials):
 N=96; verts=[]
 for r,z in profile:
  for i in range(N):
   a=2*pi*i/N; verts.append((r*math.cos(a),r*math.sin(a),z))
 faces=[]
 for j in range(len(profile)-1):
  for i in range(N): faces.append((j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i))
 faces.extend([tuple(reversed(range(N))),tuple((len(profile)-1)*N+i for i in range(N))]); mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update(); o=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(o)
 for m in materials:o.data.materials.append(m)
 smooth(o); return o

def torus(name,major,minor,z,material):
 bpy.ops.mesh.primitive_torus_add(major_segments=64,minor_segments=12,location=(0,0,z),major_radius=major,minor_radius=minor); o=bpy.context.object; o.name=name; o.data.materials.append(material); smooth(o); return o

def render(name):
 scene.render.filepath=os.path.join(OUT,name); bpy.ops.render.render(write_still=True)

# Six identically shaped standard cans, no branding.
can_profile=[(.0285,.001),(.0315,.002),(.033,.0045),(.033,.038),(.033,.078),(.033,.107),(.0315,.111),(.029,.114),(.028,.116)]
for name,(body,band) in colors.items():
 clear_objects(); bodymat=mat('Can '+name,body,.65 if name=='silver' else .20,.32); bandmat=mat('Band '+name,band,.40 if name=='silver' else .12,.38)
 ob=lathe('Can body',can_profile,[bodymat,bandmat,alum]); N=96
 ob.data.polygons[-1].material_index=2; ob.data.polygons[-2].material_index=2
 # middle band exactly .040m high
 for f in ob.data.polygons:
  if 3*N <= f.index < 4*N: f.material_index=1
 cyl('Top lid',.0278,.0010,.1160,alum); torus('Top rolled rim',.0282,.0012,.116,alum); torus('Bottom rim',.0317,.0013,.003,alum)
 torus('Top recessed ring',.021,.00055,.1168,rimdark)
 # simple aluminium pull tab with opening (a flattened ring, no text).
 bpy.ops.mesh.primitive_torus_add(major_segments=40,minor_segments=10,location=(0,-.003,.1175),major_radius=.006,minor_radius=.0015); tab=bpy.context.object;tab.name='Pull tab';tab.scale=(.72,1.32,.40);tab.data.materials.append(alum);smooth(tab)
 cyl('Tab rivet',.0014,.0007,.1173,alum,24)
 setup((0,0,.058),.09,(.23,-.36,.30),.162,roll=0);render('icon_can_'+name+'.png')

# One-piece solid lathed bottle with optical transmission and a slightly heavier base.
clear_objects()
bottle_profile=[(.000,.001),(.026,.001),(.030,.003),(.031,.009),(.031,.139),(.030,.151),(.028,.159),(.024,.167),(.019,.175),(.014,.182),(.013,.190),(.013,.248),(.0145,.250),(.0145,.256),(.012,.260),(.0095,.260),(.0095,.252),(.0105,.247),(.0105,.191)]
lathe('Green bottle',bottle_profile,[glass]); torus('Bottle lip',.0125,.0015,.254,glass)
setup((0,0,.130),.18,(.48,-.78,.50),.33,roll=0);render('icon_bottle_green.png')

# Wooden wall plate and two shallow upturned hooks. All geometry remains in this script.
clear_objects()
def box(name,location,dimensions,material,bevel):
 bpy.ops.mesh.primitive_cube_add(size=1,location=location); o=bpy.context.object;o.name=name;o.dimensions=dimensions; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(material)
 b=o.modifiers.new('Soft edges','BEVEL');b.width=bevel;b.segments=3;o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');return o
box('Wall plate',(0,0,0),(.70,.025,.10),wood,.008)
# Each hook is a solid, gently curved wooden shelf bracket, not a metal wire.
def hook(x):
 profile=[(-.008,-.030),(-.060,-.035),(-.095,-.017),(-.105,.009),(-.100,.034),(-.083,.041),(-.075,.017),(-.065,.002),(-.038,-.005),(-.008,0)]
 half=.012; verts=[(x+dx,y,z) for dx in (-half,half) for y,z in profile];N=len(profile)
 faces=[tuple(reversed(range(N))),tuple(range(N,N*2))]+[(i,(i+1)%N,(i+1)%N+N,i+N) for i in range(N)]
 m=bpy.data.meshes.new('Hook');m.from_pydata(verts,[],faces);m.update();o=bpy.data.objects.new('Wood hook',m);bpy.context.collection.objects.link(o);o.data.materials.append(wood);b=o.modifiers.new('Rounded edges','BEVEL');b.width=.004;b.segments=3;o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
hook(-.25);hook(.25)
setup((0,-.025,0),.37,(1.8,-2.4,1.25),.75,roll=-10);render('icon_rack.png')



