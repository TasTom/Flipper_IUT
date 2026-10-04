"""Original industrial enamel artwork, in the measured VPE playfield coordinate frame.
Printed color only: no baked shadows, bulbs or target numbers. Geometry is separate.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math, random

ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Assets/Art/Industries/Production'
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
for i,(x,z) in enumerate([(.284,-.401),(.366,-.370),(.124,-.432),(.357,-.235),(.278,-.268)]):
    ring(d,x,z,.035,'#3d6769',4)
    for j in range(16):
        a=j*math.tau/16
        line(d,[(x+math.cos(a)*.035,z+math.sin(a)*.035),(x+math.cos(a)*.038,z+math.sin(a)*.038)],INK,3)
text(d,.325,-.322,'MACHINES',42,INK)
text(d,.310,-.466,'BOIS : CHARGEMENT',38,CYAN)
text(d,.333,-.627,'TEXTILE',38,CYAN)
text(d,.115,-.613,'LIVRAISON',38,INK)
text(d,.155,-.700,'RAMPE BOIS',34,INK)
text(d,.332,-.573,'RAMPE TEXTILE',32,INK)
line(d,[(.177,-.710),(.184,-.697),(.191,-.710)],INK,6)
line(d,[(.375,-.591),(.384,-.559),(.393,-.580)],INK,6)
ring(d,.119,-.574,.023,INK,6)
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
text(d,.242,-.809,'CHARGER  /  TRANSFORMER  /  LIVRER',31,CYAN)
box(d,.242,-.857,.162,.024,'#183940','#537b79',3)
text(d,.242,-.851,'CHAÎNE DE PRODUCTION',40)
text(d,.242,-.866,'2 MATIÈRES  •  DOUBLE LIVRAISON',28,INK)
text(d,.280,-.590,'3 CIBLES / RAMPE / SCOOP',26,CYAN)
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

card=Image.new('RGB',(1024,640),'#eadbb4');c=ImageDraw.Draw(card)
c.rounded_rectangle((16,16,1008,624),radius=20,outline='#a98a53',width=8)
for y,label,size in [(75,'PRODUCTION',58),(180,'3 CIBLES : CHARGER',42),
    (260,'RAMPE : TRANSFORMER',42),(340,'SCOOP : LIVRER',42),
    (455,'BOIS + TEXTILE',40),(525,'DOUBLE LIVRAISON +10 000',38)]:
    c.text((512,y),label,font=font(size),fill='#304c51',anchor='mm')
card.save(OUT/'Instructions.png')

print('Production artwork and instruction card generated.')
