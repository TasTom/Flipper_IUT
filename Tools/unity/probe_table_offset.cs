// Où est la surface du plateau dans le repère du FBX ?
//
// La scène a `PinballTable/Table` à `y locale 2,360` — et aucun script ne pose cette valeur :
// `PlaceImportedTable.EnsureGroup` écrit `Vector3.zero`. C'est donc un réglage fait à la main
// dans l'éditeur. Reste à savoir laquelle des deux choses est fausse : ce décalage, ou les
// coordonnées des hôtes.
//
// Le repère du menu 4 est explicite : « y = 0 à la surface du plateau ». Si la face supérieure
// de la plaque du plateau est bien à `z = 0` dans le FBX, alors `Table.y` doit valoir 0 et les
// hôtes sont justes. Si elle est à `z = −14,16 cm`, alors les 2,36 u les rattrapent et c'est
// l'inverse.
//
// `Pinball_Table` **est** la racine du FBX : dans son repère local, les coordonnées sont donc
// celles écrites par Blender — `z` vers le haut, en centimètres (échelle de racine 1666,667 =
// 100 × 16,667). On y lit la cote de la face supérieure sans avoir à la déduire d'une boîte
// englobante inclinée, ce qui est précisément ce qui rendait la mesure précédente ambiguë.

var sb = new System.Text.StringBuilder();

var root = GameObject.Find("PinballTable");
var group = root != null ? root.transform.Find("Table") : null;
var table = root != null ? root.transform.Find("Table/Pinball_Table") : null;

if (group == null || table == null)
{
    return "PinballTable/Table/Pinball_Table introuvable";
}

const float u = 16.66667f;               // unités Unity par mètre

sb.AppendLine("=== transforms ===");
sb.AppendLine("PinballTable            : rot " + root.transform.localEulerAngles.ToString("F2")
              + "   pos " + root.transform.localPosition.ToString("F4"));
sb.AppendLine("  Table                 : pos locale " + group.localPosition.ToString("F4")
              + "   → y = " + (group.localPosition.y / u * 100f).ToString("F2") + " cm");
sb.AppendLine("    Pinball_Table       : pos locale " + table.localPosition.ToString("F4")
              + "   rot locale " + table.localEulerAngles.ToString("F2")
              + "   échelle " + table.localScale.ToString("F2"));

// --- les maillages, lus dans le repère du FBX --------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== maillages dans le repère du FBX (centimètres, z vers le haut) ===");

var interessants = new[] { "Surface", "LaneFloor", "Walls", "Playfield" };

foreach (Transform enfant in table)
{
    if (System.Array.IndexOf(interessants, enfant.name) >= 0)
    {
        Decrire(sb, enfant, table);
    }
    else
    {
        // Les enfants d'un groupe : `Playfield/Surface` est à deux niveaux.
        foreach (Transform petit in enfant)
        {
            if (System.Array.IndexOf(interessants, petit.name) >= 0)
            {
                Decrire(sb, petit, table);
            }
        }
    }
}

// --- ce que cela implique pour la surface ------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== conséquence ===");

var surf = Trouver(table, "Surface");

if (surf == null)
{
    sb.AppendLine("Surface introuvable");
    return sb.ToString();
}

var boite = BornesLocales(surf, table);

sb.AppendLine("face supérieure du plateau, dans le repère du FBX : z = "
              + boite.max.z.ToString("F4") + " cm");
sb.AppendLine("  → en unités Unity : " + (boite.max.z / 100f * u).ToString("F4") + " u");
sb.AppendLine();

float hauteurVoulue = boite.max.z / 100f * u;   // ce que le FBX réclame comme décalage

sb.AppendLine("Si la surface doit tomber à y = 0 dans `PinballTable`, le groupe `Table` doit être à "
              + (-hauteurVoulue).ToString("F4") + " u.");
sb.AppendLine("Il est à " + group.localPosition.y.ToString("F4") + " u → écart de "
              + (group.localPosition.y + hauteurVoulue).ToString("F4") + " u.");

// --- et où cela place la bille -----------------------------------------------------------------

var spawn = GameObject.Find("BallSpawnPoint");

if (spawn != null)
{
    sb.AppendLine();
    sb.AppendLine("=== la bille ===");
    sb.AppendLine("BallSpawnPoint monde   : " + spawn.transform.position.ToString("F4"));
    sb.AppendLine("rayon de bille         : 0,2250 u");
    sb.AppendLine("bas de la bille        : " + (spawn.transform.position.y - 0.225f).ToString("F4"));

    // Un rayon vers le haut depuis sous la table : le premier solide rencontré est la face
    // inférieure du premier objet au-dessus, donc ce qui arrêterait la bille en tombant.
    RaycastHit haut;

    Vector3 depuis = new Vector3(spawn.transform.position.x, -3f, spawn.transform.position.z);

    if (Physics.Raycast(depuis, Vector3.up, out haut, 30f, ~0, QueryTriggerInteraction.Ignore))
    {
        sb.AppendLine("premier solide sous la bille (vers le haut depuis y = -3) : "
                      + haut.collider.gameObject.name + " à y " + haut.point.y.ToString("F4"));
    }
    else
    {
        sb.AppendLine("aucun solide au-dessus de y = -3 : rien n'arrêterait la bille");
    }
}

return sb.ToString();

// --- helpers ------------------------------------------------------------------------------------

void Decrire(System.Text.StringBuilder texte, Transform objet, Transform repere)
{
    var filtre = objet.GetComponent<MeshFilter>();

    if (filtre == null || filtre.sharedMesh == null) { return; }

    var b = BornesLocales(objet, repere);

    texte.AppendLine("  " + objet.name.PadRight(14)
                     + " z de " + b.min.z.ToString("F3").PadLeft(9)
                     + " à " + b.max.z.ToString("F3").PadLeft(9) + " cm"
                     + "   |   y de " + b.min.y.ToString("F3").PadLeft(9)
                     + " à " + b.max.y.ToString("F3").PadLeft(9) + " cm"
                     + "   |   x de " + b.min.x.ToString("F3").PadLeft(9)
                     + " à " + b.max.x.ToString("F3").PadLeft(9));
}

// La boîte d'un maillage exprimée dans le repère du FBX : les huit coins, transformés.
Bounds BornesLocales(Transform objet, Transform repere)
{
    var maillage = objet.GetComponent<MeshFilter>().sharedMesh;
    var local = maillage.bounds;
    var resultat = new Bounds();

    for (int i = 0; i < 8; i++)
    {
        var coin = new Vector3(
            (i & 1) == 0 ? local.min.x : local.max.x,
            (i & 2) == 0 ? local.min.y : local.max.y,
            (i & 4) == 0 ? local.min.z : local.max.z);

        var enRepere = repere.InverseTransformPoint(objet.TransformPoint(coin));

        if (i == 0) { resultat = new Bounds(enRepere, Vector3.zero); }
        else { resultat.Encapsulate(enRepere); }
    }

    return resultat;
}

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
