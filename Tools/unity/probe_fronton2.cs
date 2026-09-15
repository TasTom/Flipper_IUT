// Cadrage réel du fronton + hauteurs de la géométrie dans le repère de la table. LECTURE SEULE.

var sb = new System.Text.StringBuilder();
var rt = GameObject.Find("PinballTable").transform;
var cam = Camera.main;

System.Func<Transform, string> chemin = null;
chemin = (t) => { var s = t.name; var p = t.parent; while (p != null) { s = p.name + "/" + s; p = p.parent; } return s; };

System.Func<Transform, object[]> boiteTable = (t) =>
{
    var r = t.GetComponent<Renderer>();
    if (r == null) { return null; }
    var b = r.bounds; var c = b.center; var e = b.extents;
    Vector3 mn = new Vector3(1e9f, 1e9f, 1e9f), mx = new Vector3(-1e9f, -1e9f, -1e9f);
    for (int i = 0; i < 8; i++)
    {
        var p = c + new Vector3(((i & 1) == 0 ? -1 : 1) * e.x, ((i & 2) == 0 ? -1 : 1) * e.y, ((i & 4) == 0 ? -1 : 1) * e.z);
        var q = rt.InverseTransformPoint(p);
        mn = Vector3.Min(mn, q); mx = Vector3.Max(mx, q);
    }
    return new object[] { (mn + mx) * 0.5f, mx - mn, mn, mx };
};

sb.AppendLine("=== A. BOÎTES DANS LE REPÈRE DE LA TABLE (centre | taille | ymin ymax) ===");
string[] cibles = {
    "Table/Pinball_Table/Playfield/Surface",
    "Table/Pinball_Table/Slingshots/Body_L",
    "Table/Pinball_Table/Slingshots/Body_R",
    "Table/Pinball_Table/Orbit/Rail",
    "Table/Pinball_Table/Orbit/RailCorner",
    "Table/Pinball_Table/Walls/Left","Table/Pinball_Table/Walls/Top","Table/Pinball_Table/Walls/Divider",
    "Table/Pinball_Table/Bumper_01_0_Bumper Base - Williams/Bally",
    "Table/Pinball_Table/Bumper_01_2_Bumper Cap - Clear 3\"",
    "Table/Pinball_Table/Bumper_03_2_Bumper Cap - Clear 3\"",
    "Table/Pinball_Table/Target_Matieres_01_Drop Target",
    "Table/Pinball_Table/Target_Java_01_Drop Target",
    "Table/Pinball_Table/Target_Cafe_01_Drop Target",
    "Table/Pinball_Table/Target_Reseau_03_Drop Target",
    "Table/Pinball_Table/Target_Projet_03_Drop Target",
    "Table/Pinball_Cabinet/Cabinet/Screen",
    "Table/Pinball_Cabinet/Cabinet/Ledge",
    "Table/Pinball_Cabinet/Emblem/Loop_At_Emblem",
    "Gameplay/Ball","Gameplay/DrainZone","Gameplay/BallSpawnPoint",
    "Gameplay/Slingshot_Left","Gameplay/Slingshot_Right",
    "Gameplay/Flipper_Left_Pivot","Gameplay/Flipper_Right_Pivot"
};
foreach (var c in cibles)
{
    var t = rt.Find(c);
    if (t == null) { sb.AppendLine(c + " : ABSENT"); continue; }
    var o = boiteTable(t);
    if (o == null) { sb.AppendLine(c + " : pas de renderer (centre " + rt.InverseTransformPoint(t.position).ToString("F4") + ")"); continue; }
    var centre = (Vector3)o[0]; var taille = (Vector3)o[1]; var mn = (Vector3)o[2]; var mx = (Vector3)o[3];
    sb.AppendLine(c + " : centre " + centre.ToString("F4") + " | taille " + taille.ToString("F4")
        + " | ytable " + mn.y.ToString("F4") + " -> " + mx.y.ToString("F4"));
}

sb.AppendLine();
sb.AppendLine("=== B. FRONTON : 4 coins de la face avant, projetés par Main Camera ===");
var scr = rt.Find("Table/Pinball_Cabinet/Cabinet/Screen");
if (scr != null && cam != null)
{
    var o = boiteTable(scr);
    var centre = (Vector3)o[0]; var taille = (Vector3)o[1]; var mn = (Vector3)o[2]; var mx = (Vector3)o[3];
    var zFace = mn.z;                       // face tournée vers le joueur (côté drain)
    var coins = new Vector3[] {
        new Vector3(mn.x, mn.y, zFace), new Vector3(mx.x, mn.y, zFace),
        new Vector3(mn.x, mx.y, zFace), new Vector3(mx.x, mx.y, zFace) };
    for (int i = 0; i < 4; i++)
    {
        var w = rt.TransformPoint(coins[i]);
        var v = cam.WorldToViewportPoint(w);
        sb.AppendLine("  coin " + i + " table " + coins[i].ToString("F3") + " -> monde " + w.ToString("F3")
            + " -> viewport " + v.ToString("F3") + ((v.z > 0 && v.x > 0 && v.x < 1 && v.y > 0 && v.y < 1) ? "  DANS" : "  HORS"));
    }
    // en pixels pour un rendu 1920x1080
    var v0 = cam.WorldToViewportPoint(rt.TransformPoint(new Vector3(mn.x, mn.y, zFace)));
    var v1 = cam.WorldToViewportPoint(rt.TransformPoint(new Vector3(mx.x, mx.y, zFace)));
    sb.AppendLine("  => en 1920x1080 : largeur " + ((v1.x - v0.x) * 1920f).ToString("F0") + " px, hauteur "
        + ((v1.y - v0.y) * 1080f).ToString("F0") + " px");
    sb.AppendLine("  centre du panneau en repère table : " + centre.ToString("F4") + " | taille " + taille.ToString("F4"));

    // plan du canvas monde candidat, posé 0,03 u devant la face
    var ancre = new Vector3(centre.x, centre.y, zFace - 0.03f);
    sb.AppendLine("  ANCRE CANVAS (repère table) : " + ancre.ToString("F4")
        + "  = local de Table : " + rt.Find("Table").InverseTransformPoint(rt.TransformPoint(ancre)).ToString("F4"));
    sb.AppendLine("  ANCRE CANVAS (monde) : " + rt.TransformPoint(ancre).ToString("F4"));
    sb.AppendLine("  rotation du canvas = celle du root, puis verifier le texte non miroir");
}

sb.AppendLine();
sb.AppendLine("=== C. CADRAGE DE L'AIRE DE JEU (viewport) ===");
if (cam != null)
{
    var surf = rt.Find("Table/Pinball_Table/Playfield/Surface");
    if (surf != null)
    {
        var o = boiteTable(surf);
        var mn = (Vector3)o[2]; var mx = (Vector3)o[3];
        var bas = cam.WorldToViewportPoint(rt.TransformPoint(new Vector3(0f, mx.y, mn.z)));
        var haut = cam.WorldToViewportPoint(rt.TransformPoint(new Vector3(0f, mx.y, mx.z)));
        sb.AppendLine("  aire de jeu, bord bas z=" + mn.z.ToString("F2") + " -> viewport " + bas.ToString("F3"));
        sb.AppendLine("  aire de jeu, bord haut z=" + mx.z.ToString("F2") + " -> viewport " + haut.ToString("F3"));
    }
}

return sb.ToString();
