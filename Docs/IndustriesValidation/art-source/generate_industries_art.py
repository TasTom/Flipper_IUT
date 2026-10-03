from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import math, random
out=Path('Assets/Art/Industries'); out.mkdir(parents=True,exist_ok=True)
W,H=2048,4096
im=Image.new('RGB',(W,H),(15,28,36)); d=ImageDraw.Draw(im)
def font(n): return ImageFont.truetype('C:/Windows/Fonts/bahnschrift.ttf',n)
def xy(x,y): return (round(x/952*W),round(y/2162*H))
def line(points,fill,width): d.line([xy(*p) for p in points],fill,width=width,joint='curve')
def label(p,text,n=60,fill='#e6e5d7'): d.text(xy(*p),text,font=font(n),fill=fill,anchor='mm')
# A muted circuit/production plan with warm printed brass and alpine contour lines.
for x in range(0,W,64): d.line((x,0,x,H),fill=(20,35,43),width=1)
for y in range(0,H,64): d.line((0,y,W,y),fill=(20,35,43),width=1)
for k in range(9):
    pts=[]
    for x in range(60,900,10):
        y=220+k*23+45*math.sin(x/120)+18*math.sin(x/49)
        pts.append((x,y))
    line(pts,(36,64,62),3)
line([(55,1700),(110,1310),(150,890),(98,690),(105,420),(205,280),(410,120),(780,170),(846,450),(870,820),(835,1290),(780,1710)],'#da9e4c',8)
line([(82,1665),(145,1220),(185,880),(170,750)],'#4f9696',4)
line([(801,1665),(758,1180),(730,870),(762,750)],'#4f9696',4)
# Six reward target call-outs sit in the actual banks at y=1100..1210.
for i,(x,y) in enumerate([(750,1100),(768,1150),(782,1200),(177,1100),(162,1150),(150,1200)]):
    cx,cy=xy(x,y); r=30
    d.ellipse((cx-r,cy-r,cx+r,cy+r),fill='#1e4249',outline='#d8a75f',width=4)
    d.text((cx,cy),str(i+1),font=font(30),fill='#e8cfa1',anchor='mm')
# Architectural gear emblem kept in the open middle shooting area.
cx,cy=xy(443,1420)
pts=[]
for i in range(96):
    a=i*math.tau/96; r=210 if i%4 in (1,2) else 178
    pts.append((cx+math.cos(a)*r,cy+math.sin(a)*r))
d.polygon(pts,fill='#233e47',outline='#bc8543',width=7)
d.ellipse((cx-128,cy-128,cx+128,cy+128),outline='#63a8a4',width=6)
label((443,1395),'ATELIER',64); label((443,1440),'DES VOSGES',49)
label((445,1535),'BOIS  /  TEXTILE  /  ÉNERGIE',28,'#79aca6')
label((444,1620),'CHAÎNE DE PRODUCTION',35,'#d4b883')
label((443,1660),'6 CIBLES  ·  BONUS 5 000',27,'#a9bbb5')
label((440,815),'CIRCUIT INDUSTRIEL',34,'#aec4bd')
label((445,190),'LIGNE 02',40,'#dfb47c')
# Apron printing only, avoiding all lanes and bat sweep areas.
label((443,2070),'VOSGES MANIA',76,'#e4d5b5')
label((443,2120),'IUT SAINT-DIÉ  ·  INDUSTRIES',31,'#7eaca7')
for x in range(35,910,48):
    a=xy(x,1995); b=xy(x+27,2010)
    d.polygon([a,(b[0],a[1]),b,(a[0],b[1])],fill='#b58747')
random.seed(19)
for i in range(1600):
    x=random.randrange(W); y=random.randrange(H)
    d.line((x,y,x+random.randint(1,10),y+random.randint(0,2)),fill=(26,40,46),width=1)
im.save(out/'IndustriesPlayfield.png')
# Micro scratches are lighting-independent, stored as a real tangent-space normal.
normal=Image.new('RGB',(512,1024),(128,128,255)); nd=ImageDraw.Draw(normal)
random.seed(7)
for i in range(900):
    x=random.randrange(512); y=random.randrange(1024)
    nd.line((x,y,x+random.randrange(2,13),y),fill=(126,131,254),width=1)
normal.save(out/'IndustriesMicroNormal.png')
print('Industries artwork: 2048x4096, normal 512x1024')
