# Neutral — provenance audio

Les quinze fichiers WAV de ce dossier sont des créations originales du projet,
générées par synthèse déterministe : transitoires, bruit filtré, résonances amorties
et notes d'arcade. Aucun échantillon ni enregistrement de machine commerciale
n'a été incorporé. Ce sont des effets conçus, pas des mesures de vrais solénoïdes.

La recette est archivée dans
`Docs/NeutralVpeAudioValidation/generate_vpe_audio.py.txt`.
Les fichiers sont mono PCM 16 bits, à 44 100 Hz. La normalisation préserve une
marge avant saturation ; les gains de lecture se règlent dans `NeutralAudioConfig`.

La musique de fond réutilise le fichier déjà présent dans `Assets/Audio`.
Cette passe change son import et son gain, sans lui attribuer une nouvelle licence.
