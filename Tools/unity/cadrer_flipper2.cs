// Cadre la vue Scène sur un flipper, selon un axe choisi. LECTURE SEULE sur la scène.
//
// Ne touche qu'à l'état de la vue Scène (pivot, orientation, zoom), jamais aux objets.
//
// Les paramètres viennent d'un FICHIER, pas de variables d'environnement : `eval_file` s'exécute
// dans le processus Unity, qui n'hérite pas de l'environnement du shell appelant — le premier
// essai a silencieusement gardé les valeurs par défaut. `Tools/unity/out/cadrage.txt` :
//
//   ligne 1 : cible     (défaut Flipper_Left_Pivot)
//   ligne 2 : vue       dessus | section | face
//   ligne 3 : taille ortho, en unités
//
//   dessus   : à la verticale — ce que voit le joueur
//   section  : le long du grand axe du bat — la SECTION du profil
//   face     : depuis le drain vers le fond — l'élévation du bat

var cible = "Flipper_Left_Pivot";
var vue = "dessus";
var tailleTexte = "";

var fichier = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                     "cadrage.txt");

if (System.IO.File.Exists(fichier))
{
    var lignes = System.IO.File.ReadAllLines(fichier);

    if (lignes.Length > 0 && lignes[0].Trim().Length > 0) { cible = lignes[0].Trim(); }
    if (lignes.Length > 1 && lignes[1].Trim().Length > 0) { vue = lignes[1].Trim(); }
    if (lignes.Length > 2) { tailleTexte = lignes[2].Trim(); }
}

var objet = GameObject.Find("PinballTable/Gameplay/" + cible);

if (objet == null) { return "'" + cible + "' introuvable."; }

var pivot = objet.transform;
var porteur = pivot.Find("Flipper_Bat");
var filtre = porteur != null ? porteur.GetComponentInChildren<MeshFilter>() : null;

// Repli : un objet qui n'est pas un flipper (un slingshot, par exemple) se cadre sur ses propres
// rendus. Sans ce repli, `cadrer_flipper2.cs` refusait de cadrer autre chose qu'un bat.
if (filtre == null) { filtre = pivot.GetComponentInChildren<MeshFilter>(); }

if (filtre == null) { return "aucun maillage sous '" + cible + "'."; }

// Le centre du bat en monde : plus fiable qu'une cote reconstruite, et il suit le bat quel que
// soit le repère.
var centre = filtre.GetComponent<Renderer>().bounds.center;

UnityEditor.SceneView sv = UnityEditor.SceneView.lastActiveSceneView;

if (sv == null) { return "aucune vue Scène active."; }

float taille;

switch (vue)
{
    case "section":
        // On regarde le long du grand axe du bat : la section apparaît de face.
        sv.pivot = centre;
        sv.rotation = Quaternion.Euler(0f, 90f, 0f);
        taille = 0.055f;
        break;

    case "face":
        // Depuis le drain : on voit l'élévation, la hauteur du bat.
        sv.pivot = centre;
        sv.rotation = Quaternion.Euler(0f, 0f, 0f);
        taille = 0.45f;
        break;

    default:
        sv.pivot = pivot.position;
        sv.rotation = Quaternion.Euler(90f, 0f, 0f);
        taille = 1.1f;
        break;
}

if (!string.IsNullOrEmpty(tailleTexte))
{
    float t;

    if (float.TryParse(tailleTexte, System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out t))
    {
        taille = t;
    }
}

sv.orthographic = true;
sv.size = taille;

sv.Repaint();
UnityEditor.SceneView.RepaintAll();

return "vue '" + vue + "' sur " + cible + "   centre (" + centre.x.ToString("F4") + " ; "
       + centre.y.ToString("F4") + " ; " + centre.z.ToString("F4") + ")   ortho "
       + taille + "   (source : " + (System.IO.File.Exists(fichier) ? "cadrage.txt" : "défauts")
       + ")";
