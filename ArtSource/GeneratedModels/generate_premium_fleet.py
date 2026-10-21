"""Deterministic original low-poly hard-surface fleet; output OBJ/MTL with UVs.
Run: python ArtSource/GeneratedModels/generate_premium_fleet.py
No Blender/Python packages needed; imported objects remain editable in Blender.
"""
from pathlib import Path
from math import pi, sin, cos
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets'/'Models'
OUT.mkdir(parents=True,exist_ok=True)
PALETTE={
 'Graphite':(.105,.145,.195),'Carbon':(.025,.038,.055),'Titanium':(.36,.43,.49),
 'Panel':(.19,.24,.30),'Edge':(.53,.61,.67),'Rubber':(.055,.06,.075),
 'ScoutGlow':(.055,.83,.98),'FlankerGlow':(1.0,.38,.065),
 'GuardGlow':(.98,.12,.17),'InterceptorGlow':(.52,.28,1.0),
 'Glass':(.065,.23,.36),'Warning':(.95,.72,.13)}
(OUT/'ShiftFourPalette.mtl').write_text(''.join(f'newmtl {k}\nKa 0.015 0.015 0.015\nKd {v[0]} {v[1]} {v[2]}\nKs 0.5 0.5 0.5\nNs 100\nillum 2\n\n' for k,v in PALETTE.items()),encoding='utf-8',newline='\n')
class Mesh:
 def __init__(self,name):self.name=name;self.v=[];self.uv=[];self.f=[];self.group='Chassis';self.material='Graphite'
 def set(self,group=None,mat=None):
  if group:self.group=group
  if mat:self.material=mat
 def face(self,p):
  idx=len(self.v)+1;self.v.extend(p)
  self.uv.extend((0,0) if i==0 else (1,0) if i==1 else (1,1) if i==2 else (0,1) for i in range(len(p)))
  self.f.append((self.group,self.material,tuple(range(idx,idx+len(p)))))
 def loft(self,rings):
  # rings are evenly sized arrays of points; curved/beveled panels with explicit UVs
  for a,b in zip(rings,rings[1:]):
   for i in range(len(a)):j=(i+1)%len(a);self.face((a[i],a[j],b[j],b[i]))
  self.face(tuple(reversed(rings[0])));self.face(tuple(rings[-1]))
 def block(self,x,y,z,w,h,d,bevel=.0):
  bevel=min(bevel,w*.35,d*.35)
  def ring(py,ww,dd):
   X=ww/2;Z=dd/2;B=bevel
   return [(x-X+B,py,z-Z),(x+X-B,py,z-Z),(x+X,py,z-Z+B),(x+X,py,z+Z-B),
    (x+X-B,py,z+Z),(x-X+B,py,z+Z),(x-X,py,z+Z-B),(x-X,py,z-Z+B)]
  self.loft([ring(y-h/2,w-2*bevel,d-2*bevel),ring(y-h/2+bevel,w,d),ring(y+h/2-bevel,w,d),ring(y+h/2,w-2*bevel,d-2*bevel)] if bevel else [ring(y-h/2,w,d),ring(y+h/2,w,d)])
 def silhouette(self,profile,ybot,ytop,scale_top=.88):
  def ring(y,s):return [(x*s,y,z*s) for x,z in profile]
  self.loft([ring(ybot,.90),ring(ybot+.045,1),ring(ytop-.05,1),ring(ytop,scale_top)])
 def cylinder(self,x,y,z,r,h,n=16):
  def ring(Y,R):return [(x+R*cos(2*pi*i/n),Y,z+R*sin(2*pi*i/n)) for i in range(n)]
  self.loft([ring(y-h/2,r*.88),ring(y-h/2+.025,r),ring(y+h/2-.025,r),ring(y+h/2,r*.88)])
 def ring(self,x,y,z,ro,ri,h,n=20):
  for i in range(n):
   a,b=2*pi*i/n,2*pi*(i+1)/n
   def p(R,t,Y):return(x+R*cos(t),Y,z+R*sin(t))
   self.face((p(ro,a,y+h/2),p(ro,b,y+h/2),p(ri,b,y+h/2),p(ri,a,y+h/2)))
   self.face((p(ro,b,y-h/2),p(ro,a,y-h/2),p(ri,a,y-h/2),p(ri,b,y-h/2)))
   self.face((p(ro,a,y-h/2),p(ro,b,y-h/2),p(ro,b,y+h/2),p(ro,a,y+h/2)))
   self.face((p(ri,b,y-h/2),p(ri,a,y-h/2),p(ri,a,y+h/2),p(ri,b,y+h/2)))
 def beam(self,a,b,width=.1,thick=.075):
  x=(a[0]+b[0])/2;z=(a[1]+b[1])/2
  dx=b[0]-a[0];dz=b[1]-a[1];L=(dx*dx+dz*dz)**.5
  if not L:return
  px=-dz/L*width/2;pz=dx/L*width/2
  poly=[(a[0]+px,a[1]+pz),(b[0]+px,b[1]+pz),(b[0]-px,b[1]-pz),(a[0]-px,a[1]-pz)]
  self.loft([[(u,-thick/2,v) for u,v in poly],[(u,thick/2,v) for u,v in poly]])
 def save(self):
  lines=['# Original procedural Shift Four hard-surface asset, UV mapped, 2026','mtllib ShiftFourPalette.mtl']
  lines.extend(f'v {x:.6f} {y:.6f} {z:.6f}' for x,y,z in self.v)
  lines.extend(f'vt {u:.6f} {v:.6f}' for u,v in self.uv)
  prior=None
  for group,material,ids in self.f:
   if (group,material)!=prior:
    lines+=['g '+group,'usemtl '+material];prior=(group,material)
   lines.append('f '+' '.join(f'{j}/{j}' for j in ids))
  path=OUT/(self.name+'.obj');path.write_text('\n'.join(lines)+'\n',encoding='utf-8',newline='\n')
  print(self.name,len(self.v),'vertices',len(self.f),'faces')
  return path
SHAPES={
 'Scout':[(-.34,.54),(.34,.54),(.53,.23),(.43,-.40),(.18,-.65),(-.18,-.65),(-.43,-.40),(-.53,.23)],
 'Flanker':[(-.28,.73),(.28,.73),(.40,.28),(1.05,-.20),(.94,-.40),(.30,-.26),(.25,-.68),(-.25,-.68),(-.30,-.26),(-.94,-.40),(-1.05,-.20),(-.40,.28)],
 'Guard':[(-.68,.48),(.68,.48),(.86,.20),(.86,-.45),(.57,-.70),(-.57,-.70),(-.86,-.45),(-.86,.20)],
 'Interceptor':[(-.23,.97),(.23,.97),(.32,.28),(.82,-.41),(.42,-.48),(.19,-.77),(-.19,-.77),(-.42,-.48),(-.82,-.41),(-.32,.28)]}
for typ,profile in SHAPES.items():
 m=Mesh('SF_'+typ+'Drone');g=typ+'Glow'
 m.set('Chassis','Graphite');m.silhouette(profile,-.19,.24,.84)
 m.set('ArmourTop','Panel');m.block(0,.255,-.05,.65 if typ!='Guard' else 1.1,.10,.78,.065)
 m.set('Spine','Titanium');m.block(0,.32,-.11,.12,.06,.64,.02)
 m.set('FrontSensorHousing','Carbon');m.block(0,.02,.60 if typ!='Interceptor' else .84,.42,.22,.12,.045)
 m.set('OpticalLens','Glass');m.block(0,.04,.675 if typ!='Interceptor' else .916,.28,.12,.025,.01)
 m.set('SensorGlow',g);m.block(0,.04,.695 if typ!='Interceptor' else .938,.19,.045,.009,.003)
 m.set('BellyMechanical','Rubber');m.block(0,-.235,-.04,.46,.10,.48,.03)
 # drone-specific layered armor, not just color swaps
 if typ=='Scout':
  m.set('SensorMast','Titanium');m.cylinder(0,.39,-.12,.13,.16)
  m.set('SensorMast','Glass');m.cylinder(0,.48,-.12,.085,.045)
  m.set('UpperAntenna','Titanium');m.block(-.20,.40,-.36,.035,.21,.035,.008)
  m.set('UpperAntenna','Titanium');m.block(.20,.40,-.36,.035,.21,.035,.008)
  pods=[(-.72,.43),(.72,.43),(-.68,-.51),(.68,-.51)];radius=.235
 elif typ=='Flanker':
  m.set('WingKnifeLeft','Titanium');m.beam((-.26,.20),(-1.07,-.28),.13,.10)
  m.set('WingKnifeRight','Titanium');m.beam((.26,.20),(1.07,-.28),.13,.10)
  for x in [-.51,.51]:
   m.set('WingWarningStripe',g);m.block(x,.16,-.20,.28,.025,.045,.008)
  pods=[(-.98,-.20),(.98,-.20),(-.48,-.64),(.48,-.64)];radius=.22
 elif typ=='Guard':
  for x in [-.61,.61]:
   m.set('ShieldArmour','Titanium');m.block(x,.10,-.06,.36,.23,.86,.065)
   m.set('ShieldWarning',g);m.block(x,.23,.08,.17,.025,.31,.008)
  m.set('ForwardBumper','Carbon');m.block(0,-.06,.62,1.00,.17,.19,.045)
  pods=[(-1.02,.48),(1.02,.48),(-1.02,-.58),(1.02,-.58)];radius=.285
 else:
  for x in [-.25,.25]:
   m.set('RearJetHousing','Titanium');m.block(x,-.005,-.65,.20,.24,.43,.05)
   m.set('RearEngineLight',g);m.block(x,-.008,-.89,.105,.12,.02,.004)
  m.set('ArrowKeel',g);m.block(0,.322,.31,.055,.024,.47,.009)
  pods=[(-.79,-.37),(.79,-.37),(-.52,-.69),(.52,-.69)];radius=.215
 for i,(x,z) in enumerate(pods):
  # grouped rotors share independent transforms when Blender splits OBJ groups
  m.set('Pylon'+str(i),'Titanium');m.beam((x*.48,z*.48),(x,z),.12,.095)
  m.set('Duct'+str(i),'Carbon');m.ring(x,.06,z,radius*1.23,radius*.80,.18)
  m.set('DuctAccent'+str(i),'Edge');m.ring(x,.167,z,radius*1.24,radius*1.14,.035)
  m.set('Rotor'+str(i),'Titanium');m.cylinder(x,.086,z,.053,.10,12)
  m.set('Rotor'+str(i),'Edge');m.block(x,.145,z,radius*1.52,.022,.065,.008)
  m.set('Rotor'+str(i),'Edge');m.block(x,.145,z,.065,.022,radius*1.52,.008)
  m.set('Rotor'+str(i),g);m.cylinder(x,.166,z,.040,.018,10)
 m.save()
