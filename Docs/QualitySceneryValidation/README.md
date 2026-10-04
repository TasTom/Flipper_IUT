# Décors 3D réels — Neutral et Industries

Le campus de Neutral utilise maintenant la maquette architecturale fournie dans
`Assets/Art/IUT/IUT2.obj`, au lieu du bâtiment générique. Ses façades, ailes et
proportions sont conservées. 1 361 triangles identiques ont été retirés ; 532 faces
de tôles ont été décalées de 30 mm à l'échelle source pour éviter les surfaces
coplanaires. 222 slots de matériaux sont regroupés en 31 apparences distinctes.
Les OBJ/MTL fournis ne sont pas modifiés.

Les sapins et les six pièces d'Industries sont des modèles texturés Poly Haven
(CC0), avec cartes de couleur, normales, occlusion, rugosité et métal adaptées à
URP. Rouet, perceuse à colonne, étau, rabot, cuve et souche remplacent les objets
simplifiés. Le socle du campus est un modèle Blender original avec chanfreins,
incrustations et fixations. Prefabs réutilisables :
`Assets/Art/Scenery/Quality/Prefabs`.

Rattachement au GDD : §Direction artistique, §Conception de la table / Zone haute
et §Habillage (bâtiment de l'IUT, gris béton/métal, sapins, éclairage chaud du campus).
Le décor des ateliers d'Industries complète le thème demandé par l'utilisateur.

Placement : échelles uniformes et pivots au pied du modèle ; appuis des machines
mesurés sur les surfaces natives d'Industries. Les légendes sont posées sur les
plastiques. Le campus et les sapins reposent sur leur socle adapté. Un spot sans
ombres éclaire le campus. Aucun décor ne reçoit de collider.

Validation réalisée dans Unity 6000.6.0f1 :

- Compilation de l'outil d'éditeur sans erreur C#.
- Relecture des deux scènes sauvegardées : meshes et matériaux présents, caméra
  de borne à 90° et à sa position d'origine, aucun collider PhysX/VPE sur les décors.
- Démarrage en Play de Neutral : une bille, trois crédits, PhysX ContinuousDynamic,
  masse 2 et pas physique 0,002 s.
- Démarrage en Play d'Industries : VPE initialisé, une bille distribuée, six cibles.
- Les 16 contrôles d'intégration natifs existants passent : flippers, lancement,
  contacts, six cibles, scoop, drain, fin et reprise de partie.
- Comparaison des composants sérialisés avec les scènes de départ : aucun mesh
  natif, collider, Rigidbody ou composant de gameplay existant modifié. Les IDs de
  meshes inline et le cache d'instance VPE sont normalisés pour cette comparaison.
- Inspection des rendus d'ensemble et des modèles isolés dans Unity.

Les captures d'ensemble utilisent temporairement une vue portrait lisible ; la
caméra réelle conserve son roulis de borne de 90°. Ces images ne remplacent pas
une validation sur l'écran physique de la borne. Pas de nouveau build Player pour
cette passe de décor ; les contrôles sont réalisés dans l'éditeur.

`QualitySceneryAuthoring` prépare les prefabs puis applique le remplacement
explicitement, avec Undo. Il ne sauvegarde jamais la scène et, après application,
ne recrée que les pièces manquantes sans réinitialiser les placements existants.
Les anciens objets restent présents, seuls leurs MeshRenderer sont masqués.
