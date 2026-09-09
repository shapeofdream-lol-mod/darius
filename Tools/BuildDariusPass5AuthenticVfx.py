#!/usr/bin/env python3
import argparse, ctypes, json, math, shutil, struct
from pathlib import Path
from collections import OrderedDict

KINDS={0:'None',1:'Bool',2:'I8',3:'U8',4:'I16',5:'U16',6:'I32',7:'U32',8:'I64',9:'U64',10:'F32',11:'Vector2',12:'Vector3',13:'Vector4',14:'Matrix44',15:'Color',16:'String',17:'Hash',18:'WadChunkLink',128:'Container',129:'UnorderedContainer',130:'Struct',131:'Embedded',132:'ObjectLink',133:'Optional',134:'Map',135:'BitBool'}
PRIMITIVES={'287851b9':'VfxPrimitiveCameraTrail','4beb81fd':'VfxPrimitiveArbitraryQuad','5705625a':'VfxPrimitiveArbitraryTrail','68753673':'VfxPrimitiveBeam','8594e839':'VfxPrimitiveMesh','a14bd4d0':'VfxPrimitiveRay','a4aea2a5':'VfxPrimitiveAttachedMesh','96ddbc74':'VfxPrimitiveCameraQuad','e9736dc8':'VfxPrimitiveTrail','e9945dad':'VfxPrimitivePlanarProjection'}
SHAPES={'12ab94a6':'VfxShapeCylinder','4f4e2ed7':'VfxShapeLegacy','ba945ee1':'VfxShapeBox','ee39916f':'VfxShapePointDoNotUse'}
SYSTEM_CLASS='45cd899f'

def fnv(s):
 h=0x811c9dc5
 for c in s.lower().encode('utf-8'):
  h ^= c; h=(h*0x01000193)&0xffffffff
 return f'{h:08x}'
H={n:fnv(n) for n in ['ComplexEmitterDefinitionData','ParticleName','ParticlePath','EmitterName','Disabled','Importance','Rate','Lifetime','TimeBeforeFirstEmission','ParticleLifetime','ParticleLinger','IsSingleParticle','EmitterPosition','SpawnShape','RotationOverride','IsLocalOrientation','ParticleIsLocalOrientation','IsEmitterSpace','BirthVelocity','BirthAcceleration','BirthDrag','BirthRotation0','Rotation0','BirthRotationalVelocity0','BirthScale0','Scale0','BirthColor','Color','Texture','TexDiv','BirthUvoffset','BirthUvScrollRate','EmitterUvScrollRate','Primitive','BlendMode','Pass','DisableBackfaceCull','MiscRenderFlags','MeshRenderFlags','ConstantValue','Dynamics','Times','Values']}

def rd(fmt,b,o): return struct.unpack_from(fmt,b,o)[0],o+struct.calcsize(fmt)
def string(b,o): n,o=rd('<H',b,o); return b[o:o+n].decode('utf-8','replace'),o+n

def value(b,o,k):
 if k==0:return None,o
 if k in (1,135):v,o=rd('<B',b,o);return bool(v),o
 fm={2:'<b',3:'<B',4:'<h',5:'<H',6:'<i',7:'<I',8:'<q',9:'<Q',10:'<f'}
 if k in fm:return rd(fm[k],b,o)
 if k in (11,12,13,14):
  n={11:2,12:3,13:4,14:16}[k]; return list(struct.unpack_from('<'+'f'*n,b,o)),o+4*n
 if k==15:return [x/255.0 for x in b[o:o+4]],o+4
 if k==16:return string(b,o)
 if k==17:v,o=rd('<I',b,o);return {'hash':f'{v:08x}'},o
 if k==18:v,o=rd('<Q',b,o);return {'wad':f'{v:016x}'},o
 if k==132:v,o=rd('<I',b,o);return {'link':f'{v:08x}'},o
 if k in (130,131):
  ch,o=rd('<I',b,o)
  if ch==0:return {'class':'00000000','props':{}},o
  size,o=rd('<I',b,o);start=o;cnt,o=rd('<H',b,o);p={}
  for _ in range(cnt):nh,o=rd('<I',b,o);rk,o=rd('<B',b,o);vv,o=value(b,o,rk);p[f'{nh:08x}']={'kind':KINDS.get(rk,str(rk)),'value':vv}
  if o-start!=size:raise ValueError(f'struct size {o-start}!={size}')
  return {'class':f'{ch:08x}','props':p},o
 if k in (128,129):
  ik,o=rd('<B',b,o);size,o=rd('<I',b,o);start=o;cnt,o=rd('<I',b,o);items=[]
  for _ in range(cnt):v,o=value(b,o,ik);items.append(v)
  if o-start!=size:raise ValueError(f'container size {o-start}!={size}')
  return {'itemKind':KINDS.get(ik,str(ik)),'items':items},o
 if k==133:
  ik,o=rd('<B',b,o);some,o=rd('<B',b,o);v=None
  if some:v,o=value(b,o,ik)
  return {'itemKind':KINDS.get(ik,str(ik)),'value':v},o
 if k==134:
  kk,o=rd('<B',b,o);vk,o=rd('<B',b,o);size,o=rd('<I',b,o);start=o;cnt,o=rd('<I',b,o);items=[]
  for _ in range(cnt):a,o=value(b,o,kk);c,o=value(b,o,vk);items.append([a,c])
  if o-start!=size:raise ValueError('map size')
  return {'keyKind':KINDS.get(kk,str(kk)),'valueKind':KINDS.get(vk,str(vk)),'items':items},o
 raise ValueError(f'kind {k}')

def parse_bin(path):
 b=Path(path).read_bytes();o=0
 if b[:4]!=b'PROP':raise ValueError('not PROP')
 o=4;ver,o=rd('<I',b,o)
 if ver>=2:
  n,o=rd('<I',b,o)
  for _ in range(n):_,o=string(b,o)
 count,o=rd('<I',b,o);classes=[]
 for _ in range(count):x,o=rd('<I',b,o);classes.append(x)
 out=[]
 for ch in classes:
  size,o=rd('<I',b,o);start=o;ph,o=rd('<I',b,o);cnt,o=rd('<H',b,o);props={}
  for _ in range(cnt):nh,o=rd('<I',b,o);k,o=rd('<B',b,o);v,o=value(b,o,k);props[f'{nh:08x}']={'kind':KINDS.get(k,str(k)),'value':v}
  if o-start!=size:raise ValueError(f'object size {o-start}!={size}')
  out.append({'class':f'{ch:08x}','path':f'{ph:08x}','props':props})
 return out

def prop(s,name,default=None):
 try:return s['props'][H[name]]['value']
 except:return default

def unwrap_optional(v):
 return v.get('value') if isinstance(v,dict) and 'itemKind' in v and 'value' in v else v

def track(v):
 if not isinstance(v,dict):return None
 p=v.get('props',{})
 out={}
 cv=p.get(H['ConstantValue'])
 if cv is not None:
  c=cv['value']; out['constant']=c if isinstance(c,list) else [c]
 dyn=p.get(H['Dynamics'])
 if dyn and isinstance(dyn.get('value'),dict):
  dp=dyn['value'].get('props',{})
  t=dp.get(H['Times']); vals=dp.get(H['Values'])
  if t and vals:
   tv=t['value'].get('items',[]) if isinstance(t['value'],dict) else []
   vv=vals['value'].get('items',[]) if isinstance(vals['value'],dict) else []
   if tv and vv: out['times']=tv; out['values']=[x if isinstance(x,list) else [x] for x in vv]
 if not out:return None
 return out

def recursive_strings(v,ext=None):
 out=[]
 if isinstance(v,str):
  if ext is None or v.lower().endswith(ext):out.append(v)
 elif isinstance(v,dict):
  for x in v.values():out.extend(recursive_strings(x,ext))
 elif isinstance(v,list):
  for x in v:out.extend(recursive_strings(x,ext))
 return out

def raw_shape(v):
 if not isinstance(v,dict):return None
 cls=v.get('class','00000000'); fields={}
 for k,p in v.get('props',{}).items():
  x=p.get('value')
  if isinstance(x,(int,float,bool,list)):fields[k]=x
  elif isinstance(x,dict) and x.get('props'):
   cv=x['props'].get(H['ConstantValue']);
   if cv:fields[k]=cv['value']
 return {'type':SHAPES.get(cls,'0x'+cls),'typeHash':cls,'fields':fields}

def emitter(e):
 p=e['props']; prim=prop(e,'Primitive'); pcls=(prim or {}).get('class','96ddbc74') if isinstance(prim,dict) else '96ddbc74'
 tex=prop(e,'Texture'); mesh_src=None
 if isinstance(prim,dict):
  ss=recursive_strings(prim,'.scb'); mesh_src=ss[0] if ss else None
 def opt(name):return unwrap_optional(prop(e,name))
 return {
  'name':prop(e,'EmitterName','Emitter'), 'disabled':bool(prop(e,'Disabled',False)), 'importance':prop(e,'Importance'),
  'delay':float(prop(e,'TimeBeforeFirstEmission',0.0) or 0.0), 'rate':track(prop(e,'Rate')), 'particleLifetime':track(prop(e,'ParticleLifetime')),
  'systemLifetime':opt('Lifetime'), 'particleLinger':opt('ParticleLinger'), 'single':bool(prop(e,'IsSingleParticle',False)),
  'localOrientation':bool(prop(e,'IsLocalOrientation',True)), 'uniformScale':bool(prop(e,'IsUniformScale',False)),
  'blendMode':prop(e,'BlendMode'), 'pass':prop(e,'Pass'), 'textureSource':tex, 'texDiv':(prop(e,'TexDiv') if isinstance(prop(e,'TexDiv'),list) else None),
  'primitive':PRIMITIVES.get(pcls,'0x'+pcls), 'primitiveData':{'type':PRIMITIVES.get(pcls,'0x'+pcls),'typeHash':pcls},
  'meshSource':mesh_src, 'shape':raw_shape(prop(e,'SpawnShape')),
  'position':track(prop(e,'EmitterPosition')), 'orientation': {'constant':prop(e,'RotationOverride')} if isinstance(prop(e,'RotationOverride'),list) else None,
  'birthVelocity':track(prop(e,'BirthVelocity')), 'birthAcceleration':track(prop(e,'BirthAcceleration')), 'birthDrag':track(prop(e,'BirthDrag')),
  'birthRotation':track(prop(e,'BirthRotation0')), 'rotation':track(prop(e,'Rotation0')), 'birthRotationalVelocity':track(prop(e,'BirthRotationalVelocity0')),
  'birthScale':track(prop(e,'BirthScale0')), 'scale':track(prop(e,'Scale0')), 'birthColor':track(prop(e,'BirthColor')), 'color':track(prop(e,'Color')),
  'birthUvScrollRate':track(prop(e,'BirthUvScrollRate')), 'emitterUvScrollRate':track(prop(e,'EmitterUvScrollRate')), 'birthUVOffset':track(prop(e,'BirthUvoffset')),
  'disableBackfaceCull':bool(prop(e,'DisableBackfaceCull',False)), 'meshRenderFlags':prop(e,'MeshRenderFlags'), 'miscRenderFlags':prop(e,'MiscRenderFlags')
 }

def _padded_string(data, offset, size):
 raw=data[offset:offset+size]; return raw.split(b'\0',1)[0].decode('utf-8','replace'), offset+size

def parse_scb(path):
 """Parse Riot r3d2Mesh SCB using the current LeagueToolkit layout.
 Faces carry their own UV triplets, so output expands every face corner into a unique
 Unity vertex. This preserves authored seams without guessing shared-vertex UVs.
 """
 b=Path(path).read_bytes(); o=0
 if b[:8]!=b'r3d2Mesh': raise ValueError('not r3d2Mesh')
 o=8
 major,minor=struct.unpack_from('<HH',b,o); o+=4
 if major not in (2,3) and minor!=1: raise ValueError(f'unsupported SCB {major}.{minor}')
 name,o=_padded_string(b,o,128)
 vertex_count,face_count=struct.unpack_from('<ii',b,o);o+=8
 if vertex_count<0 or face_count<0: raise ValueError('negative SCB counts')
 flags=struct.unpack_from('<I',b,o)[0];o+=4
 # AABB: min.xyz + max.xyz
 o+=24
 has_vertex_colors=False
 if major>=3 and minor>=2:
  has_vertex_colors=struct.unpack_from('<I',b,o)[0]==1;o+=4
 vertices=[]
 for _ in range(vertex_count):
  vertices.append(list(struct.unpack_from('<fff',b,o)));o+=12
 if has_vertex_colors:o+=vertex_count*4 # BGRA u8; VFX runtime currently does not consume it.
 o+=12 # central point
 faces=[]
 for _ in range(face_count):
  indices=list(struct.unpack_from('<III',b,o));o+=12
  material,o=_padded_string(b,o,64)
  u0,u1,u2,v0,v1,v2=struct.unpack_from('<ffffff',b,o);o+=24
  faces.append((indices,[[u0,v0],[u1,v1],[u2,v2]],material))
 if flags & 1:o+=face_count*9 # optional per-face RGB colors (3 corners * RGB)
 # StaticMeshFlags bit 1 stores local-origin locator + pivot (two Vec3 = 24 bytes).
 # Several Mecha Darius VFX meshes set this flag; older local parsing stopped before them.
 if flags & 2:o+=24
 # Some current Riot 3.2 meshes use flag value 0x5 but still append the same
 # local-origin/pivot pair. Preserve exact geometry and accept that known 24-byte tail.
 if len(b)-o==24:o+=24
 if o!=len(b): raise ValueError(f'SCB trailing/layout mismatch parsed={o} bytes={len(b)}')
 positions=[];uv=[];indices=[]
 for face_indices,face_uvs,_material in faces:
  for corner in range(3):
   idx=face_indices[corner]
   if idx>=len(vertices): raise ValueError(f'SCB index {idx} >= vertex_count {len(vertices)}')
   positions.append(vertices[idx]);uv.append(face_uvs[corner]);indices.append(len(indices))
 return {'name':name,'version':[major,minor],'positions':positions,'uv':uv,'indices':indices,
         'faceCount':face_count,'sourceVertexCount':vertex_count}

def xxh64_lower(s):
 lib=ctypes.CDLL('libxxhash.so');fn=lib.XXH64;fn.argtypes=[ctypes.c_void_p,ctypes.c_size_t,ctypes.c_uint64];fn.restype=ctypes.c_uint64
 b=s.lower().encode('utf-8');return f'{fn(b,len(b),0):016x}'

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--raw-root',required=True);ap.add_argument('--existing',required=True);ap.add_argument('--out',required=True);a=ap.parse_args()
 raw_root=Path(a.raw_root);bin_root=raw_root/'bins';dep_root=raw_root/'dependencies';out=Path(a.out)
 (out/'meshes').mkdir(parents=True,exist_ok=True)
 existing=json.loads(Path(a.existing).read_text(encoding='utf-8-sig'))
 source_manifest=json.loads((raw_root/'PASS2_VFX_SOURCE_MANIFEST.json').read_text(encoding='utf-8-sig'))
 dep_by_path={str(x['sourcePath']).lower():x for x in source_manifest.get('dependencies',[])}
 systems=OrderedDict(existing.get('systems',{}))
 # Riot paths are case-insensitive. Collapse any pre-existing case-only duplicates before
 # adding Pass5 dependencies, preferring already converted PNG/JSON assets.
 _asset_groups={}
 for _k,_v in existing.get('assets',{}).items(): _asset_groups.setdefault(str(_k).lower(),[]).append((_k,_v))
 assets=OrderedDict()
 for _group in _asset_groups.values():
  _keep=next(((k,v) for k,v in _group if str(v.get('file','')).lower().endswith(('.png','.json'))),None)
  if _keep is None:_keep=next(((k,v) for k,v in _group if v.get('file')), _group[0])
  assets[_keep[0]]=_keep[1]
 errors=[]
 # Only the two Darius skins that were source-captured but not runtime-converted in Pass 2.
 bins=[('Dunkmaster','a3bb0e2220b98115.bin'),('Mecha','78766ae5d5eaec06.bin')]
 allowed_prefix={'Dunkmaster':'Darius_Skin04_','Mecha':'Darius_Skin67_'}
 def asset_key(path):
  low=str(path).lower()
  for k in assets:
   if str(k).lower()==low:return k
  return path
 def combat_name(name):
  if any(x in name for x in ('Emote','Recall','Homeguard','Taunt','Dance','Joke','Idle','Death_','Z_')):return False
  return any(x in name for x in ('_BA','_Q_','_W_','_E_','_R_','_P_','Passive','Hemo','hemo'))
 converted=[]
 for skin,bn in bins:
  path=bin_root/bn
  if not path.exists():errors.append(f'missing source bin {bn}');continue
  for o in parse_bin(path):
   if o['class']!=SYSTEM_CLASS:continue
   name=o['props'].get(H['ParticleName'],{}).get('value')
   if not name or not name.startswith(allowed_prefix[skin]) or not combat_name(name):continue
   cont=o['props'].get(H['ComplexEmitterDefinitionData'],{}).get('value',{}).get('items',[])
   es=[emitter(e) for e in cont]
   systems[name]={'sourceBin':bn,'skin':skin,'emitters':es}
   converted.append(name)
   for ee in es:
    refs=[]
    for src_path,kind in [(ee.get('textureSource'),'texture'),(ee.get('meshSource'),'mesh')]:
     if not src_path:continue
     dep=dep_by_path.get(str(src_path).lower())
     if dep is None:
      assets[asset_key(src_path)]={'kind':kind,'file':None};errors.append(f'missing dependency manifest entry {kind} {src_path}');continue
     dep_file=dep_root/dep['file']
     if not dep_file.exists():
      assets[asset_key(src_path)]={'kind':kind,'file':None,'wadHash':dep.get('hash')};errors.append(f'missing dependency file {kind} {src_path} file={dep_file.name}');continue
     if kind=='texture':
      # Do not duplicate the exact Riot TEX source. Runtime resolves this relative path from assets/lol_vfx.
      rel='../raw_lol_vfx_pass2/dependencies/'+dep_file.name
     else:
      h=dep.get('hash') or dep_file.stem
      rel='meshes/pass5_'+h+'.json';dst=out/rel
      try:
       if not dst.exists():dst.write_text(json.dumps(parse_scb(dep_file),separators=(',',':')),encoding='utf-8')
      except Exception as ex:
       errors.append(f'mesh parse failed {src_path} file={dep_file.name}: {ex}');rel=None
     assets[asset_key(src_path)]={'kind':kind,'file':rel,'wadHash':dep.get('hash')}
     if rel:refs.append({'source':src_path,'kind':kind,'file':rel})
    ee['texture']=assets.get(asset_key(ee.get('textureSource')),{}).get('file') if ee.get('textureSource') else None
    ee['mesh']=assets.get(asset_key(ee.get('meshSource')),{}).get('file') if ee.get('meshSource') else None
    ee['refs']=refs
 manifest=dict(existing)
 manifest['schema']='darius-riot-vfx-v2-final'
 manifest['systems']=systems;manifest['assets']=assets
 # Recompute Pass 5 conversion errors instead of carrying stale failures from an earlier run.
 # The Base/God-King source manifest was already validated with zero asset errors.
 base_errors=[e for e in existing.get('assetErrors',[]) if not (str(e).startswith('mesh parse failed ') or str(e).startswith('missing dependency manifest entry ') or str(e).startswith('missing dependency file '))]
 manifest['assetErrors']=sorted(set(base_errors+errors))
 manifest['skinSystemPrefixes']={'Classic':'Darius_Base_','GodKing':'Darius_Skin15_','Dunkmaster':'Darius_Skin04_','Mecha':'Darius_Skin67_'}
 manifest['pass5ConvertedSystems']=sorted(converted)
 (out/'darius_lol_vfx.json').write_text(json.dumps(manifest,separators=(',',':')),encoding='utf-8')
 report=(f"Total systems: {len(systems)}\nPass5 Dunkmaster/Mecha systems added: {len(converted)}\n"
         f"Pass5 mesh JSON present: {len(list((out/'meshes').glob('pass5_*.json')))}\nAsset errors: {len(manifest['assetErrors'])}\n")
 (out/'PASS5_CONVERSION_REPORT.txt').write_text(report,encoding='utf-8')
 print(report,end='')
 if manifest['assetErrors']:
  print('\n'.join(manifest['assetErrors'][:30]))
  raise SystemExit(2)

if __name__=='__main__':main()
