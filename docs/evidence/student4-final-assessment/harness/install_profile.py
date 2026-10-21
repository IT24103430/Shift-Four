"""Measurement-only injection into disposable snapshots; refuses the real worktrees."""
from pathlib import Path
import re, sys, hashlib, json, shutil
root=Path(sys.argv[1]).resolve()
assert str(root).startswith(('/tmp/ShiftFour-Student4-','/private/tmp/ShiftFour-Student4-'))
harness=Path(__file__).parent
shutil.copyfile(harness/'Student4AssessmentPlayModeTests.cs',root/'Assets/Tests/PlayMode/Student4AssessmentPlayModeTests.cs')
markers={
 'AI/Core/WaypointGraph.cs':[('FindRoute','Route')],
 'AI/Movement/DroneMotor.cs':[('Update','Motor')],
 'AI/Core/DroneBrain.cs':[('TryShoot','Fire')],
 'Gameplay/DroneRotorVisuals.cs':[('LateUpdate','Animation'),('Update','Animation')],
 'AI/Movement/DroneBody.cs':[('LateUpdate','Body')],
 'Gameplay/DroneWeaponEffects.cs':[('Tick','Effects')],
 'Gameplay/DroneWreckMotion.cs':[('Create','WreckCreate'),('Update','WreckTick')],
}
for agent in ['Scout','Guard','Flanker','Interceptor']:
 markers[f'AI/Agents/{agent}Brain.cs']=[('Decide','Decision')]
manifest=[]
for name,methods in markers.items():
 p=root/'Assets/Scripts'/name
 if not p.exists():continue
 original=p.read_text();new=original
 for method,label in methods:
  pattern=r'((?:public|private|protected)\s+(?:(?:static|override|virtual)\s+)*[\w<>\[\]]+\s+'+method+r'\([^;{}]*\)\s*\{)'
  new,n=re.subn(pattern,lambda m:m[1]+f'\n            using var student4QaSample = ShiftFour.Student4QaMarkers.{label}.Auto();',new)
  if n>1:raise RuntimeError((name,method,n))
 p.write_text(new);manifest.append({'path':name,'source_sha256':hashlib.sha256(original.encode()).hexdigest(),'instrumented_sha256':hashlib.sha256(new.encode()).hexdigest()})
labels=sorted({label for ms in markers.values() for _,label in ms})
p=root/'Assets/Scripts/Student4QaMarkers.cs'
p.write_text('namespace ShiftFour { public static class Student4QaMarkers {\n'+''.join(f'public static readonly Unity.Profiling.ProfilerMarker {label}=new Unity.Profiling.ProfilerMarker("Student4QA.{label}");\n' for label in labels)+'}}\n')
(root/'qa-instrumentation.json').write_text(json.dumps(manifest,indent=2))
