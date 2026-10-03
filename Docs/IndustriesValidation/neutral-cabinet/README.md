# Cabinet de Neutral dans Industries

Demande utilisateur : reprendre le cabinet de la scène Neutral pour le tableau des scores.

`PinballTable/Table/Pinball_Cabinet` est copié dans `IndustriesPresentation/Backglass/Pinball_Cabinet`. Le modèle complet et ses matériaux sont réutilisés. Ses trois pièces de fronton `Backbox`, `Screen`, `Marquee` sont rendues visibles et ajustées au rectangle des scores. Dans Neutral, tous les renderers de ce modèle sont masqués ; les autres pièces gardent cet état dans la copie. Aucun nouveau maillage n'est généré et aucun matériau source n'est modifié.

Le cadre est enfant du Canvas de scores, donc il suit son cadrage. Le fond noir a des marges de 32 unités du Canvas sur les côtés et 18 en haut/bas. Les textes et la caméra gardent leurs réglages. Les colliders hérités de la copie sont retirés pour ne pas interférer avec la physique VPE.

Vérification en Play Mode : VPE initialisé, score affiché conforme au gestionnaire, trois billes, trois renderers de fronton visibles et zéro collider dans le cabinet. `Industries.png` montre ce rendu ; `runtime.txt` détaille les références des meshes et matériaux. `saved-scene.txt` vérifie la scène enregistrée.

Les sources des sondes `.cs.txt` peuvent être recopiées dans `Tools/unity/*.cs` puis exécutées avec `unity command eval_file`. L'adaptation utilise Undo, s'arrête si le cabinet existe déjà et n'enregistre pas la scène elle-même.
