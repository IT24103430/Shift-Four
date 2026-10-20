"""Bake a pistol grip from para's CC0 FPS ARMS RIG 1 source; run with Blender --background --python."""
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE = os.path.join(ROOT, 'ArtSource/ThirdParty/FirstPersonArms/Source.blend')
OUTPUT = os.path.join(ROOT, 'Assets/Resources/Weapons/FirstPersonArms.fbx')
bpy.ops.wm.open_mainfile(filepath=SOURCE)
if bpy.context.object and bpy.context.object.mode != 'OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
scene = bpy.context.scene
scene.frame_set(1)
rig = next(o for o in scene.objects if o.type == 'ARMATURE')
mesh = next(o for o in scene.objects if o.type == 'MESH')
rig.animation_data_clear()
for bone in rig.pose.bones:
    # Keep the evaluated source pose before removing authoring constraints.
    matrix = bone.matrix.copy()
    for constraint in list(bone.constraints): bone.constraints.remove(constraint)
    bone.matrix = matrix
bpy.context.view_layer.update()
SCALE = .065
def point(unity):
    x,y,z=unity
    return Vector((x,z,y))/SCALE
def pose(name, head, tail):
    bone=rig.pose.bones[name]
    head,tail=point(head),point(tail)
    direction=tail-head
    rotation=direction.to_track_quat('Y','Z')
    world=Matrix.LocRotScale(head,rotation,Vector((1,direction.length/bone.length,1)))
    bone.matrix=rig.matrix_world.inverted()@world
    bpy.context.view_layer.update()

for side, sign in [('R',1),('L',-1)]:
    # The forearms exit below/behind the camera; no torso mesh is imported.
    wrist=(.012*sign,-.045,-.045) if side=='R' else (-.038,-.05,-.025)
    elbow=(.23*sign,-.21,-.26)
    shoulder=(.32*sign,-.28,-.43)
    pose('upper_arm.'+side,shoulder,elbow)
    pose('forearm.'+side,elbow,wrist)
    pose('hand.'+side,wrist,(wrist[0],wrist[1]-.073,wrist[2]+.015))
    for finger in ['index','middle','ring','pinky']:
        for segment,angle in [('01',65),('02',80),('03',55)]:
            bone=rig.pose.bones['f_'+finger+'.'+segment+'.'+side]
            bone.rotation_mode='XYZ';bone.rotation_euler=(math.radians(angle),0,0)
    for segment,angle in [('01',25),('02',45),('03',35)]:
        bone=rig.pose.bones['thumb.'+segment+'.'+side]
        bone.rotation_mode='XYZ';bone.rotation_euler=(math.radians(angle),0,0)
bpy.context.view_layer.update()

# First-person view uses the hands and forearms; upper-arm geometry can cross the near plane
# during a pistol reload. Keep the original deform skeleton for support-arm motion.
groups={g.index:g.name for g in mesh.vertex_groups}
keep={v.index for v in mesh.data.vertices if sum(g.weight for g in v.groups
    if groups[g.group].startswith(('forearm.','hand.','palm_','f_','thumb.'))) >= .5}
bm=bmesh.new();bm.from_mesh(mesh.data)
bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index not in keep],context='VERTS')
bm.to_mesh(mesh.data);bm.free();mesh.data.update()

# Bake the fitted pose into mesh/rest bones, retaining skin weights for runtime arm motion.
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True);bpy.context.view_layer.objects.active=mesh
for modifier in list(mesh.modifiers):
    if modifier.type=='ARMATURE': bpy.ops.object.modifier_apply(modifier=modifier.name)
rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='POSE');bpy.ops.pose.select_all(action='SELECT');bpy.ops.pose.armature_apply(selected=False);bpy.ops.object.mode_set(mode='OBJECT')
# Replace the source IK-control parenting with a conventional deform chain. Keep every
# rest matrix unchanged, so skin bind positions stay exact and the forearm moves the whole hand.
bpy.ops.object.mode_set(mode='EDIT')
for side in ['L','R']:
    pairs=[('hand.'+side,'forearm.'+side)]
    for finger in ['index','middle','ring','pinky']:
        pairs += [('palm_'+finger+'.'+side,'hand.'+side),
            ('f_'+finger+'.01.'+side,'palm_'+finger+'.'+side),
            ('f_'+finger+'.02.'+side,'f_'+finger+'.01.'+side),
            ('f_'+finger+'.03.'+side,'f_'+finger+'.02.'+side)]
    pairs += [('thumb.01.'+side,'hand.'+side),('thumb.02.'+side,'thumb.01.'+side),('thumb.03.'+side,'thumb.02.'+side)]
    for child,parent in pairs:
        bone=rig.data.edit_bones[child];matrix=bone.matrix.copy();length=bone.length
        bone.use_connect=False;bone.parent=rig.data.edit_bones[parent];bone.matrix=matrix;bone.length=length
bpy.ops.object.mode_set(mode='OBJECT')
for side in ['L','R']:
    control=mesh.vertex_groups.get('hand.'+side+'.control')
    if control:
        hand=mesh.vertex_groups.get('hand.'+side)
        for vertex in mesh.data.vertices:
            weight=sum(g.weight for g in vertex.groups if g.group==control.index)
            if weight: hand.add([vertex.index],weight,'ADD')
        mesh.vertex_groups.remove(control)
modifier=mesh.modifiers.new('Arm skin','ARMATURE');modifier.object=rig
# Scale the full world transform, including the source rig's translated origin.
# Unity's FBX handedness conversion needs the source +Y view direction turned toward -Y.
rig.matrix_world=Matrix.Rotation(math.pi,4,'Z')@Matrix.Diagonal(Vector((SCALE,SCALE,SCALE,1)))@rig.matrix_world
rig.name='FirstPersonArmsRig';mesh.name='FirstPersonArmsMesh'
mesh.data.materials.clear()
for name, color, roughness in [
    ('Glove', (.045,.065,.075,1), .72),
    ('Sleeve', (.16,.21,.23,1), .9),
    ('Cuff', (.075,.095,.105,1), .63),
]:
    mat=bpy.data.materials.new(name);mat.diffuse_color=color;mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value=color
    shader.inputs['Roughness'].default_value=roughness
    mesh.data.materials.append(mat)
# Use the existing deform weights to follow the wrist boundary on both arms.
forearm_groups={g.index for g in mesh.vertex_groups if g.name.startswith('forearm.')}
for polygon in mesh.data.polygons:
    weight=sum(sum(g.weight for g in mesh.data.vertices[i].groups
        if g.group in forearm_groups) for i in polygon.vertices)/len(polygon.vertices)
    polygon.material_index=1 if weight>.94 else (2 if weight>.35 else 0)
    polygon.use_smooth=True
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);mesh.select_set(True)
os.makedirs(os.path.dirname(OUTPUT),exist_ok=True)
bpy.ops.export_scene.fbx(filepath=OUTPUT,use_selection=True,object_types={'ARMATURE','MESH'},
    add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
    armature_nodetype='NULL',use_armature_deform_only=True)
print('Exported',OUTPUT)

# Keep the fitted, editable source outside Assets so Unity does not launch Blender on import.
reference_dir=os.path.join(ROOT,'ArtSource','FirstPersonArms')
os.makedirs(reference_dir,exist_ok=True)
scene.unit_settings.system='METRIC'
rig.show_in_front=True
notes=bpy.data.texts.new('READ ME')
notes.write('Shift-Four first-person arms\nDerived from para CC0 / MakeHuman arms; see ../ThirdParty/FirstPersonArms/README.md.\n'
    'Fitted pistol rest pose, weighted forearms/hands/fingers. Glove, Sleeve and Cuff material slots.\n'
    'Regenerate with tools/prepare-first-person-arms.py. Runtime support-hand motion uses forearm.L.\n'
    'Reference camera and lighting are excluded from the FBX export.\n')
corners=[mesh.matrix_world@Vector(c) for c in mesh.bound_box]
center=sum(corners,Vector())/8
extent=max((v-center).length for v in corners)
def aim(obj):
    obj.rotation_euler=(center-obj.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=center+Vector((.15,-1,.65))*extent*3)
camera=bpy.context.object;camera.name='Reference Camera';aim(camera)
camera.data.type='ORTHO';camera.data.ortho_scale=extent*2.5;scene.camera=camera
for name, offset, energy, size in [
    ('Key',(1,-2,3),12,2),('Fill',(-2,-1,1),8,2),('Rim',(0,2,2),16,1)]:
    bpy.ops.object.light_add(type='AREA',location=center+Vector(offset)*extent*2)
    light=bpy.context.object;light.name=name;light.data.energy=energy;light.data.shape='DISK';light.data.size=size;aim(light)
scene.world.color=(.13,.13,.13)
scene.render.engine='CYCLES';scene.cycles.samples=64;scene.cycles.use_denoising=True
scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.filepath=os.path.join(reference_dir,'Reference.png')
bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);rig.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(reference_dir,'FirstPersonArms.blend'))
bpy.ops.render.render(write_still=True)
