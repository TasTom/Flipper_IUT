# Neutral — rampes, scoop, lock, aimant et lampes

Passe du 3 octobre 2026. Scène : `Assets/Scenes/Neutral.unity`.
La demande explicite ajoute un aimant, un scoop et un ball lock au design de
Vosges Mania. Ces trois mécanismes ne figuraient pas dans le GDD initial.
Les rampes, effets lumineux, gestion des billes et multiball se rattachent
aux sections correspondantes du [GameDesignDocument](../GameDesignDocument.docx).
Les six cibles de récompense et l'orientation de la borne restent les choix utilisateur.

## Agencement et références

Le plan supérieur et l'assemblage de rampe du
[manuel officiel Stern Jurassic Park LE/Premium](https://wp.sternpinball.com/wp-content/uploads/2023/04/JurassicPark_LE_Pre_web.pdf),
pages 38 et 56, ont été examinés visuellement. Ils montrent des entrées évasées,
des courbes, guides de retour et niveaux distincts. Ils servent de référence de
construction ; Neutral n'est pas une reproduction de cette machine.
Le [modèle Premium](https://www.sternpinball.com/game/jurassic-park/) illustre
aussi l'usage d'un lock mécanique. Aucun modèle ou artwork commercial n'est copié.

Les coordonnées ci-dessous sont locales à `PinballTable`, avec +Z vers le fond.

| Élément | Avant | Après |
| --- | --- | --- |
| Entrée Vosges | (−1,65 ; 0 ; 9,65) | (−2,15 ; 0 ; 11,25), soit +1,60 vers le fond |
| Entrée IUT | (0,25 ; 0 ; 8,90) | (0,25 ; 0 ; 11,15), soit +2,25 vers le fond |
| Scoop | Absent | (−1,05 ; 0 ; 11,55), entre les deux tirs |
| Aimant Café | Absent | (−0,40 ; 0 ; 9,05), noyau affleurant |
| Spinner | Traversait le tir central | Déplacé dans le passage de gauche, x=−3,67, z=12,60 ; ancrage du joint recalé |

Les entrées mesurent 0,90 unité, puis se resserrent progressivement à 0,6072,
contre un diamètre de bille de 0,414. La garde normale sous le plafond vaut
0,55. Les montées commencent avec une tangente horizontale, sans marche
verticale, et suivent des courbes de Bézier. Le trajet IUT a été rentré vers
l'intérieur pour dégager le retour Vosges : la première variante les faisait
entrer en collision. Les sorties dans les inlanes restent aux positions validées.

Le wireform est dessiné avec deux runners, rebords, rails intermédiaires et
entretoises. Sa collision reste un canal continu plutôt qu'une simulation
individuelle de chaque fil. Le plafond et les protections sous les bas de
rampe évitent que la bille ne s'échappe ou entre sous une lèvre. Cette
représentation PhysX est une adaptation, pas la physique native de VPE.

## Comment jouer les nouveaux mécanismes

**Une rampe entièrement parcourue allume le lock.** Tirer ensuite dans le
scoop verrouille une bille. Une bille de remplacement apparaît au lanceur,
sans diminuer les billes restantes. Après une nouvelle rampe, le second
verrouillage déclenche la libération successive des deux billes stockées,
puis complète le multiball à trois billes actives au maximum.

Un scoop non allumé capture brièvement et renvoie la bille. Pendant un
multiball, il renvoie aussi la bille sans engager un nouveau verrouillage.
Les billes retenues sont exclues de l'anti-blocage, de la vitesse plafond
et de la perte sous la table. Pause, reprise et redémarrage respectent leur état.
Le démarrage du multiball existant tient compte des billes déjà stockées.

La cible **Café** alimente l'aimant pendant 1,4 seconde. La force attire et
amortit progressivement la bille dans le plan du plateau ; elle ne la fige
pas. La montée/descente du courant et la force proportionnelle au carré du
courant adaptent les principes du
[guide VPE des aimants](https://docs.visualpinball.org/creators-guide/manual/mechanisms/magnets.html).
Le champ est limité en hauteur pour laisser passer les billes sur les rampes.

Le scoop est la pièce `Assets/Models/Parts/Kickers/Scoop_1.fbx` ; le noyau est
`Miscellaneous/Magnet_Core.fbx`, issus de
[pinball-parts](https://github.com/vbousquet/pinball-parts), CC BY-SA.
La réduction de 8 % porte uniquement sur leur racine. Le plateau physique et
son imprimé ont une vraie ouverture circulaire de 0,66 unité ; un trigger
sous la surface détecte la chute. Le kicker replace la bille à la lèvre
avant de lui appliquer une impulsion : la tige et la bobine internes ne
sont pas simulées mécaniquement.

## Lampes et données

**15 groupes commandent 42 émetteurs** : deux rampes, scoop, lock, aimant,
loop, six récompenses et trois groupes de GI. Chaque groupe référence
explicitement ses émetteurs, selon le principe des
[Light Groups VPE](https://docs.visualpinball.org/creators-guide/manual/mechanisms/light-groups.html).
Les réussites déclenchent les flashes ; le lock allumé pulse ; le multiball
anime les rampes, la boucle et la GI. Les lentilles n'ont aucun collider.
L'émission des matériaux et le bloom assurent leur visibilité, sans ajouter
42 lumières URP : les trois lumières de table existantes sont conservées.

Les réglages sont dans
`Assets/Generated/NeutralMechanisms/MechanismConfig.asset` : champ magnétique,
temporisations, vitesse d'éjection, capacité, points et émission lumineuse.
Les scripts sont séparés : `PlayfieldMagnet`, `PinballScoop`, `BallLock`,
`PinballLightGroup` et `PinballLightDirector`. `BallManager` distingue les
billes retenues des billes actives ; `MultiballManager` complète le nombre requis.

## Vérification

Les rapports, recettes et captures sont archivés dans
[NeutralMechanismValidation](NeutralMechanismValidation).

| Essai | Résultat |
| --- | --- |
| Géométrie finie, UV/tangentes, références, lampes, trou, marges des inserts et joint du spinner | 130 contrôles, aucun échec |
| Aimant, capture réelle, éjection, remplacement, double lock, pause, reset | 22 contrôles, aucun échec |
| Contacts réels des six cibles, lampes, deux parcours complets, armement du lock, boucle, score et sons | 32 contrôles, aucun échec |
| Régression des entrées et du mixage sonore | 25 contrôles, aucun échec |
| 36 tirs dans les rampes : 15 à 45 unités/s, trois décalages par vitesse | 11 parcours complets, 25 retours ou drains, aucune bille coincée |
| Tirs produits par les flippers, 21 temporisations de chaque côté | Au moins un tir traverse entièrement chaque rampe et déclenche sa sortie |

Le build Windows 64 bits Mono Development réussit : **331 951 219 octets,
zéro erreur**. Le player termine avec le code 0, sans exception ni avertissement
runtime observé. Le dernier build incrémental conserve un avertissement pour
le bridge Pipeline désactivé dans le player. Une compilation précédente a
émis 489 avertissements : 487 pour les variantes de shaders Inference/Sentis,
un pragma de shader TMP déprécié et un pour Pipeline. Le cache de compilation
ne constitue pas leur suppression ; le rapport précédent est aussi archivé.

La sonde force un rendu 1920 × 1080 dans une RenderTexture, fenêtre masquée :
médiane 1,038 ms  P95 1,746 ms  sur 13 441 frames ; mémoire audio réservée
3 668 303 octets. Cette courte charge active lanceur, flippers, circulation
de bille et sons. Elle ne mesure ni présentation à l'écran, ni latence
matérielle, ni une partie entière de multiball. Les contrôles de capture et
de double lock sont ceux effectués dans l'éditeur.

[Capture du player final](NeutralMechanismValidation/vpe-windows-player.png) ·
[Vérification des lampes allumées](NeutralMechanismValidation/neutral-mechanisms.png).
La seconde capture allume temporairement les groupes pour examiner couleurs
et espacement ; cet état n'est pas enregistré dans la scène.

Les tirs faibles doivent redescendre. Les fenêtres réussies prouvent que les
rampes sont atteignables ; elles ne représentent pas un taux de réussite
humain ni une aide automatique. Les flippers, bumpers, slingshots, six cibles
et la taille de la bille conservent leurs réglages physiques précédents.
Les changements physiques intentionnels portent sur les rampes, l'ouverture,
le scoop et la pose du spinner.

## Outillage et limites

`Pinball > Neutral > Agencer les rampes et ajouter les mécanismes` installe
la révision si le marqueur `Rails/MechanismRevision` manque. Chaque création
passe par Undo ; une seconde application conserve les réglages. L'outil
ne sauvegarde pas la scène. Les anciennes routes préexistantes restent
dans la scène avec renderers et colliders désactivés ; seules les versions
temporaires créées pendant cette passe ont été retirées de la hiérarchie.

Le lock n'implémente pas à lui seul le mode Nuit de l'Info complet, les
jackpots ni les récompenses de mission encore ouvertes. La nouvelle géométrie
ne transforme pas la source graphique de 887 × 1 774 pixels en artwork HD.
La validation sur les commandes et haut-parleurs de la borne et la livraison
WebGL restent à faire. Voir [l'audit GDD et ses évolutions](NeutralGddAudit.md).
