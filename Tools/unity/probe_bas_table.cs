// SONDE (lecture seule) — la place disponible en bas de table.
//
// Avant d'élargir le drain gap, il faut savoir ce qui borne le bas de table à gauche et à droite.
// Un `RaycastAll` lancé du centre de l'aire de jeu vers chaque mur, à plusieurs hauteurs de z,
// donne les faces RÉELLES — et c'est la seule mesure qui vaille : un balayage qui ne relève que
// les faces d'entrée fait croire à un écart (leçon du projet : « mesurer un intervalle se fait
// depuis le centre vers chaque mur »).
//
// On relève aussi l'outlane — le couloir entre le slingshot et le mur — parce que déplacer les
// flippers vers l'extérieur rapproche les slingshots du mur si on les suit.

var sb = new System.Text.StringBuilder();

const float U = 16.6666667f;

string Mm(float u) { return (u * 1000f / U).ToString("F2") + " mm"; }

var racine = GameObject.Find("PinballTable");
if (racine == null) { return "PinballTable introuvable."; }

var rt = racine.transform;
var gameplay = rt.Find("Gameplay");

sb.AppendLine("=== balayage horizontal, du centre vers chaque mur ===");
sb.AppendLine("(repère de TABLE : x = 0 au centre de l'aire, z = 0 au bord bas)");
sb.AppendLine();

// Balayage : depuis x = 0 vers ±X, à la hauteur de z demandée, juste au-dessus du plateau.
foreach (var z in new float[] { 1.0f, 1.5f, 2.0f, 2.5f, 3.0f, 3.5f, 4.0f, 5.0f })
{
    sb.AppendLine("z = " + z.ToString("F2") + " :");

    foreach (var sens in new[] { -1f, 1f })
    {
        var origine = rt.TransformPoint(new Vector3(0f, 0.25f, z));
        var direction = (rt.rotation * new Vector3(sens, 0f, 0f)).normalized;

        var hits = Physics.RaycastAll(origine, direction, 6f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        var morceaux = new System.Collections.Generic.List<string>();

        foreach (var h in hits)
        {
            var p = rt.InverseTransformPoint(h.point);
            morceaux.Add(h.collider.name + " @x " + p.x.ToString("F4"));
        }

        sb.AppendLine("   " + (sens < 0f ? "gauche" : "droite ") + " : "
                      + (morceaux.Count == 0 ? "RIEN" : string.Join("  |  ", morceaux)));
    }
}

// --- les murs proches du bas ---------------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== colliders des murs, bornes par maillage ===");

var murs = GameObject.Find("PinballTable/Table/Pinball_Table/Walls");

if (murs != null)
{
    for (int i = 0; i < murs.transform.childCount; i++)
    {
        var e = murs.transform.GetChild(i);
        var col = e.GetComponent<Collider>();

        if (col == null) { continue; }

        var b = col.bounds;
        var c = rt.InverseTransformPoint(b.center);

        // Seuls les murs qui descendent dans le bas de table nous intéressent.
        if (c.z > 8f) { continue; }

        sb.AppendLine("  " + e.name.PadRight(22)
                      + " centre (" + c.x.ToString("F3") + " ; " + c.z.ToString("F3") + ")"
                      + "   x ∈ [" + (c.x - b.extents.x).ToString("F3") + " ; "
                      + (c.x + b.extents.x).ToString("F3") + "]"
                      + "   z ∈ [" + (c.z - b.extents.z).ToString("F3") + " ; "
                      + (c.z + b.extents.z).ToString("F3") + "]");
    }
}

// --- l'outlane : entre la face externe du slingshot et le mur ------------------------------------

sb.AppendLine();
sb.AppendLine("=== outlane (slingshot → mur) ===");

var slingG = GameObject.Find("PinballTable/Table/Pinball_Table/Slingshots/Body_L");

if (slingG != null && murs != null)
{
    var b = slingG.GetComponent<Collider>().bounds;
    var c = rt.InverseTransformPoint(b.center);

    sb.AppendLine("  Body_L : centre (" + c.x.ToString("F4") + " ; " + c.z.ToString("F4") + ")"
                  + "   x ∈ [" + (c.x - b.extents.x).ToString("F4") + " ; "
                  + (c.x + b.extents.x).ToString("F4") + "]");

    // Le mur de gauche, à la hauteur du slingshot.
    var origine = rt.TransformPoint(new Vector3(0f, 0.25f, c.z));
    var hits = Physics.RaycastAll(origine, (rt.rotation * Vector3.left).normalized, 6f, ~0,
                                  QueryTriggerInteraction.Ignore);
    System.Array.Sort(hits, (a, bb) => a.distance.CompareTo(bb.distance));

    sb.AppendLine("  à z = " + c.z.ToString("F3") + ", vers la gauche : ");

    foreach (var h in hits)
    {
        var p = rt.InverseTransformPoint(h.point);
        sb.AppendLine("      " + h.collider.name.PadRight(24) + " x = " + p.x.ToString("F4"));
    }
}

// --- la marge des flippers ------------------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== flippers : ce qui borne leur débattement ===");

sb.AppendLine("  profil de sol : raycast vers le BAS depuis la pointe, aux angles extrêmes");

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    var pivot = gameplay != null ? gameplay.Find(nom) : null;

    if (pivot == null) { continue; }

    bool gauche = nom.Contains("Left");

    sb.AppendLine();
    sb.AppendLine("  " + nom);

    foreach (var angle in new[] { -30f, 0f, 30f })
    {
        var q = Quaternion.AngleAxis(gauche ? angle : -angle, Vector3.forward);

        // Extrémité du bat (rayon mesuré : 1,3635 u depuis l'axe).
        var pointe = pivot.TransformPoint(q * new Vector3(1.3635f, 0f, -0.2f));
        var table = rt.InverseTransformPoint(pointe);

        var bas = Physics.RaycastAll(pointe, -rt.up, 1.0f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(bas, (a, b) => a.distance.CompareTo(b.distance));

        var sol = bas.Length > 0 ? bas[0].collider.name + " à " + Mm(bas[0].distance) : "RIEN";

        sb.AppendLine("      angle " + angle.ToString("F0").PadLeft(4)
                      + "°  pointe table (" + table.x.ToString("F3") + " ; " + table.z.ToString("F3")
                      + ")   sol dessous : " + sol);
    }
}

var sortie = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "bas_de_table.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(sortie));
System.IO.File.WriteAllText(sortie, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(sortie) + "  (" + sb.Length + " car.)";
