import bpy,bmesh
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=r'C:\Users\james\Desktop\Derail Valley Mods\Mods (code)\RailRider\Models\rail_rider_rifle.glb')
for o in bpy.context.scene.objects:
 if o.type=='MESH':
  bm=bmesh.new(); bm.from_mesh(o.data); bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=1e-7); vol=bm.calc_volume(signed=True); bad=sum(1 for e in bm.edges if not e.is_manifold); badw=sum(1 for e in bm.edges if e.is_manifold and not e.is_contiguous)
  print('AUDIT',o.name,'volume',round(vol,9),'nonmanifold',bad,'wrong_edges',badw,'faces',len(bm.faces)); bm.free()
