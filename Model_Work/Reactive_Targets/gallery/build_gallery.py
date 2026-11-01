import bpy, math, random, os
from mathutils import Vector
import numpy as np

ROOT = os.path.dirname(os.path.abspath(__file__))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.materials, bpy.data.images, bpy.data.actions):
    pass
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.render.engine = 'CYCLES'
scene.cycles.samples = 32
scene.render.resolution_x = 1200
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.world.color = (.16,.16,.16)

def material(name, color, metallic=0, rough=.5, seed=1):
    mat=bpy.data.materials.new(name); mat.use_nodes=True
    p=mat.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metallic; p.inputs['Roughness'].default_value=rough
    # Original, restrained mottled coating. Embedded in the glTF as a UV texture.
    rng=np.random.default_rng(seed)
    fine=rng.random((256,256))
    coarse=np.repeat(np.repeat(rng.random((32,32)),8,axis=0),8,axis=1)
    shade=np.clip(.88 + .12*coarse + .10*fine,0,1)
    arr=np.empty((256,256,4),dtype=np.float32)
    for i in range(3): arr[:,:,i]=np.clip(color[i]*shade,0,1)
    arr[:,:,3]=1
    img=bpy.data.images.new(name+'_original_texture',width=256,height=256,alpha=True)
    img.pixels.foreach_set(arr.ravel()); img.pack()
    node=mat.node_tree.nodes.new('ShaderNodeTexImage'); node.image=img
    mat.node_tree.links.new(node.outputs['Color'],p.inputs['Base Color'])
    return mat
shell=material('Graphite powder coated steel',(.045,.052,.054),.40,.72,12)
edges=material('Edge wear steel',(.26,.28,.28),.82,.43,3)
silver=material('Silver target enamel',(.68,.71,.69),.67,.36,17)
dark=material('Darkened screw heads',(.12,.13,.13),.8,.4,6)
accent=material('Ivory stencil',(.68,.65,.53),.2,.65,25)

collection=bpy.data.collections.new('EXPORT_GALLERY'); scene.collection.children.link(collection)
def export_object(o):
    for c in list(o.users_collection): c.objects.unlink(o)
    collection.objects.link(o)
    return o
def box(name, loc, scale, mat, bevel=.0015, rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o=export_object(bpy.context.object); o.name=name; o.dimensions=scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if rotation: o.rotation_euler=rotation
    o.data.materials.append(mat)
    if bevel:
        mod=o.modifiers.new('Soft manufactured edges','BEVEL'); mod.width=bevel; mod.segments=2
        o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    return o
def mesh(name, vertices, faces, mat):
    m=bpy.data.meshes.new(name); m.from_pydata(vertices, [], faces); m.update()
    o=bpy.data.objects.new(name,m); collection.objects.link(o); m.materials.append(mat)
    # Explicit UV layer ensures PBR image textures survive GLB export.
    uv=m.uv_layers.new(name='UVMap')
    for poly in m.polygons:
        for idx in poly.loop_indices:
            v=m.vertices[m.loops[idx].vertex_index].co
            uv.data[idx].uv=(v.x*3 + .5, v.z*3 + v.y*1.5 + .5)
    return o
def extrude_outline(name, points, thickness, mat, parent=None):
    n=len(points); verts=[(x,-thickness/2,z) for x,z in points]+[(x,thickness/2,z) for x,z in points]
    faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    o=mesh(name,verts,faces,mat); o.parent=parent
    bevel=o.modifiers.new('Rounded cut edges','BEVEL'); bevel.width=.0007; bevel.segments=2
    o.modifiers.new('Face weighted normals','WEIGHTED_NORMAL')
    return o
def cylinder(name, loc, radius, depth, mat, rotation=(0,0,0), vertices=24, parent=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rotation)
    o=export_object(bpy.context.object); o.name=name; o.data.materials.append(mat)
    if parent: o.parent=parent
    return o

# Folded front-opening shroud, all dimensions in metres. Front faces negative Y.
# The interior wall rises 190 mm while receding 190 mm: exactly 45 degrees.
# A full roof, bottom, and matching end plates close every side except the front.
box('Tray bottom',(0,0,.006),(.50,.205,.006),shell)
box('Front folded lip',(0,-.102,.020),(.50,.004,.033),shell)
box('Front upper rolled edge',(0,-.102,.037),(.50,.005,.004),edges,.001)
box('Overhanging shroud roof',(0,0,.203),(.50,.205,.006),shell)
box('Roof front folded edge',(0,-.102,.199),(.50,.005,.007),edges,.001)
box('Enclosed rear casing',(0,.101,.105),(.50,.004,.192),shell)
# Close the rear of the hood with a continuous sloping steel deflector.
v=[(-.247,-.094,.011),(.247,-.094,.011),(.247,.096,.201),(-.247,.096,.201)]
o=mesh('45 degree rear backstop',v,[(0,1,2,3)],shell)
solid=o.modifiers.new('Steel sheet thickness','SOLIDIFY'); solid.thickness=.003
# Full-height end caps join floor, back and roof. There are no cutouts.
v=[(-.25,-.102,.009),(-.25,.102,.009),(-.25,.102,.201),(-.25,-.102,.201),
   (.25,-.102,.009),(.25,.102,.009),(.25,.102,.201),(.25,-.102,.201)]
o=mesh('Fitted solid end caps',v,[(0,1,2,3),(7,6,5,4)],shell)
solid=o.modifiers.new('Steel sheet thickness','SOLIDIFY'); solid.thickness=.003
for x in (-.246,.246):
    box('Side folded roof seam', (x,0,.205),(.005,.205,.005),edges,.001)
    for y in (-.075,.075):
        cylinder('Cheek rivet', (x,y,.025 if y<0 else .09),.0034,.003,dark,(0,math.pi/2,0))
for x in (-.22,.22):
    cylinder('Front flush screw', (x,-.105,.020),.003,.002,edges,(math.pi/2,0,0))
# A fixed, full-width axle supports the four separate hinge sleeves. The rat
# plates rotate around its centreline rather than floating in the shroud.
cylinder('Common rat support axle',(0,-.056,.104),.0019,.495,edges,(0,math.pi/2,0),32)
# Four individually moving targets have independent sleeves on the common axle.
positions=[-.186,-.080,.080,.186]
for i,x in enumerate(positions,1):
    cylinder('Pivot %02d'%i,(x,-.056,.104),.005,.034,edges,(0,math.pi/2,0))
    for dx in (-.018,.018):
        cylinder('Pivot pin %02d'%i,(x+dx,-.056,.104),.0035,.002,dark,(0,math.pi/2,0))

# Original rat outline: long tapering tail, rounded body, ears, pointed muzzle and feet.
# Coordinates are local to each hinge, in the vertical X-Z plane.
outline=[(-.028,.015),(-.038,.016),(-.046,.019),(-.052,.023),(-.055,.029),
 (-.055,.035),(-.053,.039),(-.051,.038),(-.053,.034),(-.053,.030),
 (-.050,.025),(-.044,.022),(-.037,.021),(-.034,.023),
 (-.031,.028),(-.025,.034),(-.016,.038),(-.005,.039),(.005,.037),(.012,.032),
 (.017,.030),(.020,.034),(.024,.035),(.027,.032),(.026,.027),(.032,.024),
 (.039,.021),(.046,.019),(.049,.016),(.044,.013),(.038,.013),(.034,.009),
 (.028,.008),(.026,.003),(.020,.003),(.019,.007),(.012,.007),(.006,.003),
 (.000,.003),(-.002,.007),(-.014,.007),(-.020,.003),(-.027,.003),(-.028,.008),
 (-.030,.012)]
# Leave visible space around the central reset in every target pose.
outline=[(x*.88,z) for x,z in outline]
rats=[]
for i,x in enumerate(positions,1):
    pivot=bpy.data.objects.new('Rat_%02d_Pivot'%i,None); collection.objects.link(pivot)
    pivot.location=(x,-.056,.104); rats.append(pivot)
    rat=extrude_outline('Rat_%02d_Silver'%i,outline,.0032,silver,pivot)
    # Eye is on the front face and follows the same keyed pivot.
    eye=cylinder('Rat_%02d_Eye'%i,(.029*.88,-.0020,.024),.0012,.0008,dark,(math.pi/2,0,0),12,pivot)
    # Separate NLA tracks export as individual clips. Rest pose is upright.
    for action_name,start,end in [('Rat_%02d_Down'%i,0,-math.pi/2),('Rat_%02d_Up'%i,-math.pi/2,0)]:
        pivot.rotation_mode='XYZ'; pivot.rotation_euler=(start,0,0)
        pivot.keyframe_insert(data_path='rotation_euler',frame=1)
        pivot.rotation_euler=(end,0,0); pivot.keyframe_insert(data_path='rotation_euler',frame=13)
        act=pivot.animation_data.action; act.name=action_name
        track=pivot.animation_data.nla_tracks.new(); track.name=action_name
        track.strips.new(action_name,1,act); track.mute=True
        pivot.animation_data.action=None
    pivot.rotation_euler=(0,0,0)
# The complete reset paddle (stem, disc and centre stud) is one moving assembly.
# Its lower hinge sits ahead of the axle so the backward hit reaches the
# axle at peak deflection, then rebounds without passing through it.
cylinder('Reset lower hinge',(0,-.090820,.044),.0035,.013,edges,(0,math.pi/2,0))
reset=bpy.data.objects.new('Reset_Pivot',None); collection.objects.link(reset)
reset.location=(0,-.090820,.044)
# The stem sits slightly behind the disc to avoid coplanar surfaces at the join.
stem=box('Reset disc stem',(0,.0035,.036),(.006,.004,.072),silver,.001); stem.parent=reset
cylinder('Reset_Circle',(0,0,.080),.019,.004,silver,(math.pi/2,0,0),48,reset)
cylinder('Reset circle centre',(0,-.003,.080),.002,.001,dark,(math.pi/2,0,0),16,reset)
reset.rotation_mode='XYZ'
for frame,angle in [(1,0),(5,-math.radians(24)),(8,math.radians(5)),(12,0)]:
    reset.rotation_euler=(angle,0,0); reset.keyframe_insert(data_path='rotation_euler',frame=frame)
act=reset.animation_data.action; act.name='Reset_Hit'
track=reset.animation_data.nla_tracks.new(); track.name='Reset_Hit'
track.strips.new('Reset_Hit',1,act); track.mute=True
reset.animation_data.action=None; reset.rotation_euler=(0,0,0)

# A small original label reinforces scale without hiding the folded steel lip.
def text_object(body,name,loc,size,material):
    curve=bpy.data.curves.new(name,'FONT'); curve.body=body; curve.align_x='CENTER'; curve.size=size; curve.extrude=.0001
    obj=bpy.data.objects.new(name,curve); collection.objects.link(obj); obj.location=loc
    obj.rotation_euler=(math.pi/2,0,0); curve.materials.append(material)
    bpy.context.view_layer.objects.active=obj; obj.select_set(True); bpy.ops.object.convert(target='MESH'); obj.select_set(False)
    return obj
text_object("DAD'S RESET-ABLE RAT REVUE",'Front stencil',(0,-.105,.016),.011,accent)

# Presentation setup excluded from export collection.
box('Studio floor',(0,0,-.008),(200,200,.006),material('Studio',(.12,.13,.13),0,.8,31),0)
floor=bpy.data.objects['Studio floor']; collection.objects.unlink(floor); scene.collection.objects.link(floor)
def area(name,loc,power,size):
    data=bpy.data.lights.new(name,'AREA'); ob=bpy.data.objects.new(name,data); scene.collection.objects.link(ob)
    ob.location=loc; data.energy=power; data.shape='DISK'; data.size=size
    ob.rotation_euler=(Vector((0,0,.08))-ob.location).to_track_quat('-Z','Y').to_euler()
area('Key',(-.5,-.8,1.1),90,1.0); area('Fill',(.7,-.2,.7),55,.8); area('Rim',(0,.7,.9),80,.7)
camera_data=bpy.data.cameras.new('Review camera'); cam=bpy.data.objects.new('Review camera',camera_data); scene.collection.objects.link(cam)
cam.location=(.62,-1.15,.59); cam.rotation_euler=(Vector((0,0,.10))-cam.location).to_track_quat('-Z','Y').to_euler()
camera_data.type='ORTHO'; camera_data.ortho_scale=.71; scene.camera=cam
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'; scene.render.filepath=os.path.join(ROOT,'rat_gallery_still.png')
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'rat_gallery.blend'))
# Select only prop objects, preserving the named pivots and clip tracks.
bpy.ops.object.select_all(action='DESELECT')
for obj in collection.objects: obj.select_set(True)
bpy.ops.export_scene.gltf(filepath=os.path.join(ROOT,'rat_gallery.glb'),export_format='GLB',use_selection=True,export_apply=True,export_animations=True,export_animation_mode='NLA_TRACKS')
bpy.ops.render.render(write_still=True)
print('GALLERY FINISHED',flush=True)


