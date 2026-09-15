// Où le sol de la table s'arrête-t-il ?
//
// Après correction du décalage, la bille roule correctement dans le couloir puis disparaît à
// `z −0,505` — en dehors de l'emprise de la table, dont le bord bas est à `z = 0`. Elle ne
// traverse donc rien : elle **sort par le bas du couloir de lancement**, qui n'est fermé par
// rien. L'hôte `Plunger` est à `z 0,167`, juste là où il faudrait un butoir — mais il est vide,
// comme tous les hôtes.
//
// Cette sonde relève le profil du sol, maille par maille, sur l'axe du couloir et sur l'axe
// médian. C'est ce profil qui dira où poser le lanceur, et quelle longueur de couloir il doit
// fermer. Elle sert aussi de contrôle : un trou dans le plateau se verrait comme une colonne
// sans sol au milieu des autres.

var sb = new System.Text.StringBuilder();

var root = GameObject.Find("PinballTable");
var spawn = GameObject.Find("BallSpawnPoint");

if (root == null) { return "PinballTable introuvable"; }

sb.AppendLine("=== profil du sol (rayon vers le bas depuis y = 3) ===");
sb.AppendLine("rappel : bas de la table à z = 0, mur du fond à z ≈ 17,5 ; bille de rayon 0,225");

// --- les deux axes ------------------------------------------------------------------------------

float[] axes = { 4.670f, 0.000f };
string[] noms = { "couloir (x 4,670)", "médian  (x 0,000)" };

sb.AppendLine();

for (int i = 0; i < axes.Length; i++)
{
    sb.AppendLine("--- " + noms[i] + " ---");

    for (float z = 2.50f; z >= -0.75f; z -= 0.25f)
    {
        // Le point de départ est au-dessus de la table, dans le repère du monde ; on corrige la
        // pente pour rester à hauteur constante au-dessus de la surface.
        var origine = new Vector3(axes[i], 3f + z * 0.122f, z);

        RaycastHit hit;

        bool touche = Physics.Raycast(origine, Vector3.down, out hit, 30f, ~0, QueryTriggerInteraction.Ignore);

        sb.AppendLine("  z " + z.ToString("F2").PadLeft(5) + "   "
                      + (touche
                         ? "y " + hit.point.y.ToString("F4").PadLeft(8) + "   " + hit.collider.gameObject.name
                           + "   " + (hit.collider.isTrigger ? "DÉCLENCHEUR" : "")
                         : "— AUCUN SOL —"));
    }

    sb.AppendLine();
}

// --- les hôtes du bas de table ------------------------------------------------------------------

sb.AppendLine("=== hôtes du bas de table ===");

string[] hotes = { "Plunger", "DrainZone", "BallSpawnPoint", "Flipper_Left_Pivot", "Flipper_Right_Pivot" };

foreach (string nom in hotes)
{
    var trouve = Trouver(root.transform, nom);

    if (trouve == null)
    {
        sb.AppendLine("  " + nom.PadRight(20) + "ABSENT");
        continue;
    }

    int colliders = trouve.GetComponentsInChildren<Collider>(true).Length;
    int renderers = trouve.GetComponentsInChildren<Renderer>(true).Length;

    sb.AppendLine("  " + nom.PadRight(20)
                  + " local " + trouve.localPosition.ToString("F4").PadRight(26)
                  + " colliders " + colliders + "   renderers " + renderers);
}

// --- le drain -----------------------------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== drain ===");

var drain = Trouver(root.transform, "DrainZone");

if (drain != null)
{
    sb.AppendLine("  position monde : " + drain.position.ToString("F4"));

    var collider = drain.GetComponent<Collider>();

    sb.AppendLine("  collider       : "
                  + (collider != null
                     ? collider.GetType().Name + "   déclencheur " + collider.isTrigger
                     : "AUCUN — aucune bille ne sera comptée perdue"));
}

return sb.ToString();

// --- helper -------------------------------------------------------------------------------------

Transform Trouver(Transform parent, string nom)
{
    if (parent.name == nom) { return parent; }

    foreach (Transform enfant in parent)
    {
        var trouve = Trouver(enfant, nom);

        if (trouve != null) { return trouve; }
    }

    return null;
}
