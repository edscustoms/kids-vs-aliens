"""Offline source upgrade for BikeRoute's mountain settlement. Originals are read-only.
Run: blender -b --python Tools/Art/BikeRouteSettlement/build_settlement.py
Exports local FBX variants; never writes Unity scenes or original source assets.
"""
import bpy, bmesh, math, json, os
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Assets/Game/Art/Environment/Buildings/MountainSettlement'
SOURCE=ROOT/'SourceArt/BikeRoute/Settlement'
LOG=ROOT/'Logs/BikeRouteSettlementPass7'
for p in (OUT,SOURCE,LOG):p.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'Originals/BikeRoute_DesertSettlement.blend'))
originals={o.name:o.copy() for o in bpy.context.scene.objects if o.type=='MESH'}
for o in originals.values():o.data=o.data.copy()
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
materials={}
# Shared material vocabulary; Unity uses the same existing texture sets.
def material(name,color,texture=None,metal=0,rough=.7):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
 if texture:
  img=bpy.data.images.load(str(ROOT/texture),check_existing=True);t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=img
  mix=m.node_tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=(*color,1)
  m.node_tree.links.new(t.outputs['Color'],mix.inputs[1]);m.node_tree.links.new(mix.outputs[0],p.inputs['Base Color'])
 materials[name]=m;return m
material('WeatheredTimber',(.82,.78,.68),'Assets/Game/Art/Environment/Textures/Wood/wood_planks/wood_planks_diff_1k.jpg')
material('GalvanizedMetal',(.82,.86,.83),'Assets/Game/Art/Environment/Textures/Wall/corrugated_iron/corrugated_iron_diff_1k.jpg',.15,.65)
material('Concrete',(.60,.59,.54),'Assets/Game/Art/Environment/Textures/Ground/rough_concrete/rough_concrete_diff_1k.jpg')
material('PaintedTrim',(.15,.205,.185),None,.15,.65)
material('DarkRecess',(.065,.09,.09),None,.1,.42)
material('SignIvory',(.66,.65,.54),None,0,.7)
parts=[];detail=[];collection=None

def add(o,mat='WeatheredTimber',small=False):
 for c in list(o.users_collection):c.objects.unlink(o)
 collection.objects.link(o);o.data.materials.clear();o.data.materials.append(materials[mat]);parts.append(o)
 if small:detail.append(o)
 return o

def select_only(o):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o

def box(name,pos,size,mat='WeatheredTimber',small=False):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name;o.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return add(o,mat,small)

def mesh(name,verts,faces,mat='WeatheredTimber',small=False):
 m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces);m.update();o=bpy.data.objects.new(name,m);bpy.context.scene.collection.objects.link(o)
 bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(m);bm.free();return add(o,mat,small)

def cylinder(name,pos,radius,depth,mat='PaintedTrim',rotation=(0,0,0),small=False,vertices=12):
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=pos,rotation=rotation);o=bpy.context.object;o.name=name
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return add(o,mat,small)

def recover_core(original,w,d,h):
 # The original joined buildings consist of disconnected modeled pieces. Keep their
 # largest structural cuboid and foundation instead of replacing usable geometry.
 src=originals[original];m=src.data;parent=list(range(len(m.vertices)))
 def find(i):
  while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
  return i
 for edge in m.edges:
  a,b=map(find,edge.vertices);parent[a]=b
 groups={}
 for v in m.vertices:groups.setdefault(find(v.index),[]).append(v.index)
 candidates=[]
 for ids in groups.values():
  vs=[m.vertices[i].co for i in ids];lo=Vector([min(v[j] for v in vs) for j in range(3)]);hi=Vector([max(v[j] for v in vs) for j in range(3)])
  candidates.append((math.prod(hi-lo),ids,lo,hi))
 candidates.sort(reverse=True,key=lambda x:x[0]);core=None
 for name,entry,sz,z,mat in [('Retained structural core',candidates[0],(w,d,h),.5,'WeatheredTimber'),('Retained foundation',next(c for c in candidates if c[3].z-c[2].z<.5 and c[3].x-c[2].x>w*.8),(w+.15,d+.15,.5),0,'Concrete')]:
  _,ids,lo,hi=entry;mapping={old:i for i,old in enumerate(ids)};verts=[]
  for i in ids:
   v=m.vertices[i].co;verts.append(((v.x-lo.x)/(hi.x-lo.x)*sz[0]-sz[0]/2,(v.y-lo.y)/(hi.y-lo.y)*sz[1]-sz[1]/2,(v.z-lo.z)/(hi.z-lo.z)*sz[2]+z))
  faces=[[mapping[i] for i in poly.vertices] for poly in m.polygons if all(i in mapping for i in poly.vertices)]
  obj=mesh(name,verts,faces,mat)
  if core is None:core=obj
 return core

def recess(body,x,y,z,w,h,depth=.28):
 cutter=box('Temporary recess cutter',(x,y+depth*.5-.06,z),(w,depth+.12,h));parts.remove(cutter)
 mod=body.modifiers.new('Recessed opening','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
 bpy.context.view_layer.objects.active=body;bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)

def window(body,x,y,z,w=1.2,h=1.25):
 recess(body,x,y,z,w,h)
 box('Inset glass',(x,y+.22,z),(w-.09,.045,h-.09),'DarkRecess')
 for side in [-1,1]:box('Window jamb',(x+side*(w/2+.045),y-.035,z),(.09,.17,h+.2),'PaintedTrim')
 for side in [-1,1]:box('Window lintel and sill',(x,y-.065,z+side*(h/2+.045)),(w+.18,.23,.09),'PaintedTrim')
 box('Window mullion',(x,y+.11,z),(.055,.055,h),'SignIvory',True)
 box('Window transom',(x,y+.11,z+.12),(w,.055,.05),'SignIvory',True)
 box('Drip flashing',(x,y-.1,z+h/2+.12),(w+.3,.3,.035),'GalvanizedMetal',True)

def door(body,x,y):
 recess(body,x,y,1.58,1.05,2.16)
 box('Recessed timber door',(x,y+.19,1.58),(.98,.07,2.09),'PaintedTrim')
 for side in [-1,1]:box('Door jamb',(x+side*.575,y-.04,1.57),(.1,.17,2.23),'WeatheredTimber')
 box('Door lintel',(x,y-.04,2.73),(1.25,.2,.12),'WeatheredTimber')
 box('Door upper glass',(x,y+.135,2.04),(.62,.018,.65),'DarkRecess')
 cylinder('Door pull',(x+.35,y+.075,1.45),.035,.18,'GalvanizedMetal',(math.pi/2,0,0),True,8)
 for step in range(3):box('Entry step',(x,y-.25-step*.28,.42-step*.14),(1.5,.6,.18),'Concrete')

def bay(body,x,y,w=3.5,h=3.65,open_bay=False):
 z=.5+h/2;depth=2.7 if open_bay else .35;recess(body,x,y,z,w,h,depth)
 for side in [-1,1]:box('Structural bay jamb',(x+side*(w/2+.09),y-.06,z),(.18,.26,h+.14),'PaintedTrim')
 box('Bay lintel',(x,y-.08,.5+h+.12),(w+.4,.3,.24),'PaintedTrim')
 if open_bay:
  box('Bay shadow back',(x,y+depth-.06,z),(w-.06,.06,h-.06),'DarkRecess')
  box('Bay concrete floor',(x,y+depth/2,.51),(w,depth,.08),'Concrete')
  closed=.78
  box('Raised roller door',(x,y+.14,.5+h-closed/2),(w-.08,.1,closed),'GalvanizedMetal')
 else:
  closed=h-.06;box('Inset roller door',(x,y+.23,z),(w-.12,.1,h-.07),'GalvanizedMetal')
 for dz in range(int(closed/.26)):
  zz=.5+h-.17-dz*.26;box('Door slat shadow',(x,y+.16 if not open_bay else y+.08,zz),(w-.15,.025,.022),'PaintedTrim',True)
 box('Concrete bay apron',(x,y-1,.07),(w+1,2.2,.14),'Concrete')

def roof(w,d,eave,rise):
 half=w/2+.45;pitch=math.atan2(rise,w/2);peak=eave+rise
 for side in [-1,1]:
  o=box('Thick roof underside',(side*half/2,0,peak-half/2*math.tan(pitch)-.09),(half/math.cos(pitch),d+.9,.10),'PaintedTrim');o.rotation_euler.y=side*pitch
  count=math.ceil((d+.9)/.28)*4;vs=[]
  for j in range(count+1):
   y=-(d+.9)/2+(d+.9)*j/count;wave=.023*(1-math.cos(j*math.pi/2))
   vs.extend([(0,y,peak+.065+wave),(side*half,y,peak-half*math.tan(pitch)+.065+wave)])
  mesh('Corrugated roof sheet',vs,[(2*j,2*j+1,2*j+3,2*j+2) if side>0 else (2*j+2,2*j+3,2*j+1,2*j) for j in range(count)],'GalvanizedMetal',True)
  for y in [-d/2-.46,d/2+.46]:
   o=box('Rake fascia',(side*half/2,y,peak-half/2*math.tan(pitch)-.06),(half/math.cos(pitch),.13,.19),'PaintedTrim');o.rotation_euler.y=side*pitch
  box('Eave fascia',(side*half,0,peak-half*math.tan(pitch)-.07),(.11,d+.95,.22),'PaintedTrim')
  cylinder('Gutter',(side*(half+.055),0,peak-half*math.tan(pitch)-.08),.065,d+.9,'GalvanizedMetal',(math.pi/2,0,0),True,8)
  cylinder('Downpipe',(side*(w/2+.18),-d/2+.12,(eave-.15)/2),.045,eave-.15,'GalvanizedMetal',small=True,vertices=8)
 # Flat secondary roof sheets remain at LOD1 when fine corrugation drops out.
 for side in [-1,1]:
  o=box('Roof backing',(side*half/2,0,peak-half/2*math.tan(pitch)+.015),(half/math.cos(pitch),d+.9,.04),'GalvanizedMetal');o.rotation_euler.y=side*pitch
 box('Ridge cap',(0,0,peak+.1),(.22,d+1.0,.1),'GalvanizedMetal')
 mesh('Solid gable',[(-w/2,-d/2,eave),(w/2,-d/2,eave),(0,-d/2,peak),(-w/2,d/2,eave),(w/2,d/2,eave),(0,d/2,peak)],[(0,1,2),(5,4,3),(0,3,4,1),(0,2,5,3),(1,4,5,2)],'WeatheredTimber')

def porch(w,y,z=3.05,depth=2.2):
 box('Porch slab',(0,y-depth/2,.12),(w,depth,.24),'Concrete')
 canopy=box('Porch roof',(0,y-depth/2,z),(w+.25,depth+.25,.13),'GalvanizedMetal');canopy.rotation_euler.x=.09
 box('Porch beam',(0,y-depth+.08,z-.2),(w,.18,.27),'WeatheredTimber')
 for x in [-w/2+.18,w/2-.18]:
  box('Porch support',(x,y-depth+.08,(z-.2)/2),(.18,.18,z-.2),'WeatheredTimber')
  for sign in [-1,1]:
   brace=box('Knee brace',(x+sign*.22,y-depth+.08,z-.55),(.1,.13,.65),'WeatheredTimber');brace.rotation_euler.y=sign*.65

def sign(text,x,y,z,width):
 box('Timber sign board',(x,y,z),(width,.13,.72),'PaintedTrim')
 bpy.ops.object.text_add(location=(x,y-.075,z-.23),rotation=(math.pi/2,0,0));o=bpy.context.object;o.data.body=text;o.data.align_x='CENTER';o.data.size=.48;o.data.extrude=.008;o.data.resolution_u=2
 bpy.ops.object.convert(target='MESH');o=bpy.context.object;o.name='Original sign '+text;add(o,'SignIvory')

def uv_all():
 for o in parts:
  if o.type!='MESH':continue
  # Consistent metric projection: boards remain horizontal on every elevation.
  for old_uv in list(o.data.uv_layers):o.data.uv_layers.remove(old_uv)
  uv=o.data.uv_layers.new(name='MetricUV');uv.active_render=True
  for poly in o.data.polygons:
   n=poly.normal
   for li in poly.loop_indices:
    v=o.matrix_world@o.data.vertices[o.data.loops[li].vertex_index].co
    uv.data[li].uv=(v.x/3,v.z/2) if abs(n.y)>.5 else ((v.y/3,v.z/2) if abs(n.x)>.5 else (v.x/3,v.y/2))

def join_export(name):
 uv_all();exports=[];counts=[]
 for level in [0,1,2]:
  clones=[]
  for p in parts:
   if level==0 and p.name.startswith("Roof backing"):continue
   if level==1 and p in detail:continue
   if level==2 and not any(p.name.startswith(k) for k in ['Retained structural core','Retained foundation','Thick roof underside','Porch slab','Porch support','Entry step','Bay shadow back','Bay concrete floor','Raised roller door','Inset roller door','Concrete bay apron','Shed structure','Shed slab','Container body','Steel drum','Machine housing','Wooden crate']):continue
   o=p.copy();o.data=p.data.copy();bpy.context.scene.collection.objects.link(o);clones.append(o)
  bpy.ops.object.select_all(action='DESELECT')
  for o in clones:o.select_set(True)
  bpy.context.view_layer.objects.active=clones[0];bpy.ops.object.join();o=bpy.context.object;o.name=name+('_COLLISION' if level==2 else '_LOD'+str(level))
  bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
  # Explicit triangulation makes Blender and Unity agree on recessed facades.
  mod=o.modifiers.new('Export triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
  o.data.name=o.name;o.data.calc_loop_triangles();counts.append(len(o.data.loop_triangles));exports.append(o)
 bpy.ops.object.select_all(action='DESELECT')
 for o in exports:o.select_set(True)
 bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,axis_forward='-Z',axis_up='Y',bake_anim=False,object_types={'MESH'},use_mesh_modifiers=True,add_leaf_bones=False)
 for o in exports:bpy.data.objects.remove(o,do_unlink=True)
 return counts

specs=[('DuneStepHouse','RidgeHouse',6.6,8,5.5,2.0),('OasisShop','TrailSupplyShop',8.5,7,3.5,1.7),('RedClayTownhouse','BoardingHouse',6,8.5,5.6,2.1),('ShadePorchHouse','PorchCabin',8,7,3.1,1.85),('BlueShutterHouse','CaretakerCottage',7.5,8,3.5,1.9),('RouteServiceHall','MaintenanceHall',13,10,5.0,1.6),('CornerMarket','GeneralStore',10,8,3.8,2.0),('ArchedWorkshop','MountainGarage',11,9,4.5,1.8)]
records=[];build_collections=[]
for i,(old,name,w,d,h,rise) in enumerate(specs):
 collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(collection);parts=[];detail=[]
 body=recover_core(old,w,d,h);y=-d/2;eave=h+.5
 industrial=name in ['MaintenanceHall','MountainGarage'];body.data.materials[0]=materials['GalvanizedMetal' if industrial else 'WeatheredTimber']
 if industrial:
  bay(body,-w*.24,y,3.8 if w>12 else 3.5,h-.65,name=='MountainGarage');bay(body,w*.16,y,3.8 if w>12 else 3.5,h-.65,False)
  door(body,w*.415,y)
  for side in [-1,1]:
   for yy in range(int(d/.36)):
    box('Metal siding raised seam',(side*(w/2+.025),-d/2+.18+yy*.36,.5+h/2),(.035,.045,h-.06),'GalvanizedMetal',True)
  sign('RIDGE WORKS' if name=='MountainGarage' else 'MAINTENANCE',0,y-.24,eave-.13,w*.83)
 else:
  door(body,-w*.27,y)
  window(body,w*.20,y,1.85,2.0 if name in ['GeneralStore','TrailSupplyShop'] else 1.4,1.4)
  if h>5:
   for x in [-w*.26,w*.26]:window(body,x,y,4.4,1.15,1.25)
  if name in ['PorchCabin','CaretakerCottage','TrailSupplyShop','GeneralStore']:porch(w*.88,y-.1,3.03,2.0 if 'Shop' not in name else 2.6)
  if name in ['TrailSupplyShop','GeneralStore']:sign('TRAIL SUPPLIES' if name=='TrailSupplyShop' else 'PASS STORES',0,y-.22,eave-.24,w*.8)
 # Side windows are actual recesses, not decals. Rotate a temporary front-facing module into side elevations.
 for side in [-1,1]:
  for yy in [-d*.22,d*.22]:
   # Turn the body temporarily so the same clear opening construction applies to a side.
   select_only(body);body.rotation_euler.z=side*math.pi/2;bpy.ops.object.transform_apply(location=False,rotation=True,scale=False)
   start=len(parts);window(body,yy,-w/2,2.0,1.15,1.15)
   select_only(body);body.rotation_euler.z=-side*math.pi/2;bpy.ops.object.transform_apply(location=False,rotation=True,scale=False)
   from mathutils import Matrix
   rot=Matrix.Rotation(-side*math.pi/2,4,'Z')
   for o in parts[start:]:o.matrix_world=rot@o.matrix_world
 for x in [-w/2,w/2]:
  for yy in [-d/2,d/2]:box('Corner structural trim',(x,yy,.5+h/2),(.15,.15,h+.06),'PaintedTrim')
 roof(w,d,eave,rise)
 if not industrial:
  chimney=box('Chimney',(w*.24,d*.18,eave+rise*.7),( .55,.6,1.7),'Concrete');box('Chimney cap',(w*.24,d*.18,eave+rise*.7+.9),(.75,.8,.14),'GalvanizedMetal')
 else:
  for x in [-w*.3,w*.3]:box('Roof ridge vent',(x,0,eave+rise*.48),(1,.9,.32),'PaintedTrim')
 counts=join_export(name);records.append(dict(original=old,name=name,width=w,depth=d,height=eave+rise,triangles=dict(lod0=counts[0],lod1=counts[1],collision=counts[2]),materials=sorted(set(p.data.materials[0].name for p in parts))))
 offset=Vector(((i%4)*24,(i//4)*25,0))
 for o in parts:o.location+=offset
 build_collections.append(collection)
 print('UPGRADED',old,'->',name,counts)

# Missing support assets. These are original, plain rural service-yard designs.
for index,name in enumerate(['UtilityShed','StorageContainer','YardBarrel','ServiceGenerator','TimberCrate']):
 collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(collection);parts=[];detail=[]
 if name=='UtilityShed':
  w,d,h=4.8,4,2.7;body=box('Shed structure',(0,0,h/2+.25),(w,d,h),'GalvanizedMetal');box('Shed slab',(0,0,.12),(w+.25,d+.25,.24),'Concrete')
  bay(body,0,-d/2,2.8,2.3,True);roof(w,d,h+.25,.7)
  for side in [-1,1]:
   for j in range(13):box('Shed rib',(side*2.42,-1.9+j*.3,1.6),(.045,.04,2.45),'GalvanizedMetal',True)
 elif name=='StorageContainer':
  box('Container body',(0,0,1.30),(6.1,2.44,2.59),'PaintedTrim')
  for side in [-1,1]:
   for j in range(24):box('Corrugation',( -2.93+j*.255,side*1.235,1.3),(.075,.035,2.38),'GalvanizedMetal',True)
   box('Container bottom rail',(0,side*1.23,.08),(6.2,.1,.16),'GalvanizedMetal')
   box('Container top rail',(0,side*1.23,2.55),(6.2,.1,.12),'GalvanizedMetal')
  for y in [-.6,.6]:
   box('Container door',(-3.07,y,1.3),(.06,1.15,2.42),'GalvanizedMetal');cylinder('Door locking bar',(-3.12,y,1.3),.03,2.25,'PaintedTrim',small=True,vertices=8)
  for x in [-3.05,3.05]:
   for y in [-1.22,1.22]:box('Corner post',(x,y,1.3),(.16,.16,2.6),'GalvanizedMetal')
 elif name=='YardBarrel':
  cylinder('Steel drum',(0,0,.45),.3,.9,'PaintedTrim',vertices=16)
  for z in [.035,.26,.64,.875]:cylinder('Rolled hoop',(0,0,z),.314,.032,'GalvanizedMetal',small=True,vertices=16)
  cylinder('Drum bung',(.14,0,.91),.035,.025,'GalvanizedMetal',small=True,vertices=8)
 elif name=='ServiceGenerator':
  box('Machine housing',(0,0,.63),(1.35,.78,.85),'PaintedTrim');box('Skid',(0,0,.13),(1.6,.95,.16),'GalvanizedMetal')
  for x in [-.73,.73]:
   for y in [-.4,.4]:box('Protective frame',(x,y,.61),(.065,.065,1.1),'GalvanizedMetal')
  for x in [-.73,.73]:box('Frame handle',(x,0,1.15),(.065,.9,.065),'GalvanizedMetal')
  box('Vent recess',(0,-.399,.7),(.8,.025,.48),'DarkRecess')
  for z in [.52,.63,.74,.85]:box('Vent louver',(0,-.424,z),(.81,.06,.033),'GalvanizedMetal',True)
  cylinder('Exhaust',(.45,.22,1.12),.045,.32,'GalvanizedMetal',small=True,vertices=8)
 elif name=='TimberCrate':
  box('Wooden crate',(0,0,.5),(1.2,1,1),'WeatheredTimber')
  for x in [-.53,.53]:
   for y in [-.525,.525]:box('Crate corner',(x,y,.5),(.12,.07,1.02),'WeatheredTimber')
  for y in [-.55,.55]:
   brace=box('Crate diagonal',(0,y,.5),(1.28,.05,.11),'PaintedTrim');brace.rotation_euler.y=-.62
 counts=join_export(name);records.append(dict(name=name,original=None,triangles=dict(lod0=counts[0],lod1=counts[1],collision=counts[2])))
 for o in parts:o.location+=Vector((index*14,52,0))
 print('NEW',name,counts)
# Readable source layout. Export meshes were generated at ground-center origins in metres.
bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
for img in bpy.data.images:
 if img.filepath:img.filepath=bpy.path.relpath(img.filepath,start=str(SOURCE))
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'MountainSettlement.blend'),compress=True,relative_remap=False)
(OUT/'AssetManifest.json').write_text(json.dumps(records,indent=2));(LOG/'asset-manifest.json').write_text(json.dumps(records,indent=2))
print('SETTLEMENT_EXPORT_COMPLETE')
