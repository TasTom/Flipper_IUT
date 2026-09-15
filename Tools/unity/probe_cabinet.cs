// Où est le panneau du caisson (le « screen » du Pinball_Cabinet) ? LECTURE SEULE.
//
// Demande : les HUD ne doivent pas être un Canvas en superposition d'écran, mais
// posés sur le panneau au-dessus du pinball — le fronton du caisson. Pour le poser,
// il faut sa géométrie exacte : maillages, bornes, orientation, dans le repère table.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
if (racine == null) { return "PinballTable introuvable."; }
var rt = racine.transform;

var table = rt.Find("Table");
if (table == null) { return "PinballTable/Table introuvable."; }

var cabinet = table.Find("Pinball_Cabinet");

if (cabinet == null)
{
    sb.AppendLine("Pinball_Cabinet absent. Contenu de Table :");
    foreach (Transform e in table) { sb.AppendLine("  " + e.name); }
    return sb.ToString();
}

sb.AppendLine("Pinball_Cabinet : " + cabinet.name);
sb.AppendLine("  position locale (PinballTable) : " + rt.InverseTransformPoint(cabinet.position).ToString("F4"));
sb.AppendLine("  échelle locale : " + cabinet.localScale.ToString("F6"));
sb.AppendLine();

sb.AppendLine("maillages (bornes ramenées au repère PinballTable) :");

var filtres = cabinet.GetComponentsInChildren<MeshFilter>(true);

sb.AppendLine("  total : " + filtres.Length);

foreach (var f in filtres)
{
    var rend = f.GetComponent<Renderer>();
    if (rend == null) { continue; }

    Bounds b = rend.bounds;
    Vector3 c = rt.InverseTransformPoint(b.center);

    // taille : les axes du monde ne sont pas ceux de la table (root incliné à −7°).
    // On mesure donc la boîte dans les axes de la table via les 8 coins.
    Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
    Vector3 mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);

    for (int i = 0; i < 8; i++)
    {
        Vector3 coin = b.center + new Vector3(
            (i & 1) == 0 ? -b.extents.x : b.extents.x,
            (i & 2) == 0 ? -b.extents.y : b.extents.y,
            (i & 4) == 0 ? -b.extents.z : b.extents.z);
        Vector3 l = rt.InverseTransformPoint(coin);
        mn = Vector3.Min(mn, l);
        mx = Vector3.Max(mx, l);
    }

    Vector3 taille = mx - mn;

    sb.AppendLine("  '" + f.name + "'  centre (" + c.x.ToString("F3") + ", " + c.y.ToString("F3")
        + ", " + c.z.ToString("F3") + ")  taille (" + taille.x.ToString("F3") + ", "
        + taille.y.ToString("F3") + ", " + taille.z.ToString("F3") + ")"
        + "  sommets " + (f.sharedMesh != null ? f.sharedMesh.vertexCount : 0)
        + "  actif " + f.gameObject.activeInHierarchy);
}

// Cherche un maillage dont le nom évoque un écran / fronton / backglass.
sb.AppendLine();
sb.AppendLine("candidats « panneau » (nom contenant screen/glass/back/panel/display/head) :");

bool trouve = false;

foreach (var f in filtres)
{
    string n = f.name.ToLowerInvariant();
    if (n.Contains("screen") || n.Contains("glass") || n.Contains("back")
        || n.Contains("panel") || n.Contains("display") || n.Contains("head")
        || n.Contains("fronton") || n.Contains("marquee") || n.Contains("affiche"))
    {
        trouve = true;
        sb.AppendLine("  '" + f.name + "'");
    }
}

if (!trouve) { sb.AppendLine("  (aucun — les maillages du caisson ont des noms neutres)"); }

return sb.ToString();
