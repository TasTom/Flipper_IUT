# Référence — Visual Pinball Engine, ce qu'on peut en tirer

Analyse du dépôt [freezy/VisualPinball.Engine](https://github.com/freezy/VisualPinball.Engine) (VPE),
clone local dans `Tools/VisualPinball.Engine/`. Fait le 2026-09-15.

**Objectif de ce document :** comparer notre physique à celle d'un moteur qui fait référence, et
savoir ce qui est transposable.

---

## ⚠️ Licence — à lire avant de copier quoi que ce soit

VPE est en **GPL-3.0**, comme `vpinball/vpinball`. C'est un copyleft : **recopier du code de ces
dépôts rendrait tout le projet GPL-3.0**, avec obligation de distribuer les sources.

Ce qui n'est PAS contaminant :
- **les valeurs numériques** (une constante physique n'est pas une œuvre) ;
- **les approches et algorithmes** — une méthode de résolution n'est pas protégeable ;
- **lire pour comprendre**, puis réécrire à sa façon.

Ce qui l'est : copier-coller du code, même renommé.

Ce document ne consigne donc que des **valeurs** et des **principes**. Aucune ligne de VPE n'est
reprise dans `Assets/Scripts/`.

---

## 1. L'architecture : deux mondes différents

| | VPE / VPX | Notre projet (Unity PhysX) |
|---|---|---|
| Moteur | **écrit à la main**, analytique | solveur de contraintes généraliste |
| Formes | primitives : points, segments, cercles | meshes, boîtes, capsules |
| Détection | **temps de collision calculé exactement** | discrète ou continue, par balayage |
| Pas de temps | **1 kHz**, sous-pas jusqu'à 5 µs | 50 Hz |
| Collisions | résolues par temps, pas par pénétration | dé-pénétration itérative |
| Échelle | **unités réelles** (1 u Unity = 1 m) | 1 u = 60 mm |

C'est la différence fondamentale, et elle explique pourquoi VPX « sonne juste » : en calculant le
**temps exact** de chaque collision, il n'y a ni tunnelling, ni pénétration à rattraper, ni ces
oscillations qu'on a passé des heures à traquer sur nos flippers.

Le README de leur moteur physique le décrit : à chaque pas de 1 ms, ils calculent le prochain
instant de collision, avancent les objets mobiles jusqu'à cet instant, résolvent, et **recommencent
en sous-pas** tant qu'il reste des collisions.

## 2. L'échelle — la confirmation de notre diagnostic

`VisualPinball.Unity/Physics/Physics.cs` :

```
Scale = 1852.71        // conversion unités VPX -> unités Unity
```

Une unité VPX vaut ~0,54 mm (leur plateau standard fait 952 unités pour 0,514 m). Donc
`1 unité Unity = 1 mètre`. **VPE travaille à l'échelle réelle**, et toutes ses constantes sont des
valeurs réelles.

Ce que ça donne chez eux :

| Grandeur | Valeur VPE | En réel |
|---|---|---|
| Gravité (référence) | `Constants.Gravity = 1.81751` | 9,81 m/s² |
| Gravité par défaut d'une table | `DefaultTableGravity = 0.97` | × 0,97 |
| Gravité effective | `GravityConst × 0.97 = 1.762985` | — |
| Masse du flipper | `1 = 80 g` (poids de la bille) | 80 g |
| Rayon du socle de bumper | `45` unités VPX | ≈ 24 mm |
| Rayon de base du flipper | `21.5` | ≈ 11,6 mm |
| Rayon de pointe du flipper | `13.0` | ≈ 7,0 mm |
| Longueur max de flipper | `130` | ≈ 70 mm (3") |

**Et chez nous :** 1 u = 60 mm, mais la gravité appliquée vaut 9,81 u/s² — soit **0,59 m/s² en
réel**. Elle est **16,67× trop faible**, exactement le rapport d'échelle.

→ La gravité correcte pour notre table est **163,5 u/s²** (9,81 ÷ 0,06).

## 3. Les valeurs du flipper, comparées aux nôtres

`VisualPinball.Engine/VPT/Flipper/FlipperData.cs` :

| Paramètre VPE | Valeur | Sens | Chez nous |
|---|---|---|---|
| `Mass` | **1** | 1 = poids de la bille (80 g) | Rigidbody masse 1 — ✅ cohérent |
| `Elasticity` | **0.8** | rebond du bat | matériau physique absent (défaut 0) ❌ |
| `ElasticityFalloff` | **0.43** | perte de rebond avec l'angle | absent |
| `Strength` | **2200** | force du solénoïde | `swingSpeed` 2200 °/s — **homonyme, unités différentes** |
| `Return` | **0.058** | rappel au repos, en fraction de `Strength` | `returnSpeed` 700 °/s |
| `TorqueDamping` | **0.75** | amortissement en fin de course | absent |
| `TorqueDampingAngle` | **6** | angle où l'amortissement s'applique | absent |

**À noter :** leur rappel vaut **5,8 %** de la force d'attaque — donc environ 17× plus lent que la
montée. Le nôtre est à 700/2200 = **32 %**, soit un retour deux fois plus vif que la référence.

Et leur bat a une **élasticité propre de 0,8** : c'est elle qui donne le « clac » du flipper. Chez
nous le collider du bat n'a aucun matériau physique, donc un rebond par défaut de 0 — la bille
s'écrase dessus au lieu d'être catapultée.

## 4. Le plateau, la bille, le lanceur

| Élément | VPE | Chez nous | Écart |
|---|---|---|---|
| Gravité | échelle réelle | 9,81 u/s² | **16,7× trop faible** |
| Pas physique | 1 kHz + sous-pas | 50 Hz | **20× trop grossier** |
| Vitesse angulaire bille | — | 50 rad/s | **15× trop bas** pour rouler |
| `PlungerMass` | 30 | — | non modélisé |
| `MechStrength` (plongeur) | 85 (défaut table : 82,3) | 17,96 u/s de poussée | **4–5× trop lent** |
| `DefaultTableGravity` | 0,97 | — | réglable par table |

## 5. Ce qui est transposable, et à quel prix

### ✅ Immédiat — des constantes, aucun code

| Quoi | Maintenant | Cible | Où |
|---|---|---|---|
| Gravité | 9,81 | **163,5** | `TableGravity.gravityStrength` |
| Rotation de la bille | 50 | **1000 rad/s** | prefab `Ball` |
| Vitesse max de la bille | 22 | **≈ 170** | `BallManager.maxSpeed` |
| Vitesse du plongeur | 17,96 | **≈ 80** | `Plunger` |
| Pas physique | 50 Hz | **200 Hz** | `Time.fixedDeltaTime` |
| Itérations solveur | 6 | **12** | `Physics.defaultSolverIterations` |

Effet attendu : la bille retombe en 0,47 s au lieu de 1,90 s, roule au lieu de glisser, et les tirs
portent. **C'est le plus gros gain de ressenti disponible, pour quelques valeurs.**

### ✅ Court terme — deux comportements à ajouter

1. **Matériau physique sur le bat de flipper**, élasticité ≈ 0,8. Sans lui, pas de « clac ».
2. **Rapport de rappel** : ramener `returnSpeed` de 32 % à ~6 % de `swingSpeed`, comme la
   référence.

### ⚠️ Moyen terme — l'architecture, inatteignable

Reproduire le moteur analytique (primitives, temps de collision, 1 kHz avec sous-pas) demanderait
de **réécrire toute la physique du projet** — c'est le cœur de VPE, des milliers de lignes. Ce
n'est pas un objectif raisonnable ici, et le GDD demande notre propre architecture.

Ce qu'on peut en imiter à bon compte : **monter la fréquence physique** (200 Hz au lieu de 50) et
**activer la détection continue** sur la bille. Ça rapproche du comportement sans le réécrire.

## 6. Ce que ce dépôt n'apporte PAS

- **Pas de géométrie de table** : c'est un moteur, pas un catalogue. Pour la géométrie, c'est
  `vbousquet/pinball-parts` qu'on utilise déjà.
- **Pas de son** : VPX joue ses sons via `PinMAME` et des tables `.vpx`.
- **Pas d'assets réutilisables directement** : leurs meshes sont générés depuis des `.vpx`.

---

## Conclusion

Le clone sert à deux choses :

1. **Il confirme par une source indépendante que notre problème est l'échelle.** Un moteur qui fait
   référence travaille en unités réelles ; nous appliquons 9,81 à une table où 1 unité vaut 60 mm.
2. **Il fournit des valeurs de référence** — élasticité de flipper 0,8, rappel à 5,8 %, masse de
   flipper = masse de bille, pas de temps 1 kHz.

La jouabilité ne se rattrape pas en réécrivant le moteur, mais **en alignant les constantes**.
C'est mesurable, et ça se fait en une passe.
