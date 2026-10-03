from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
out=Path('Assets/Art/Industries')
font=lambda s: ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',s)
for name,title,lines in [
 ('ControlsCard','COMMANDES',['Q / A   FLIPPER GAUCHE','D   FLIPPER DROIT','ESPACE   CHARGER / LANCER','ÉCHAP   PAUSE']),
 ('ProductionCard','CHAÎNE DE PRODUCTION',['6 CIBLES  ·  BONUS 5 000','CIBLE 500  ·  BUMPER 100','SCOOP 1 000','ATELIER DES VOSGES'])]:
 im=Image.new('RGB',(1024,512),(231,227,210)); d=ImageDraw.Draw(im)
 d.rounded_rectangle((12,12,1012,500),radius=18,outline=(142,104,46),width=7)
 d.text((512,66),title,font=font(49),fill=(30,56,60),anchor='mm')
 d.line((100,115,924,115),fill=(142,104,46),width=3)
 for i,line in enumerate(lines):d.text((512,173+i*74),line,font=font(38),fill=(36,57,58),anchor='mm')
 im.save(out/(name+'.png'))
