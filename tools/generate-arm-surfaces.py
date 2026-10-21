"""Deterministic, tileable PBR detail maps for the owned arms; no external image sources."""
import math,struct,os,sys
out=sys.argv[1];os.makedirs(out,exist_ok=True)
size=256
def hash2(x,y,period):
 x%=period;y%=period
 n=(x*374761393+y*668265263+23871)&0xffffffff;n=(n^(n>>13))*1274126177&0xffffffff
 return ((n^(n>>16))&0xffffff)/0xffffff
def noise(x,y,period):
 ix=int(math.floor(x));iy=int(math.floor(y));fx=x-ix;fy=y-iy
 fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy)
 return (hash2(ix,iy,period)*(1-fx)+hash2(ix+1,iy,period)*fx)*(1-fy)+(hash2(ix,iy+1,period)*(1-fx)+hash2(ix+1,iy+1,period)*fx)*fy

def height(kind,x,y):
 if kind=='Leather':return .55*noise(x/size*48,y/size*48,48)+.24*noise(x/size*96,y/size*96,96)+.14*noise(x/size*12,y/size*12,12)
 # Fine weave with a subtly raised reinforcing ripstop grid.
 weave=.06*math.sin(x*math.tau/4)*math.sin(y*math.tau/4)
 grid=.16*(math.exp(-((x%64-32)/1.2)**2)+math.exp(-((y%64-32)/1.2)**2))
 return .45+weave+grid+.06*noise(x/size*32,y/size*32,32)
def tga(path,pixels):
 with open(path,'wb') as f:
  f.write(struct.pack('<BBBHHBHHHHBB',0,0,2,0,0,0,0,0,size,size,24,0))
  for r,g,b in pixels:f.write(bytes((b,g,r)))
for kind in ('Leather','Ripstop'):
 base=[];normal=[]
 for y in range(size):
  for x in range(size):
   h=height(kind,x,y);tone=max(0,min(255,round((.89+.10*h)*255)))
   base.append((tone,tone,tone))
   dx=(height(kind,(x+1)%size,y)-height(kind,(x-1)%size,y))*1.35
   dy=(height(kind,x,(y+1)%size)-height(kind,x,(y-1)%size))*1.35
   length=math.sqrt(dx*dx+dy*dy+1)
   normal.append(tuple(round((v/length*.5+.5)*255) for v in (-dx,-dy,1)))
 tga(os.path.join(out,kind+'Base.tga'),base);tga(os.path.join(out,kind+'Normal.tga'),normal)
print('Generated four owned 256x256 surface maps in',out)
