"""Read-only export contract checks; run with Blender's bundled Python."""
import json,struct,pathlib,math
p=pathlib.Path(__file__).parent
d=(p/'rail_rider_adult_preview.glb').read_bytes();size=struct.unpack_from('<I',d,12)[0];j=json.loads(d[20:20+size]);binary=20+size+8
required=['Lever','RearSight','FrontSight','Muzzle','Chamber','RackDock']
nodes={n['name']:n for n in j['nodes']}
assert all(n in nodes for n in required)
assert all(n.get('scale',[1,1,1])==[1,1,1] for n in j['nodes'])
assert all(len(m['primitives'])==1 for m in j['meshes'])
assert all(len(n.get('children',[]))==0 for n in j['nodes'])
def read(acc):
    a=j['accessors'][acc];v=j['bufferViews'][a['bufferView']];off=binary+v.get('byteOffset',0)+a.get('byteOffset',0)
    count={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
    assert a['componentType']==5126
    stride=v.get('byteStride',count*4)
    return [struct.unpack_from('<'+'f'*count,d,off+i*stride) for i in range(a['count'])]
receiver=j['meshes'][nodes['Receiver - stamped shell']['mesh']]['primitives'][0]
a=receiver['attributes']; pos=read(a['POSITION']); uv=read(a['TEXCOORD_0'])
marked=[v for v,t in zip(pos,uv) if math.dist(t,(.02,.98))>1e-5]
assert marked and all(v[0]>.01199 for v in marked)
assert 'normalTexture' in j['materials'][receiver['material']]
points=[]
for n in j['nodes']:
    if 'mesh' not in n:continue
    prim=j['meshes'][n['mesh']]['primitives'][0];t=n.get('translation',[0,0,0])
    assert n.get('rotation',[0,0,0,1])==[0,0,0,1],n['name']
    points += [tuple(v[k]+t[k] for k in range(3)) for v in read(prim['attributes']['POSITION'])]
bounds=[[min(v[k] for v in points),max(v[k] for v in points)] for k in range(3)]
rear=nodes['RearSight']['translation'];front=nodes['FrontSight']['translation']
assert abs(rear[0]-front[0])<1e-7 and abs(rear[1]-front[1])<1e-7
stock=j['meshes'][nodes['StockVisual']['mesh']]['primitives'][0]
stockmat=j['materials'][stock['material']]
assert 'normalTexture' in stockmat and 'TANGENT' in stock['attributes']
assert all(abs(sum(c*c for c in v[:3])-1)<.005 for v in read(stock['attributes']['TANGENT']))
def end_points(name):
    pr=j['meshes'][nodes[name]['mesh']]['primitives'][0]
    return [v for v in read(pr['attributes']['POSITION']) if abs(v[2]+.445)<1e-6]
wood=end_points('StockVisual');plate=end_points('Buttplate')
gap=max(min(math.dist(a,b) for b in plate) for a in wood)
assert gap<1e-6,gap
audit=json.loads((p/'geometry_audit.json').read_text())
assert all(m['nonmanifold']==0 and m['winding_edges']==0 and m['volume']>0 for m in audit['meshes'])
assert not any(s['overlap'] for s in audit['sweep'])
report={'triangles':sum(j['accessors'][pr['indices']]['count']//3 for m in j['meshes'] for pr in m['primitives']), 'materials':len(j['materials']),'meshes':len(j['meshes']),'bounds_metres':bounds,'overall_length_metres':bounds[2][1]-bounds[2][0],'buttplate_fit_max_gap_metres':gap,'required_nodes':{n:nodes[n].get('translation') for n in required},'stock_tangents':True,'embedded_normal_map':True,'single_primitive_meshes':True,'flat_hierarchy':True,'sweep_samples':len(audit['sweep']),'sweep_interval_degrees':1,'sweep_overlaps':0}
(p/'export_validation.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
