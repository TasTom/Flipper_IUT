# Neutral — proportions et circulation

État enregistré du **28 septembre 2026**, `Assets/Scenes/Neutral.unity`. [Capture Unity](NeutralFinal.png). Cette implantation remplace celle décrite dans le rapport du 26 septembre.

## Changements

La bille reste à 0,45 unité de diamètre pour 27 mm réels. La surface de jeu et son inclinaison de 7° restent inchangées. La réduction concerne le mobilier, pas toute la physique.

- Chapeaux de bumpers réduits de 14 % en largeur, modèles de cibles de 15 %, slingshots d'environ 14 % en largeur par rapport à la passe précédente. Bumpers disposés en triangle et deux banques de trois cibles. Les colliders des bumpers conservent leurs dimensions ; ceux des cibles et slingshots suivent leur nouvelle taille.
- Deux rampes à retours croisés : Vosges revient à droite, IUT à gauche. Largeur intérieure de 0,66 unité pour la bille de 0,45 ; croisements séparés en hauteur, rails chromés et doublure transparente. Les tiges décoratives ne portent pas de colliders.
- Le plafond tient compte de la pente : une hauteur verticale constante pinçait la bille dans la montée IUT. Le dégagement est désormais calculé pour conserver environ 0,59 unité perpendiculairement à la pente locale.
- Portes de rampe repositionnées et références `partner` câblées réciproquement. Les essais vérifient l'événement réel `RampGate.Completed`.
- Inlanes courbes jusqu'aux talons des flippers. Pivots rapprochés et remontés ; angles, vitesses et contrôles conservés. Apron courbe couvrant les drains inférieurs. Les outlanes restent des sorties de perte, distinctes des inlanes.
- Guide court en sortie gauche d'orbite, avec une tangente descendante. Un guide trop long et horizontal avait créé un appui contre l'entrée de rampe : ce défaut a été reproduit puis corrigé.
- Six cibles conservées, conformément à la demande utilisateur. Cadrage de borne à 90°, score sur toute la hauteur des 15 % de droite, plateau dans le reste de l'écran.

L'illustration Vosges/IUT, les modèles Parts et le spinner Stern sont conservés. Les circuits remplacés et l'ancien `OrbitOuter_V2` restent désactivés.

## Vérifications

[Rapports et sondes](NeutralValidation/2026-09-28/). Unity 6000.6.0f1, colliders de la scène, simulation contrôlée au pas de 5 ms.

| Contrôle | Résultat |
| --- | --- |
| Deux rampes, vitesses 25/35/50/70 | 8 parcours sans fuite ni blocage durable ; six tirs à 35/50/70 terminent avec `Completed`, deux tirs faibles redescendent |
| Dégagement des parcours | Aucun collider étranger relevé par les sondes sphériques le long des lignes centrales |
| Retours gauche/droit et frappe | 5 timings sur 6 par côté relancent au-delà de Z=9 ; les frappes les plus tardives produisent un tir plus court |
| Lancements avec frappes simulées à 65/80/100 % | Accès aux flippers dans les trois cas, 1 à 2 frappes, aucun blocage durable ; 3,62 à 5,88 s avant perte |
| Quatre puissances de lancement et sept retombées hautes | Aucune fuite ; retours au lanceur relançables, sans immobilisation durable dans l'aire de jeu |
| Six cibles | 6/6 accessibles et validées par impact |
| Outlanes G/D à 5/25/60 | 6/6 : une seule perte, deux billes restantes |
| Multiball | Trois billes suivies, espacement minimal 0,60, volumes libres, départ vers les flippers, nettoyage au redémarrage |
| Spinner | Ancre concordante, rotation de 134° après impact, score augmenté |
| Scores en 1920×1080, 2560×1440 et 1920×1200 | Coins projetés X=0,85…1 et Y=0…1 |
| Intégrité | Six cibles, zéro script manquant, compilation réussie |

Les erreurs de console de cette passe sont des expirations de 5 secondes du bridge Pipeline, pas des exceptions de gameplay. Les rapports finaux proviennent d'appels réussis. Les messages d'abonnement Unity AI sont indépendants de la scène.

Ces essais ne remplacent pas une partie humaine sur borne. Les frappes utilisent la méthode de rotation des flippers, sans bouton xin-mo ; le ressenti et l'équilibrage restent à valider sur le matériel. Aucun nouvel exécutable n'est livré.

## Provenance et entretien

[Sources et licences de la passe précédente](NeutralProfessional.md#références-et-provenance), notamment [vbousquet/pinball-parts](https://github.com/vbousquet/pinball-parts), CC BY-SA 4.0. Les modèles adaptés conservent ces conditions. [Bille standard](https://www.pinballlife.com/1-116-pinball-standard-size.html) et [flipper 3 pouces](https://www.pinballlife.com/flipper-bat-and-shaft-assemblies-no-logo.html) servent d'ancres dimensionnelles. Les réductions esthétiques locales sont un choix pour cette table arcade, pas une reproduction mécanique exacte. VPE demeure une référence, sans migration du moteur Unity/URP.

Correspondance GDD : rampes, loops, bumpers, cibles, flippers, score et contrôle de la bille. La reconstruction et le nombre de six cibles sont explicitement demandés par l'utilisateur.

`RefineNeutralFlow` est un outil d'édition explicite avec Undo, protégé contre une nouvelle application sur un groupe existant. Il ne sauvegarde pas automatiquement la scène. La scène enregistrée et les maillages `Assets/Generated/NeutralFlow/` constituent l'état livré. Les corrections intermédiaires sont conservées, leurs anciens rendus et colliders désactivés ; ne pas les rejouer aveuglément.
