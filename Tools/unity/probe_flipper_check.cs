// Contrôle post-pose des flippers. LECTURE SEULE.
// 1. Identifie `Body_L` / `Body_R` (touchés par le balayage) : chemin, composants, boîte monde,
//    distance aux pivots — pour juger si le contact est un vrai chevauchement ou un coin de boîte.
// 2. Profil du bat en place : 9 tranches le long de x (repère du pivot), étalement en y par tranche.
//    Le talon est le gros bout : la tranche la plus large dit de quel côté il est.
// 3. Rappelle l'orientation actuelle des pivots (doit être root × Euler(-90,0,0) après la pose).

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null) { return "'PinballTable/Gameplay' introuvable."; }

// --- 1. Body_L / Body_R -----------------------------------------------------------------------------------

foreach (var nom in new string[] { "Body_L", "Body_R" })
{
    sb.AppendLine("=== " + nom + " ===");

    var t = racine.GetComponentInChildren<Transform>(true);
    Transform trouve = null;

    foreach (var c in racine.GetComponentsInChildren<Transform>(true))
    {
        if (c.name == nom) { trouve = c; break; }
    }

    if (trouve == null) { sb.AppendLine("introuvable sous PinballTable"); continue; }

    var chemin = trouve.name;
    var p = trouve.parent;

    while (p != null) { chemin = p.name + "/" + chemin; p = p.parent; }

    sb.AppendLine("chemin : " + chemin);
    sb.AppendLine("pos monde " + trouve.position.ToString("F4"));

    foreach (var c in trouve.GetComponents<Component>())
    {
        sb.AppendLine("  composant : " + c.GetType().Name);
    }

    var col = trouve.GetComponent<Collider>();

    if (col != null)
    {
        sb.AppendLine("  collider " + col.GetType().Name + " trigger=" + col.isTrigger
                      + "   boîte monde " + col.bounds.ToString("F4"));
    }
    else
    {
        var r = trouve.GetComponent<Renderer>();

        if (r != null) { sb.AppendLine("  SANS collider — boîte rendue " + r.bounds.ToString("F4")); }
        else { sb.AppendLine("  ni collider ni renderer"); }
    }

    foreach (var pn in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
    {
        var pivot = gameplay.Find(pn);

        if (pivot == null) { continue; }

        sb.AppendLine("  distance au pivot " + pn + " : "
                      + Vector3.Distance(trouve.position, pivot.position).ToString("F4") + " u");
    }
}

// --- 2 et 3. Profil des bats + orientation des pivots ------------------------------------------------------

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    sb.AppendLine();
    sb.AppendLine("=== " + nom + " ===");

    var pivot = gameplay.Find(nom);

    if (pivot == null) { sb.AppendLine("pivot absent"); continue; }

    sb.AppendLine("rot monde " + pivot.rotation.eulerAngles.ToString("F2"));

    var porteur = pivot.Find("Flipper_Bat");

    if (porteur == null) { sb.AppendLine("pas de Flipper_Bat"); continue; }

    var filtre = porteur.GetComponentInChildren<MeshFilter>();

    if (filtre == null || filtre.sharedMesh == null) { sb.AppendLine("pas de maillage"); continue; }

    // Nuage en repère pivot.
    var nuage = new System.Collections.Generic.List<Vector3>();

    foreach (var v in filtre.sharedMesh.vertices)
    {
        nuage.Add(pivot.InverseTransformPoint(filtre.transform.TransformPoint(v)));
    }

    float minX = float.MaxValue, maxX = float.MinValue;

    foreach (var q in nuage)
    {
        minX = Mathf.Min(minX, q.x);
        maxX = Mathf.Max(maxX, q.x);
    }

    sb.AppendLine("maillage '" + filtre.sharedMesh.name + "' : " + nuage.Count + " sommets"
                  + "   x de " + minX.ToString("F4") + " à " + maxX.ToString("F4"));

    // 9 tranches, étalement en y.
    int tranches = 9;
    var largeurs = new float[tranches];
    var comptes = new int[tranches];

    for (int i = 0; i < tranches; i++)
    {
        float a = minX + (maxX - minX) * i / tranches;
        float b = minX + (maxX - minX) * (i + 1) / tranches;
        float lo = float.MaxValue, hi = float.MinValue;
        int n = 0;

        foreach (var q in nuage)
        {
            if (q.x >= a && (q.x < b || i == tranches - 1))
            {
                n++;
                lo = Mathf.Min(lo, q.y);
                hi = Mathf.Max(hi, q.y);
            }
        }

        comptes[i] = n;
        largeurs[i] = n >= 3 ? hi - lo : -1f;
    }

    string profil = "";

    for (int i = 0; i < tranches; i++)
    {
        profil += (i > 0 ? " " : "") + (largeurs[i] < 0f ? "—" : largeurs[i].ToString("F3"));
    }

    sb.AppendLine("largeurs par tranche (x croissants) : " + profil);
    sb.AppendLine("comptes : " + string.Join(" ", System.Array.ConvertAll(comptes, c => c.ToString())));

    float bas = 0f, haut = 0f;
    int nBas = 0, nHaut = 0;

    for (int i = 0; i < 3; i++)
    {
        if (largeurs[i] >= 0f) { bas += largeurs[i]; nBas++; }
        if (largeurs[tranches - 1 - i] >= 0f) { haut += largeurs[tranches - 1 - i]; nHaut++; }
    }

    if (nBas > 0 && nHaut > 0)
    {
        bas /= nBas;
        haut /= nHaut;
        sb.AppendLine("tiers bas (x petits) " + bas.ToString("F4")
                      + "   tiers haut (x grands) " + haut.ToString("F4")
                      + "   → gros bout côté " + (bas > haut * 1.05f ? "X PETITS"
                          : (haut > bas * 1.05f ? "X GRANDS" : "INDÉCIDABLE")));
    }
}

return sb.ToString();
