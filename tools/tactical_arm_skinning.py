"""Anatomically constrain garment weights without changing mesh or bind geometry.

Run in Blender. Also called by prepare-tactical-arms.py after joining the skin.
Region identification uses connectivity, material names and existing bone names,
never hard-coded vertex indices. Original CC0 sources are never written.
"""
import argparse
import json
import os
import sys
import bpy


def components(mesh):
    adjacent = [set() for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        adjacent[a].add(b)
        adjacent[b].add(a)
    remaining = set(range(len(adjacent)))
    result = []
    while remaining:
        seed = min(remaining)
        remaining.remove(seed)
        todo, indices = [seed], []
        while todo:
            index = todo.pop()
            indices.append(index)
            for neighbor in sorted(adjacent[index] & remaining):
                remaining.remove(neighbor)
                todo.append(neighbor)
        result.append(sorted(indices))
    return result, adjacent


def normalized(weights):
    values = sorted(((name, weight) for name, weight in weights.items() if weight > 1e-7),
                    key=lambda item: (-item[1], item[0]))[:4]
    total = sum(weight for _, weight in values)
    if total <= 0:
        raise ValueError('No anatomical influence remains')
    return {name: weight / total for name, weight in values}


def write_weights(skin, index, weights):
    for group_index in [g.group for g in skin.data.vertices[index].groups]:
        skin.vertex_groups[group_index].remove([index])
    for name, weight in normalized(weights).items():
        skin.vertex_groups[name].add([index], weight, 'REPLACE')


def repair(skin):
    """Repair reinforcement weights while preserving native skin and bind geometry."""
    mesh = skin.data
    stamp = 'sf_anatomical_skin_weights_v1'
    if stamp in mesh:
        return dict(json.loads(mesh[stamp]), already_repaired=True)
    groups = {g.index: g.name for g in skin.vertex_groups}
    original = [{groups[g.group]: g.weight for g in v.groups if g.weight > 0}
                for v in mesh.vertices]
    regions, adjacent = components(mesh)
    vertex_materials = [set() for _ in mesh.vertices]
    for face in mesh.polygons:
        for index in face.vertices:
            vertex_materials[index].add(mesh.materials[face.material_index].name)
    report = {'algorithm': 'anatomical-panel-remap-v1', 'regions': [], 'changed_vertices': 0}
    for indices in regions:
        materials = set().union(*(vertex_materials[i] for i in indices))
        if materials not in ({'GloveReinforcement'}, {'KnuckleRubber'}):
            continue
        mass = {}
        for i in indices:
            for name, weight in original[i].items():
                mass[name] = mass.get(name, 0) + weight
        side = max(('R', 'L'), key=lambda s: sum(w for n, w in mass.items() if n.endswith('.' + s)))
        dorsal = materials == {'GloveReinforcement'}
        finger = None if dorsal else max(('middle', 'ring', 'pinky'),
            key=lambda f: sum(w for n, w in mass.items() if n.startswith('f_' + f + '.')))
        values = {}
        for i in indices:
            remapped = {}
            for name, weight in original[i].items():
                if not name.endswith('.' + side):
                    raise ValueError('Panel has opposite-hand weights')
                target = name
                if dorsal:
                    if name.startswith('f_'):
                        target = 'palm_' + name.split('.')[0][2:] + '.' + side
                    elif name.startswith('thumb.'):
                        target = 'hand.' + side
                else:
                    # Preserve the guard's own phalangeal transition. A fitted,
                    # already curled pinky guard can cover the middle joint.
                    if name.startswith('f_') and not name.startswith('f_' + finger + '.'):
                        target = 'f_' + finger + '.01.' + side
                    elif name.startswith('palm_'):
                        target = 'palm_' + finger + '.' + side
                    elif name.startswith('thumb.'):
                        target = 'hand.' + side
                remapped[target] = remapped.get(target, 0) + weight
            values[i] = remapped
        # Smooth over surface neighbors. Pair front/back vertices of each
        # Solidify panel to retain identical weights through its thickness.
        # Finding coincident surface pairs spatially also works after reordering.
        pairs = {}
        for i in indices:
            candidates = [j for j in adjacent[i] if (mesh.vertices[i].co - mesh.vertices[j].co).length < .0011]
            pairs[i] = candidates
        names = sorted(set().union(*(set(w) for w in values.values())))
        for _ in range(6):
            smoothed = {}
            for i in indices:
                neighbors = adjacent[i]
                smoothed[i] = {n: .5 * values[i].get(n, 0) + .5 *
                    sum(values[j].get(n, 0) for j in neighbors) / len(neighbors) for n in names}
            values = smoothed
        # Pair averaging keeps reinforcement thickness from shearing.
        paired_values = {}
        for i in indices:
            partners = pairs[i]
            paired_values[i] = {n: sum(values[j].get(n, 0) for j in [i] + partners) /
                               (1 + len(partners)) for n in names}
        values = paired_values
        changed = 0
        for i in indices:
            weights = normalized(values[i])
            if any(abs(weights.get(n, 0) - original[i].get(n, 0)) > 1e-6
                   for n in set(weights) | set(original[i])):
                changed += 1
            write_weights(skin, i, weights)
        report['changed_vertices'] += changed
        report['regions'].append({'surface': 'dorsal' if dorsal else finger + '-guard',
            'side': side, 'vertices': len(indices), 'changed_vertices': changed,
            'before_influence_mass': mass,
            'after_bones': sorted(set().union(*(set(normalized(values[i])) for i in indices)))})
    if len(report['regions']) != 8:
        raise ValueError('Expected two dorsal panels and six guards; refusing unknown mesh')
    report['max_influences'] = max(sum(g.weight > 1e-6 for g in v.groups) for v in mesh.vertices)
    report['max_normalization_error'] = max(abs(sum(g.weight for g in v.groups) - 1) for v in mesh.vertices)
    if report['max_influences'] > 4 or report['max_normalization_error'] > 1e-6:
        raise ValueError('Invalid normalized skin weights: ' + str(report))
    mesh[stamp] = json.dumps(report, sort_keys=True)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input-blend', required=True)
    parser.add_argument('--output-dir', required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    source, out = os.path.abspath(args.input_blend), os.path.abspath(args.output_dir)
    if os.path.dirname(source) == out:
        raise ValueError('Use a staging directory, never overwrite the input')
    os.makedirs(out, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=source)
    skin = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
    rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    report = repair(skin)
    with open(os.path.join(out, 'skin-weight-repair.json'), 'w') as stream:
        json.dump(report, stream, indent=2)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, 'SF_TacticalArms.blend'))
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    skin.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'SF_TacticalArms.fbx'),
        use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
        bake_anim=False, axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL', armature_nodetype='NULL', use_armature_deform_only=True)
    print('STAGED_WEIGHT_REPAIR', json.dumps(report))


if __name__ == '__main__':
    main()
