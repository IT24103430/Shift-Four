"""Audit an actual Unity baked-grip JSON using Blender's triangle BVH.

Run with Blender --background --python tools/audit-pistol-grip.py --
  --geometry /path/to/actual-baked-grip.json --report /path/to/audit.json
This is read-only geometry QA; it never edits source meshes, weights or prefabs.
Pure finger regions omit mixed palm/web triangles, which are connected skin.
Visual front/side inspection complements this deliberately stated coverage.
"""
import argparse
import json
from pathlib import Path
import sys
from mathutils import Vector
from mathutils.bvhtree import BVHTree

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--geometry', type=Path, required=True)
parser.add_argument('--report', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
data = json.loads(args.geometry.read_text())
vertices = [Vector((v['x'], v['y'], v['z'])) for v in data['vertices']]
names = [b['name'] for b in data['bones']]
labels = []
for weight in data['weights']:
    roles = {}
    for index, value in zip(weight['bones'], weight['weights']):
        name = names[index]
        if name.startswith('f_'):
            key = name.split('.')[0] + '.' + name[-1]
        elif name.startswith('thumb.'):
            key = 'thumb.' + name[-1]
        else:
            key = 'palm.' + name[-1]
        roles[key] = roles.get(key, 0) + value
    labels.append(max(roles, key=roles.get))
indices = next(p['triangles'] for p in data['arms'] if p['name'] == 'GloveShell')
regions = {}
mixed = 0
for start in range(0, len(indices), 3):
    triangle = tuple(indices[start:start + 3])
    roles = [labels[v] for v in triangle]
    if roles[0] == roles[1] == roles[2]:
        regions.setdefault(roles[0], []).append(triangle)
    else:
        mixed += 1
trees = {name: BVHTree.FromPolygons(vertices, faces, all_triangles=True) for name, faces in regions.items()}
pistol = {}
for part in data['pistol']:
    points = [Vector((v['x'], v['y'], v['z'])) for v in part['vertices']]
    faces = [tuple(part['triangles'][i:i + 3]) for i in range(0, len(part['triangles']), 3)]
    pistol[part['name']] = BVHTree.FromPolygons(points, faces, all_triangles=True)
checks = []
for name, tree in sorted(trees.items()):
    if name.startswith('palm.'):
        continue
    for other, obstacle in sorted(trees.items()):
        if other == name or (other.startswith('palm.') and name[-1] == other[-1]):
            continue
        if not other.startswith('palm.') and other < name:
            continue
        checks.append({'region': name, 'obstacle': other, 'crossing_triangle_pairs': len(tree.overlap(obstacle))})
    for other, obstacle in sorted(pistol.items()):
        checks.append({'region': name, 'obstacle': other, 'crossing_triangle_pairs': len(tree.overlap(obstacle))})
result = {
    'geometry': str(args.geometry),
    'capture': 'Actual Unity SkinnedMeshRenderer.BakeMesh plus editor read-only pistol mesh data',
    'glove_shell_triangles': len(indices) // 3,
    'pure_region_triangles': {key: len(value) for key, value in sorted(regions.items())},
    'mixed_palm_web_triangles_not_claimed': mixed,
    'scope': 'Pure digit surfaces against other digits, opposite-hand palm and pistol. Mixed connected palm/web and same-digit self-contact are outside this test.',
    'crossing_triangle_pairs': sum(c['crossing_triangle_pairs'] for c in checks),
    'checks': checks,
}
args.report.parent.mkdir(parents=True, exist_ok=True)
args.report.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({k: v for k, v in result.items() if k not in ('checks', 'pure_region_triangles')}, indent=2))
