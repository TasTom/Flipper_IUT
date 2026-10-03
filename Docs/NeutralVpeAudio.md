# Neutral — audio et coordonnées des surfaces

Passe du 2 octobre 2026, après la [finition PBR](NeutralVpeQuality.md).
Le choix utilisateur reste l'adaptation des méthodes VPE à PhysX et URP,
avec six cibles de récompense et l'affichage tourné pour la borne.

## Défauts corrigés

La piste de fond dure 4 069,825 secondes. Son ancien import demandait une
décompression complète : **717 917 164 octets de PCM 16 bits**, avant le coût
interne de Unity. Elle est maintenant importée en streaming Vorbis, avec
chargement en arrière-plan, et reste stéréo. Ce chiffre est un calcul depuis
le nombre d'échantillons et les canaux, pas une mesure de mémoire récupérée.
Le [mode streaming Unity](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioClipLoadType.Streaming.html)
permet de lire progressivement les longues pistes.

Le gestionnaire réutilisait correctement `BackgroundMusic`, mais oubliait
d'appliquer son gain de 45 %. Ce gain s'applique maintenant à la source
réutilisée comme à une nouvelle source. Il n'y a toujours qu'une piste de fond.

Un autre défaut faisait jouer les contacts sous le seuil de silence au volume
maximal. Les petits effleurements restent maintenant silencieux ; l'intensité
des autres impacts utilise la composante de vitesse normale au contact.
L'écart de pitch exprimé en demi-tons est converti comme tel, plutôt qu'en
variation directe de ±12 %.

## Intégration sonore

`NeutralAudioConfig` regroupe gains, nombre de voix, banques de clips,
variations, délais et routage. `NeutralMixer` sépare Music, Mechanical et
Rewards, sous un Master à −6 dB. Les sons courts sont mono PCM préchargé.
La réserve de douze AudioSources réutilise une voix libre avant de couper
la plus ancienne ; elle ne crée pas un objet à chaque impact.

Quinze nouveaux WAV originaux accompagnent les appuis et retours des flippers,
le lanceur, les cibles validées, les rampes et la boucle réussies, les hausses
de multiplicateur, les billes supplémentaires, la fin de partie et les records.
Les flippers alternent trois samples par mouvement, le lanceur deux. Ce sont
des effets synthétisés avec transitoires et résonances amorties, pas des
enregistrements d'une machine réelle. Leur provenance et leur recette sont
documentées dans `Assets/Generated/NeutralVpeAudio/NOTICE.md`.

Les listeners audio se branchent sur les événements des mécanismes et des
gestionnaires. Ils n'attribuent aucun score et n'appliquent aucune force.
Les sons de réussite ne se déclenchent qu'après validation du parcours.
La pause suspend les voix et reprend la piste sans la relancer au début.
Cette organisation adapte les méthodes de banques, variation, mixage et
événements du [guide sonore VPE](https://docs.visualpinball.org/creators-guide/manual/sound.html).

L'écoute mécanique reste en 2D, adaptée à la caméra fixe et aux haut-parleurs
de la borne. Aucun modèle de diffusion spatiale VPE ni annonce vocale n'est
présenté comme implémenté. La musique existante garde sa provenance antérieure.

## Surfaces texturées

Plusieurs meshes générés n'avaient aucune UV ; le plateau imprimé avait des UV
mais aucune tangente. **Quarante variantes visuelles** complètent les coordonnées
et tangentes de **82 renderers**. La projection sépare les sommets lorsque son
plan change, en conservant les triangles et leurs normales d'origine. Les
coordonnées existantes du plateau restent intactes, seules ses tangentes sont
calculées. Le détail normal peut donc fonctionner avec une base tangentielle.

Les bandes animées conservent leurs normales analytiques et leur déformation
centrale. Leur matériau propre supprime la micro-normale inutile, sans modifier
la teinte ni la réponse mate. Les MeshColliders continuent de référencer leurs
meshes physiques d'origine. La passe ne modifie aucun composant physique ni
aucun réglage de flipper, de rampe, de bille ou de gravité.

Les outils d'installation utilisent Undo et ne sauvegardent pas la scène.
Le marqueur d'installation et la config déjà affectée empêchent de réinitialiser
les réglages manuels lors d'une deuxième application.

## Validation

Les recettes, rapports et captures sont dans
[NeutralVpeAudioValidation](NeutralVpeAudioValidation).

| Contrôle | Résultat |
| --- | --- |
| Surfaces, bases tangentielles, config, références et empreinte physique | 266 contrôles réussis |
| Audio en Play : variantes, routage, effleurements, cooldowns, flippers, lanceur, pause et récompenses | 25 contrôles réussis |
| Contacts réels des six cibles, parcours complets des deux rampes et loop, scores et sons de réussite | 23 contrôles réussis |
| Diff de scène | Aucun collider ni Rigidbody changé ou supprimé |
| Enregistrement après essais | Neutral enregistrée, Edit Mode, dirty=False ; 0 erreur, 0 avertissement |

## Traçabilité et limites

Un player Windows 64 bits Mono de contrôle a été construit avec la scène
Neutral explicitement sélectionnée : 329 391 129 octets, zéro erreur et un
avertissement d'outillage indiquant que Pipeline n'est pas activé dans le
player. Les anciennes passes de compilation exposent aussi des avertissements
dans les packages Inference et des exemples TMP ; un build incrémental ne
constitue pas leur suppression. Le lancement a terminé avec le code 0,
sans exception ni avertissement runtime observé.

Sur Ryzen 7 7800X3D / RTX 4070, la sonde force un rendu 1920 × 1080 dans une
RenderTexture, fenêtre masquée. Les 15 052 échantillons donnent une durée
médiane de frame de 0,901 ms, P95 1,612 ms et P99 2,063 ms. C'est une durée
murale incluant le rendu forcé, sans présentation à l'écran : ce n'est pas
un benchmark GPU isolé ni une mesure de latence des commandes de la borne.
La mémoire audio réservée mesurée est de 3 669 119 octets ; le pic du mix
observé vaut 0,496, sans saturation observée dans cette séquence courte.
Le compteur de draw calls renvoie zéro et n'est pas exploitable ici.

Ces mesures portent sur la passe audio/UV avant le nouvel agencement.
Le build et les contrôles de la scène avec scoop, aimant et lock sont
archivés séparément dans [NeutralMechanisms](NeutralMechanisms.md).

GDD : « Direction sonore », « Effets sonores », « AudioManager », « AudioConfig »,
« Direction artistique », « Architecture Unity » et « Performances WebGL ».
Cette passe ne complète pas les modes et parcours encore ouverts dans
[l'audit GDD](NeutralGddAudit.md). La source native du plateau reste à
887 × 1 774 pixels ; les UV corrigées ne créent pas de nouveau détail dans
cette illustration. La latence et le mixage sur la borne physique restent
à qualifier par une écoute sur ses haut-parleurs.

## Outillage

- `Pinball > Neutral > Installer les sons et le mixage` : pose les composants manquants et affecte la config audio vide.
- `Pinball > Neutral > Compléter les coordonnées des surfaces` : crée les variantes de rendu.
- `Pinball > Neutral > Build Windows de contrôle` : construit uniquement Neutral dans `Tools/build/NeutralVpeQuality`, sans modifier les scènes de Build Settings.

Le player de contrôle est lancé avec `-neutralQualityProbe <rapport.json>`.
La sonde est limitée aux variantes de code de debug et n'active aucune action
lors d'une partie normale. Elle lance la bille, lit le mix, enregistre des
durées de frame, capture le rendu et quitte après la séquence de contrôle.
