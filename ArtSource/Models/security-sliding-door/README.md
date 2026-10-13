# Heavy horizontal security gate — current model

Source: SF_HeavySecurityDoor_GV.blend. Generator: ../../GeneratedModels/polish_nimthara_assets.py. Export: Assets/Models/SF_HeavySecurityDoor_GV.fbx.

Procedural Blender work; do not claim manual student authorship. Both leaves cover a 4m-wide, 3m-high opening. Frame extends to x ±2.21m; terminal projects to x 2.7m. Ground pivot at origin; +Y up after Unity import. Leaves move 2.15m outward along assembly X, bolts .35m first and then follow their leaves. Keep stable groups Door assembly, LeftPanel, RightPanel, LeftBolt, RightBolt. Frame and terminal stay still.

Mesh-only export; no generated colliders. Unity LabDoor owns the single passage collider and carving obstacle. Installer normalizes FBX wrapper transforms into metre-scale animation pivots; never reset imported transforms blindly. Each gate root's yaw controls orientation.

Inspect smart-projected UVs and editable bevels in Blender. Flat material palette is intentional, not a texture atlas. See docs/ART_ASSET_PROVENANCE.md for metrics and evidence requirements.
