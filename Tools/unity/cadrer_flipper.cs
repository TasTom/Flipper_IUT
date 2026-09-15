// Cadre la vue Scène à la VERTICALE au-dessus d'un flipper, pour regarder ce que voit le joueur.
//
// Lecture seule sur la scène : ne touche qu'à l'état de la vue Scène (pivot, orientation, zoom),
// jamais aux objets. Le paramètre `--cible` se passe par variable d'environnement, faute de
// pouvoir transporter d'argument par `eval_file`.

var nom = System.Environment.GetEnvironmentVariable("FLIPPER_CIBLE");

if (string.IsNullOrEmpty(nom)) { nom = "Flipper_Left_Pivot"; }

var objet = GameObject.Find("PinballTable/Gameplay/" + nom);

if (objet == null) { return "'" + nom + "' introuvable."; }

var t = objet.transform;

var vue = UnityEditor.SceneView.lastActiveSceneView;

if (vue == null) { return "aucune vue Scène active."; }

vue.pivot = t.position;
vue.rotation = Quaternion.Euler(90f, 0f, 0f);   // à la verticale, on regarde le plateau
vue.orthographic = true;
vue.size = 1.1f;                                 // ~2,2 u de champ : le flipper et le slingshot

vue.Repaint();
UnityEditor.SceneView.RepaintAll();

return "cadré sur " + nom + " à " + t.position.ToString("F3") + "   ortho, taille " + vue.size;
