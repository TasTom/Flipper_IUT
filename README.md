# Vosges Mania - Flipper 3D

Projet de flipper 3D développé sous Unity (Universal Render Pipeline) dans le cadre du BUT Informatique. 
Le jeu propose une expérience d'arcade articulée autour de plusieurs niveaux thématiques (Campus IUT, Forêt vosgienne, Industries), un système de score avec multiplicateurs et une gestion complète du cycle de partie.

---

## Sommaire

1. [Présentation du projet](#présentation-du-projet)
2. [Commandes du jeu](#commandes-du-jeu)
3. [Fonctionnalités et règles du jeu](#fonctionnalités-et-règles-du-jeu)
4. [Configuration et équilibrage](#configuration-et-équilibrage)
5. [Installation et exécution](#installation-et-exécution)
6. [Auteurs](#auteurs)

---

## 1. Présentation du projet

Ce projet a été réalisé en C# avec le moteur de jeu Unity (URP). L'objectif était de concevoir un jeu de flipper jouable, modulaire et fidèle aux mécaniques classiques des machines physiques :

- Physique réaliste de la bille et des batteurs.
- Système de score dynamique avec combos, multiplicateurs et bonus de fin de bille.
- Architecture multi-scènes gérant la progression entre les tables.
- Menu principal avec sauvegarde locale du meilleur score.
- Cartes d'instructions directement intégrées sur la table (apron).

---

## 2. Commandes du jeu

Les contrôles sont configurés pour un clavier AZERTY :

| Action | Touche |
| :--- | :--- |
| **Batteur gauche** | `Q` |
| **Batteur droit** | `D` |
| **Lanceur de bille (Plunger)** | `Espace` (maintenir pour jauger la force) |
| **Pause** | `Échap` |

*Note : Les commandes sont également consultables directement dans le jeu depuis le bouton "Contrôles" du menu principal et sur la plaque de gauche du flipper.*

---

## 3. Fonctionnalités et règles du jeu

### Déroulement d'une partie
- Le joueur dispose de **3 billes** au départ.
- Des billes supplémentaires peuvent être débloquées en franchissant certains paliers de score.
- La bille est perdue lorsqu'elle tombe dans le drain inférieur.

### Progression entre les niveaux
La progression ne dépend pas uniquement de la perte des billes, mais du score accumulé :
- **Table 1 : Neutral (Campus & Forêt)**
  - Cibles simples, cibles matières, bumpers et rampes spécifiques (Vosges et IUT).
  - **Palier de passage :** 1 500 000 points.
  - Dès que le score atteint ce palier, la porte de progression (`ScoreProgressionGate`) déclenche automatiquement la transition vers la table suivante.
- **Table 2 : Industries (Atelier des Vosges)**
  - Deuxième environnement avec barème et contraintes de jeu plus exigeants.

### Système de score et combos
- **Multiplicateur global :** Peut monter jusqu'à ×5 en enchaînant les rampes ou en complétant des séries de cibles.
- **Combo Tracker :** Réaliser des tirs consécutifs dans un intervalle de temps réduit applique un bonus exponentiel.
- **Bonus de fin de bille :** À chaque perte de bille (hors tilt), un décompte de points est calculé selon le nombre de rampes empruntées et le niveau de combo maximal atteint.
- **Gestion du Tilt :** Secouer abusivement la table entraîne un avertissement, puis le blocage temporaire des batteurs et la perte des bonus de la bille en cours.

---

### Points clés de conception :
- **Persistance du record :** Le meilleur score est sauvegardé localement via l'API `PlayerPrefs` (`VosgesTilt_HighScore`) et synchronisé automatiquement sur le HUD et le menu principal.
- **ScriptableObjects :** Le barème des points (`ScoreConfig`) et les paramètres de niveau (`ProgressionConfig`) sont isolés du code dans des fichiers `.asset`, permettant d'ajuster l'équilibrage sans recompiler le projet.

---

## 5. Configuration et équilibrage

Les paramètres principaux peuvent être ajustés directement depuis l'inspecteur Unity :

1. **Seuil de changement de niveau :**  
   Fichier `Assets/Resources/Progression/ProgressionConfig.asset`  
   Variable : `Score Threshold` (par défaut fixé à `1 500 000`).

2. **Valeur des points par élément :**  
   Fichier `Assets/Config/NeutralScore.asset`  
   Contient la table des gains pour chaque cible, rampe, bumper et bonus de multiball.

3. **Tolérance du Tilt et billes bonus :**  
   Fichiers `Assets/Config/Difficulte_*.asset`  
   Ajuste le nombre de secousses autorisées avant pénalité et le palier d'extra ball.

---

## 6. Installation et exécution

### Prérequis
- **Unity 2022.3 LTS** (ou version compatible supportant URP).
- Module de compilation correspondant à votre OS (Windows / macOS / Linux).

### Lancement du projet dans l'éditeur
1. Cloner le dépôt :
   ```bash
   git clone <url-du-depot>
   ```
2. Ouvrir **Unity Hub**, cliquer sur **Add** et sélectionner le dossier racine du projet.
3. Vérifier la configuration des scènes dans **File > Build Settings...** :
   - Index `0` : `Assets/Scenes/MainMenu.unity`
   - Index `1` : `Assets/Scenes/Neutral.unity`
   - Index `2` : `Assets/Scenes/Industries.unity`
4. Ouvrir la scène `MainMenu` et lancer le mode **Play**.

---

## 7. Auteurs

Projet développé dans le cadre du cursus BUT Informatique (3ème année) :
- Équipe de développement : TAS Tom, CESAR Lucas, HOJA Valentin et FREY Alyssia