# Plastique du slingshot gauche

Défaut signalé dans Neutral : le plateau était visible à travers `SlingPlastic_Left`, alors que le plastique droit était opaque. Reproduction dans l’éditeur et comparaison des deux côtés, Unity 6000.6.0f1, URP 17.6.0. Base Git : `41e9cdc`.

Le [relevé des matériaux et maillages](baseline-plastics.txt) confirme le même matériau `ForestPlastic_PBR` opaque, alpha 1, sur les deux pièces, sans MaterialPropertyBlock en mode édition. Le défaut venait du maillage gauche : sa surface supérieure ne couvrait que 0,017970 unité² de bordure, contre 0,612597 à droite. Le rayon vertical au centre ne rencontrait aucune face gauche, contre deux à droite. Les faces centrales supérieure et inférieure étaient absentes.

La correction complète les deux faces planes à partir du contour existant du plastique gauche. Elle conserve les sommets, normales et UV précédents, le biseau, le matériau, le placement, les imprimés et les fixations. Le mesh visuel passe de 518 à 588 triangles. Aucun collider ne référence cet asset. La scène n’a pas été réenregistrée ; les ajustements antérieurs de l’utilisateur sont conservés.

- [Avant](before.png) et [après](after.png) : même caméra de contrôle, mêmes lumières.
- [Rendu de borne en Play](Neutral-play.png).
- [11 contrôles en édition](validation-edit.txt) et [11 contrôles en Play](validation-play.txt) : faces couvrantes des deux côtés, matériau opaque, UV/normales/tangentes, absence de référence physique, surfaces comparables et références de flash/rubber conservées.
- [Sonde géométrique après correction](cap-probe.txt) : deux intersections au centre de chaque pièce ; surface supérieure gauche 0,612601, écart à droite inférieur à 0,000005 unité².

Asset corrigé : `Assets/Generated/NeutralSurfaceCoordinates/000_SlingPlastic_Left_SurfaceCoordinates.asset`, avec GUID inchangé. L’édition utilise Undo et `SaveAssetIfDirty` pour enregistrer ce seul asset. Le script de réparation refuse un mesh déjà modifié ou utilisé en physique ; il est archivé ici pour la reproduction. Les contrôles portent sur le visuel et ses références, sans rejouer toute la physique du flipper. Référence de conception : GDD §Slingshots, présentation et feedback d’impact.

La Console conserve l’erreur d’import antérieure `AutodeskInteractiveTransparent.shadergraph` du package URP ; le défaut de plastique était reproduit malgré un matériau URP valide et résolu par la correction géométrique.
