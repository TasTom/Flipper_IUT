# Rejouer la validation de score de Neutral

Ces sources sont destinées au CLI de l'éditeur Unity ouvert. Elles sont archivées en
`.cs.txt` afin de ne pas devenir des scripts importés ou des composants du jeu.

1. Copier les recettes voulues dans `Tools/unity/` et retirer l'extension `.txt`.
2. Copier `refined-paths.json` dans `Tools/unity/out/`. Ouvrir `Neutral` en mode édition.
3. Exécuter `inspect_score_setup.cs` pour relever la configuration et générer l'empreinte
   des colliders de la session. Les identifiants Unity ne sont pas comparables entre
   sessions différentes.
4. `install_score.cs` installe les références vides et les objets absents avec Undo.
   La recette ne sauvegarde pas la scène. Si elle est déjà équipée, elle conserve
   ses positions et références.
5. Passer en Play Mode et exécuter les recettes `test_score_*.cs`. La recette de règles
   sauvegarde puis restaure le record utilisateur. Les changements de configuration
   utilisés comme fixtures sont également restaurés.
6. Revenir en édition, exécuter `verify_score_scene.cs`, puis sauvegarder séparément
   par le CLI si une installation a été effectuée.

Exemple, depuis la racine du projet :

```powershell
rtk proxy unity command eval_file D:/Flipper_IUT/Tools/unity/test_score_rules.cs --caller plugin --skill unity-cli --format json
```

Les rapports de cette passe totalisent **90 contrôles réussis** dans les cinq recettes
de score, plus les essais physiques de contacts et de circulation. Les captures de
Super Combo utilisent volontairement le score entier maximal comme cas de lisibilité.
Elles ne représentent pas un record réel enregistré. `neutral-score.png` montre le
panneau sauvegardé en mode édition.

Voir [NeutralScoreRules](../NeutralScoreRules.md) pour les règles, leur lien au GDD,
les décisions d'équilibrage et les limites de la validation.
