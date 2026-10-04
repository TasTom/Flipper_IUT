# Correction des cibles d'Industries — 4 octobre 2026

Branche `fix/industries-targets`. Sources : GDD §Cibles fixes et état visuel, règles de la seconde table décrites dans `Docs/Industries.md`, demande de l'utilisateur d'harmoniser les six cibles et leurs numéros.

Les six cibles utilisent le même maillage et le même matériau. La banque `sw11–sw13` est le miroir de `sw1–sw3` ; les composants natifs restent trois DropTarget et trois HitTarget. Les colliders fixes passent au gabarit de 19 mm. Chaque numéro est placé à 18 mm de sa face de contact, centré, avec le même diamètre de voyant (15 mm). Les lampes VPE pilotent l'émission des anneaux ; aucune décoration ne porte de collider. Les paramètres visuels sont dans `IndustriesConfig`.

Le plateau imprimé original est conservé. Une variante est générée depuis sa source procédurale, avec uniquement les six anciens repères supprimés. Le nouveau matériau de plateau utilise cette variante. La commande d'éditeur `Flipper/Industries/Corriger les six cibles et leurs numéros` est explicite et annulable ; elle n'enregistre pas la scène.

- [Cibles 1–3 après correction](targets-1-3.png)
- [Cibles 4–6 après correction](targets-4-6.png)
- [29 vérifications de géométrie](editor.txt) : gabarit, centrage des six chiffres, rayon des six voyants, symétrie, types de cibles et colliders.
- [Validation de partie avec contacts physiques](runtime.txt) : suite existante de 16 contrôles, incluant les six cibles et le bonus de série.
- [Six voyants pilotés par VPE](lamps.txt) : émission des anneaux allumés et éteints.
- [Annulation et relance](undo.txt) : Undo, Redo et absence de doublons.

Les changements restent appliqués dans la scène Industries ouverte, sans sauvegarde automatique conformément à AGENTS.md. Pour les conserver dans le fichier de scène, enregistrer Industries dans Unity. L'erreur d'import du shadergraph AutodeskInteractiveTransparent de URP, déjà présente avant la correction, subsiste ; elle a mis le début du test en pause, puis le test a été repris.
