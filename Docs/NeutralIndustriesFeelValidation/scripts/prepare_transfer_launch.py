from pathlib import Path
code=Path('Tools/unity/start_neutral_shared_launch.cs').read_text(encoding='utf-8-sig')
code=code.replace('Docs/SharedMechanicsValidation','Docs/NeutralIndustriesFeelValidation')
Path('Tools/unity/start_transfer_launch.cs').write_text(code,encoding='utf-8')
