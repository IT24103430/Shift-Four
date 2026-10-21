"""Read-only Blender source audit. Outputs are automated QA, never authorship evidence.

blender -b --python tools/audit-gv-models.py -- --output Logs/GVCompletion/models
"""
import argparse
import hashlib
import json
import math
import sys
from datetime import datetime, timezone
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
MODELS = {
    "security-drone": "SF_GuardDrone_GV",
    "security-sliding-door": "SF_HeavySecurityDoor_GV",
}


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inspect_export(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    depsgraph = bpy.context.evaluated_depsgraph_get()
    triangles = 0
    names = []
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        names.append(obj.name)
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        try:
            mesh.calc_loop_triangles()
            triangles += len(mesh.loop_triangles)
        finally:
            evaluated.to_mesh_clear()
    return {"mesh_objects": len(names), "mesh_names": sorted(names), "triangles": triangles}


def inspect_model(folder, name, output):
    source = ROOT / "ArtSource/Models" / folder / (name + ".blend")
    export = ROOT / "Assets/Models" / (name + ".fbx")
    bpy.ops.wm.open_mainfile(filepath=str(source))
    depsgraph = bpy.context.evaluated_depsgraph_get()
    objects, corners, uv_lines = [], [], []
    for obj in sorted(bpy.context.scene.objects, key=lambda item: item.name):
        if obj.type != "MESH":
            continue
        mesh = obj.data
        corners.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
        bm = bmesh.new()
        bm.from_mesh(mesh)
        boundary = sum(edge.is_boundary for edge in bm.edges)
        non_manifold = sum(not edge.is_manifold for edge in bm.edges)
        loose = sum(not vertex.link_edges for vertex in bm.verts)
        bm.free()
        evaluated = obj.evaluated_get(depsgraph)
        evaluated_mesh = evaluated.to_mesh()
        try:
            evaluated_mesh.calc_loop_triangles()
            triangles = len(evaluated_mesh.loop_triangles)
        finally:
            evaluated.to_mesh_clear()
        uv_layer = mesh.uv_layers.active
        invalid_uvs = 0
        zero_area_uv_faces = 0
        zero_area_uv_face_indices = []
        if uv_layer:
            for face in mesh.polygons:
                points = [tuple(uv_layer.data[index].uv) for index in face.loop_indices]
                if any(not math.isfinite(value) for point in points for value in point):
                    invalid_uvs += 1
                    continue
                area = abs(sum(a[0] * b[1] - b[0] * a[1]
                               for a, b in zip(points, points[1:] + points[:1]))) * .5
                zero_area_uv_faces += area < 1e-12
                if area < 1e-12:
                    zero_area_uv_face_indices.append(face.index)
                coords = " ".join(f"{40 + u * 720:.3f},{800 - v * 720:.3f}" for u, v in points)
                uv_lines.append(f'<polygon points="{coords}"/>')
        objects.append({
            "name": obj.name, "parent": obj.parent.name if obj.parent else None,
            "vertices": len(mesh.vertices), "edges": len(mesh.edges), "faces": len(mesh.polygons),
            "evaluated_triangles": triangles, "boundary_edges": boundary,
            "non_manifold_edges_including_boundary": non_manifold, "loose_vertices": loose,
            "uv_layers": list(mesh.uv_layers.keys()), "invalid_uv_faces": invalid_uvs,
            "zero_area_uv_faces": zero_area_uv_faces,
            "zero_area_uv_face_indices": zero_area_uv_face_indices,
            "local_scale": list(obj.scale), "local_location": list(obj.location),
            "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
            "modifiers": [{"name": mod.name, "type": mod.type} for mod in obj.modifiers],
        })
    if not corners:
        raise RuntimeError(f"No meshes found: {source}")
    low = [min(point[i] for point in corners) for i in range(3)]
    high = [max(point[i] for point in corners) for i in range(3)]
    (output / (folder + "-uv.svg")).write_text(
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 840 850">'
        '<rect width="840" height="850" fill="#172027"/>'
        '<text x="20" y="25" fill="white">Automated active UV overlay; not personal modeling evidence</text>'
        '<rect x="40" y="80" width="720" height="720" fill="none" stroke="#aaa"/>'
        '<g fill="none" stroke="#68cfdf" stroke-width="0.6">' + "".join(uv_lines) + '</g>'
        '<text x="20" y="830" fill="white">Objects may use separate material UV spaces; overlay is not an overlap test.</text></svg>',
        encoding="utf-8")
    result = {
        "source": source.relative_to(ROOT).as_posix(), "source_sha256": sha256(source),
        "export": export.relative_to(ROOT).as_posix(), "export_exists": export.is_file(),
        "export_sha256": sha256(export) if export.is_file() else None,
        "unit_system": bpy.context.scene.unit_settings.system,
        "unit_scale": bpy.context.scene.unit_settings.scale_length,
        "world_bounds_blender_axes": {"min": low, "max": high,
                                      "dimensions": [b - a for a, b in zip(low, high)]},
        "evaluated_triangles": sum(obj["evaluated_triangles"] for obj in objects),
        "objects": objects,
    }
    result["fbx_reimport"] = inspect_export(export) if export.is_file() else None
    result["source_export_triangle_count_matches"] = (
        result["fbx_reimport"]["triangles"] == result["evaluated_triangles"]
        if result["fbx_reimport"] else False)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default="Logs/GVCompletion/models")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    output = (ROOT / args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    report = {
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "blender_version": bpy.app.version_string,
        "classification": "Automated read-only QA; no personal authorship or manual UV acceptance claimed.",
        "limitations": ["UV overlap, distortion and texel density require visual review.",
                        "Open boundaries may be intentional; counts are review signals, not failures.",
                        "FBX hashes identify files; they do not establish source/export equivalence.",
                        "Bounds include all source scene meshes; Blender axes are not Unity axes."],
        "models": [inspect_model(folder, name, output) for folder, name in MODELS.items()],
    }
    (output / "model-audit.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    failures = [obj["name"] for model in report["models"] for obj in model["objects"]
                if not obj["uv_layers"] or obj["invalid_uv_faces"]]
    missing_exports = [model["export"] for model in report["models"] if not model["export_exists"]]
    print(json.dumps({"report": str(output / "model-audit.json"),
                      "triangles": {m["source"]: m["evaluated_triangles"] for m in report["models"]},
                      "collapsed_uv_faces_for_review": {obj["name"]: obj["zero_area_uv_face_indices"]
                          for model in report["models"] for obj in model["objects"]
                          if obj["zero_area_uv_faces"]},
                      "source_export_triangle_counts_match": all(
                          model["source_export_triangle_count_matches"] for model in report["models"]),
                      "missing_or_invalid_uv_objects": failures, "missing_exports": missing_exports}))
    if failures or missing_exports:
        raise RuntimeError("Model audit found missing/invalid UVs or missing FBX files")


if __name__ == "__main__":
    main()
