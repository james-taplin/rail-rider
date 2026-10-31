"""Rail-Rider adult revision. Run with Blender --background --python this_file.
Metres; author in glTF coordinates via B(), export +Y up/+Z muzzle.
Preview GLB is separate from release assets. No installed game writes.
"""
import bpy, bmesh, math, os, json, sys
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
P=os.path.dirname(os.path.abspath(__file__))
os.makedirs(os.path.join(P,'renders'),exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
def B(p): return (p[0],-p[2],p[1])

def save_image(name,a,linear=False):
    h,w=a.shape[:2]; im=bpy.data.images.new(name,width=w,height=h,alpha=True)
    im.colorspace_settings.name='Non-Color' if linear else 'sRGB'
    rgba=np.ones((h,w,4),dtype=np.float32); rgba[:,:,:a.shape[2]]=a
    im.pixels.foreach_set(rgba.ravel()); im.filepath_raw=os.path.join(P,name+'.png'); im.file_format='PNG'; im.save(); im.pack()
    return im

def textures():
    n=2048; y,x=np.mgrid[0:n,0:n].astype(np.float32); rng=np.random.default_rng(1938)
    # Longitudinal fine grain with low contrast. No photograph lighting baked in.
    flow=y+8*np.sin(x*.004)+3*np.sin(x*.011+y*.006)
    def noise(nx,ny):
        values=rng.normal(0,1,(ny,nx)); xx=np.linspace(0,nx-1,n); yy=np.linspace(0,ny-1,n)
        tmp=np.array([np.interp(xx,np.arange(nx),r) for r in values],dtype=np.float32)
        return np.array([np.interp(yy,np.arange(ny),r) for r in tmp.T],dtype=np.float32).T
    grain_source=bpy.data.images.load(os.path.join(P,'beech_source.png'));grain_source.scale(n,n)
    col=np.array(grain_source.pixels[:],dtype=np.float32).reshape(n,n,4)[:,:,:3].copy()
    # Match dark stained beech while retaining the source's irregular fibre pattern.
    col*=np.array([.145,.061,.024],np.float32)/col.mean(axis=(0,1))
    col[n//2:]=col[:n//2] # identical grain on the clean/marked UV halves at edge transitions
    grain_height=(col[:,:,0]-col[:,:,0].mean())*.00008
    src=bpy.data.images.load(os.path.join(P,'logo_approved_mask.png'))
    a=np.array(src.pixels[:],dtype=np.float32).reshape(src.size[1],src.size[0],4)
    alpha=a[:,:,3]; rows,cols=np.where(alpha>.1)
    if len(rows)<100: raise RuntimeError('Logo alpha mask is empty')
    crop=alpha[rows.min():rows.max()+1,cols.min():cols.max()+1]
    # Author material mask at actual UV scale; preserve source logo aspect ratio.
    mw=1024; mh=round(mw*crop.shape[0]/crop.shape[1])
    xx=np.linspace(0,crop.shape[1]-1,mw); yy=np.linspace(0,crop.shape[0]-1,mh)
    tmp=np.array([np.interp(xx,np.arange(crop.shape[1]),r) for r in crop],dtype=np.float32)
    small=np.array([np.interp(yy,np.arange(crop.shape[0]),r) for r in tmp.T],dtype=np.float32).T
    mask=np.zeros((n,n),np.float32); cx=1394; cy=1585
    mask[cy-mh//2:cy-mh//2+mh,cx-mw//2:cx-mw//2+mw]=small
    # Repeated small Gaussian-like smoothing yields rounded concave shoulders.
    scoop=mask.copy()
    for _ in range(14):
        scoop=(scoop*4+np.roll(scoop,1,0)+np.roll(scoop,-1,0)+np.roll(scoop,1,1)+np.roll(scoop,-1,1))/8
    height=-.00020*scoop # nominal 0.20 mm virtual recess, not geometry displacement
    height+=grain_height
    gy,gx=np.gradient(height,.36/n,.36/n)
    normal=np.stack([-gx,-gy,np.ones_like(gx)],axis=-1); normal/=np.linalg.norm(normal,axis=2,keepdims=True)
    col*=1-.60*mask[:,:,None]
    base=save_image('beech_basecolor',np.clip(col,0,1))
    norm=save_image('beech_normal',normal*.5+.5,True)
    save_image('woodcut_height',np.repeat((.5+height/.002)[:,:,None],3,axis=2),True)
    save_image('woodcut_mask',np.repeat(mask[:,:,None],3,axis=2),True)
    print('LOGO_ALPHA',float(alpha.min()),float(alpha.max()),'bounds',crop.shape,'placed',mw,mh)
    return base,norm
base,norm=textures()

def material(name,color,metal,rough,images=None):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Metallic'].default_value=metal; bs.inputs['Roughness'].default_value=rough
    if images:
        t=m.node_tree.nodes.new('ShaderNodeTexImage'); t.image=images[0]; m.node_tree.links.new(t.outputs['Color'],bs.inputs['Base Color'])
        t=m.node_tree.nodes.new('ShaderNodeTexImage'); t.image=images[1]
        nm=m.node_tree.nodes.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value=1
        m.node_tree.links.new(t.outputs['Color'],nm.inputs['Color']); m.node_tree.links.new(nm.outputs['Normal'],bs.inputs['Normal'])
    return m
wood=material('Stained beech - shallow carved logo',(.3,.15,.07),0,.36,(base,norm))
steel=material('Blued barrel steel',(.035,.043,.049),.82,.30)
stamped=material('Satin blackened receiver',(.026,.031,.034),.75,.43)
levermat=material('Blackened lever',(.035,.039,.042),.72,.35)
hardware=material('Dark polished fasteners',(.12,.135,.145),.9,.27)
sightmat=material('Matte sight steel',(.018,.022,.024),.65,.64)

def mesh(name,verts,faces,mat):
    me=bpy.data.meshes.new(name); me.from_pydata([B(v) for v in verts],[],faces); me.update()
    ob=bpy.data.objects.new(name,me); bpy.context.collection.objects.link(ob); me.materials.append(mat)
    bm=bmesh.new(); bm.from_mesh(me); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(me); bm.free(); me.update()
    return ob
def bevel(o,width=.001,segments=3):
    m=o.modifiers.new('Soft manufactured edges','BEVEL'); m.width=width; m.segments=segments
    m.harden_normals=True
    for f in o.data.polygons: f.use_smooth=True
    m=o.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL'); m.keep_sharp=True
    return o
def apply(o):
    bpy.context.view_layer.objects.active=o
    for mod in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=mod.name)
    return o
def cube(name,c,d,mat,bev=.001):
    bpy.ops.mesh.primitive_cube_add(size=1,location=B(c)); o=bpy.context.object; o.name=name; o.dimensions=(d[0],d[2],d[1])
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True); o.data.materials.append(mat)
    return bevel(o,bev) if bev else o
def cyl(name,c,r,depth,axis,mat,n=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=n,radius=r,depth=depth,location=B(c)); o=bpy.context.object; o.name=name
    if axis=='z': o.rotation_euler[0]=math.pi/2
    if axis=='x': o.rotation_euler[1]=math.pi/2
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True); o.data.materials.append(mat)
    for f in o.data.polygons: f.use_smooth=len(f.vertices)==4
    return bevel(o,.00035,2)
def profile(name,pts,half,mat,b=.001):
    n=len(pts); vs=[(x,y,z) for x in [-half,half] for z,y in pts]
    fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return bevel(mesh(name,vs,fs,mat),b,4) if b else mesh(name,vs,fs,mat)
def smooth_path(points,steps=4,closed=False):
    out=[]; n=len(points)
    for i in range(n if closed else n-1):
        p0=points[(i-1)%n] if closed else points[max(0,i-1)]; p1=points[i]; p2=points[(i+1)%n]; p3=points[(i+2)%n] if closed else points[min(n-1,i+2)]
        for k in range(steps):
            t=k/steps
            out.append(tuple(.5*(2*p1[j]+(-p0[j]+p2[j])*t+(2*p0[j]-5*p1[j]+4*p2[j]-p3[j])*t*t+(-p0[j]+3*p1[j]-3*p2[j]+p3[j])*t*t*t) for j in range(2)))
    if not closed: out.append(points[-1])
    return out

# Adult silhouette: shaped comb, curved wrist, broad butt, restrained rounded edges.
stock_outline=[(-.441,-.024),(-.37,-.012),(-.305,-.002),(-.275,.003),(-.252,-.004),(-.231,-.008),(-.193,.001),(-.15,.020),(-.113,.029),(-.097,.021),(-.103,-.006),(-.137,-.025),(-.18,-.045),(-.218,-.068),(-.237,-.085),(-.251,-.081),(-.31,-.098),(-.428,-.129),(-.445,-.126)]
def stock_loft(name,sections,mat,steps=4):
    # Smooth longitudinal sections and rounded superellipse cross-sections.
    # Shared end section guarantees an exactly fitted buttplate perimeter.
    interp=[]
    for i in range(len(sections)-1):
        a=sections[max(0,i-1)];b=sections[i];c=sections[i+1];d=sections[min(len(sections)-1,i+2)]
        for k in range(steps):
            t=k/steps
            vals=[b[0]+(c[0]-b[0])*t]
            for j in range(1,4): vals.append(.5*(2*b[j]+(-a[j]+c[j])*t+(2*a[j]-5*b[j]+4*c[j]-d[j])*t*t+(-a[j]+3*b[j]-3*c[j]+d[j])*t*t*t))
            interp.append(vals)
    interp.append(sections[-1]);n=40;vs=[]
    for z,top,bottom,half in interp:
        cy=(top+bottom)/2;hh=(top-bottom)/2
        for i in range(n):
            a=i*2*math.pi/n;co=math.cos(a);si=math.sin(a)
            vs.append((half*math.copysign(abs(co)**.55,co),cy+hh*math.copysign(abs(si)**.7,si),z))
    fs=[]
    for j in range(len(interp)-1):
        for i in range(n):fs.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
    fs += [tuple(reversed(range(n))),tuple((len(interp)-1)*n+i for i in range(n))]
    ob=mesh(name,vs,fs,mat)
    for f in ob.data.polygons:f.use_smooth=f.index<len(fs)-2
    return ob
stock_sections=[(-.445,-.024,-.125,.015),(-.438,-.021,-.127,.016),(-.39,-.013,-.116,.017),(-.34,-.006,-.104,.0175),(-.30,0,-.092,.0175),(-.275,.003,-.085,.017),(-.255,-.004,-.080,.016),(-.238,-.008,-.081,.015),(-.224,-.007,-.070,.014),(-.20,-.002,-.055,.013),(-.17,.011,-.040,.012),(-.14,.024,-.025,.0115),(-.113,.029,-.017,.011),(-.098,.018,-.007,.0105)]
stock=stock_loft('StockVisual',stock_sections,wood)
# Planar marked face only. Other faces sample clean lower half of atlas.
def wood_uv(o,is_stock=False):
    uv=o.data.uv_layers.new(name='UVMap')
    for poly in o.data.polygons:
        near=is_stock and poly.normal.x>.55
        for li in poly.loop_indices:
            x,by,y=o.data.vertices[o.data.loops[li].vertex_index].co; z=-by
            if is_stock:
                u=(-z-.09)/.36; v=(y-.20*(z+.30)+.15)/.18
            else:
                u=(.32-z)/.36; v=(y+.10)/.18
            uv.data[li].uv=(u,.5*v+(.5 if near else 0))
wood_uv(stock,True)
# Thin dark buttplate matching sloped butt outline.
butt=stock_loft('Buttplate',[(-.449,-.025,-.124,.0142),(-.448,-.0243,-.1247,.0148),stock_sections[0]],stamped,2)
receiverpts=[(-.112,.024),(-.092,.031),(.071,.031),(.085,.023),(.085,-.012),(.063,-.020),(.029,-.026),(-.022,-.030),(-.065,-.020),(-.101,-.012)]
receiver=profile('Receiver - stamped shell',receiverpts,.012,stamped,.0024)
slot=cube('Underside pivot slot cutter',(0,-.019,-.044),(.0075,.050,.083),stamped,0)
mod=receiver.modifiers.new('Open underside lever mortise','BOOLEAN');mod.operation='DIFFERENCE';mod.object=slot
apply(receiver);bpy.data.objects.remove(slot,do_unlink=True)
# The stamp uses a recessed normal and darkened trough on the shooter's flat face.
im=bpy.data.images.load(os.path.join(P,'receiver_stamp_mask.png'))
mask=np.array(im.pixels[:],dtype=np.float32).reshape(768,2048,4)[:,:,0]
scoop=mask.copy()
for _ in range(4): scoop=(4*scoop+np.roll(scoop,1,0)+np.roll(scoop,-1,0)+np.roll(scoop,1,1)+np.roll(scoop,-1,1))/8
gy,gx=np.gradient(-.000105*scoop,.064/768,.170/2048)
n=np.stack([-gx,-gy,np.ones_like(gx)],axis=-1);n/=np.linalg.norm(n,axis=2,keepdims=True)
col=(1-.57*mask[:,:,None])*np.array([.026,.031,.034])
sm=material('Receiver - impressed maker stamp',(.026,.031,.034),.75,.43,(save_image('receiver_stamp_colour',col),save_image('receiver_stamp_normal',n*.5+.5,True)))
receiver.data.materials[0]=sm
uv=receiver.data.uv_layers.active or receiver.data.uv_layers.new(name='UVMap')
for face in receiver.data.polygons:
    for li in face.loop_indices:
        v=receiver.matrix_world @ receiver.data.vertices[receiver.data.loops[li].vertex_index].co
        uv.data[li].uv=((v.y+.069)/.170,(v.z+.028)/.064) if face.normal.x>.99 and v.x>.01199 else (.02,.02)
# Fasteners have recessed slots, not extra bright dots.
for side in [-1,1]:
    for k,(z,y,r) in enumerate([(-.083,.010,.0038),(-.096,-.007,.0033),(.063,.005,.0026)]):
        screw=cyl('Receiver screw %s %s'%(side,k),(side*.0125,y,z),r,.0013,'x',hardware)
        cutter=cube('slot cutter',(side*.0134,y,z),(.0015,.0008,r*1.45),hardware,0)
        mod=screw.modifiers.new('Recessed driver slot','BOOLEAN'); mod.object=cutter; mod.operation='DIFFERENCE'; apply(screw); bpy.data.objects.remove(cutter,do_unlink=True)

def tube(name,z0,z1,y,r,inner,mat):
    n=48; vs=[]
    for z,rr in [(z0,r),(z1,r),(z1,inner),(z1-.009,inner)]:
        for i in range(n):
            a=i*2*math.pi/n; vs.append((rr*math.cos(a),y+rr*math.sin(a),z))
    fs=[]
    for j in range(3):
        for i in range(n): fs.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
    fs += [tuple(reversed(range(n))),tuple(3*n+i for i in range(n))]
    o=mesh(name,vs,fs,mat)
    for f in o.data.polygons: f.use_smooth=f.index<n or 2*n<=f.index<3*n
    return o
tube('Barrel - recessed muzzle',.066,.484,.023,.008,.00225,steel)
cyl('BB reserve tube',(0,.005,.276),.008,.398,'z',steel,48)
# Forearm with a rounded rectangular cross-section, snug against receiver.
def forearm():
    vs=[]; n=24
    for z,w,h,cy in [(.079,.012,.014,-.006),(.088,.016,.016,-.008),(.105,.017,.017,-.009),(.235,.017,.017,-.009),(.261,.016,.016,-.008),(.271,.014,.014,-.007)]:
        for i in range(n):
            a=2*math.pi*i/n; co=math.cos(a); si=math.sin(a)
            vs.append((w*math.copysign(abs(co)**.55,co),cy+h*math.copysign(abs(si)**.55,si),z))
    fs=[]
    for j in range(5):
        for i in range(n): fs.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
    fs += [tuple(reversed(range(n))),tuple(5*n+i for i in range(n))]
    ob=mesh('Forearm',vs,fs,wood)
    for f in ob.data.polygons: f.use_smooth=f.index<5*n
    wood_uv(ob); return ob
forearm()
# Band is an actual thin strap around wood, not an oversized cylinder.
def band():
    n=32; vs=[]
    for z,w,h in [(.249,.018,.020),(.257,.018,.020),(.257,.0169,.0189),(.249,.0169,.0189)]:
        for i in range(n):
            a=i*2*math.pi/n; co=math.cos(a); si=math.sin(a)
            vs.append((w*math.copysign(abs(co)**.6,co),-.003+h*math.copysign(abs(si)**.6,si),z))
    fs=[]
    for j in range(4):
        for i in range(n): fs.append((j*n+i,j*n+(i+1)%n,((j+1)%4)*n+(i+1)%n,((j+1)%4)*n+i))
    o=mesh('Forearm retaining band',vs,fs,stamped)
    for f in o.data.polygons: f.use_smooth=True
band()

# Reference registration for the cosmetic game silhouette. The attached 1938B
# sheet (98L) establishes the open trigger saddle and single enclosed rear loop;
# the adult photo gives the exterior proportions. No internal mechanism is built.
# Points below are observations on adult_lever_reference_detail.png, not measured
# part dimensions. Register its apparent pin to the game receiver and fit the wrist.
pivot=(0,-.022,-.042); tilt=math.radians(9)
def ref2d(px,py):
    dz=(px-780)*.000236;dy=(220-py)*.000236
    return (pivot[2]+math.cos(tilt)*dz+math.sin(tilt)*dy,pivot[1]+math.cos(tilt)*dy-math.sin(tilt)*dz)
def traced(points):return [ref2d(x,y) for x,y in points]
outer=[(130,505),(140,482),(191,438),(266,386),(343,338),(420,301),(482,271),(515,256),(544,252),(558,263),(560,303),(570,329),(592,344),(622,351),(662,348),(696,337),(722,322),(739,299),(748,273),(746,244),(738,220),(735,202),(744,188),(762,177),(787,181),(804,196),(810,213),(820,224),(838,220),(854,202),(863,203),(865,220),(855,245),(833,272),(804,301),(765,332),(728,357),(684,381),(641,396),(597,401),(543,401),(517,405),(480,421),(425,450),(368,484),(313,522),(263,559),(228,582),(195,589),(165,582),(143,566),(132,545),(128,523)]
finger_hole=[(154,505),(169,482),(214,443),(276,402),(345,361),(408,328),(453,307),(476,302),(496,310),(508,326),(517,347),(514,365),(502,380),(464,399),(420,421),(363,451),(303,490),(251,531),(214,558),(190,566),(171,560),(157,546),(151,528)]
lever=profile('Lever',smooth_path(traced(outer),2,True),.0016,levermat,0)
cutter=profile('Rear loop cut',smooth_path(traced(finger_hole),2,True),.010,levermat,0)
mod=lever.modifiers.new('Rear loop cut','BOOLEAN');mod.operation='DIFFERENCE';mod.object=cutter;apply(lever);bpy.data.objects.remove(cutter,do_unlink=True)
apply(bevel(lever,.00035,2))
# Low, raised, same-finish branding on the solid web in the former brand position.
# Convert to mesh and join both readable outward faces to the animated node.
lettering=[]; mz,my=ref2d(573,368)
for side in [-1,1]:
    cu=bpy.data.curves.new('Dad’s raised lettering','FONT');cu.body="Dad's";cu.align_x='CENTER';cu.align_y='CENTER'
    cu.font=bpy.data.fonts.load('C:/Windows/Fonts/BOOKOSBI.TTF',check_existing=True)
    cu.resolution_u=3;cu.extrude=.013;cu.bevel_depth=.004;cu.bevel_resolution=1
    ob=bpy.data.objects.new('Raised Dad’s '+str(side),cu);bpy.context.collection.objects.link(ob);cu.materials.append(levermat)
    bpy.context.view_layer.update(); k=min(.024/ob.dimensions.x,.0068/ob.dimensions.y);ob.scale=(k,k,k)
    right=Vector((0,side*math.cos(tilt),side*math.sin(tilt)));up=Vector((0,-math.sin(tilt),math.cos(tilt)));out=right.cross(up)
    ob.rotation_euler=Matrix((right,up,out)).transposed().to_euler();ob.location=B((side*(.0016+.000035),my,mz))
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob;bpy.ops.object.convert(target='MESH')
    vs=ob.data.vertices; cx=(min(v.co.x for v in vs)+max(v.co.x for v in vs))/2; cy=(min(v.co.y for v in vs)+max(v.co.y for v in vs))/2
    for v in vs: v.co.x-=cx; v.co.y-=cy
    lettering.append(ob)
bpy.ops.object.select_all(action='DESELECT');lever.select_set(True)
for ob in lettering:ob.select_set(True)
bpy.context.view_layer.objects.active=lever;bpy.ops.object.join()
for v in lever.data.vertices:v.co-=Vector(B(pivot))
lever.location=B(pivot)
for side in [-1,1]:cyl('Lever pivot pin '+str(side),(side*.0125,pivot[1],pivot[2]),.0035,.0015,'x',hardware)
trigger_pts=[(620,192),(650,182),(663,203),(657,240),(655,265),(658,281),(667,297),(670,304),(665,312),(653,306),(638,293),(627,275),(620,253)]
trigger=profile('Trigger',smooth_path(traced(trigger_pts),2,True),.0014,levermat,.00045)

# Practical low leaf and a true open notch. Avoid old loader's name-specific sight reshape.
cube('Rear sight spring',(0,.033,-.009),(.009,.0018,.082),sightmat,.0003)
def sight_profile(name,points,z0,z1):
    n=len(points);vs=[(x,y,z) for z in [z0,z1] for x,y in points]
    fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,vs,fs,sightmat)
sight_profile('Rear notch - open U',[(-.007,.034),(.007,.034),(.007,.046),(.0022,.046),(.0022,.039),(-.0022,.039),(-.0022,.046),(-.007,.046)],-.057,-.055)
cube('Front sight base',(0,.032,.452),(.009,.003,.019),sightmat,.0008)
sight_profile('Front sight blade',[(-.0012,.032),(.0012,.032),(.0012,.0425),(-.0012,.0425)],.449,.455)
for name,pos in {'RearSight':(0,.0425,-.056),'FrontSight':(0,.0425,.452),'Muzzle':(0,.023,.484),'Chamber':(0,.023,.10),'RackDock':(0,0,.045)}.items():
    o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.location=B(pos)

# Apply modifiers/transforms so loader can consume one primitive per flat node.
for ob in list(bpy.context.scene.objects):
    if ob.type=='MESH':
        apply(ob); bpy.context.view_layer.objects.active=ob; ob.select_set(True)
        bpy.ops.object.transform_apply(location=False,rotation=True,scale=True);ob.select_set(False)
        tri=ob.modifiers.new('Export triangulation','TRIANGULATE');tri.keep_custom_normals=True;apply(ob)

def audit():
    dg=bpy.context.evaluated_depsgraph_get(); meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    def tree(o):
        me=o.data;return BVHTree.FromPolygons([o.matrix_world@v.co for v in me.vertices],[tuple(f.vertices) for f in me.polygons])
    report={'meshes':[],'sweep':[]}
    for o in meshes:
        bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
        report['meshes'].append({'name':o.name,'nonmanifold':sum(not e.is_manifold for e in bm.edges),'winding_edges':sum(e.is_manifold and not e.is_contiguous for e in bm.edges),'volume':bm.calc_volume(signed=True)})
        bm.free()
    fixed=[(o.name,tree(o)) for o in meshes if o!=lever]
    for deg in range(0,71):
        lever.rotation_euler.x=-math.radians(deg);bpy.context.view_layer.update();lt=tree(lever)
        hits=[name for name,t in fixed if lt.overlap(t)]
        report['sweep'].append({'degrees':deg,'overlap':hits})
    lever.rotation_euler.x=0;bpy.context.view_layer.update()
    with open(os.path.join(P,'geometry_audit.json'),'w') as f:json.dump(report,f,indent=2)
    print('SWEEP_HITS',[(r['degrees'],r['overlap']) for r in report['sweep'] if r['overlap']])
audit()

# Native runtime uses procedural Lever rotation; clip is supplied for DCC preview.
for frame,angle in [(1,0),(12,0),(30,-70),(36,-70),(54,0),(66,0)]:
    lever.rotation_euler.x=math.radians(angle);lever.keyframe_insert(data_path='rotation_euler',frame=frame)
if lever.animation_data: lever.animation_data.action.name='Lever_cycle_preview'
scene=bpy.context.scene;scene.frame_set(1);scene.render.fps=30;scene.frame_start=1;scene.frame_end=66
bpy.ops.export_scene.gltf(filepath=os.path.join(P,'rail_rider_adult_preview.glb'),export_format='GLB',export_apply=True,export_tangents=True,export_animations=False)

def point(o,p):o.rotation_euler=(Vector(B(p))-o.location).to_track_quat('-Z','Y').to_euler()
world=scene.world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.075,.085,.10,1);world.node_tree.nodes['Background'].inputs[1].default_value=.55
def light(name,pos,power,size,target=(0,0,0)):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=B(pos);point(o,target);return o
key=light('Large daylight key',(.55,.65,-.35),55,.65)
light('Soft fill',(-.6,.15,.2),32,.9)
light('Edge strip',(.2,.4,.8),40,.5)
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1600;scene.render.resolution_y=760;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
camd=bpy.data.cameras.new('Review camera');cam=bpy.data.objects.new('Review camera',camd);bpy.context.collection.objects.link(cam);scene.camera=cam
def render(name,pos,target,ortho=None,lens=45):
    cam.location=B(pos);point(cam,target);camd.type='ORTHO' if ortho else 'PERSP';camd.ortho_scale=ortho or 1;camd.lens=lens
    scene.render.filepath=os.path.join(P,'renders',name+'.png');bpy.ops.render.render(write_still=True)
render('01_shooter_side',(1.5,.08,-.05),(0,-.03,.01),1.04)
render('02_stock_relief',(.38,.035,-.37),(0,-.057,-.32),.27)
render('03_receiver',(.45,.08,-.015),(0,-.018,-.025),.30)
render('17_receiver_stamp',(.26,.015,-.015),(0,.004,-.016),.133)
render('14_lever_reference_side',(-.6,-.004,-.08),(0,-.043,-.105),.26)
render('15_dads_shooter_side',(.27,-.035,-.10),(0,my,mz),.074)
render('16_dads_other_side',(-.27,-.035,-.10),(0,my,mz),.074)
render('04_sight_view',(0,.0425,-.196),(0,.0425,.452),None,35)
scene.frame_set(30)
render('05_lever_open',(1.5,.12,-.05),(0,-.08,.01),1.10)
scene.frame_set(1)
render('06_plain_side',(-1.5,.08,.04),(0,-.03,.01),1.04)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(P,'rail_rider_adult.blend'))
print('ADULT_BUILD_COMPLETE',P)
