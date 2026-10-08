"""Render source previews without saving changes to the Blender authoring files."""
import bpy,math,json,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3];LOG=ROOT/'Logs/BikeRouteSettlementPass7/Blender';LOG.mkdir(parents=True,exist_ok=True)
original='--original' in sys.argv
file=ROOT/('SourceArt/BikeRoute/Settlement/Originals/BikeRoute_DesertSettlement.blend' if original else 'SourceArt/BikeRoute/Settlement/MountainSettlement.blend')
bpy.ops.wm.open_mainfile(filepath=str(file))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=20;scene.cycles.use_denoising=True;scene.cycles.device='CPU';scene.render.resolution_x=640;scene.render.resolution_y=512;scene.render.resolution_percentage=100
scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.30,.35,.42,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.6
scene.view_settings.view_transform='AgX'
if original:
 objects=[o for o in scene.objects if o.type=='MESH'];groups={o.name:[o] for o in objects}
 for mat in bpy.data.materials:
  color=mat.diffuse_color[:];mat.use_nodes=True;mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=color
else:
 names=[x['name'] for x in json.loads((ROOT/'Assets/Game/Art/Environment/Buildings/MountainSettlement/AssetManifest.json').read_text()) if x.get('original')]
 groups={name:list(bpy.data.collections[name].objects) for name in names};objects=[o for o in scene.objects if o.type=='MESH']
 # Verify portable relative image references before rendering.
 for img in bpy.data.images:
  if img.filepath and not Path(bpy.path.abspath(img.filepath)).exists():raise RuntimeError('Missing texture '+img.filepath)
for o in objects:o.hide_render=True
bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.name='Preview ground';mat=bpy.data.materials.new('Preview grey');mat.diffuse_color=(.20,.22,.22,1);floor.data.materials.append(mat)
bpy.ops.object.camera_add();camera=bpy.context.object;scene.camera=camera;camera.data.type='ORTHO';camera.data.lens=45
bpy.ops.object.light_add(type='SUN');sun=bpy.context.object;sun.rotation_euler=(math.radians(28),math.radians(-24),math.radians(-35));sun.data.energy=2.4;sun.data.angle=math.radians(12)
for name,parts in groups.items():
 for o in objects:o.hide_render=True
 for o in parts:o.hide_render=False
 points=[o.matrix_world@Vector(corner) for o in parts for corner in o.bound_box]
 lo=Vector([min(p[j] for p in points) for j in range(3)]);hi=Vector([max(p[j] for p in points) for j in range(3)]);center=(lo+hi)/2;span=max(hi.x-lo.x,hi.y-lo.y,hi.z-lo.z)
 floor.location=(center.x,center.y,lo.z-.025)
 camera.location=center+Vector((span*1.25,-span*1.75,span*.95));camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=span*1.65
 scene.render.filepath=str(LOG/(('before-' if original else 'after-')+name+'.png'));bpy.ops.render.render(write_still=True)
 if name=='MountainGarage' and not original:
  scene.render.resolution_x=1280;scene.render.resolution_y=900;camera.data.ortho_scale=span*1.55
  scene.render.filepath=str(LOG/'garage-detail.png');bpy.ops.render.render(write_still=True);scene.render.resolution_x=640;scene.render.resolution_y=512
print('PREVIEW_COMPLETE')
