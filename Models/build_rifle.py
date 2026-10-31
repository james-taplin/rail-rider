import bpy, math, os
from mathutils import Vector
from math import pi
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
P=os.path.dirname(os.path.abspath(__file__)); os.chdir(P)
def B(p): return (p[0],-p[2],p[1])
def material(name,color,metal,rough,texture=None):
 m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*color,1); bs.inputs['Metallic'].default_value=metal; bs.inputs['Roughness'].default_value=rough
 if texture:
  t=m.node_tree.nodes.new('ShaderNodeTexImage'); t.image=bpy.data.images.load(os.path.join(P,texture)); t.image.pack(); m.node_tree.links.new(t.outputs['Color'],bs.inputs['Base Color'])
 return m
wood=material('Walnut',(.38,.2,.09),0,.32,'rail_rider_wood.png'); metal=material('Blued steel',(.025,.033,.039),.72,.38)
def mesh(name,verts,faces,mat):
 me=bpy.data.meshes.new(name); me.from_pydata([B(v) for v in verts],[],faces); me.update(); ob=bpy.data.objects.new(name,me); bpy.context.collection.objects.link(ob); ob.data.materials.append(mat); return ob
def loft(name,sections,mat,n=24):
 verts=[]
 for z,cy,hh,hw in sections:
  for i in range(n):
   a=2*pi*i/n; verts.append((hw*math.cos(a),cy+hh*math.sin(a),z))
 faces=[]
 for j in range(len(sections)-1):
  for i in range(n): faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
 faces.extend([tuple(reversed(range(n))),tuple((len(sections)-1)*n+i for i in range(n))]); ob=mesh(name,verts,faces,mat)
 for p in ob.data.polygons: p.use_smooth=True
 uv=ob.data.uv_layers.new(name='UVMap')
 for poly in ob.data.polygons:
  for li in poly.loop_indices:
   v=verts[ob.data.loops[li].vertex_index]; x,y,z=v
   # Stock logo is placed on +X because the current Unity loader mirrors X.
   # Keep it entirely on the side-facing sectors, never on the upper comb.
   u=max(.005,min(.635,(z+.455)/.365*.60+.02)); vv=max(.005,min(.49,((.061-y+.16*(z+.30)) if name=='StockVisual' else (.073-y))/.22*.49))
   if name=='StockVisual' and poly.normal.x > .55:
    u=.64-u
   else:
    vv+=.5
   uv.data[li].uv=(u,1-vv)
 return ob
def cube(name,center,dim,mat,bev=.003):
 bpy.ops.mesh.primitive_cube_add(size=1,location=B(center)); o=bpy.context.object; o.name=name; o.dimensions=(dim[0],dim[2],dim[1]); bpy.ops.object.transform_apply(location=False,rotation=False,scale=True); o.data.materials.append(mat)
 if bev:
  b=o.modifiers.new('Soft machined edges','BEVEL'); b.width=bev; b.segments=3
  o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
 return o
def cylinder(name,center,r,depth,axis,mat,vertices=48):
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=B(center)); o=bpy.context.object; o.name=name
 if axis=='z': o.rotation_euler[0]=pi/2
 if axis=='x': o.rotation_euler[1]=pi/2
 o.data.materials.append(mat)
 for p in o.data.polygons: p.use_smooth=True
 b=o.modifiers.new('Edge glint','BEVEL'); b.width=.0007; b.segments=2; o.modifiers.new('Normals','WEIGHTED_NORMAL'); return o
def curve(name,points,r,mat,cyclic=False):
 cu=bpy.data.curves.new(name,'CURVE'); cu.dimensions='3D'; cu.resolution_u=16; cu.bevel_depth=r; cu.bevel_resolution=4
 s=cu.splines.new('BEZIER'); s.bezier_points.add(len(points)-1)
 for b,p in zip(s.bezier_points,points): b.co=B(p); b.handle_left_type='AUTO'; b.handle_right_type='AUTO'
 s.use_cyclic_u=cyclic; ob=bpy.data.objects.new(name,cu); bpy.context.collection.objects.link(ob); ob.data.materials.append(mat); bpy.context.view_layer.objects.active=ob; ob.select_set(True); bpy.ops.object.convert(target='MESH'); ob.select_set(False); return ob
# Rounded cast receiver, tapering gently into the wooden wrist.
stock=loft('StockVisual',[(-.455,-.048,.054,.018),(-.44,-.047,.054,.020),(-.40,-.042,.052,.022),(-.36,-.035,.048,.023),(-.31,-.026,.042,.021),(-.255,-.017,.035,.019),(-.21,-.010,.028,.018),(-.17,-.006,.023,.016),(-.125,-.005,.023,.017),(-.09,-.004,.024,.019)],wood,32)
loft('Buttplate',[(-.465,-.049,.054,.018),(-.455,-.048,.054,.018)],metal,32)
def extrude_x(name,profile,half,mat,bevel=0):
 vs=[(x,y,z) for x in (-half,half) for z,y in profile]; n=len(profile)
 faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 o=mesh(name,vs,faces,mat)
 if bevel:
  mod=o.modifiers.new('Rounded stamped edges','BEVEL'); mod.width=bevel; mod.segments=3
  o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
 return o
receiver_profile=[(-.10,-.008),(-.091,.018),(-.065,.028),(.060,.028),(.085,.024),(.091,.015),(.091,-.014),(.076,-.024),(.040,-.028),(-.030,-.028),(-.075,-.022)]
extrude_x('Receiver',receiver_profile,.019,metal,.004)
# Steel pin and screw heads are flush to the receiver.
for x in [-.0195,.0195]:
 cylinder('Pivot pin',(x,-.034,.076),.0035,.0015,'x',metal,32)
 cylinder('Receiver screw',(x,.004,-.045),.0024,.0015,'x',metal,24)
# Bore runs above forearm
# Open ended tube with a recessed bore and annular muzzle face.
N=64; vs=[]
for z,r in [(.065,.008),(.490,.008),(.490,.0035),(.477,.0035)]:
 for i in range(N):
  a=2*pi*i/N; vs.append((r*math.cos(a),.022+r*math.sin(a),z))
fs=[]
for j in range(3):
 for i in range(N): fs.append((j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i))
# Close the hidden breech and the end of the recessed bore; keep the muzzle opening.
fs.append(tuple(reversed(range(N))))
fs.append(tuple(3*N+i for i in range(N)))
barrel=mesh('Barrel and open muzzle',vs,fs,metal)
for f in barrel.data.polygons: f.use_smooth=True
# A cosmetic BB reservoir under the barrel, ending just short of the muzzle.
cylinder('BB reserve tube',(0,.003,.275),.008,.410,'z',metal,48)
# Extended timber forearm, with a narrow steel barrel band.
loft('Forearm',[(.070,-.006,.010,.012),(.085,-.010,.014,.015),(.100,-.014,.017,.017),(.11,-.016,.019,.018),(.18,-.016,.020,.019),(.25,-.014,.018,.017),(.285,-.012,.015,.015)],wood,32)
cylinder('Forearm band',(0,-.002,.274),.019,.006,'z',metal,48)
# One flat stamped lever: an open-edged guard and elongated gripping loop.
# Build a thin rectangular strip in the YZ plane, rather than a round tube.
pivot=(0,-.034,.076)
controls=[(.076,-.034),(.063,-.037),(.045,-.051),(.025,-.066),(.002,-.070),(-.025,-.065),(-.048,-.066),(-.073,-.075),(-.11,-.093),(-.155,-.111),(-.182,-.112),(-.195,-.098),(-.207,-.070),(-.211,-.053),(-.18,-.050),(-.14,-.044),(-.10,-.033)]
def catmull(points,steps=8):
 out=[]
 for i in range(len(points)-1):
  p0=points[max(i-1,0)]; p1=points[i]; p2=points[i+1]; p3=points[min(i+2,len(points)-1)]
  for k in range(steps):
   t=k/steps; t2=t*t; t3=t2*t
   out.append(tuple(.5*((2*p1[j])+(-p0[j]+p2[j])*t+(2*p0[j]-5*p1[j]+4*p2[j]-p3[j])*t2+(-p0[j]+3*p1[j]-3*p2[j]+p3[j])*t3) for j in range(2)))
 out.append(points[-1]); return out
path=catmull(controls); vv=[]; halfwidth=.0033; thickness=.0015
for i,(z,y) in enumerate(path):
 prev=path[max(0,i-1)]; nex=path[min(len(path)-1,i+1)]; dz=nex[0]-prev[0]; dy=nex[1]-prev[1]; length=max(1e-9,math.hypot(dz,dy)); nz=-dy/length; ny=dz/length
 for x,side in [(-thickness,-1),(-thickness,1),(thickness,-1),(thickness,1)]: vv.append((x,y+side*halfwidth*ny,z+side*halfwidth*nz))
ff=[]
for i in range(len(path)-1):
 a=i*4; b=(i+1)*4
 ff.extend([(a,b,b+1,a+1),(a+2,a+3,b+3,b+2),(a,a+2,b+2,b),(a+1,b+1,b+3,a+3)])
ff += [(0,1,3,2),tuple(range((len(path)-1)*4,len(path)*4))]
lever=mesh('Lever',vv,ff,metal)
for v in lever.data.vertices: v.co-=Vector(B(pivot))
lever.location=B(pivot)
bevel=lever.modifiers.new('Soft stamped edge','BEVEL'); bevel.width=.0006; bevel.segments=3
lever.modifiers.new('Stamped normals','WEIGHTED_NORMAL')
# Trigger bends forward toward the muzzle and sits within the guard opening.
curve('Trigger',[(.006,-.031,-.014),(.006,-.043,-.011),(.006,-.050,-.004),(.006,-.049,.002)],.0018,metal)
# Rear sight leaf, two wings leave a visible V notch; notch bottom on line at .064.
# extruded wings, notch at center.
def extrude_profile(name,profile,z0,z1):
 vs=[(x,y,z) for z in (z0,z1) for x,y in profile]; n=len(profile); fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]; return mesh(name,vs,fs,metal)
# Wide U notch with a flat floor: the line of sight passes through its opening.
extrude_profile('Rear sight leaf',[(-.009,.028),(.009,.028),(.009,.058),(.0035,.058),(.0035,.047),(-.0035,.047),(-.0035,.058),(-.009,.058)],-.064,-.059)
cube('Front sight foot',(0,.032,.446),(.013,.007,.018),metal,.001)
extrude_profile('Front blade',[(-.0018,.034),(.0018,.034),(.0018,.0525),(-.0018,.0525)],.443,.449)
def empty(name,pos):
 o=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(o); o.location=B(pos); return o
empty('RearSight',(0,.0525,-.0615)); empty('FrontSight',(0,.0525,.446)); empty('Muzzle',(0,.022,.490)); empty('Chamber',(0,.022,.10)); empty('RackDock',(0,0,.045))
# Export before staging cameras and lights.
bpy.ops.export_scene.gltf(filepath=os.path.join(P,'rail_rider_rifle.glb'),export_format='GLB',export_apply=True)
# Render previews in orthographic side and perspective eye positions.
world=bpy.context.scene.world; world.color=(.18,.18,.18)
def point(o,p): o.rotation_euler=(Vector(B(p))-o.location).to_track_quat('-Z','Y').to_euler()
def light(pos,power,size):
 data=bpy.data.lights.new('Softbox','AREA'); data.energy=power; data.shape='DISK'; data.size=size; o=bpy.data.objects.new('Softbox',data); bpy.context.collection.objects.link(o); o.location=B(pos); point(o,(0,0,0))
light((-1,1,-.2),65,1.5); light((.6,.8,.4),45,1.2); light((-.2,-.5,.5),20,1)
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=32; scene.render.resolution_x=1400; scene.render.resolution_y=650; scene.render.resolution_percentage=100
scene.world.color=(.28,.28,.28); scene.view_settings.view_transform='AgX'; scene.render.image_settings.file_format='PNG'; scene.render.film_transparent=False
camdata=bpy.data.cameras.new('Preview'); cam=bpy.data.objects.new('Preview',camdata); bpy.context.collection.objects.link(cam); scene.camera=cam
def render(filename,pos,target,ortho=None,lens=40):
 cam.location=B(pos); point(cam,target); camdata.type='ORTHO' if ortho else 'PERSP'; camdata.ortho_scale=ortho or 1; camdata.lens=lens; scene.render.filepath=os.path.join(P,filename); bpy.ops.render.render(write_still=True)
render('rail_rider_receiver_join.png',(.45,.09,.16),(0,-.003,.085),.20)
render('rail_rider_left.png',(1.5,.14,-.08),(0,-.01,.01),1.12)
render('rail_rider_stock_player_view.png',(.24,.105,-.52),(0,-.012,-.28),None,40)
render('rail_rider_sights.png',(0,.0525,-.2015),(0,.0525,.446),None,35)
# Inventory image: actual rifle, transparent background, no UI tile.
scene.render.resolution_x=512; scene.render.resolution_y=512; scene.render.film_transparent=True
cam.location=B((1.4,.70,-.48)); point(cam,(0,-.015,.01)); camdata.type='ORTHO'; camdata.ortho_scale=1.05
from mathutils import Quaternion
cam.rotation_euler=(cam.rotation_euler.to_quaternion() @ Quaternion((0,0,1),math.radians(-32))).to_euler()
scene.render.filepath=os.path.join(P,'rail_rider_inventory.png'); bpy.ops.render.render(write_still=True)
scene.render.resolution_x=1400; scene.render.resolution_y=650; scene.render.film_transparent=False
# Local glTF -X maps to Blender -X
lever.rotation_euler[0]=-math.radians(70)
render('rail_rider_lever_open.png',(-1.5,.28,-.08),(0,-.105,.01),1.22)



