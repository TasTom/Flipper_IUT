// La bille est-elle trop grosse, ou la table trop petite ?
//
// La question se tranche par un rapport sans dimension : **combien de billes tiennent sur la
// largeur de l'aire de jeu**. C'est lui qui décide de l'impression visuelle, pas la taille
// absolue. Les cotes réelles du standard, celles qui ont servi d'ancre à `build_table.py` :
//
//   bille                  1"1/16     =  26,99 mm
//   plateau                20,25"     = 514,35 mm, et c'est aussi la largeur **entre les murs**
//   rapport largeur / bille           =  19,06
//   couloir de lancement              ≈  32 mm, soit 1,19 bille
//
// Mesuré : l'aire de jeu fait 514,3 mm entre les faces internes du mur gauche et du séparateur,
// soit 19,05 bille. Le compte est bon.
//
// Deux corrections par rapport au premier jet de cette sonde, toutes deux vérifiées :
//
//   * `RaycastAll` ne relève que les faces d'**entrée** d'un collider — la face interne d'un
//     mur n'apparaît jamais. Un balayage de gauche à droite donnait donc « Left → Divider »
//     comme 520 mm, c'est-à-dire la face *externe* gauche jusqu'au séparateur. On tire
//     maintenant **depuis le centre vers chaque mur** : le premier contact est bien une face
//     interne.
//   * Une boîte englobante se calcule en transformant les **huit coins** du maillage, pas en
//     mettant à l'échelle son centre et sa taille : la racine du FBX porte un quart de tour, et
//     une taille mise à l'échelle sans la tourner échange `y` et `z`.

var sb = new System.Text.StringBuilder();

const float u = 16.66667f;          // unités Unity par mètre

// --- la bille ---------------------------------------------------------------------------------

var bille = GameObject.FindGameObjectWithTag("Ball");

if (bille == null) { return "aucune bille taggée 'Ball' dans la scène"; }

var rendu = bille.GetComponent<Renderer>();
var sphere = bille.GetComponent<SphereCollider>();

float diametre = rendu.bounds.size.x;
float rayon = sphere != null ? sphere.radius * bille.transform.lossyScale.x : diametre * 0.5f;

sb.AppendLine("=== la bille ===");
sb.AppendLine("échelle  : " + bille.transform.localScale.ToString("F4"));
sb.AppendLine("diamètre : " + diametre.ToString("F4") + " u = "
              + (diametre / u * 1000f).ToString("F2") + " mm   (standard : 26,99 mm)");
sb.AppendLine("rayon    : " + rayon.ToString("F4") + " u");

// --- la table, coins par coins ------------------------------------------------------------------

var racine = GameObject.Find("PinballTable");
var table = racine != null ? racine.transform.Find("Table/Pinball_Table") : null;

if (table == null) { return sb.Append("\nPinball_Table introuvable").ToString(); }

var bornes = new Bounds();
bool premier = true;

foreach (var filtre in table.GetComponentsInChildren<MeshFilter>())
{
    if (filtre.sharedMesh == null) { continue; }

    var local = filtre.sharedMesh.bounds;

    for (int i = 0; i < 8; i++)
    {
        var coin = new Vector3(
            (i & 1) == 0 ? local.min.x : local.max.x,
            (i & 2) == 0 ? local.min.y : local.max.y,
            (i & 4) == 0 ? local.min.z : local.max.z);

        var monde = filtre.transform.TransformPoint(coin);

        if (premier) { bornes = new Bounds(monde, Vector3.zero); premier = false; }
        else { bornes.Encapsulate(monde); }
    }
}

sb.AppendLine();
sb.AppendLine("=== la table (boîte englobante, monde) ===");
sb.AppendLine("largeur x : " + bornes.size.x.ToString("F4") + " u = "
              + (bornes.size.x / u * 1000f).ToString("F1") + " mm");
sb.AppendLine("longueur z: " + bornes.size.z.ToString("F4") + " u = "
              + (bornes.size.z / u * 1000f).ToString("F1") + " mm"
              + "   (standard : 1066,8 mm)");

// --- les intervalles, tirés depuis le centre -----------------------------------------------------

// Hauteur de tir : 0,30 u au-dessus de la surface de jeu — au-dessus du centre de la bille
// (0,225) et sous son sommet (0,45), la hauteur où un mur la retient.
float Hauteur(float z) { return z * 0.1228f + 0.30f; }

float Mesurer(Vector3 depuis, Vector3 direction, float portee, string nom)
{
    RaycastHit hit;

    if (!Physics.Raycast(depuis, direction, out hit, portee, ~0, QueryTriggerInteraction.Ignore))
    {
        sb.AppendLine("  " + nom.PadRight(34) + " : aucun mur dans la portée");
        return float.NaN;
    }

    sb.AppendLine("  " + nom.PadRight(34) + " : x " + hit.point.x.ToString("F4").PadLeft(9)
                  + "   (" + hit.collider.gameObject.name + ")");

    return hit.point.x;
}

sb.AppendLine();
sb.AppendLine("=== largeurs, tirées depuis le centre vers chaque mur ===");

const float z = 8f;
var centre = new Vector3(0f, Hauteur(z), z);

float gauche = Mesurer(centre, Vector3.left, 8f, "mur gauche (face interne)");
float droite = Mesurer(centre, Vector3.right, 8f, "séparateur du couloir (face interne)");

if (!float.IsNaN(gauche) && !float.IsNaN(droite))
{
    float largeur = droite - gauche;

    sb.AppendLine();
    sb.AppendLine("  aire de jeu              : " + largeur.ToString("F4") + " u = "
                  + (largeur / u * 1000f).ToString("F1") + " mm");
    sb.AppendLine("  rapport largeur / bille  : " + (largeur / diametre).ToString("F2")
                  + "   (table réelle : 19,06)");
}

// --- le couloir de lancement --------------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== couloir de lancement (z = 1,00, celui du point d'apparition) ===");

var dansCouloir = new Vector3(4.6f, Hauteur(1f), 1f);

float cGauche = Mesurer(dansCouloir, Vector3.left, 3f, "séparateur (face interne)");
float cDroite = Mesurer(dansCouloir, Vector3.right, 3f, "paroi externe (face interne)");

if (!float.IsNaN(cGauche) && !float.IsNaN(cDroite))
{
    float largeur = cDroite - cGauche;

    sb.AppendLine();
    sb.AppendLine("  largeur du couloir       : " + largeur.ToString("F4") + " u = "
                  + (largeur / u * 1000f).ToString("F1") + " mm = "
                  + (largeur / diametre).ToString("F2") + " bille"
                  + "   (réelle : ~32 mm = 1,19 bille)");
}

// --- le verdict ---------------------------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== verdict ===");
sb.AppendLine("bille : " + (diametre / u * 1000f).ToString("F2") + " mm"
              + "   (standard 26,99 mm)");

return sb.ToString();
