// Minimal glTF 2 binary reader for these self-contained Blender exports.
// It reads the actual exported geometry, PBR materials, hierarchy and animation tracks.
import * as THREE from 'three';

export async function loadGLB(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`Could not load ${url}: ${response.status}`);
  const data = await response.arrayBuffer(), view = new DataView(data);
  if (view.getUint32(0, true) !== 0x46546c67 || view.getUint32(4, true) !== 2) throw Error('Expected a glTF 2 GLB');
  let offset = 12, json, bin;
  while (offset < data.byteLength) {
    const len = view.getUint32(offset, true), type = view.getUint32(offset + 4, true); offset += 8;
    if (type === 0x4e4f534a) json = JSON.parse(new TextDecoder().decode(new Uint8Array(data, offset, len)));
    if (type === 0x004e4942) bin = data.slice(offset, offset + len);
    offset += len;
  }
  if (!json || !bin) throw Error('GLB is missing JSON or embedded binary');
  const types = {5120:Int8Array,5121:Uint8Array,5122:Int16Array,5123:Uint16Array,5125:Uint32Array,5126:Float32Array};
  const counts = {SCALAR:1,VEC2:2,VEC3:3,VEC4:4,MAT4:16};
  function accessor(i) {
    const a=json.accessors[i], b=json.bufferViews[a.bufferView], Type=types[a.componentType], n=counts[a.type];
    if (!b || !Type || !n || a.sparse) throw Error('Unsupported glTF accessor ' + i);
    const start=(b.byteOffset||0)+(a.byteOffset||0), stride=b.byteStride||n*Type.BYTES_PER_ELEMENT;
    let array;
    if (stride===n*Type.BYTES_PER_ELEMENT) array=new Type(bin,start,a.count*n).slice();
    else { array=new Type(a.count*n); const d=new DataView(bin); const reads={5120:'getInt8',5121:'getUint8',5122:'getInt16',5123:'getUint16',5125:'getUint32',5126:'getFloat32'}; for(let j=0;j<a.count;j++)for(let k=0;k<n;k++)array[j*n+k]=d[reads[a.componentType]](start+j*stride+k*Type.BYTES_PER_ELEMENT,true); }
    return {array,n,normalized:!!a.normalized};
  }
  const textureCache = new Map();
  async function texture(index, color) {
    if (index == null) return null;
    const key=index+':'+color; if(textureCache.has(key)) return textureCache.get(key);
    const t=json.textures[index], img=json.images[t.source]; let url;
    if (img.bufferView != null) {const b=json.bufferViews[img.bufferView];url=URL.createObjectURL(new Blob([bin.slice(b.byteOffset||0,(b.byteOffset||0)+b.byteLength)],{type:img.mimeType}));}
    else url=new URL(img.uri, new URL(response.url)).href;
    const out=await new THREE.TextureLoader().loadAsync(url); if(img.bufferView!=null)URL.revokeObjectURL(url);
    out.flipY=false; out.colorSpace=color?THREE.SRGBColorSpace:THREE.NoColorSpace;
    const sampler=json.samplers?.[t.sampler]; const wrap={33071:THREE.ClampToEdgeWrapping,33648:THREE.MirroredRepeatWrapping,10497:THREE.RepeatWrapping};
    out.wrapS=wrap[sampler?.wrapS]??THREE.RepeatWrapping;out.wrapT=wrap[sampler?.wrapT]??THREE.RepeatWrapping; out.needsUpdate=true; textureCache.set(key,out); return out;
  }
  const mats=await Promise.all((json.materials||[]).map(async m=>{
    const p=m.pbrMetallicRoughness||{}, c=p.baseColorFactor||[1,1,1,1];
    const mat=new THREE.MeshStandardMaterial({name:m.name||'',color:new THREE.Color().setRGB(c[0],c[1],c[2]),opacity:c[3],transparent:m.alphaMode==='BLEND',alphaTest:m.alphaMode==='MASK'?(m.alphaCutoff??.5):0,side:m.doubleSided?THREE.DoubleSide:THREE.FrontSide,metalness:p.metallicFactor??1,roughness:p.roughnessFactor??1});
    mat.map=await texture(p.baseColorTexture?.index,true); const mr=await texture(p.metallicRoughnessTexture?.index,false);mat.metalnessMap=mr;mat.roughnessMap=mr;
    mat.normalMap=await texture(m.normalTexture?.index,false);if(m.normalTexture?.scale!=null)mat.normalScale.setScalar(m.normalTexture.scale);
    mat.emissiveMap=await texture(m.emissiveTexture?.index,true);if(m.emissiveFactor)mat.emissive.fromArray(m.emissiveFactor);mat.needsUpdate=true;return mat;
  }));
  const nodes=(json.nodes||[]).map((n,i)=>{const o=new THREE.Group();o.name=n.name||`node_${i}`;if(n.matrix){o.matrix.fromArray(n.matrix);o.matrix.decompose(o.position,o.quaternion,o.scale);} else {if(n.translation)o.position.fromArray(n.translation);if(n.rotation)o.quaternion.fromArray(n.rotation);if(n.scale)o.scale.fromArray(n.scale);}return o;});
  (json.nodes||[]).forEach((n,i)=>{
    if(n.mesh!=null)for(const p of json.meshes[n.mesh].primitives){if(p.mode!=null&&p.mode!==4)throw Error('Only triangle mesh primitives are supported');const g=new THREE.BufferGeometry();
      for(const [semantic,id] of Object.entries(p.attributes)){const name={POSITION:'position',NORMAL:'normal',TEXCOORD_0:'uv',TANGENT:'tangent',COLOR_0:'color'}[semantic];if(name){const a=accessor(id);g.setAttribute(name,new THREE.BufferAttribute(a.array,a.n,a.normalized));}}
      if(p.indices!=null){const a=accessor(p.indices);g.setIndex(new THREE.BufferAttribute(a.array,1));}if(!g.getAttribute('normal'))g.computeVertexNormals();const mesh=new THREE.Mesh(g,mats[p.material]||new THREE.MeshStandardMaterial());mesh.name=(json.meshes[n.mesh].name||nodes[i].name)+'_surface';nodes[i].add(mesh);
    } for(const child of n.children||[])nodes[i].add(nodes[child]);
  });
  const root=new THREE.Group();root.name='GLB';for(const i of json.scenes[json.scene||0].nodes)root.add(nodes[i]);
  const clips=(json.animations||[]).map((a,i)=>{const tracks=[];for(const ch of a.channels){const s=a.samplers[ch.sampler], target=nodes[ch.target.node], path=ch.target.path, property={translation:'position',rotation:'quaternion',scale:'scale'}[path];if(!property)continue;
    const input=accessor(s.input).array,output=accessor(s.output).array;if(s.interpolation==='CUBICSPLINE')throw Error('Cubic spline animation unsupported');const interp=s.interpolation==='STEP'?THREE.InterpolateDiscrete:THREE.InterpolateLinear;
    const Track=path==='rotation'?THREE.QuaternionKeyframeTrack:THREE.VectorKeyframeTrack;tracks.push(new Track(target.uuid+'.'+property,input,output,interp));}
    return new THREE.AnimationClip(a.name||`Animation_${i}`,-1,tracks);
  });
  return {root,clips,json,nodes};
}
