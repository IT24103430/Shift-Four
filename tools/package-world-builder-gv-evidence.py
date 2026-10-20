"""Package actual Unity results; never manufacture test outcomes or screenshots."""
from pathlib import Path
import csv
import hashlib
import html
import json
import shutil
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PROJECT = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT / "Temp/WorldBuilderVerification"
SOURCE = PROJECT / "Logs/WorldBuilderGV"
DEST = ROOT / "docs/evidence/world-builder-gv"
DEST.mkdir(parents=True, exist_ok=True)

for path in SOURCE.glob("*.png"):
    shutil.copy2(path, DEST / path.name)
for name in ("environment-audit.json", "EditMode.xml", "PlayMode.xml", "build-result.txt"):
    shutil.copy2(SOURCE / name, DEST / name)
standalone = SOURCE / "StandaloneAcceptance"
if standalone.is_dir():
    (DEST / "standalone").mkdir(exist_ok=True)
    for path in standalone.iterdir():
        if path.suffix in (".csv", ".txt", ".png", ".log"):
            shutil.copy2(path, DEST / "standalone" / path.name)

summary = {"suites": {}, "audit": json.loads((SOURCE / "environment-audit.json").read_text())}
for mode in ("EditMode", "PlayMode"):
    result = ET.parse(SOURCE / f"{mode}.xml").getroot()
    summary["suites"][mode] = {key: result.get(key) for key in ("result", "total", "passed", "failed", "skipped", "duration")}
    if result.get("result") != "Passed":
        raise SystemExit(f"Cannot package a passing claim: {mode} is {result.get('result')}")
(DEST / "test-summary.json").write_text(json.dumps(summary, indent=2) + "\n")

rooms = ("Checkpoint", "Server", "Control", "Storage")
parts = ["""<!doctype html><html lang="en"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Shift Four — World Builder evidence</title><style>
body{background:#11191f;color:#edf0ec;font:16px/1.6 system-ui;margin:0 auto;max-width:1500px;padding:36px}
h1,h2{line-height:1.2}p{max-width:80ch;color:#bbc8cc}a{color:#85d4d4}
.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0 0 24px}img{width:100%;display:block;border:1px solid #3b4b54}
figcaption{padding:8px 0;color:#bac7ca}section{margin-top:40px}
@media(max-width:750px){.pair{grid-template-columns:1fr}body{padding:18px}}
</style><h1>Shift Four / World Builder finish</h1>
<p>Actual Unity 6000.6.4f1 renders of the saved scene. Agent-authored finishing work.
These are controlled camera views, not proof of a manual combat playthrough or personal student authorship.</p>
<p>"""]
parts.append(" · ".join(f"<a href='#{room}'>{room}</a>" for room in rooms) + "</p>")
for room in rooms:
    parts.append(f"<section id='{room}'><h2>{room}</h2><div class='pair'>")
    for state in ("before", "after"):
        name = f"{state}-{room}.png"
        parts.append(f"<figure><a href='{name}'><img src='{name}' alt='{html.escape(room)} {state}' loading='lazy'></a><figcaption>{state.title()} / same camera</figcaption></figure>")
    parts.append(f"</div><figure><a href='after-{room}-walls.png'><img src='after-{room}-walls.png' alt='{room} finished walls' loading='lazy'></a><figcaption>Finished walls and depth-tested room signage</figcaption></figure></section>")
parts.append("<section><h2>Layout and navigation</h2><p>Roofs are hidden temporarily for these evidence views. The navigation overlay shows the playable ground layer. The saved game remains enclosed.</p><div class='pair'>")
for name, caption in (("layout-roof-hidden-for-evidence.png", "Connected layout"), ("navigation-roof-hidden-for-evidence.png", "Ground NavMesh overlay")):
    parts.append(f"<figure><a href='{name}'><img src='{name}' alt='{caption}' loading='lazy'></a><figcaption>{caption}</figcaption></figure>")
parts.append("</div></section><section><h2>Environment showcase</h2><p>A 16-second sequence of actual room captures: Checkpoint, Server, Control, Storage. Supporting visual evidence, not a manual walkthrough or the final assessed video.</p><video controls preload='metadata' style='width:100%' src='environment-showcase.mp4'></video></section><p><a href='../../WORLD_BUILDER_GV_FINISH.md'>Verification details and limits</a> · <a href='test-summary.json'>Machine-readable results</a></p></html>")
(DEST / "index.html").write_text("\n".join(parts), encoding="utf-8")

assets = [ROOT / "Assets/Scenes/MainLab.unity", ROOT / "Assets/Scenes/MainLab_NavMesh.asset",
          ROOT / "Assets/Editor/WorldBuilderEnvironmentBuilder.cs", ROOT / "Assets/Shaders/WorldBuilderSignage.shader",
          ROOT / "Assets/Scripts/Gameplay/LabNavigationStartup.cs"]
assets.extend(ROOT / f"Assets/Prefabs/Sections/{room}.prefab" for room in rooms)
assets.extend(path for path in (ROOT / "Assets/Environment/WorldBuilder").rglob("*") if path.is_file())
hashes = {str(path.relative_to(ROOT)).replace("\\", "/"): hashlib.sha256(path.read_bytes()).hexdigest() for path in assets if path.is_file()}
(DEST / "delivered-sha256.json").write_text(json.dumps(hashes, indent=2) + "\n")
print(json.dumps(summary, indent=2))
