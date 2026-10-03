from pathlib import Path
import shutil
root=Path('Tools/unity/out/industry-transfer')
root.mkdir(parents=True,exist_ok=True)
shutil.copyfile('Assets/Scenes/Neutral.unity',root/'neutral-before.unity')
shutil.copyfile('Assets/Prefabs/Ball.prefab',root/'ball-before.prefab')
for side in ('left','right'):
    code=Path(f'Tools/unity/test_ramp_flipper_{side}.cs').read_text(encoding='utf-8-sig')
    code=code.replace('string variant=System.IO.File.Exists("Tools/unity/out/ramp-access-modified.flag")?"after-'+side+'":"before-'+side+'";',f'string variant="transfer-baseline-{side}";')
    Path(f'Tools/unity/transfer_baseline_{side}.cs').write_text(code,encoding='utf-8')

from PIL import Image, ImageDraw, ImageFont
out=Path('Assets/Art/NeutralInstructionCards');out.mkdir(parents=True,exist_ok=True)
font=lambda s:ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',s)
for name,title,lines in [
 ('NeutralControlsCard','COMMANDES',['Q / A   FLIPPER GAUCHE','D   FLIPPER DROIT','ESPACE   CHARGER / LANCER','ÉCHAP   PAUSE']),
 ('NeutralRulesCard','PARCOURS IUT',['VISEZ LES 6 CIBLES','ENCHAÎNEZ LES 2 RAMPES','100 000  →  INDUSTRIES','3 BILLES POUR RÉUSSIR'])]:
 im=Image.new('RGB',(1024,512),(231,227,210));d=ImageDraw.Draw(im)
 d.rounded_rectangle((12,12,1012,500),radius=18,outline=(142,104,46),width=7)
 d.text((512,66),title,font=font(49),fill=(30,56,60),anchor='mm')
 d.line((100,115,924,115),fill=(142,104,46),width=3)
 for i,line in enumerate(lines):d.text((512,173+i*74),line,font=font(38),fill=(36,57,58),anchor='mm')
 im.save(out/(name+'.png'))
