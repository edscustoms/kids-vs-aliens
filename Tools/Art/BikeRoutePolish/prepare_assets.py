"""Local BikeRoute presentation refinements. Originals and shared assets stay read-only.
Run with Blender --background --python Tools/Art/BikeRoutePolish/prepare_assets.py.
"""
import bpy, bmesh, math, json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Assets/Game/Art/Environment/Buildings/BikeRoutePolish'
SOURCE = ROOT / 'SourceArt/BikeRoute/Polish'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'SourceArt/BikeRoute/Settlement/Originals/BikeRoute_DesertSettlement.blend'))
originals = {n: bpy.data.objects[n].data.copy() for n in ['ArchedWorkshop', 'ShadePorchHouse']}
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
with bpy.data.libraries.load(str(ROOT/'SourceArt/BikeRoute/Settlement/MountainSettlement.blend'), link=False) as (available, requested):
    requested.materials = ['WeatheredTimber', 'GalvanizedMetal', 'Concrete', 'PaintedTrim', 'DarkRecess', 'SignIvory']
M = {m.name: m for m in requested.materials}
for m in M.values():
    for node in m.node_tree.nodes:
        if node.type == 'TEX_IMAGE' and node.image:
            paths = {'wood_planks_diff_1k.jpg':'Wood/wood_planks', 'corrugated_iron_diff_1k.jpg':'Wall/corrugated_iron', 'rough_concrete_diff_1k.jpg':'Ground/rough_concrete'}
            name=Path(node.image.filepath).name
            if name in paths: node.image.filepath=str(ROOT/'Assets/Game/Art/Environment/Textures'/paths[name]/name)
parts=[]
fine=[]
collection=None

def adopt(o, material, detail=False):
    for c in list(o.users_collection): c.objects.unlink(o)
    collection.objects.link(o)
    o.data.materials.clear(); o.data.materials.append(M[material])
    parts.append(o)
    if detail: fine.append(o)
    return o

def mesh(name, vertices, faces, material, detail=False):
    data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces);data.update()
    bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(data);bm.free()
    obj=bpy.data.objects.new(name,data);bpy.context.scene.collection.objects.link(obj)
    return adopt(obj,material,detail)

def box(name, center, size, material, detail=False):
    bpy.ops.mesh.primitive_cube_add(size=1,location=center)
    obj=bpy.context.object;obj.name=name;obj.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return adopt(obj,material,detail)

def beam(name, a, b, thickness, material='PaintedTrim', detail=False):
    a,b=Vector(a),Vector(b);obj=box(name,(a+b)/2,(thickness,thickness,(b-a).length),material,detail)
    obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    return obj

def cut(body, center, size):
    cutter=box('Opening cutter',center,size,'DarkRecess');parts.remove(cutter)
    modifier=body.modifiers.new('Opening depth','BOOLEAN');modifier.operation='DIFFERENCE';modifier.solver='EXACT';modifier.object=cutter
    bpy.context.view_layer.objects.active=body;bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.data.objects.remove(cutter,do_unlink=True)

def components(data):
    parent=list(range(len(data.vertices)))
    def find(i):
        while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
        return i
    for e in data.edges:parent[find(e.vertices[0])]=find(e.vertices[1])
    groups={}
    for v in data.vertices:groups.setdefault(find(v.index),[]).append(v.index)
    result=[]
    for ids in groups.values():
        indices={old:i for i,old in enumerate(ids)}
        polygons=[p for p in data.polygons if p.vertices[0] in indices]
        result.append((ids,[[indices[i] for i in p.vertices] for p in polygons],data.materials[polygons[0].material_index].name))
    return result

def metric_uv():
    for obj in parts:
        for old in list(obj.data.uv_layers):obj.data.uv_layers.remove(old)
        uv=obj.data.uv_layers.new(name='MetricUV')
        for poly in obj.data.polygons:
            n=obj.matrix_world.to_3x3()@poly.normal
            for i in poly.loop_indices:
                v=obj.matrix_world@obj.data.vertices[obj.data.loops[i].vertex_index].co
                uv.data[i].uv=(v.x/3,v.z/2) if abs(n.y)>.5 else ((v.y/3,v.z/2) if abs(n.x)>.5 else (v.x/3,v.y/2))

def export(name):
    metric_uv();exports=[];counts=[]
    for level in [0,1]:
        bpy.ops.object.select_all(action='DESELECT');clones=[]
        for obj in parts:
            if level==1 and obj in fine:continue
            copy=obj.copy();copy.data=obj.data.copy();bpy.context.scene.collection.objects.link(copy);clones.append(copy);copy.select_set(True)
        bpy.context.view_layer.objects.active=clones[0];bpy.ops.object.join();obj=bpy.context.object
        obj.name=name+'_LOD'+str(level);bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
        bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
        modifier=obj.modifiers.new('Triangulated export','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.data.name=obj.name;counts.append(len(obj.data.polygons));exports.append(obj)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in exports:obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,axis_forward='-Z',axis_up='Y',bake_anim=False,object_types={'MESH'},add_leaf_bones=False)
    for obj in exports:bpy.data.objects.remove(obj,do_unlink=True)
    return {'name':name,'lod0_triangles':counts[0],'lod1_triangles':counts[1]}

records=[]
for number,name in enumerate(['ArchedWorkshop','ShadePorchHouse']):
    collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(collection);parts=[];fine=[]
    data=originals[name];w,d,h=(11,9,5.7) if number==0 else (8,7,6.2)
    pieces=components(data);body=None;windows=[];retained=[]
    for index,(ids,faces,oldmat) in enumerate(pieces):
        # The old workshop's flat ground-floor window occupied the roller-door bay.
        if number==0 and 14<=index<=18:continue
        verts=[data.vertices[i].co.copy() for i in ids]
        if number==0 and 19<=index<=44:
            for v in verts:v.z=4.45+(v.z-4.75)*.8
        lo=Vector([min(v[j] for v in verts) for j in range(3)]);hi=Vector([max(v[j] for v in verts) for j in range(3)])
        size=hi-lo;center=(lo+hi)/2
        mat={'Stucco':'GalvanizedMetal' if number==0 else 'WeatheredTimber','Trim':'PaintedTrim','Roof':'GalvanizedMetal','Glass':'DarkRecess','Teal':'PaintedTrim','Red':'PaintedTrim'}[oldmat]
        if index==1 or hi.z<.7:mat='Concrete'
        if number==1 and index==4:mat='GalvanizedMetal'
        # Solid backing slabs made the old window frames look painted on.
        is_frame=oldmat=='Trim' and size.z>1 and min(size.x,size.y)<.3 and max(size.x,size.y)>1
        if is_frame:
            axis=0 if size.x<size.y else 1;wide=1-axis
            for side in [-1,1]:
                c=center.copy();s=size.copy();s[wide]=.11;c[wide]+=side*(size[wide]/2-.055);box('Retained opening jamb',c,s,'PaintedTrim')
                c=center.copy();s=size.copy();s.z=.11;c.z+=side*(size.z/2-.055);box('Retained opening lintel',c,s,'PaintedTrim')
            continue
        obj=mesh('Retained '+oldmat+' '+str(index),verts,faces,mat)
        if index==0:body=obj
        if oldmat=='Glass':windows.append((obj,center,size))
        retained.append(obj)
    for obj,center,size in windows:
        axis=0 if size.x<size.y else 1;sign=1 if center[axis]>0 else -1
        depth_center=center.copy();depth_center[axis]=sign*((w if axis==0 else d)/2-.12)
        cut_size=size.copy();cut_size[axis]=.65
        cut(body,depth_center,cut_size)
        obj.location[axis]=depth_center[axis]-sign*.22-center[axis]
    # Small geometric seams and roof edges carry detail at riding distance.
    if number==0:
        for x in [-w/2,w/2]:
            for j in range(26):box('Metal siding seam',(x, -d/2+.16+j*.34,h/2),(.045,.042,h-.15),'GalvanizedMetal',True)
        for j in range(31):box('Roof standing seam',(-5.4+j*.36,0,5.955),(.028,9.2,.045),'GalvanizedMetal',True)
        # Keep the closed workshop bay, but seat it behind a substantial opening.
        cut(body,(1.65,-4.43,1.5),(4.84,.6,2.7))
        for obj in retained:
            if obj.name=='Retained Teal 67' or any(obj.name=='Retained Roof '+str(i) for i in range(68,76)):obj.location.y+=.34
        for x in [-.83,4.13]:box('Bay structural jamb',(x,-4.62,1.5),(.17,.25,2.9),'GalvanizedMetal')
        box('Bay lintel',(1.65,-4.62,2.96),(5.1,.27,.22),'GalvanizedMetal')
    else:
        peak=8.4;half=4.25;pitch=math.atan2(2.2,half)
        for side in [-1,1]:
            for j in range(26):
                y=-3.75+j*.3;beam('Roof standing seam',(0,y,peak+.018),(side*half,y,6.218),.026,'GalvanizedMetal',True)
            for y in [-3.83,3.83]:beam('Rake fascia',(0,y,peak),(side*half,y,6.2),.14)
            box('Eave fascia',(side*half,0,6.17),(.13,7.75,.20),'PaintedTrim')
            beam('Porch knee brace',(side*4,-5.65,2.6),(side*3.45,-5.65,3.35),.13,'WeatheredTimber')
        box('Ridge flashing',(0,0,8.405),(.17,7.75,.045),'GalvanizedMetal')
    for x in [-w/2,w/2]:
        for y in [-d/2,d/2]:box('Corner framing',(x,y,h/2),(.13,.13,h),'PaintedTrim')
    records.append(export(name+'Refined'))
    for obj in parts:obj.location.x+=number*22

# The original concrete defense footprint/roof envelope is retained. This is art,
# not a replacement for either rifle rig or any authored collider.
collection=bpy.data.collections.new('DefenseTower');bpy.context.scene.collection.children.link(collection);parts=[];fine=[]
body=box('Concrete structural core',(0,0,3.5),(3.2,4,7),'Concrete')
for x in [-1.52,1.52]:
    for y in [-1.92,1.92]:box('Corner pilaster',(x,y,3.5),(.22,.22,7),'Concrete')
for z in [.25,2.5,5.0,6.87]:box('Structural ring',(0,0,z),(3.24,4.04,.18),'PaintedTrim')
for side in [-1,1]:
    # Recessed metal service panels between concrete uprights.
    box('Service siding',(side*1.607,0,4.67),(.045,3.55,4.0),'GalvanizedMetal')
    for j in range(14):box('Panel standing seam',(side*1.645,-1.62+j*.25,4.67),(.035,.032,3.9),'GalvanizedMetal',True)
    for a,b in [((side*1.68,-1.78,2.65),(side*1.68,1.78,4.9)),((side*1.68,1.78,5.1),(side*1.68,-1.78,6.78))]:beam('Visible structural brace',a,b,.12)
    box('Front upper panel',(0,side*2.01,4.7),(2.8,.04,3.8),'GalvanizedMetal')
    for j in range(11):box('Front panel seam',(-1.25+j*.25,side*2.045,4.7),(.034,.03,3.75),'GalvanizedMetal',True)
    box('Shadowed equipment vent',(0,side*2.05,5.7),(1.5,.08,.8),'DarkRecess')
    for z in [5.4,5.57,5.74,5.91,6.08]:box('Vent louver',(0,side*2.11,z),(1.56,.12,.05),'PaintedTrim',True)
box('Roof deck',(0,0,7),(4,4.8,.35),'Concrete')
box('Roof edge flashing',(0,0,7.17),(4.01,4.81,.05),'GalvanizedMetal')
# Authored tower roots sit 1.54 m below the surrounding grade. Retain the
# structural footprint and seat the visible service opening above that grade.
for obj in list(parts):
    if obj==body or obj.name.startswith(('Front upper panel','Front panel seam','Structural ring')):
        cut(obj,(0,-1.96,2.78),(1.05,.7,2.1))
box('Inset service door',(0,-1.825,2.78),(.97,.065,2.03),'PaintedTrim')
for x in [-.57,.57]:box('Door steel jamb',(x,-2.025,2.79),(.10,.14,2.22),'GalvanizedMetal')
box('Door lintel',(0,-2.025,3.95),(1.25,.16,.13),'GalvanizedMetal')
box('Door pull',(.33,-1.88,2.73),(.04,.06,.22),'SignIvory',True)
box('Service identification plate',(0,-2.11,4.25),(1.4,.035,.32),'PaintedTrim')
for x in [-.45,0,.45]:box('Plate marking',(x,-2.137,4.25),(.09,.015,.19),'SignIvory')
records.append(export('DefenseTower'))
for obj in parts:obj.location.x+=44

bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
for image in bpy.data.images:
    if image.filepath:image.filepath=bpy.path.relpath(image.filepath,start=str(SOURCE))
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'BikeRoutePolish.blend'),compress=True,relative_remap=False)
(OUT/'AssetManifest.json').write_text(json.dumps(records,indent=2)+'\n')
print(json.dumps(records))
