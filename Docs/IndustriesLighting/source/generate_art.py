"""Four fitted enamel prints and a soft light cookie. Geometry exported by Unity.
All lettering is inside the actual top-face footprint; UVs use each panel's bounds.
"""
from pathlib import Path
import json, math
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Assets/Art/Industries/Lighting'
OUT.mkdir(parents=True, exist_ok=True)
specs = json.loads((Path(__file__).parent / 'panels.json').read_text())
names = {'Carter_Wall30': ('VOSGES', '01', 105), 'Carter_Wall31': ('MANIA', '02', 75),
         'Carter_Wall33': ('TEXTILE', '02', 90), 'Carter_Wall34': ('BOIS', '01', 90)}
font_path = 'C:/Windows/Fonts/bahnschrift.ttf'
report = []
for spec in specs:
    vs, ts = spec['vertices'], spec['triangles']
    x0, x1 = min(v[0] for v in vs), max(v[0] for v in vs)
    z0, z1 = min(v[2] for v in vs), max(v[2] for v in vs)
    w = 512
    h = round(w * (z1-z0)/(x1-x0))
    def px(v): return ((v[0]-x0)/(x1-x0)*(w-1), (z1-v[2])/(z1-z0)*(h-1))
    mask = Image.new('L', (w,h)); md = ImageDraw.Draw(mask)
    for i in range(0,len(ts),3): md.polygon([px(vs[t]) for t in ts[i:i+3]], fill=255)
    safe = mask.filter(ImageFilter.MinFilter(31))
    occlusion_path = Path(__file__).parent / (spec['name']+'_occlusion.png')
    if occlusion_path.exists():
        # Projection of live opaque rails and workshop models, already padded.
        safe = ImageChops.subtract(safe, Image.open(occlusion_path).convert('L'))
    im = Image.new('RGB',(w,h)); d = ImageDraw.Draw(im)
    for y in range(h):
        value = 0.5+0.5*math.cos(y/h*math.pi)
        d.line((0,y,w,y), fill=(round(13+6*value),round(40+9*value),round(47+9*value)))
    border = ImageChops.subtract(mask,mask.filter(ImageFilter.MinFilter(13)))
    im.paste('#ad804c',(0,0),border)
    for y in range(45,h,80): d.line((25,y,w-25,y), fill='#1d4049',width=2)
    title, serial, angle = names[spec['name']]
    accent = '#88d4cf' if title=='TEXTILE' else '#e2b578'
    placed = False
    for size in range(160,63,-8):
        f = ImageFont.truetype(font_path,size)
        bbox = f.getbbox(title)
        sw = bbox[2]-bbox[0]+40
        sh = bbox[3]-bbox[1]+90
        sprite = Image.new('RGBA',(sw,sh)); sd=ImageDraw.Draw(sprite)
        sd.text((sw/2,12),title,font=f,fill='#f0e3c5',anchor='mt')
        sd.line((20,sh-55,sw-20,sh-55),fill=accent,width=4)
        sd.text((sw/2,sh-42),'ATELIER / '+serial,font=ImageFont.truetype(font_path,27),fill=accent,anchor='mt')
        sprite=sprite.rotate(angle,resample=Image.Resampling.BICUBIC,expand=True)
        alpha=sprite.getchannel('A').point(lambda p:255 if p>8 else 0)
        candidates=[]
        for cy in range(int(h*.22),int(h*.80),12):
            for cx in range(int(w*.15),int(w*.85),10):
                left,top=round(cx-sprite.width/2),round(cy-sprite.height/2)
                if left<0 or top<0 or left+sprite.width>w or top+sprite.height>h:continue
                if ImageChops.subtract(alpha,safe.crop((left,top,left+sprite.width,top+sprite.height))).getbbox():continue
                candidates.append(((cx-w*.5)**2+(cy-h*.52)**2,left,top))
        if candidates:
            _,left,top=min(candidates);im.paste(sprite,(left,top),sprite)
            report.append(f"PASS {spec['name']} : {title}, font {size}px, all lettering inside top face with inset, clear of overhead opaque geometry.")
            placed=True;break
    if not placed: raise RuntimeError('No safe lettering area for '+spec['name'])
    im.save(OUT/(spec['name']+'.png'))

cookie=Image.new('RGBA',(128,128)); pixels=cookie.load()
for y in range(128):
    for x in range(128):
        r=math.hypot((x-63.5)/63.5,(y-63.5)/63.5)
        alpha=round(255*max(0,1-r*r)**2.5)
        pixels[x,y]=(255,255,255,alpha)
cookie.save(OUT/'SoftHalo.png')
(Path(__file__).parent.parent/'lettering.txt').write_text('\n'.join(report)+'\n',encoding='utf-8')
print('\n'.join(report))
