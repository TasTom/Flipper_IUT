// Valide le balayage des flippers en pur géométrique. LECTURE SEULE, zéro mutation.
//
// v2 — la v1 pilotait le ressort en `Physics.Simulate` contre le pied du slingshot : le talon
// a labouré, le solveur a arraché les deux Rigidbody de leur ancre (dérive jusqu'à ~1,1 u,
// `fix_flipper_pivots.cs` a remis les plots). Leçon : en édit, JAMAIS de ressort simulé contre
// un obstacle — on mesure des poses, on ne joue pas la dynamique (le Play au clavier Q/D,
// par l'utilisateur, reste le vrai test final).
//
// Pour chaque côté, 13 poses (−30°…+30° par pas de 5°, calculées à la main depuis
// `pivot.rotation`, SANS toucher aux corps) :
// 1. `OverlapBox` de la boîte du bat : touche-t-elle le mur (`Body_L`/`Body_R`) ?
// 2. inclusion des SOMMETS du mesh dans le mur (parité d'un rayon +Y : un sommet dedans voit
//    la surface un nombre impair de fois ; seuil ≥ 3 sommets pour filtrer les tangences).
// Attendu après le jour de 0,03 u (`place_scene_flippers.cs`) : boîte libre partout, 0 sommet
// dedans. La boîte sharp dépasse du talon arrondi (~5 mm aux arêtes) : c'est elle qu'on teste
// en (1), le mesh en (2) — les deux doivent passer.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null) { return "'PinballTable/Gameplay' introuvable."; }

sb.AppendLine("=== balayage géométrique (13 poses, aucune mutation) ===");

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    bool aGauche = nom.Contains("Left");

    sb.AppendLine();
    sb.AppendLine("=== " + nom + " ===");

    var pivot = gameplay.Find(nom);

    if (pivot == null) { sb.AppendLine("pivot absent"); continue; }

    var porteur = pivot.Find("Flipper_Bat");
    var bat = porteur != null ? porteur.Find("Bat_Mesh") : null;

    if (bat == null) { sb.AppendLine("Bat_Mesh absent"); continue; }

    // Boîte + sommets en local pivot (état posé actuel, sans rien bouger).
    Bounds boite = default(Bounds);
    bool premier = true;
    var locaux = new System.Collections.Generic.List<Vector3>();

    foreach (var filtre in bat.GetComponentsInChildren<MeshFilter>())
    {
        if (filtre.sharedMesh == null) { continue; }

        var m = filtre.sharedMesh.bounds;

        for (int i = 0; i < 8; i++)
        {
            var p = pivot.InverseTransformPoint(filtre.transform.TransformPoint(new Vector3(
                (i & 1) == 0 ? m.min.x : m.max.x,
                (i & 2) == 0 ? m.min.y : m.max.y,
                (i & 4) == 0 ? m.min.z : m.max.z)));

            if (premier) { boite = new Bounds(p, Vector3.zero); premier = false; }
            else { boite.Encapsulate(p); }
        }

        int k = 0;

        foreach (var v in filtre.sharedMesh.vertices)
        {
            if ((k++ % 2) == 0)   // un sommet sur deux : ~900 tests par pose, représentatif
            {
                locaux.Add(pivot.InverseTransformPoint(filtre.transform.TransformPoint(v)));
            }
        }
    }

    var mur = GameObject.Find("PinballTable/Table/Pinball_Table/Slingshots/"
                              + (aGauche ? "Body_L" : "Body_R"));

    if (mur == null) { sb.AppendLine("mur introuvable"); continue; }

    var colMur = mur.GetComponent<Collider>();

    if (colMur == null) { sb.AppendLine("mur sans collider"); continue; }

    sb.AppendLine("boîte " + boite.size.ToString("F4") + "   " + locaux.Count + " sommets testés/pose");

    bool toutLibre = true;

    for (int deg = -30; deg <= 30; deg += 5)
    {
        var q = Quaternion.AngleAxis((float)deg, Vector3.forward);
        var rotMonde = pivot.rotation * q;

        // 1. La boîte.
        var c = pivot.TransformPoint(q * boite.center);
        var hits = Physics.OverlapBox(c, boite.size * 0.5f, rotMonde, ~0,
                                      QueryTriggerInteraction.Ignore);

        string qui = "";

        foreach (var h in hits)
        {
            if (h.name == "Body_L" || h.name == "Body_R") { qui += h.name + " "; }
        }

        // 2. Les sommets : parité du rayon +Y monde contre le mur.
        int dedans = 0;

        foreach (var s in locaux)
        {
            var monde = pivot.TransformPoint(q * s);
            var rayons = Physics.RaycastAll(monde, Vector3.up, 100f, ~0,
                                            QueryTriggerInteraction.Ignore);
            int n = 0;

            foreach (var r in rayons)
            {
                if (r.collider == colMur) { n++; }
            }

            if ((n % 2) == 1) { dedans++; }
        }

        bool libre = qui == "" && dedans < 3;

        if (!libre) { toutLibre = false; }

        sb.AppendLine("  " + deg + "° : boîte " + (qui == "" ? "libre" : "TOUCHE " + qui)
                      + "   sommets dans le mur : " + dedans
                      + (libre ? "" : "   ⚠"));
    }

    sb.AppendLine(toutLibre ? "  → course −30°…+30° : RIEN NE TOUCHE ✓" : "  → ⚠ contact résiduel, voir ci-dessus");
}

return sb.ToString();
