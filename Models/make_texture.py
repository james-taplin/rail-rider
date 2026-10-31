from PIL import Image
import math,random
random.seed(14); w=h=1024; im=Image.new('RGB',(w,h)); px=im.load()
for y in range(h):
 for x in range(w):
  g=9*math.sin(y*.08+2.2*math.sin(x*.012+y*.003))+5*math.sin(y*.23+math.sin(x*.014)*2)+3*math.sin(y*.37+1.2*math.sin(x*.019))+random.random()*2
  px[x,y]=(int(88+g),int(46+g*.7),int(22+g*.5))
a=Image.open('rail_rider_engraving.png').convert('RGB').crop((150,100,2050,560)).resize((350,106),Image.Resampling.LANCZOS)
for yy in range(a.height):
 for xx in range(a.width):
  r,g,b=a.getpixel((xx,yy)); d=max(0,min(1,(220-(r+g+b)/3)/145))*.9
  x=175+xx; y=135+yy; old=px[x,y]; px[x,y]=tuple(int(c*(1-d)+t*d) for c,t in zip(old,(28,12,5)))
im.save('rail_rider_wood.png')
