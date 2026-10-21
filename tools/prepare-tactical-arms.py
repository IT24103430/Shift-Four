"""Derive a posed, garment-equipped rig from para's verified CC0 FPS arms.
No original source or vendor model is overwritten. Blender 4.5+.
"""
import bpy, bmesh, math, json, os, sys, argparse
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output-dir',required=True,help='A staging directory; existing original assets are never overwritten.')
parser.add_argument('--pistol-reference',required=True,help='Temporary JSON exported by TacticalArmsAssetBuilder.ExportPistolReference.')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
OUT=os.path.abspath(args.output_dir)
os.makedirs(OUT,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=ROOT+'/ArtSource/ThirdParty/FirstPersonArms/Source.blend')
scene=bpy.context.scene
if bpy.context.object and bpy.context.object.mode!='OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
rig=next(o for o in scene.objects if o.type=='ARMATURE')
body=next(o for o in scene.objects if o.type=='MESH')
for o in list(scene.objects):
 if o not in (rig,body): bpy.data.objects.remove(o,do_unlink=True)
rig.animation_data_clear();body.animation_data_clear()
for p in rig.pose.bones:
 for c in list(p.constraints):p.constraints.remove(c)
 p.matrix_basis=Matrix.Identity(4)
# Replace legacy controls before posing; keep all deform rest matrices intact.
bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
for side in ('L','R'):
 b=rig.data.edit_bones['hand.'+side];m=b.matrix.copy();length=b.length
 b.parent=rig.data.edit_bones['forearm.'+side];b.use_connect=False;b.matrix=m;b.length=length
 for name in ['index','middle','ring','pinky']:
  b=rig.data.edit_bones['palm_'+name+'.'+side];m=b.matrix.copy();length=b.length
  b.parent=rig.data.edit_bones['hand.'+side];b.use_connect=False;b.matrix=m;b.length=length
bpy.ops.object.mode_set(mode='OBJECT')
for side in ('L','R'):
 old=body.vertex_groups.get('hand.'+side+'.control');new=body.vertex_groups.get('hand.'+side)
 if old:
  for v in body.data.vertices:
   w=sum(g.weight for g in v.groups if g.group==old.index)
   if w:new.add([v.index],w,'ADD')
  body.vertex_groups.remove(old)
bpy.ops.object.mode_set(mode='EDIT')
for name in ('hand.L.control','hand.R.control'):
 b=rig.data.edit_bones.get(name)
 if b:rig.data.edit_bones.remove(b)
# Rest geometry and skeleton are uniformly scaled together, never stretched to a pose.
SCALE=.085
original_edit={b.name:(b.matrix.copy(),b.length) for b in rig.data.edit_bones}
for b in rig.data.edit_bones:b.use_connect=False
for b in rig.data.edit_bones:
 m,length=original_edit[b.name];m.translation*=SCALE;b.matrix=m;b.length=length*SCALE
bpy.ops.object.mode_set(mode='OBJECT')
body.parent=rig;body.matrix_parent_inverse=Matrix.Identity(4)
rig.matrix_world=Matrix.Identity(4);body.matrix_basis=Matrix.Identity(4)
for v in body.data.vertices:v.co*=SCALE
body.data.update();bpy.context.view_layer.update()
for mod in list(body.modifiers):body.modifiers.remove(mod)
for p in rig.pose.bones:p.matrix_basis=Matrix.Identity(4)
# Normalize source weights and remove four irrelevant unweighted source vertices.
groups={g.index:g.name for g in body.vertex_groups}
unused={v.index for v in body.data.vertices if not any(g.weight>.00001 for g in v.groups)}
bm=bmesh.new();bm.from_mesh(body.data);bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index in unused],context='VERTS');bm.to_mesh(body.data);bm.free()
for v in body.data.vertices:
 weights=sorted([(groups[g.group],g.weight) for g in v.groups if g.weight>.00001],key=lambda a:-a[1])[:4]
 total=sum(w for n,w in weights)
 for index in [g.group for g in v.groups]:body.vertex_groups[index].remove([v.index])
 for n,w in weights:body.vertex_groups[n].add([v.index],w/total,'REPLACE')
# Canonical conversion used by Blender FBX -> Unity with baked axes.
def U(p):x,y,z=p;return Vector((x,z,y))
def unity(p):return [p.x,p.z,p.y]
def basis(radial,distal):
 y=distal.normalized();x=(radial-y*radial.dot(y)).normalized();z=x.cross(y).normalized()
 return Matrix((x,y,z)).transposed()
rest={b.name:b.matrix_local.copy() for b in rig.data.bones}
lengths={b.name:b.length for b in rig.data.bones}
# Materials are shared named slots remapped to project-owned URP materials in Unity.
materials={}
for name,color,roughness in [
 ('GloveShell',(.045,.056,.048,1),.76),('GloveReinforcement',(.095,.108,.085,1),.61),
 ('SleeveRipstop',(.18,.22,.175,1),.87),('CuffElastic',(.065,.077,.06,1),.89),
 ('StitchThread',(.28,.30,.23,1),.88),('KnuckleRubber',(.032,.039,.033,1),.6)]:
 mat=bpy.data.materials.new(name);mat.diffuse_color=color;mat.use_nodes=True
 bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=color;bs.inputs['Roughness'].default_value=roughness
 materials[name]=mat

def split_side(source,side):
 obj=source.copy();obj.data=source.data.copy();bpy.context.collection.objects.link(obj)
 bm=bmesh.new();bm.from_mesh(obj.data)
 sign=1 if side=='L' else -1
 bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.co.x*sign<0],context='VERTS');bm.to_mesh(obj.data);bm.free()
 return obj

def cut(obj,point,normal,keep_distal):
 bm=bmesh.new();bm.from_mesh(obj.data)
 bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=point,plane_no=normal,clear_inner=keep_distal,clear_outer=not keep_distal)
 bm.to_mesh(obj.data);bm.free();obj.data.update()

def applymod(obj,mod):
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 bpy.ops.object.modifier_apply(modifier=mod.name)

parts=[]
for side in ('R','L'):
 w=rig.data.bones['hand.'+side].head_local.copy();e=rig.data.bones['forearm.'+side].head_local.copy();axis=(w-e).normalized()
 glove=split_side(body,side);glove.name='Glove_'+side
 cut(glove,w-axis*.044,axis,True)
 glove.data.materials.clear();glove.data.materials.append(materials['GloveShell'])
 for poly in glove.data.polygons:poly.material_index=0
 # One subdivision smooths the original anatomical surface; it is not a substitute for a new rig.
 sub=glove.modifiers.new('Anatomical glove smoothing','SUBSURF');sub.levels=1;applymod(glove,sub)
 glove.data.update()
 for v in glove.data.vertices:v.co+=v.normal*.0012
 glove.data.update()
 for p in glove.data.polygons:p.use_smooth=True
 arm=glove.modifiers.new('Arm skin','ARMATURE');arm.object=rig;arm.use_deform_preserve_volume=False
 parts.append(glove)
bpy.data.objects.remove(body,do_unlink=True)

def set_world(name,head,rotation):
 p=rig.pose.bones[name]
 p.matrix=Matrix.LocRotScale(head,rotation.to_quaternion(),Vector((1,1,1)))
 bpy.context.view_layer.update()
def aim(name,direction):
 p=rig.pose.bones[name];a=(p.tail-p.head).normalized();b=direction.normalized()
 q=a.rotation_difference(b)
 set_world(name,p.head.copy(),q.to_matrix()@p.matrix.to_3x3().normalized())
def solve_elbow(s,w,a,b,pole):
 axis=(w-s).normalized();d=(w-s).length
 along=(a*a-b*b+d*d)/(2*d);height=math.sqrt(max(.0001,a*a-along*along))
 tangent=pole-s-axis*(pole-s).dot(axis);tangent.normalize()
 return s+axis*along+tangent*height

# Fit the existing anatomy around the pistol. Bone lengths stay identical to the scaled source.
pose_spec={
 'R':{'wrist':(.030,-.022,-.126),'shoulder':(.18,-.21,-.39),'pole':(.30,-.24,-.19),'distal':(0,-.14,1),'radial':(0,1,.14)},
 'L':{'wrist':(-.040,-.041,-.118),'shoulder':(-.34,-.22,-.36),'pole':(-.32,-.29,-.19),'distal':(0,-.06,1),'radial':(0,1,.06)},
}
for side,spec in pose_spec.items():
 w=U(spec['wrist']);s=U(spec['shoulder']);elbow=solve_elbow(s,w,lengths['upper_arm.'+side],lengths['forearm.'+side],U(spec['pole']))
 # Rotate upper/forearm from the original rest orientation, without nonuniform scaling.
 a=(rig.data.bones['upper_arm.'+side].tail_local-rig.data.bones['upper_arm.'+side].head_local).normalized()
 rot=a.rotation_difference((elbow-s).normalized()).to_matrix()@rest['upper_arm.'+side].to_3x3()
 set_world('upper_arm.'+side,s,rot)
 aim('forearm.'+side,w-elbow)
 h=rig.data.bones['hand.'+side];idx=rig.data.bones['f_index.01.'+side];pink=rig.data.bones['f_pinky.01.'+side];mid=rig.data.bones['f_middle.01.'+side]
 source_basis=basis(idx.head_local-pink.head_local,mid.head_local-h.head_local)
 target_basis=basis(U(spec['radial']),U(spec['distal']))
 hand_rot=target_basis@source_basis.transposed()@rest['hand.'+side].to_3x3()
 set_world('hand.'+side,w,hand_rot)
 # Pose each finger individually. Segment lengths and joint positions remain anatomical.
 for finger in ('index','middle','ring','pinky'):
  if side=='R':
   directions=[(0,.08,1),(-.65,-.06,.76),(-1,0,-.2)] if finger=='index' else [(-.20,0,1),(-1,0,.12),(-.42,0,-1)]
  else:
   directions=[(.16,0,1),(1,0,-.12),(.25,0,-1)]
  for seg,direction in zip(('01','02','03'),directions):aim('f_'+finger+'.'+seg+'.'+side,U(direction))
 # Thumbs are posed after the palm so their bases keep their skin connections.
 for seg,direction in zip(('01','02','03'),[(0,.35,1),(0,0,1),(0,-.1,1)]):
  aim('thumb.'+seg+'.'+side,U(direction))

# Bake this fitted grip into the mesh and rest skeleton; preserve usable deform weights.
for obj in parts:
 for mod in list(obj.modifiers):
  if mod.type=='ARMATURE':applymod(obj,mod)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='POSE');bpy.ops.pose.select_all(action='SELECT');bpy.ops.pose.armature_apply(selected=False);bpy.ops.object.mode_set(mode='OBJECT')
for obj in parts:
 arm=obj.modifiers.new('Arm skin','ARMATURE');arm.object=rig
rig.name='SF_TacticalArmsRig';rig.data.name='SF_TacticalArmsSkeleton'
# Finish the glove gauntlet beneath the garment cuff; no exposed skin cut or protruding wrist flap.
for side in ('R','L'):
 glove=next(o for o in parts if o.name=='Glove_'+side)
 wrist=rig.data.bones['hand.'+side].head_local.copy();elbow=rig.data.bones['forearm.'+side].head_local.copy();axis=(wrist-elbow).normalized()
 for v in glove.data.vertices:
  d=(v.co-wrist).dot(axis)
  if d<0:
   origin=wrist+axis*d;radial=v.co-origin
   if radial.length>.020:
    blend=min(1,-d/.008);v.co=origin+radial.lerp(radial.normalized()*.020,blend)
 cut(glove,wrist-axis*.020,axis,True)
# Add tailored garment meshes and surface-following glove reinforcement after fitting.
def weighted_mesh(name,verts,faces,weights,mat_names,uv=None):
 data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
 obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj);obj.parent=rig
 for mat in mat_names:data.materials.append(materials[mat])
 for p in data.polygons:p.use_smooth=True
 for i,items in enumerate(weights):
  total=sum(items.values())
  for n,w in items.items():
   group=obj.vertex_groups.get(n) or obj.vertex_groups.new(name=n);group.add([i],w/total,'REPLACE')
 layer=data.uv_layers.new(name='UVMap')
 if uv:
  for poly in data.polygons:
   for loop in poly.loop_indices:layer.data[loop].uv=uv[data.loops[loop].vertex_index]
 else:
  for poly in data.polygons:
   for loop in poly.loop_indices:layer.data[loop].uv=(data.vertices[data.loops[loop].vertex_index].co.x*12,data.vertices[data.loops[loop].vertex_index].co.y*12)
 mod=obj.modifiers.new('Arm skin','ARMATURE');mod.object=rig;parts.append(obj);return obj

for side in ('R','L'):
 # Garment follows the upper arm and forearm at their unchanged natural lengths.
 wrist=rig.data.bones['hand.'+side].head_local.copy();elbow=rig.data.bones['forearm.'+side].head_local.copy();shoulder=rig.data.bones['upper_arm.'+side].head_local.copy()
 fore=(wrist-elbow).normalized();upper=(elbow-shoulder).normalized();sideaxis=fore.cross(U((0,1,0))).normalized();radial=sideaxis.cross(fore).normalized()
 # Forearm end at 20 mm from the wrist, with an overlapping elastic cuff.
 rings=[]
 length=(wrist-elbow).length;total=length+(elbow-shoulder).length-.008
 for i in range(84):
  distance=.008+i/83*(total-.008);t=distance/length
  if distance<length-.04:point=wrist-fore*distance
  elif distance>length+.04:point=elbow-upper*(distance-length)
  else:
   blend=(distance-length+.04)/.08
   point=(elbow+fore*.04)*(1-blend)**2+elbow*2*blend*(1-blend)+(elbow-upper*.04)*blend**2
  r=.022+(.040-.022)*math.sin(min(1,t)*math.pi*.53) if t<=1 else .040+.006*math.sin((t-1)*math.pi*.7)
  rings.append((point,r,t))
 verts=[];faces=[];weights=[];uv=[]
 count=32
 for i,(point,r,t) in enumerate(rings):
  axial=(rings[max(0,i-1)][0]-rings[min(len(rings)-1,i+1)][0]).normalized()
  x=(sideaxis-axial*sideaxis.dot(axial)).normalized();z=x.cross(axial).normalized()
  for j in range(count):
   angle=j/count*math.tau
   ease=.0012*math.sin(t*38+angle*2)+.00055*math.sin(t*73-angle*3)
   cuff=t<.16
   if cuff:ease=.00065*math.cos(t*210)+.0005
   radius=r+ease
   verts.append(point+x*(math.cos(angle)*radius)+z*(math.sin(angle)*radius*.88))
   elbowblend=max(0,min(1,(t-.80)/.35))
   weights.append({'forearm.'+side:1-elbowblend,'upper_arm.'+side:elbowblend})
   uv.append((j/count,t))
   if i and j:
    k=i*count+j;faces.append((k-count-1,k-count,k,k-1))
  if i:
   k=i*count;faces.append((k-count,k-count+count-1,k+count-1,k))
 obj=weighted_mesh('TailoredSleeve_'+side,verts,faces,weights,['SleeveRipstop','CuffElastic'],uv)
 for p in obj.data.polygons:p.material_index=1 if sum(rings[v//count][2] for v in p.vertices)/len(p.vertices)<.16 else 0
 # Give both openings a finished return edge with real thickness, skin weights follow through.
 arm=obj.modifiers.get('Arm skin');obj.modifiers.remove(arm)
 solid=obj.modifiers.new('Finished fabric hem','SOLIDIFY');solid.thickness=.0014;solid.offset=-1;applymod(obj,solid)
 arm=obj.modifiers.new('Arm skin','ARMATURE');arm.object=rig
 # Reinforcement panels are projected onto the fitted anatomical glove, with transferred weights.
 glove=next(o for o in parts if o.name=='Glove_'+side)
 mesh=glove.data;tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[list(p.vertices) for p in mesh.polygons],all_triangles=False)
 gn={g.index:g.name for g in glove.vertex_groups}
 def sample_weights(position,face):
  source=mesh.polygons[face];acc={}
  for idx in source.vertices:
   v=mesh.vertices[idx];f=1/max(1e-9,(v.co-position).length_squared)
   for g in v.groups:acc[gn[g.group]]=acc.get(gn[g.group],0)+g.weight*f
  acc=dict(sorted(acc.items(),key=lambda v:-v[1])[:4]);total=sum(acc.values());return {n:w/total for n,w in acc.items()}
 def panel(name,origin,longi,across,normal,length,width,material):
  v=[];w=[];uv=[];f=[];rows=9;cols=7
  for i in range(rows):
   t=i/(rows-1);taper=.72+.28*math.sin(math.pi*t)**.4
   for j in range(cols):
    x=(j/(cols-1)-.5)*width*taper;at=origin+longi*(t*length)+across*x
    hit,n,face,distance=tree.ray_cast(at+normal*.1,-normal,.2)
    if hit is None:
     hit,n,face,distance=tree.find_nearest(at)
    if hit is None:raise RuntimeError('Cannot fit reinforcement to glove '+name)
    puff=.0007+.0010*math.sin(math.pi*t)*math.sin(math.pi*j/(cols-1))
    v.append(hit+n*puff);w.append(sample_weights(hit,face));uv.append((j/(cols-1),t))
    if i and j:
     k=i*cols+j;f.append((k-cols-1,k-cols,k,k-1))
  obj=weighted_mesh(name,v,f,w,[material],uv)
  # A visible beveled edge; no disconnected rigid ornament or physics component.
  mod=obj.modifiers.get('Arm skin');obj.modifiers.remove(mod)
  sol=obj.modifiers.new('Reinforcement thickness','SOLIDIFY');sol.thickness=.0007;sol.offset=-1;applymod(obj,sol)
  mod=obj.modifiers.new('Arm skin','ARMATURE');mod.object=rig
  return obj
 d=(rig.data.bones['f_middle.01.'+side].head_local-wrist).normalized()
 r=(rig.data.bones['f_index.01.'+side].head_local-rig.data.bones['f_pinky.01.'+side].head_local).normalized()
 r=(r-d*r.dot(d)).normalized();n=U((1 if side=='R' else -1,0,0))
 panel('DorsalReinforcement_'+side,wrist+d*.024,d,r,n,.045,.049,'GloveReinforcement')
 for finger in ('middle','ring','pinky'):
  b=rig.data.bones['f_'+finger+'.01.'+side];d=(b.tail_local-b.head_local).normalized()
  panel('KnuckleGuard_'+finger+'_'+side,b.head_local+d*.003,d,r,n,b.length*.58,.012 if finger!='pinky' else .010,'KnuckleRubber')

# One skin, shared palette: keep the current lifecycle/rendering budget and hierarchy contract.
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join();combined=bpy.context.object;combined.name='SF_TacticalArmsMesh';parts=[combined]
for v in combined.data.vertices:
 values=sorted([(combined.vertex_groups[g.group].name,g.weight) for g in v.groups if g.weight>1e-6],key=lambda a:-a[1])[:4]
 if not values:raise RuntimeError('Unweighted derivative vertex '+str(v.index))
 total=sum(w for n,w in values)
 for index in [g.group for g in v.groups]:combined.vertex_groups[index].remove([v.index])
 for n,w in values:combined.vertex_groups[n].add([v.index],w/total,'REPLACE')
# Constrain projected garment weights anatomically after all regions are joined.
# This same correction can stage a weights-only update of an existing derivative.
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
sys.dont_write_bytecode=True
from tactical_arm_skinning import repair
skin_repair=repair(combined)
json.dump(skin_repair,open(OUT+'/skin-weight-repair.json','w'),indent=2)
# Save editable arms only. Pistol reference geometry is reconstructed from the existing Unity import for previews.
# No pistol mesh or texture is included in the derivative .blend or FBX deliverable.
pistol=[]
def create_pistol_reference():
 ref_path=os.path.abspath(args.pistol_reference)
 if not os.path.exists(ref_path):raise RuntimeError('Export the existing Unity pistol reference before fitting the arms.')
 ref=json.load(open(ref_path))
 img=bpy.data.images.load(ROOT+'/Assets/PolyOne/Free Gun/Texture/T_PropsFreeGun_BaseColor.png',check_existing=True)
 refmat=bpy.data.materials.new('REFERENCE_ONLY_Pistol');refmat.use_nodes=True;bs=refmat.node_tree.nodes.get('Principled BSDF');tx=refmat.node_tree.nodes.new('ShaderNodeTexImage');tx.image=img;refmat.node_tree.links.new(tx.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.6
 for part in ref['parts']:
  data=bpy.data.meshes.new('REFERENCE_ONLY_'+part['name']);verts=[U((v['x'],v['y'],v['z'])) for v in part['vertices']];indices=part['triangles'];faces=[tuple(reversed(indices[i:i+3])) for i in range(0,len(indices),3)]
  data.from_pydata(verts,[],faces);data.materials.append(refmat);layer=data.uv_layers.new(name='UVMap')
  for poly in data.polygons:
   for loop in poly.loop_indices:
    uv=part['uv'][data.loops[loop].vertex_index];layer.data[loop].uv=(uv['x'],uv['y'])
  obj=bpy.data.objects.new('REFERENCE_ONLY_'+part['name'],data);scene.collection.objects.link(obj);pistol.append(obj)
 return pistol
# Studio preview: game-camera coordinate, with the unchanged authored pistol pivot.
pivot=U((.12,-.105,.42))
# Move the inspection camera to -pivot rather than changing the exported local arms pose.
bpy.ops.object.camera_add(location=-pivot);cam=bpy.context.object;cam.name='PREVIEW_GameCamera';cam.rotation_euler=(Vector((0,1,0))).to_track_quat('-Z','Y').to_euler();cam.data.lens=31.1769;cam.data.sensor_width=36;cam.data.sensor_fit='VERTICAL';cam.data.clip_start=.01;scene.camera=cam
# 60-degree vertical FOV; Blender camera sensor height is sensor_width when fit is VERTICAL.
cam.data.angle_y=math.radians(60)
for name,pos,energy,size in [('Key',(-1,-2,2),160,2),('Fill',(2,-1,1),100,2),('Rim',(-1,1,2),200,1.5)]:
 bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.name='PREVIEW_'+name;o.data.energy=energy;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
scene.world.color=(.13,.15,.18)
scene.use_nodes=False;scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=True
scene.render.resolution_x=1600;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.view_settings.exposure=0;scene.view_settings.gamma=1;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
# Auditable geometry and proportions, independent of a rendered preview.
metrics={'source':'para / MakeHuman CC0','source_scale':SCALE,'bones':len(rig.data.bones),'bone_lengths':{b.name:b.length for b in rig.data.bones},'mesh_vertices':len(combined.data.vertices),'submeshes':[m.name for m in combined.data.materials],'reference_geometry_exported':False}
combined.data.calc_loop_triangles();metrics['mesh_triangles']=len(combined.data.loop_triangles)
metrics['unweighted_vertices']=sum(not any(g.weight>1e-6 for g in v.groups) for v in combined.data.vertices)
metrics['max_influences']=max(sum(g.weight>1e-6 for g in v.groups) for v in combined.data.vertices)
if metrics['max_influences']>4:raise RuntimeError('More than four significant skin weights')
metrics['scale_deviation']=max(abs(s-1) for b in rig.pose.bones for s in b.scale)
json.dump(metrics,open(OUT+'/source-metrics.json','w'),indent=2)
# Export only our derivative, with unit object transforms, no reference gun, lights, cameras or actions.
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=OUT+'/SF_TacticalArms.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',armature_nodetype='NULL',use_armature_deform_only=True)
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
rig.show_in_front=True
scene.render.filepath=OUT+'/game-preview.png'
bpy.context.preferences.filepaths.save_version=0
for action in list(bpy.data.actions):bpy.data.actions.remove(action)
for mesh in list(bpy.data.meshes):
 if mesh.users==0:bpy.data.meshes.remove(mesh)
for material in list(bpy.data.materials):
 if material.users==0:bpy.data.materials.remove(material)
for image in list(bpy.data.images):
 if image.users==0:bpy.data.images.remove(image)
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/SF_TacticalArms.blend')
pistol=create_pistol_reference()
bpy.ops.render.render(write_still=True)
# Side reference showing contact, never confused with Game view.
cam.location=U((.45,-.025,0));cam.rotation_euler=(U((0,-.04,-.02))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=.46
scene.render.filepath=OUT+'/side-preview.png';bpy.ops.render.render(write_still=True)
print('TACTICAL_ARMS_EXPORTED',OUT)
