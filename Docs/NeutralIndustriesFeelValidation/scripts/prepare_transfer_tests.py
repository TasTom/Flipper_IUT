from pathlib import Path
for variant in ('baseline','after'):
 for side in ('left','right'):
  p=Path(f'Tools/unity/transfer_{variant}_{side}.cs')
  code=Path(f'Tools/unity/test_ramp_flipper_{side}.cs').read_text(encoding='utf-8-sig')
  code=code.replace('var table=GameObject.Find', 'var transition=UnityEngine.Object.FindAnyObjectByType<ScoreProgressionGate>();bool transitionEnabled=transition!=null&&transition.enabled;if(transition!=null)transition.enabled=false;\nvar table=GameObject.Find',1)
  code=code.replace('UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=background;GameManager.Instance.StartGame();Physics.simulationMode=previous;', 'UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=background;GameManager.Instance.StartGame();Physics.simulationMode=previous;if(transition!=null)transition.enabled=transitionEnabled;')
  code=code.replace('string variant=System.IO.File.Exists("Tools/unity/out/ramp-access-modified.flag")?"after-'+side+'":"before-'+side+'";',f'string variant="transfer-{variant}-{side}";')
  p.write_text(code,encoding='utf-8')
