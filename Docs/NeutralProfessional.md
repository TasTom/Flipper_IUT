# Neutral — Vosges Mania

État du **26 septembre 2026**. Scène : `Assets/Scenes/Neutral.unity`. [Capture Unity](NeutralFinal.png).

Cette version remplace la table trop large et l'ancienne présentation du 25 septembre. L'implantation actuelle prend pour base une largeur classique de 20,25 pouces et une bille de 27 mm, représentée par un diamètre de 0,45 unité Unity. La surface de jeu mesure 8,573 × 16,78 unités, complétée par l'apron et le fronton. L'inclinaison de 7° et les paramètres de commande/force des flippers sont conservés.

## Construction et rendu

- Deux pistes montantes séparées, virages supérieurs et retours descendants vers les inlanes. Le passage intérieur des rampes fait 0,64 unité pour une bille de 0,45. Les retours associent rails ronds chromés, traverses, arceaux et une doublure transparente. Leurs colliders restent les pistes continues testées : les fines tiges décoratives n'ajoutent pas de contacts parasites.
- Le contour arrière ferme le plateau jusqu'à la vitre, sans rebord extérieur sur lequel la bille puisse rester perchée au-dessus de l'ancien `OrbitOuter_V2`. La vitre est un volume fini à Y=2,6 ; les anciens colliders et rendus incompatibles sont désactivés.
- Le guide de sortie de l'orbite ramène la bille vers le centre. Son extrémité a été reculée pour dégager le tir vers BDD, après un échec reproduit pendant les essais.
- Exactement **six `SubjectTarget`** : ALGO, WEB, BDD, JAVA, PROJET, CAFÉ. Les voyants sont entourés de bagues et les légendes ont un fond contrasté. Ce nombre est une dérogation explicite de l'utilisateur au nombre du GDD.
- Illustration originale imprimée sur le plateau, reliefs des Vosges et circuits informatiques, bleu nuit/vert forêt/ivoire/ambre. Bumpers issus des modèles Parts, plastiques de slingshots, vis réelles, matériaux distincts pour l'acrylique et l'acier, lumière de table et sonde de réflexion.
- Spinner Stern : support et ailette du dépôt Parts montés avec leur origine commune. L'ancre du `HingeJoint`, restée à une ancienne position monde, est corrigée. La gravité ramène l'ailette ; son impact marque des points.
- `CabinetViewport` conserve la rotation de borne à 90°, adapte l'angle de vue au format de l'écran et réserve les 15 % de droite au score. Le panneau regarde la caméra, occupe toute sa hauteur et reste devant le mobilier élevé. Les textes restent dans le panneau.

Les outlanes sont bien des sorties de perte sur de vrais flippers ; les inlanes voisines ramènent la bille aux flippers. Le sauvetage gauche existant demeure une aide limitée. Une bille retournée au lanceur peut être relancée avec Espace.

## Validation de cette version

Rapports et sondes : [`NeutralValidation/2026-09-26`](NeutralValidation/2026-09-26/). Essais dans Unity 6000.6.0f1, colliders de la scène, simulation contrôlée au pas de 5 ms.

| Contrôle | Résultat |
| --- | --- |
| Quatre puissances de lancement, six parcours de rampe à 35/50/70, sept retombées aériennes | 17 trajectoires sans fuite ; aucun blocage durable dans l'aire de jeu observé |
| Retours au lanceur | Quatre scénarios y reviennent ; relance à pleine charge vérifiée |
| Six cibles visées depuis leur face accessible | 6/6 après correction du guide devant BDD |
| Outlanes G/D à 5/25/60 | 6/6 : une seule perte, deux billes restantes |
| Spinner | Ancre concordante, rotation de plus de 160° après impact et score augmenté |
| Multiball | Trois billes suivies, espacement minimal 0,60, volumes libres, départ vers les flippers, nettoyage au restart |
| Retours des rampes et frappe des flippers | Sur six timings par côté, 5 relances gauches et 2 droites au-delà de Z=9 ; les timings tardifs peuvent échouer |
| Panneau des scores en 1920×1080, 2560×1440, 1920×1200 | Coins projetés X=0,85…1 et Y=0…1 |
| Intégrité | Six cibles, zéro collider dans `ProfessionalArt`, zéro script manquant, compilation réussie |

Ces sondes ne remplacent pas une partie humaine. Les flippers sont actionnés par leur méthode de rotation existante dans les essais de relance, pas par un bouton physique. La difficulté ressentie et la réponse de la borne xin-mo restent à valider sur le matériel. Aucun nouvel exécutable n'est livré. Les rapports du 25 septembre restent historiques. Un délai de 5 secondes du bridge a expiré pendant l'import de l'habillage ; l'opération a fini correctement, puis son résultat a été inspecté.

## Références et provenance

- [VisualPinball.Engine](https://github.com/freezy/VisualPinball.Engine) et [documentation officielle](https://docs.visualpinball.org/creators-guide/introduction/overview.html) : référence pour la construction et les mécanismes. VPE propose sa physique compatible VPX, l'import des tables et une présentation HDRP. **Il n'est pas installé dans ce projet URP** : ce serait une migration du moteur et des composants. Le manifeste consulté annonce Unity 6000.5 et `0.0.1-preview.201` ; la licence du moteur est GPL-3.0.
- [Page GitHub pinball-game](https://github.com/topics/pinball-game) : annuaire examiné, contenant notamment des jeux 2D et des ports Space Cadet ; ce n'est pas une bibliothèque uniforme de pièces Unity.
- [vbousquet/pinball-parts](https://github.com/vbousquet/pinball-parts), **CC BY-SA 4.0** : modèles de bumpers, cibles, vis et spinner déjà importés dans `Assets/Models/Parts`. Modifications : placement, matériaux et, pour les bumpers, transformation des maillages dans le repère de leurs hôtes. [Copie de licence](NeutralValidation/2026-09-26/pinball-parts-LICENSE.md). Les modèles adaptés conservent ces conditions.
- [Fabrication d'un plateau standard](https://howtobuildapinballmachine.wordpress.com/2014/01/31/playfield-fabrication-blank/) : dimensions ; manuel Bally Xenon consulté pour l'organisation et le réglage des retours/outlanes.
- Les images Space Cadet et Jurassic Park fournies servent de références visuelles. Leurs textures ne sont pas copiées dans la scène.
- Illustration créée avec **imagegen intégré**, enregistrée dans `Assets/Generated/NeutralReference/VosgesPlayfield.png`. [Prompt de génération](NeutralValidation/2026-09-26/playfield-prompt.md).

Correspondance GDD : ambiance Vosges/IUT, rampes et loops, flippers, bumpers, cibles/missions, score, contrôle de la bille et interface. La reconstruction, les six cibles et le cadrage de borne ont été demandés explicitement par l'utilisateur.

## Maintenance

La scène enregistrée et les maillages dans `Assets/Generated/NeutralReference/` sont les références de l'implantation finale. `StyleNeutralProfessional` ajoute l'habillage explicitement en édition et refuse de remplacer un groupe déjà présent. Les ajustements finaux sont archivés avec les rapports ; ce sont des traces de travail, pas une recette à rejouer aveuglément. L'ancien générateur initial de reconstruction a été archivé hors d'Assets car ses positions initiales précédaient les corrections physiques.

Les créations passent par Undo. Aucun outil d'habillage n'enregistre automatiquement la scène : les sauvegardes ont été effectuées explicitement par la commande Unity. Le décor n'introduit pas de collider. Aucun changement de puissance des flippers n'a été fait dans cette passe.
