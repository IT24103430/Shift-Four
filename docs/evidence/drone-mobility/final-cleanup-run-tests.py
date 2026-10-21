from pathlib import Path
import os,subprocess,json,datetime,xml.etree.ElementTree as E
qa=Path('/tmp/ShiftFour-StorageInspection-87d5803');out=Path('/Users/dev/Developer/ShiftFour-drone-mobility-evidence/20261021/final-cleanup');unity='/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity'
a='ShiftFour.Tests.InterceptorMainLabPlayModeTests.MainLab_BaselineComparison_IdenticalMotorRouteAttempts';b='ShiftFour.Tests.InterceptorMainLabPlayModeTests.MainLab_GenuineVisibleMotion_DrivesActualBrainDecisions'
runs=[('EditMode',['-assemblyNames','ShiftFour.Movement.EditModeTests;ShiftFour.Navigation.EditModeTests']),('PlayMode',['-testFilter','StorageCoverPlayModeTests'])]
records=[]
for name,args in runs:
 d=out/name;d.mkdir(exist_ok=True)
 cmd=[unity,'-batchmode','-projectPath',str(qa),'-runTests','-testPlatform',name,*args,'-testResults',str(d/'results.xml'),'-logFile',str(d/'Editor.log')]
 env=os.environ.copy()
 for key in ['SHIFTFOUR_EVIDENCE','SHIFTFOUR_INTEGRATION_EVIDENCE','SHIFTFOUR_ARMS_MATERIAL_EVIDENCE','SHIFTFOUR_MULTI_GRIP_EVIDENCE','SHIFTFOUR_GRIP_EVIDENCE','SHIFTFOUR_MOVEMENT_EVIDENCE','SHIFTFOUR_DEMO_OUTPUT','SHIFTFOUR_PISTOL_EVIDENCE','SHIFTFOUR_FINAL_EVIDENCE','SHIFTFOUR_JUMP_EVIDENCE','SHIFTFOUR_IDLE_EVIDENCE','SHIFTFOUR_ANIMATION_EVIDENCE','SHIFTFOUR_EQUIP_EVIDENCE','SHIFTFOUR_FRAMING_EVIDENCE','SHIFTFOUR_RELOAD_EVIDENCE','SHIFTFOUR_ARMS_EVIDENCE','SHIFTFOUR_FIRING_EVIDENCE']:env[key]=str(d/'artifacts'/key.lower())
 record={'name':name,'command':cmd,'cwd':str(qa),'started_utc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
 print('START',name,flush=True)
 with (d/'launcher.log').open('w') as log:p=subprocess.run(cmd,cwd=qa,env=env,stdout=log,stderr=subprocess.STDOUT)
 record.update(exit_code=p.returncode,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
 if (d/'results.xml').exists():
  r=E.parse(d/'results.xml').getroot();record['counts']={k:r.get(k) for k in ['total','passed','failed','skipped','inconclusive']}
  record['failures']=[{'name':t.get('fullname'),'message':t.findtext('./failure/message',''),'stack':t.findtext('./failure/stack-trace','')} for t in r.findall('.//test-case[@result="Failed"]')]
 records.append(record);(out/'matrix.json').write_text(json.dumps(records,indent=2))
 print('FINISH',name,record.get('counts',{}),flush=True)
