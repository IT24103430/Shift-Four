"""Automated mesh previews; NOT screenshots of student modeling work."""
from pathlib import Path
import bpy
from mathutils import Vector
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs/QA/previews'
OUT.mkdir(parents=True, exist_ok=True)
for name, source in [('Guard', 'security-drone/SF_GuardDrone_GV.blend'), ('Door', 'security-sliding-door/SF_HeavySecurityDoor_GV.blend')]:
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'ArtSource/Models' / source))
    scene = bpy.context.scene
    corners = [o.matrix_world @ Vector(c) for o in scene.objects if o.type == 'MESH' for c in o.bound_box]
    low = Vector(tuple(min(c[i] for c in corners) for i in range(3)))
    high = Vector(tuple(max(c[i] for c in corners) for i in range(3)))
    target = (low+high)*.5
    size = max(high-low)
    bpy.ops.object.camera_add(location=target+Vector((.9, 1.6, .9))*size)
    camera = bpy.context.object
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = size*1.4
    scene.camera = camera
    for position, energy, scale in [((2,3,4),1300,3), ((-3,1,2),900,2), ((0,-3,3),1600,2)]:
        bpy.ops.object.light_add(type='AREA', location=target+Vector(position)*size*.5)
        light = bpy.context.object
        light.data.energy = energy
        light.data.shape='DISK'
        light.data.size=scale*size*.5
        light.rotation_euler=(target-light.location).to_track_quat('-Z','Y').to_euler()
    if scene.world is None: scene.world = bpy.data.worlds.new('Preview world')
    scene.world.color=(.11,.11,.11)
    scene.render.engine='CYCLES'
    scene.cycles.samples=24
    scene.render.resolution_x=800
    scene.render.resolution_y=800
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.render.film_transparent=False
    scene.render.filepath=str(OUT/(name+'.png'))
    bpy.ops.render.render(write_still=True)
