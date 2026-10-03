# Musique et maquettes des deux tables

Industries possède désormais une boucle orchestrale de Scott Buckley, trois secteurs de
décor (bois, textile, énergie) et une façade d’atelier. Trois des cinq bumpers sont déplacés
en quinconce, avec leurs deux lampes respectives. Les six cibles, les couloirs, les flippers,
le lanceur et les rampes natifs gardent leur câblage. Neutral reçoit une maquette de campus
et deux sapins à la place du terminal décoratif. Le cadrage de borne et le cabinet des
scores sont conservés.

Les objets de décor sont dépourvus de colliders, distincts de la simulation VPE/PhysX.
Les maquettes sont construites dans Blender puis importées en FBX : unités mesurées,
arêtes biseautées, UV explicites, matériaux URP Lit réutilisés par les deux tables.
Chaque modèle reste réglable dans la hiérarchie `MiniatureScenery`.

Le GDD relie ces ajouts à **Conception de la table / Zone haute** (IUT, sapins et Vosges)
et à **Direction sonore** (musique de partie, impacts lisibles, attribution des sons).
La préférence orchestrale pour Industries vient de la demande explicite de l’utilisateur.

## Musique

« Machinery of the Stars » by Scott Buckley — released under CC-BY 4.0.
www.scottbuckley.com.au

[Morceau et licence de l’auteur](https://www.scottbuckley.com.au/library/machinery-of-the-stars/).
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
L’extrait 00:35–03:42 est bouclé avec un fondu circulaire de six secondes, sans le silence
final de huit secondes du MP3 original. Fichier PCM stéréo 44,1 kHz, durée 181 secondes,
crête −1 dBFS. Unity importe la musique en Streaming Vorbis qualité 0,85.
La musique utilise le groupe Music du mixer existant, volume de source 0,28 ; les impacts
Industries utilisent Mechanical. Le profil de Neutral est conservé ; Industries en a une copie.
Un fondu d’entrée de 1,2 seconde est configuré uniquement sur le profil Industries.
La pause suspend/reprend la lecture à la même position et affiche le crédit musical.

## Guides consultés et application

- [Exécution VPE](https://docs.visualpinball.org/creators-guide/setup/running-vpe.html) :
  essai en Game View, distribution de bille et comportement de la table native. Les
  contrôles GDD/manette existants priment sur les touches par défaut du tutoriel.
- [Vue d’ensemble](https://docs.visualpinball.org/creators-guide/introduction/overview.html)
  et [fonctionnalités](https://docs.visualpinball.org/creators-guide/introduction/features.html) :
  physique et animation natives conservées ; ajustements locaux des bumpers plutôt qu’une
  reconstruction des mécanismes. Les matériaux importés restent un point de départ à habiller.
- [Manuel](https://docs.visualpinball.org/creators-guide/manual/manual.html) et
  [audio](https://docs.visualpinball.org/creators-guide/manual/sound.html) : séparation
  musique/retours mécaniques/règles de jeu, suspension lors des transitions et des pauses.
- [Tutoriels](https://docs.visualpinball.org/creators-guide/tutorials/index.html) : les
  parcours plateau, plastiques, backglass et scan ont été examinés. Les nouvelles maquettes
  suivent les étapes de nettoyage, UV, export et import ; elles sont originales et ne
  nécessitent pas la retopologie d’un scan. Le plateau et le backglass existants ne sont
  pas remplacés dans cette passe.
- [Styleguide](https://docs.visualpinball.org/creators-guide/editor/asset-library-styleguide.html) :
  échelle cohérente, petits détails géométriques, bevels, matériaux PBR, couleurs sans
  éclairage précalculé, séparation des colliders. Les recettes HDRP sont adaptées à URP.

## Vérifications

Les fichiers de ce dossier contiennent les résultats réels des tests, les captures des
deux scènes, la préparation de boucle et le rapport de build Windows. Les tests en Play
couvrent la lecture musicale, la pause/reprise, le crédit, le lancement natif, les six
cibles, le bonus, le scoop, les drains, le redémarrage et la transition à 100 000 points.
Ces essais ne remplacent pas une écoute subjective sur les haut-parleurs de la borne ni
une session prolongée avec son contrôleur physique.

Les scripts d’éditeur ajoutent uniquement les enfants absents avec Undo ; le déplacement
de trois bumpers est une migration explicite séparée, marquée `OriginalWorkshopLayout`,
exécutée une seule fois. Ils n’enregistrent jamais automatiquement les scènes. Les anciens
visuels du terminal de Neutral restent présents avec leurs seuls Renderer désactivés.

Sources reproductibles : `build_miniature_scenery.py.txt`, `prepare_industries_music.py.txt`
et les scripts C# archivés ici. Les scripts exécutables d’édition restent aussi dans Assets/Editor.

Résultats : **14 contrôles audio/décor**, **16 contrôles de gameplay natif** et **7 contrôles
de transition** passent. Le build Windows des deux scènes est produit avec succès.
Il contient encore trois diagnostics d’import du Shader Graph AutodeskInteractiveTransparent
et des avertissements des shaders Sentis/VPE/TMP déjà présents dans le projet ; le détail
figure dans `windows-build.txt`. Aucun échec de compilation C#.
Les réglages de préfiltrage URP, la liste des assets préchargés et UnityConnect modifiés par
le build ont été restaurés via les API Unity. Le record de l’utilisateur reste à sa valeur initiale.
