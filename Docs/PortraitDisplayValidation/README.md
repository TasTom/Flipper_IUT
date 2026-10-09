# Affichage portrait — 9 octobre 2026

Demande utilisateur : afficher le jeu en portrait dans Unity et dans le jeu compilé, à 1080 × 1920. Cette adaptation concerne le GDD §Interface et le cadrage fixe de la table.

`CabinetViewport` adapte le cadrage au rapport de l'écran. En portrait, la table est verticale et le score occupe les 15 % supérieurs. Les références, dimensions et colliders de la table restent ceux de la scène. Le carton de `SceneTransition` se lit verticalement en portrait.

`ProjectSettings/ProjectSettings.asset` enregistre une résolution Windows de 1080 × 1920, en fenêtre redimensionnable, et les mêmes dimensions de canvas WebGL. La fenêtre Game utilise le preset « Portrait 1080 × 1920 ». Aucun exécutable existant n'a été recompilé : les réglages s'appliqueront à la prochaine compilation. Une fenêtre peut être adaptée par Windows si sa hauteur dépasse la zone disponible du bureau.

## Vérifications

- Compilation Unity terminée sans erreur C#.
- [Industries](Industries.txt) et [Neutral](Neutral.txt) : cadrage complet, panneau du score dans l'écran et orientation attendue à 1080 × 1920, 720 × 1280 et 1920 × 1080.
- [Play Industries](runtime.txt) : initialisation VPE, distribution d'une bille, trois billes restantes, jeu non suspendu et carton de transition vertical. Le carton a été construit puis retiré sans charger une autre scène.
- Captures portrait : [Industries](Industries-1080x1920.png), [Neutral](Neutral-1080x1920.png). La capture de Neutral vérifie le cadrage dans l'éditeur ; elle ne constitue pas un test de partie complète.

L'erreur d'import URP `AutodeskInteractiveTransparent.shadergraph` était présente avant la modification. Les sondes temporaires ont été retirées. Les scènes n'ont pas été enregistrées par les outils.

Outillage conservé : `Tools/unity/set_portrait_display.cs`, `verify_portrait_display.cs` et `verify_portrait_runtime.cs`, exécutables par `unity command eval_file` avec leur chemin absolu. Les deux premiers s'utilisent hors Play ; la vérification runtime exige une nouvelle partie Industries en Play.
