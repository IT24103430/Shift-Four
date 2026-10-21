from pathlib import Path
import math
root=Path(__file__).resolve().parents[2]; models=root/'Assets/Models';models.mkdir(parents=True,exist_ok=True)
class Model:
 def __init__(self,name):self.name=name;self.verts=[];self.uvs=[];self.faces=[];self.mat='Shell'
 def material(self,m):self.mat=m
 def quad(self,pts):
  start=len(self.verts)+1;self.verts.extend(pts); self.uvs.extend([(0,0),(1,0),(1,1),(0,1)]);self.faces.append((self.mat,[start+i for i in range(4)]))
 def box(self,x,y,z,sx,sy,sz):
  a=x-sx/2;b=x+sx/2;c=y-sy/2;d=y+sy/2;e=z-sz/2;f=z+sz/2
  for face in [[(a,c,f),(b,c,f),(b,d,f),(a,d,f)],[(b,c,e),(a,c,e),(a,d,e),(b,d,e)],[(b,c,f),(b,c,e),(b,d,e),(b,d,f)],[(a,c,e),(a,c,f),(a,d,f),(a,d,e)],[(a,d,f),(b,d,f),(b,d,e),(a,d,e)],[(a,c,e),(b,c,e),(b,c,f),(a,c,f)]]:self.quad(face)
 def cylinder(self,x,y,z,r,h,n=12,axis='y'):
  def pos(ang,offset):
   a=2*math.pi*ang/n;v=(r*math.cos(a),r*math.sin(a))
   return (x+v[0],y+offset,z+v[1]) if axis=='y' else (x+v[0],y+v[1],z+offset)
  for i in range(n):self.quad([pos(i,-h/2),pos((i+1)%n,-h/2),pos((i+1)%n,h/2),pos(i,h/2)])
  centerbottom=(x,y-h/2,z) if axis=='y' else (x,y,z-h/2)
  centertop=(x,y+h/2,z) if axis=='y' else (x,y,z+h/2)
  for i in range(n):
   a=pos(i,-h/2);b=pos((i+1)%n,-h/2);self.quad([centerbottom,a,b,centerbottom]);a=pos(i,h/2);b=pos((i+1)%n,h/2);self.quad([centertop,b,a,centertop])
 def save(self):
  p=models/(self.name+'.obj');lines=['# Original procedurally modelled, UV mapped Shift Four game asset','mtllib ShiftFourPalette.mtl','o '+self.name]
  lines += ['v %.5f %.5f %.5f'%v for v in self.verts]; lines+=['vt %.4f %.4f'%uv for uv in self.uvs];current=None
  for material,ids in self.faces:
   if current!=material:lines.append('usemtl '+material);current=material
   lines.append('f '+' '.join(f'{i}/{i}' for i in ids))
  p.write_text('\n'.join(lines)+'\n');print(p,len(self.faces),'quads',len(self.verts),'verts')
materials={'Shell':(.16,.22,.29),'Trim':(.36,.45,.54),'Light':(.08,.74,.92),'Dark':(.065,.085,.11),'Blade':(.61,.72,.76),'Door':(.25,.31,.37),'Warning':(.98,.62,.15)}
(models/'ShiftFourPalette.mtl').write_text('\n'.join(f'newmtl {name}\nKa 0.03 0.03 0.03\nKd {c[0]} {c[1]} {c[2]}\nKs 0.12 0.12 0.12\nNs 30\nd 1\nillum 2\n' for name,c in materials.items()))
m=Model('SF_SecurityDrone')
m.material('Shell');m.box(0,0,0,1.05,.35,.75);m.box(0,.18,0,.72,.13,.50)
m.material('Trim');m.box(0,-.16,0,.78,.08,.5)
m.material('Dark');m.box(0,.025,.39,.48,.18,.07)
m.material('Light');m.box(0,.04,.432,.30,.065,.015)
for x in [-.72,.72]:
 for z in [-.53,.53]:
  m.material('Trim');m.box(x*.63,0,z*.62,.45,.09,.13)
  m.material('Dark');m.cylinder(x,0,z,.27,.12,12)
  m.material('Blade');m.box(x,.087,z,.51,.024,.065);m.box(x,.087,z,.065,.024,.51)
  m.material('Light');m.box(x,-.088,z,.12,.025,.12)
m.material('Warning');m.box(-.29,0,.396,.10,.09,.016);m.box(.29,0,.396,.10,.09,.016)
m.save()
d=Model('SF_SlidingLabDoor');d.material('Door');d.box(0,1.2,0,.23,2.35,3.55)
d.material('Trim');d.box(-.17,1.2,0,.065,2.30,3.39);d.box(-.17,2.30,0,.07,.11,3.55);d.box(-.17,.10,0,.07,.11,3.55)
for z in [-1.4,1.4]:d.box(-.18,1.2,z,.07,2.28,.11)
d.material('Dark');d.box(-.215,1.28,0,.018,1.60,2.75)
d.material('Shell');d.box(-.24,1.33,-.68,.04,1.46,1.18);d.box(-.24,1.33,.68,.04,1.46,1.18)
d.material('Light');d.box(-.269,1.38,-.035,.015,1.27,.045)
d.material('Warning');d.box(-.27,2.06,0,.015,.09,2.7)
d.material('Light');d.box(-.27,2.28,0,.015,.04,2.8)
d.save()
