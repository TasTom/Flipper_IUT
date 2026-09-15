// SONDE (lecture seule) — ce que le flipper touche, et où tombe réellement sa pointe.
//
// `place_scene_flippers.cs` a signalé « balayage neutre/repos/actif : touche Slingshot_Left »
// et « garde talon 0,0 mm (Body_L) » des deux côtés. Deux lectures possibles, et elles ne
// mènent pas au même remède :
//
//   a) un collider solide occupe l'emplacement du flipper — la bille n'y arriverait jamais,
//      et le flipper serait « mal placé » au sens fort ;
//   b) la garde est mesurée sur une arête de boîte qui frôle, sans conséquence de jeu.
//
// Cette sonde tranche en mesurant, pas en supposant :
//   1. la boîte de balayage du flipper aux trois angles, exprimée en repère de TABLE ;
//   2. les colliders réellement présents dans cette boîte, avec leur type et leurs bornes ;
//   3. l'écart minimal talon ↔ corps de slingshot, calculé sur la distance point-segment
//      (la vraie géométrie du prisme) et pas sur une arête de boîte ;
//   4. l'écart entre les deux pointes au repos — le « drain gap », à comparer aux 39,4 mm
//      du générateur (1,46 bille) et à ce que montrent les plans de référence.

var sb = new System.Text.StringBuilder();

const float U = 16.6666667f;   // unités Unity par mètre (ancre : bille Ø 0,45 = 27 mm)

var racine = GameObject.Find("PinballTable");
if (racine == null) { return "PinballTable introuvable."; }

var rt = racine.transform;
var gameplay = rt.Find("Gameplay");
if (gameplay == null) { return "Gameplay introuvable."; }

// Repère de table : monde -> repère local du root (c'est là que vivent les cotes du générateur).
Vector3 Table(Vector3 monde) { return rt.InverseTransformPoint(monde); }
string Mm(float u) { return (u * 1000f / U).ToString("F2") + " mm"; }

// --- 1 & 2 : le balayage, et ce qu'il rencontre -------------------------------------------------

sb.AppendLine("=== balayage du flipper, en repère de table ===");

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    var pivot = gameplay.Find(nom);

    sb.AppendLine();
    sb.AppendLine("### " + nom);

    if (pivot == null) { sb.AppendLine("  ABSENT"); continue; }

    var porteur = pivot.Find("Flipper_Bat");
    var piece = porteur != null ? porteur.GetComponentInChildren<MeshFilter>() : null;

    if (piece == null) { sb.AppendLine("  pas de bat"); continue; }

    bool aGauche = nom.Contains("Left");
    float repos = aGauche ? -30f : 30f;

    // Boîte du bat, dans le repère du pivot.
    var b = piece.sharedMesh.bounds;
    var boite = new Bounds(Vector3.zero, Vector3.zero);
    bool premier = true;

    for (int k = 0; k < 8; k++)
    {
        var p = pivot.InverseTransformPoint(piece.transform.TransformPoint(new Vector3(
            (k & 1) == 0 ? b.min.x : b.max.x,
            (k & 2) == 0 ? b.min.y : b.max.y,
            (k & 4) == 0 ? b.min.z : b.max.z)));

        if (premier) { boite = new Bounds(p, Vector3.zero); premier = false; }
        else { boite.Encapsulate(p); }
    }

    foreach (var cas in new[] { new { nom = "neutre", a = 0f }, new { nom = "repos", a = repos } })
    {
        var q = Quaternion.AngleAxis(cas.a, Vector3.forward);
        var centreMonde = pivot.TransformPoint(q * boite.center);
        var rotMonde = pivot.rotation * q;

        // Coin de la pointe, en table : c'est lui qui décide du drain gap.
        float hx = aGauche ? Mathf.Max(Mathf.Abs(boite.min.x), Mathf.Abs(boite.max.x))
                           : -Mathf.Max(Mathf.Abs(boite.min.x), Mathf.Abs(boite.max.x));

        var pointeTable = Table(pivot.TransformPoint(q * new Vector3(
            hx, boite.center.y, boite.center.z)));

        sb.AppendLine("  '" + cas.nom + "' (" + cas.a.ToString("F0") + "°)  coin pointe table ("
                      + pointeTable.x.ToString("F4") + " ; " + pointeTable.y.ToString("F4") + " ; "
                      + pointeTable.z.ToString("F4") + ")   z = " + Mm(pointeTable.z) + " sous la surface");

        var presents = Physics.OverlapBox(centreMonde, boite.size * 0.5f, rotMonde, ~0,
                                          QueryTriggerInteraction.Ignore);

        if (presents.Length == 0)
        {
            sb.AppendLine("      rien dans la boîte ✓");
            continue;
        }

        foreach (var h in presents)
        {
            var tb = Table(h.bounds.center);

            sb.AppendLine("      " + h.name + "  [" + h.GetType().Name + "]"
                          + "  bornes table centre (" + tb.x.ToString("F3") + " ; "
                          + tb.y.ToString("F3") + " ; " + tb.z.ToString("F3") + ")  taille ("
                          + h.bounds.size.x.ToString("F3") + " ; " + h.bounds.size.y.ToString("F3")
                          + " ; " + h.bounds.size.z.ToString("F3") + ")");

            var mc = h as MeshCollider;

            if (mc != null) { sb.AppendLine("          MeshCollider convex = " + mc.convex); }
        }
    }
}

// --- 3 : l'écart talon ↔ corps, sur la vraie géométrie du prisme --------------------------------

sb.AppendLine();
sb.AppendLine("=== écart talon ↔ corps de slingshot (distance point-segment) ===");

// Cotes du générateur, en unités Unity : SLING_TOP (0,1925 ; 0,330) m, SLING_BOT (0,088 ; 0,085) m.
Vector3 SlingTop(int s) { return new Vector3(s * 0.1925f * U, 0f, 0.330f * U); }
Vector3 SlingBot(int s) { return new Vector3(s * 0.088f * U, 0f, 0.085f * U); }

float DistanceSegment(Vector3 p, Vector3 a, Vector3 b)
{
    var ab = b - a;
    float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Vector3.Dot(ab, ab));
    return Vector3.Distance(p, a + t * ab);
}

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    var pivot = gameplay.Find(nom);
    if (pivot == null) { continue; }

    bool aGauche = nom.Contains("Left");
    int s = aGauche ? -1 : 1;
    var a2 = SlingTop(s);
    var b2 = SlingBot(s);

    // Les 4 coins du talon, aux trois angles.
    float meilleur = float.MaxValue;
    string ou = "";

    foreach (var angle in new[] { 0f, aGauche ? -30f : 30f, aGauche ? 30f : -30f })
    {
        var q = Quaternion.AngleAxis(angle, Vector3.forward);

        foreach (var cz in new[] { -0.373f, -0.02f })
        {
            foreach (var cy in new[] { -0.2165f, 0.2165f })
            {
                var p = Table(pivot.TransformPoint(
                    q * new Vector3(aGauche ? 0.030f : -0.030f, cy, cz)));

                float d = DistanceSegment(new Vector3(p.x, 0f, p.z), a2, b2);

                if (d < meilleur) { meilleur = d; ou = angle + "°  coin (" + p.x.ToString("F3") + " ; " + p.z.ToString("F3") + ")"; }
            }
        }
    }

    sb.AppendLine("  " + nom + " : talon à " + Mm(meilleur) + " de la face   (" + ou + ")");
}

// --- 4 : le drain gap ---------------------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== drain gap (écart entre pointes au repos) ===");

var pivotG = gameplay.Find("Flipper_Left_Pivot");
var pivotD = gameplay.Find("Flipper_Right_Pivot");

if (pivotG != null && pivotD != null)
{
    // La pointe est le point du bat le plus éloigné de l'axe du joint ; on la prend sur la
    // boîte reçue, qui est déjà alignée sur les axes du pivot.
    float[] pointes = new float[2];
    var noms = new[] { "gauche", "droite" };
    var pivots = new[] { pivotG, pivotD };

    for (int i = 0; i < 2; i++)
    {
        bool aGauche = i == 0;
        var pivot = pivots[i];
        var porteur = pivot.Find("Flipper_Bat");
        var piece = porteur != null ? porteur.GetComponentInChildren<MeshFilter>() : null;

        var b = piece.sharedMesh.bounds;
        var boite = new Bounds(Vector3.zero, Vector3.zero);
        bool premier = true;

        for (int k = 0; k < 8; k++)
        {
            var p = pivot.InverseTransformPoint(piece.transform.TransformPoint(new Vector3(
                (k & 1) == 0 ? b.min.x : b.max.x,
                (k & 2) == 0 ? b.min.y : b.max.y,
                (k & 4) == 0 ? b.min.z : b.max.z)));

            if (premier) { boite = new Bounds(p, Vector3.zero); premier = false; }
            else { boite.Encapsulate(p); }
        }

        var q = Quaternion.AngleAxis(aGauche ? -30f : 30f, Vector3.forward);
        float hx = aGauche ? Mathf.Max(Mathf.Abs(boite.min.x), Mathf.Abs(boite.max.x))
                           : -Mathf.Max(Mathf.Abs(boite.min.x), Mathf.Abs(boite.max.x));

        pointes[i] = Table(pivot.TransformPoint(q * new Vector3(hx, boite.center.y, boite.center.z))).x;

        sb.AppendLine("  " + noms[i] + " : pointe au repos à x = " + pointes[i].ToString("F4"));
    }

    float gap = pointes[1] - pointes[0];

    sb.AppendLine("  écart entre pointes : " + gap.ToString("F4") + " u = " + Mm(gap)
                  + " = " + (gap / 0.45f).ToString("F2") + " bille(s)");
    sb.AppendLine("  cible du générateur (DRAIN_GAP) : " + (0.03942f * U).ToString("F4") + " u = "
                  + Mm(0.03942f * U) + " = " + (0.03942f * U / 0.45f).ToString("F2") + " bille(s)");
}

// --- 5 : de quoi fixer les idées sur le bas de table --------------------------------------------

sb.AppendLine();
sb.AppendLine("=== bas de table, en repère de table (x ; z) ===");

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot",
                                   "Slingshot_Left", "Slingshot_Right", "DrainZone" })
{
    var t = gameplay.Find(nom);

    if (t == null) { sb.AppendLine("  " + nom.PadRight(22) + " ABSENT"); continue; }

    var p = Table(t.position);

    sb.AppendLine("  " + nom.PadRight(22) + " (" + p.x.ToString("F4") + " ; "
                  + p.y.ToString("F4") + " ; " + p.z.ToString("F4") + ")");
}

var chemin = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "flippers_bas_table.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(chemin));
System.IO.File.WriteAllText(chemin, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(chemin) + "  (" + sb.Length + " car.)";
