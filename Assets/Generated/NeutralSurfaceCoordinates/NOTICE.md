# Neutral — coordonnées des surfaces

Ce dossier contient des variantes destinées au rendu des meshes de Neutral.
Les positions des triangles, les normales de surface et les volumes sont ceux
des sources existantes. La projection UV sépare les sommets lorsque le plan de
projection change ; les tangentes sont calculées par Unity. Les MeshColliders
continuent d'utiliser leurs meshes sources.

Les variantes des pièces de `Assets/Models/NeutralMechanical` conservent la
provenance et la licence CC BY-SA de
`Assets/Models/NeutralMechanical/NOTICE.md` et de la bibliothèque
[vbousquet/pinball-parts](https://github.com/vbousquet/pinball-parts).
Les autres sources sont la géométrie originale générée pour le projet.

L'outil reproductible est `Assets/Editor/InstallNeutralSurfaceCoordinates.cs`.
Les recettes de validation sont dans `Docs/NeutralVpeAudioValidation`.
