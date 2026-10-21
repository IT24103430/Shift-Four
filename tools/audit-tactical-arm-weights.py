"""Blender bind-data fingerprints and repeatable garment deformation inspection.
Blender -b --python tools/audit-tactical-arm-weights.py -- --blend FILE --output-dir DIR [--render]
Renders are Blender inspection views, not Unity Game-view captures.
"""
import argparse
import hashlib
import json
import math
import os
import sys
import bpy
from mathutils import Euler, Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.dont_write_bytecode = True
from tactical_arm_skinning import components

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--blend', required=True)
parser.add_argument('--output-dir', required=True)
parser.add_argument('--render', action='store_true')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
os.makedirs(args.output_dir, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=os.path.abspath(args.blend))
rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
skin = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
mesh = skin.data

def digest(value):
    return hashlib.sha256(json.dumps(value, separators=(',', ':')).encode()).hexdigest()

weights = [{skin.vertex_groups[g.group].name: g.weight for g in v.groups if g.weight > 0} for v in mesh.vertices]
identity = {
    'vertex_count': len(mesh.vertices), 'polygon_count': len(mesh.polygons),
    'vertices': digest([list(v.co) for v in mesh.vertices]),
    'topology': digest([list(p.vertices) for p in mesh.polygons]),
    'material_regions': digest([p.material_index for p in mesh.polygons]),
    'uvs': digest([[list(d.uv) for d in layer.data] for layer in mesh.uv_layers]),
    'materials': [m.name for m in mesh.materials],
    'bones': digest([(b.name, b.parent.name if b.parent else None, [list(row) for row in b.matrix_local], b.length, b.use_deform) for b in rig.data.bones]),
    'object_transforms': digest([(o.name, [list(row) for row in o.matrix_world]) for o in [skin, rig]]),
    'weights': digest(weights)}
regions, _ = components(mesh)
vertex_materials = [set() for _ in mesh.vertices]
for face in mesh.polygons:
    for i in face.vertices:
        vertex_materials[i].add(mesh.materials[face.material_index].name)
labels = {}
for ids in regions:
    materials = set().union(*(vertex_materials[i] for i in ids))
    mass = {}
    for i in ids:
        for name, weight in weights[i].items():
            mass[name] = mass.get(name, 0) + weight
    side = max(('R', 'L'), key=lambda s: sum(w for n, w in mass.items() if n.endswith('.' + s)))
    if materials == {'GloveShell'}: name = 'shell'
    elif materials == {'GloveReinforcement'}: name = 'dorsal'
    elif materials == {'KnuckleRubber'}:
        name = max(('middle', 'ring', 'pinky'), key=lambda f: sum(w for n, w in mass.items() if n.startswith('f_' + f + '.'))) + '-guard'
    else: name = 'sleeve'
    label = name + '-' + side
    if label in labels: raise ValueError('Duplicate region ' + label)
    labels[label] = ids
membership = {i: name for name, ids in labels.items() for i in ids}
rest = [v.co.copy() for v in mesh.vertices]
edges = {label: [] for label in labels}
for edge in mesh.edges:
    a, b = edge.vertices
    edges[membership[a]].append((a, b, (rest[a] - rest[b]).length))
poses = {'neutral': {}}
for finger in ['index', 'middle', 'ring', 'pinky', 'thumb']:
    for sign, label, angles in [(1, 'positive-stress', (75, 80, 40)), (-1, 'negative-stress', (75, 80, 40))]:
        poses[finger + '-' + label] = {('thumb.' if finger == 'thumb' else 'f_' + finger + '.') + seg + '.' + side: (angle * sign, 0, 0)
            for side in ['R', 'L'] for seg, angle in zip(['01', '02', '03'], angles)}
    for sign in [1, -1]:
        poses[finger + '-proximal-' + str(15 * sign)] = {('thumb.' if finger == 'thumb' else 'f_' + finger + '.') + '01.' + side: (15 * sign, 0, 0) for side in ['R', 'L']}
for name, rot in [('wrist-flex', (35, 0, 0)), ('wrist-extend', (-35, 0, 0)), ('wrist-deviation', (0, 0, 30)), ('reload-pronation', (0, 75, 0))]:
    poses[name] = {('forearm.' if name == 'reload-pronation' else 'hand.') + side: rot for side in ['R', 'L']}
scene = bpy.context.scene
scene.render.engine = 'CYCLES'; scene.cycles.samples = 16
scene.render.resolution_x = 1200; scene.render.resolution_y = 800; scene.render.resolution_percentage = 100
cam = scene.camera; cam.data.type = 'ORTHO'; cam.data.ortho_scale = .19
metrics = {}
for name, changes in poses.items():
    for bone in rig.pose.bones:
        bone.rotation_mode = 'QUATERNION'; bone.matrix_basis.identity()
    for bone, angles in changes.items():
        rig.pose.bones[bone].rotation_quaternion = Euler(tuple(math.radians(a) for a in angles)).to_quaternion()
    bpy.context.view_layer.update()
    evaluated = skin.evaluated_get(bpy.context.evaluated_depsgraph_get()); deformed = evaluated.to_mesh()
    coords = [v.co.copy() for v in deformed.vertices]
    metrics[name] = {label: {
        'max_edge_growth_m': max((coords[a] - coords[b]).length - length for a, b, length in region_edges),
        'max_displacement_m': max((coords[i] - rest[i]).length for i in labels[label]),
        'max_edge_ratio': max((coords[a] - coords[b]).length / length for a, b, length in region_edges if length > 1e-6)}
        for label, region_edges in edges.items()}
    evaluated.to_mesh_clear()
    if args.render and name in ['neutral', 'pinky-positive-stress', 'pinky-negative-stress', 'wrist-flex', 'reload-pronation']:
        for side, sign in [('right', 1), ('left', -1)]:
            cam.location = Vector((.30 * sign, -.10, .085))
            target = Vector((.03 * sign, -.064, -.044))
            cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
            scene.render.filepath = os.path.join(args.output_dir, name + '-' + side + '.png')
            bpy.ops.render.render(write_still=True)
result = {'capture': 'Scripted Blender inspection, local bone rotations in degrees; FBX axis conversion can reverse curl signs in Unity.',
    'identity': identity, 'bone_lengths': {b.name: b.length for b in rig.data.bones},
    'max_influences': max(sum(w > 1e-6 for w in v.values()) for v in weights),
    'max_normalization_error': max(abs(sum(v.values()) - 1) for v in weights),
    'region_weight_hashes': {label: digest([weights[i] for i in ids]) for label, ids in labels.items()},
    'deformations': metrics}
with open(os.path.join(args.output_dir, 'audit.json'), 'w') as stream:
    json.dump(result, stream, indent=2)
print('ARM_WEIGHT_AUDIT_COMPLETE', args.output_dir)
