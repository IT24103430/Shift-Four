"""Auditable procedural Blender refinement, not evidence of student manual authorship.
Run with Blender 4.5: blender -b --python ArtSource/GeneratedModels/polish_nimthara_assets.py
Preserves legacy OBJ/blend/FBX; exports new GV variants. Units: metres.
"""
from pathlib import Path
import json
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
MODELS = ROOT / 'Assets/Models'
REPORT = {}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1.0


def material(name, color, metal=0.6, glow=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Metallic'].default_value = metal
    bsdf.inputs['Roughness'].default_value = .36
    bsdf.inputs['Emission Color'].default_value = (*color, 1)
    bsdf.inputs['Emission Strength'].default_value = glow
    return mat


def empty(name, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    return obj


def cube(name, pos, size, mat, parent=None):
    # Unity contract coordinates -> Blender (X, -Z, Y).
    bpy.ops.mesh.primitive_cube_add(size=1, location=(pos[0], -pos[2], pos[1]))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    if parent:
        world = obj.matrix_world.copy()
        obj.parent = parent
        obj.matrix_world = world
    bevel = obj.modifiers.new('Machined edges', 'BEVEL')
    bevel.width = .012
    bevel.segments = 2
    return obj


def prepare_uv():
    for obj in list(bpy.context.scene.objects):
        if obj.type != 'MESH':
            continue
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.uv.smart_project(island_margin=.02)
        bpy.ops.object.mode_set(mode='OBJECT')


def save(model, source):
    prepare_uv()
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    depsgraph = bpy.context.evaluated_depsgraph_get()
    triangles = 0
    for obj in meshes:
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        triangles += len(mesh.loop_triangles)
        evaluated.to_mesh_clear()
    source = ROOT / source
    source.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    bpy.ops.export_scene.fbx(filepath=str(MODELS / (model + '.fbx')), use_selection=False,
        object_types={'MESH', 'EMPTY'}, axis_forward='-Z', axis_up='Y',
        apply_unit_scale=True, bake_anim=False, add_leaf_bones=False, use_mesh_modifiers=True)
    REPORT[model] = dict(source=str(source.relative_to(ROOT)), mesh_objects=len(meshes),
        evaluated_triangles=triangles, uv_objects=sum(bool(o.data.uv_layers) for o in meshes),
        materials=len({m.name for o in meshes for m in o.data.materials if m}),
        provenance='Procedural refinement of repository geometry; no manual student authorship claimed')


reset()
bpy.ops.wm.obj_import(filepath=str(MODELS / 'SF_GuardDrone.obj'), use_split_groups=True)
# Consolidate static groups to reduce renderer overhead; rotors stay individually named.
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
static = [o for o in meshes if not o.name.startswith('Rotor')]
bpy.ops.object.select_all(action='DESELECT')
for obj in static:
    obj.select_set(True)
if static:
    bpy.context.view_layer.objects.active = static[0]
    bpy.ops.object.join()
    bpy.context.object.name = 'GuardArmouredChassis'
    # Weld the OBJ's per-face vertices, preserving hard angles with flat shading.
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=.00001)
    bpy.ops.object.mode_set(mode='OBJECT')
for index in range(4):
    blades = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.name.startswith('Rotor'+str(index))]
    bpy.ops.object.select_all(action='DESELECT')
    for obj in blades:
        obj.select_set(True)
    if len(blades) > 1:
        bpy.context.view_layer.objects.active = blades[0]
        bpy.ops.object.join()
        bpy.context.object.name = 'Rotor'+str(index)
for obj in list(bpy.context.scene.objects):
    if obj.type == 'MESH':
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
for mat in bpy.data.materials:
    if mat.use_nodes:
        bsdf = mat.node_tree.nodes.get('Principled BSDF')
        if bsdf:
            bsdf.inputs['Metallic'].default_value = .55
            bsdf.inputs['Roughness'].default_value = .32
save('SF_GuardDrone_GV', 'ArtSource/Models/security-drone/SF_GuardDrone_GV.blend')

reset()
metal = material('GV_Graphite', (.12,.16,.20))
trim = material('GV_Titanium', (.35,.41,.46))
warning = material('GV_Hazard', (.95,.52,.08), .15)
red = material('GV_Status', (.9,.08,.035), .1, 2)
glass = material('GV_Terminal', (.035,.55,.65), .1, 1.5)
assembly = empty('Door assembly')
for side in (-1, 1):
    cube('FrameLeft' if side < 0 else 'FrameRight', (side*2.1,1.5,0), (.22,3,.55), trim, assembly)
    panel = empty('LeftPanel' if side < 0 else 'RightPanel', assembly)
    panel.location = (side*1, 0, 1.5)
    cube('PanelCore', (side*1,1.5,0), (2,3,.24), metal, panel)
    for height in (.35,1.0,2.0,2.65):
        cube('ArmourRib', (side*1,height,-.17), (1.7,.13,.12), trim, panel)
    cube('HazardStripe', (side*1.8,1.5,-.18), (.1,2.6,.045), warning, panel)
    cube('StatusLight', (side*1.65,2.8,-.19), (.25,.10,.04), red, panel)
    cube('LeftBolt' if side < 0 else 'RightBolt', (side*.28,1.5,-.26), (.5,.18,.18), trim, assembly)
cube('Header', (0,3.15,0), (4.45,.3,.6), trim, assembly)
cube('HeaderStatus', (0,3.15,-.32), (1.8,.065,.04), red, assembly)
cube('AccessTerminal', (2.48,1.35,-.2), (.44,.75,.28), metal, assembly)
cube('TerminalScreen', (2.48,1.46,-.36), (.31,.37,.035), glass, assembly)
cube('Reader', (2.48,1.12,-.36), (.2,.09,.045), warning, assembly)
save('SF_HeavySecurityDoor_GV', 'ArtSource/Models/security-sliding-door/SF_HeavySecurityDoor_GV.blend')
(ROOT / 'docs/QA/NIMTHARA_ASSET_METRICS.json').write_text(json.dumps(REPORT, indent=2)+'\n')
print(json.dumps(REPORT, indent=2))
