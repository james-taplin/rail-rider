"""Build the wall rack and render a fit check with the shipping rifle. Units are metres."""
import bpy, math
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
P=Path(__file__).resolve().parent
models=P.parent.parent/'Models'
bpy.ops.wm.read_factory_settings(use_empty=True)
def material(name, color, metal, rough):
    m=bpy.data.materials.new(name);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF')
    for key,val in [('Base Color',(*color,1)),('Roughness',rough),('Metallic',metal)]:
        bs.inputs[key].default_value=val
    return m
steel=material('Blackened bent steel',(.038,.042,.042),.77,.34)
liner=material('Waxed russet leather',(.115,.043,.021),0,.62)
screw=material('Slotted steel fasteners',(.11,.105,.089),.85,.33)
wood=material('Unfinished pine',(.70,.53,.31),0,.89)
image=bpy.data.images.load(str(P/'unfinished_pine.png'))
texture=wood.node_tree.nodes.new('ShaderNodeTexImage'); texture.image=image
wood.node_tree.links.new(texture.outputs['Color'],wood.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
def active(o):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
def soften(o,width):
    active(o)
    mod=o.modifiers.new('Radiused edges','BEVEL');mod.width=width;mod.segments=4
    bpy.ops.object.modifier_apply(modifier=mod.name)
    mod=o.modifiers.new('Face normals','WEIGHTED_NORMAL')
    bpy.ops.object.modifier_apply(modifier=mod.name)
def mesh(name,verts,faces,mat):
    import bmesh
    m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces)
    bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(m);bm.free();m.update()
    o=bpy.data.objects.new(name,m);bpy.context.collection.objects.link(o);m.materials.append(mat);return o
# Rounded board outline, extruded from the wall to its finished face.
ring=[]
for cx,cz,start in [(.336,.036,0),(-.336,.036,90),(-.336,-.036,180),(.336,-.036,270)]:
    for i in range(9):
        a=math.radians(start+i*90/8);ring.append((cx+.014*math.cos(a),cz+.014*math.sin(a)))
n=len(ring)
board=mesh('Pine backing',[(x,y,z) for y in [0,.014] for x,z in ring],
    [tuple(range(n)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],wood)
soften(board,.002)
uv=board.data.uv_layers.new(name='Wood grain')
for loop in board.data.loops:
    v=board.data.vertices[loop.vertex_index].co
    uv.data[loop.index].uv=(.5+v.x/.72,.48+(v.z+v.y)/.235)
# Slice bottom of each rifle support directly from the shipped mesh.
bpy.ops.import_scene.gltf(filepath=str(models/'rail_rider_rifle.glb'))
rifle=list(bpy.context.selected_objects)
pose=Matrix.Translation((.0225,.048,.045)) @ Matrix.Rotation(math.pi/2,4,'Z')
for o in rifle:
    if o.parent is None:o.matrix_world=pose @ o.matrix_world
bpy.context.view_layer.update()
def surface(o):
    return BVHTree.FromPolygons([o.matrix_world@v.co for v in o.data.vertices],[list(f.vertices) for f in o.data.polygons])
def section(target,x):
    contact=target.ray_cast(Vector((x,.048,-.20)),Vector((0,0,1)),.4)[0]
    assert contact is not None
    return contact.z-.0003
def strap(name,x,width,path,target,mat):
    # Each path point specifies depth, height above the rifle seat, and sheet thickness direction.
    verts=[]
    for side in (-1,1):
        xx=x+side*width/2
        for y,h,dy,dz in path:verts.extend([(xx,y,section(target,xx)+h),(xx,y+dy,section(target,xx)+h+dz)])
    count=len(path)*2
    faces=[]
    for i in range(0,count-2,2):
        for offset in (0,count):faces.append(tuple(offset+j for j in [i,i+1,i+3,i+2]))
        for j in (i,i+1):faces.append((j,j+2,j+2+count,j+count))
    for i in (0,count-2):faces.append((i,i+1,i+1+count,i+count))
    o=mesh(name,verts,faces,mat);soften(o,.00045);return o
def saddle(label,x,width,target):
    base=section(target,x)
    # Flat bearing face with formed quarter bends at both edges.
    path=[(.018,.041-base,-.004,0)]
    for i in range(9):
        a=math.pi+i*math.pi/16
        path.append((.030+.012*math.cos(a),.010+.012*math.sin(a),.004*math.cos(a),.004*math.sin(a)))
    for i in range(9):
        a=-math.pi/2+i*math.pi/16
        path.append((.065+.010*math.cos(a),.008+.010*math.sin(a),.004*math.cos(a),.004*math.sin(a)))
    path.append((.075,.022,.004,0))
    strap(label+' hook',x,width,path,target,steel)
    pad=[(y,h,-dy/2,-dz/2) for y,h,dy,dz in path[1:]]
    strap(label+' lining',x,width-.002,pad,target,liner)
    for pos in [-1,1]:
        xx=x+pos*width*.24; z=.034+section(target,xx)-base
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,location=(xx,.018,z))
        head=bpy.context.object;head.name=label+' hook screw';head.scale=(.0037,.0016,.0037);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);head.data.materials.append(screw)
        bpy.ops.mesh.primitive_cube_add(size=1,location=(xx,.01985,z))
        cut=bpy.context.object;cut.dimensions=(.010,.0025,.0009);cut.rotation_euler.y=pos*.35
        active(head);mod=head.modifiers.new('Driver slot','BOOLEAN');mod.operation='DIFFERENCE';mod.object=cut
        bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cut,do_unlink=True)
for label,x,width,part in [('Fore-end',.2425,.023,'Forearm'),('Stock',-.2425,.033,'StockVisual')]:
    saddle(label,x,width,surface(next(o for o in rifle if o.name==part)))
rack=[o for o in bpy.context.scene.objects if o not in rifle]
bpy.context.view_layer.update()
for o in rack:
    for part in rifle:
        if part.type=='MESH' and surface(o).overlap(surface(part)):raise RuntimeError('Fit interference: '+o.name+' / '+part.name)
bpy.ops.object.select_all(action='DESELECT')
for o in rack:o.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(models/'rail_rider_rack.glb'),export_format='GLB',use_selection=True,export_animations=False)
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=12;s.cycles.use_denoising=True
s.render.resolution_x=1400;s.render.resolution_y=650;s.render.resolution_percentage=100
s.world=bpy.data.worlds.new('Studio');s.world.use_nodes=True;s.world.node_tree.nodes['Background'].inputs[0].default_value=(.21,.24,.25,1)
def aim(o,pt):o.rotation_euler=(Vector(pt)-o.location).to_track_quat('-Z','Y').to_euler()
for pos,power,size in [((-.4,.55,.7),22,.75),((.6,.42,.16),9,.38)]:
    bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;aim(o,(0,0,0))
bpy.ops.object.camera_add(location=(-.24,1,.19));cam=bpy.context.object;s.camera=cam;cam.data.type='ORTHO';aim(cam,(0,.033,.005))
for shown,name,scale in [(True,'rack_loaded',1.04),(False,'rack_empty',.82)]:
    for o in rifle:o.hide_render=not shown
    cam.data.ortho_scale=scale;s.render.filepath=str(P/(name+'.png'));bpy.ops.render.render(write_still=True)
s.render.film_transparent=True
s.render.resolution_x=512;s.render.resolution_y=512;cam.data.ortho_scale=.77
cam.location=(-.10,.84,.28);aim(cam,(0,.023,0))
s.render.filepath=str(models/'icons'/'icon_rack.png');bpy.ops.render.render(write_still=True)
print('RACK_VERIFIED',len(rack))
