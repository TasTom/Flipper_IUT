"""Original industrial enamel artwork, in the measured VPE playfield coordinate frame.
Printed color only: no baked shadows, bulbs or target numbers. Geometry is separate.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math, random

ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Assets/Art/Industries/Remodel'
OUT.mkdir(parents=True,exist_ok=True)
W,H=2048,4096
PW,PL=.5138,1.1669
INK='#dcb778'; LIGHT='#eee1c1'; CYAN='#69a7a9'
def font(size): return ImageFont.truetype('C:/Windows/Fonts/bahnschrift.ttf',size)
def pixel(x,z): return (round(x/PW*W),round(-z/PL*H))
def text(draw,x,z,value,size=50,color=LIGHT):
    draw.text(pixel(x,z),value,font=font(size),fill=color,anchor='mm')
def line(draw,points,color=INK,width=5):
    draw.line([pixel(*p) for p in points],fill=color,width=width,joint='curve')
def box(draw,x,z,w,h,fill,outline=None,width=3):
    a=pixel(x-w/2,z+h/2);b=pixel(x+w/2,z-h/2)
    draw.rounded_rectangle((*a,*b),radius=12,fill=fill,outline=outline,width=width)
def ring(draw,x,z,r,color=INK,width=5):
    a=pixel(x-r,z+r);b=pixel(x+r,z-r);draw.ellipse((*a,*b),outline=color,width=width)
def gear(draw,x,z,r,color=INK):
    points=[]
    for i in range(96):
        a=i*math.tau/96;rr=r if i%4 in (1,2) else r*.90
        points.append(pixel(x+math.cos(a)*rr,z+math.sin(a)*rr))
    draw.polygon(points,outline=color,width=7)
    ring(draw,x,z,r*.78,CYAN,4)

im=Image.new('RGB',(W,H),(17,41,48));d=ImageDraw.Draw(im)
# Faint drafting lines leave the ball and inserts easy to read.
for x in range(0,W,128): d.line((x,0,x,H),fill=(23,49,55),width=1)
for y in range(0,H,128): d.line((0,y,W,y),fill=(23,49,55),width=1)
for points in [
    [(.054,-.99),(.046,-.75),(.10,-.55),(.145,-.45)],
    [(.424,-.99),(.439,-.75),(.385,-.53),(.347,-.44)],
    [(.152,-.45),(.194,-.40),(.205,-.275)],
    [(.33,-.40),(.30,-.35),(.333,-.27)]]:
    line(d,points,'#377275',6)
    line(d,[(x+.003,z) for x,z in points],'#285359',3)
# Boiler station rings are printing beneath the actual native bumpers.
for i,(x,z) in enumerate([(.24,-.371),(.335,-.410),(.14,-.410),(.335,-.2676),(.2068,-.26)]):
    ring(d,x,z,.035,'#3d6769',4)
    for j in range(16):
        a=j*math.tau/16
        line(d,[(x+math.cos(a)*.035,z+math.sin(a)*.035),(x+math.cos(a)*.038,z+math.sin(a)*.038)],INK,3)
text(d,.265,-.335,'SALLE DES MACHINES',42,INK)
text(d,.245,-.455,'PRESSION  /  PRODUCTION',38,CYAN)
# Factory and Vosges skyline, drawn as ink rather than geometry in the ball path.
line(d,[(.11,-.065),(.16,-.035),(.195,-.057),(.24,-.026),(.28,-.05),(.33,-.032),(.40,-.075)],'#538b8d',7)
text(d,.255,-.077,'VOSGES  /  LIGNE INDUSTRIELLE',46)
for x in [.18,.21,.24,.27,.30,.33]:
    line(d,[(x,-.12),(x,-.102),(x+.018,-.12)],INK,4)
line(d,[(.175,-.122),(.35,-.122),(.35,-.137),(.175,-.137),(.175,-.122)],INK,4)
# Open shooting area: one strong identifying badge, without duplicating the six digits.
gear(d,.242,-.744,.061)
box(d,.242,-.733,.208,.044,'#142c34',INK,6)
text(d,.242,-.733,'INDUSTRIES',126)
text(d,.242,-.774,'ATELIER DES VOSGES',48,INK)
text(d,.242,-.809,'BOIS   /   TEXTILE   /   ÉNERGIE',31,CYAN)
box(d,.242,-.857,.162,.024,'#183940','#537b79',3)
text(d,.242,-.851,'CHAÎNE DE PRODUCTION',40)
text(d,.242,-.866,'6 CIBLES  •  BONUS 5 000',28,INK)
text(d,.242,-.603,'ASSEMBLAGE',33,CYAN)
line(d,[(.165,-.615),(.32,-.615)],'#365b62',3)
for x in [.048,.432]:
    text(d,x,-.95,'SORTIE',24,INK)
# Apron art is repeated in the physical apron mesh, for views beneath its edge.
text(d,.235,-1.115,'VOSGES MANIA',66)
text(d,.235,-1.138,'IUT SAINT-DIÉ  •  ATELIER 02',28,INK)
random.seed(14)
for _ in range(1500):
    x=random.randrange(W);y=random.randrange(H)
    d.line((x,y,x+random.randrange(1,7),y),fill=(24,48,52),width=1)
im.save(OUT/'Playfield.png')

# One projected atlas shared by all fitted carters: labels remain aligned to geometry.
panels=Image.new('RGB',(W,H),(13,47,53));p=ImageDraw.Draw(panels)
for y in range(0,H,200): p.line((0,y,W,y),fill=(18,58,64),width=2)
for x,z,label in [(.077,-.284,'ÉNERGIE'),(.454,-.657,'TEXTILE'),(.047,-.59,'BOIS')]:
    # Native world labels remain in place; the panel carries decorative enamel lines only.
    box(p,x,z+.030,.052,.037,'#123a40',INK,5)
    for n in range(4): line(p,[(x-.017,z+.021+n*.004),(x+.017,z+.021+n*.004)],'#4e7678',3)
for x,z in [(.115,-.845),(.356,-.845)]:
    text(p,x,z,'VM',80)
    text(p,x,z-.018,'ATELIER',22,INK)
    for n in range(5): line(p,[(x-.014,z+.018+n*.004),(x+.014,z+.018+n*.004)],'#567b7e',4)
text(p,.258,-.06,'ATELIER DES VOSGES',65)
text(p,.258,-.084,'TRANSMISSIONS   /   LIGNE 02',26,INK)
text(p,.235,-1.04,'INDUSTRIES',60,INK)
text(p,.235,-1.145,'VOSGES MANIA  •  IUT SAINT-DIÉ',37,LIGHT)
for x in [.025,.451]:
    for z in [-1.10,-1.12,-1.14]: line(p,[(x,z),(x+.018,z-.006)],INK,7)
panels.save(OUT/'Panels.png')

# Pressure dials: actual cap surface follows each native bumper animation.
dial=Image.new('RGB',(512,512),(226,215,183));q=ImageDraw.Draw(dial)
q.ellipse((18,18,494,494),outline='#9c784a',width=20)
q.ellipse((48,48,464,464),outline='#344d53',width=5)
for i in range(41):
    a=math.radians(140+i*6.5);r1=172 if i%5==0 else 186;r2=200
    q.line((256+math.cos(a)*r1,256+math.sin(a)*r1,256+math.cos(a)*r2,256+math.sin(a)*r2),fill='#2a4147',width=5 if i%5==0 else 3)
q.text((256,318),'PRESSION',font=font(46),fill='#344d53',anchor='mm')
q.text((256,359),'ATELIER 02',font=font(23),fill='#9c784a',anchor='mm')
q.line((256,256,370,146),fill='#b25c30',width=10)
q.ellipse((240,240,272,272),fill='#344d53')
dial.save(OUT/'PressureDial.png')
print('Original art: 2 atlases 2048x4096 + dial 512x512; no baked lights or target digits.')
