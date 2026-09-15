// SONDE (lecture seule) — répartition ANGULAIRE de chaque matériau autour du grand axe du bat.
//
// Le relevé par boîtes (`probe_flipper_roulis.cs`) donne les bornes, pas la forme : on sait que le
// caoutchouc fait 12,5 mm de large sur 21,2 mm de haut et que le plastique fait 26,0 sur 16,1 —
// mais pas s'ils forment un anneau, une bande, ou un croisillon. Deux boîtes ne le disent pas.
//
// Cette sonde projette chaque sommet dans la SECTION (le plan Y-Z perpendiculaire au grand axe) et
// relève son ANGLE autour de l'axe :
//
//     0° = +Y = vers le fond de table    90° = −Z = vers le HAUT (le ciel)
//   180° = −Y = vers le bas de table   270° = +Z = vers le PLATEAU (le bas)
//
// Un anneau couvre les huit secteurs à peu près également. Une bande se concentre dans un ou deux.
// C'est cette concentration qui dit de quel quart de tour il faut faire rouler le bat — et le
// rayon dit si le matériau est en surface (rayon élevé) ou au cœur (rayon faible).

var sb = new System.Text.StringBuilder();

const float U = 16.6666667f;

var racine = GameObject.Find("PinballTable");
if (racine == null) { return "PinballTable introuvable."; }

var rt = racine.transform;
var gameplay = rt.Find("Gameplay");

if (gameplay == null) { return "Gameplay introuvable."; }

// Les huit secteurs, nommés par ce qu'ils montrent au joueur. Index = angle / 45°.
var noms = new string[]
{
    "  0°– 45°  vers le FOND      ",   // +Y, bord attaquant au repos
    " 45°– 90°  fond + HAUT       ",
    " 90°–135°  HAUT + fond       ",   // −Z : le ciel
    "135°–180°  HAUT + drain      ",
    "180°–225°  vers le DRAIN     ",   // −Y
    "225°–270°  drain + plateau   ",
    "270°–315°  PLATEAU + drain   ",
    "315°–360°  PLATEAU + fond    ",
};

sb.AppendLine("SECTION DU BAT — angle autour du grand axe (X)");
sb.AppendLine("  0° = +Y (vers le fond de table)   90° = −Z (vers le HAUT, le ciel)");
sb.AppendLine("180° = −Y (vers le drain)         270° = +Z (vers le PLATEAU)");
sb.AppendLine();

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    sb.AppendLine("############### " + nom);

    var pivot = gameplay.Find(nom);

    if (pivot == null) { sb.AppendLine("  ABSENT"); continue; }

    var porteur = pivot.Find("Flipper_Bat");
    var filtre = porteur != null ? porteur.GetComponentInChildren<MeshFilter>() : null;
    var rendu = porteur != null ? porteur.GetComponentInChildren<MeshRenderer>() : null;

    if (filtre == null || filtre.sharedMesh == null) { sb.AppendLine("  pas de maillage"); continue; }

    var maille = filtre.sharedMesh;
    var sommets = maille.vertices;

    Vector3 DansPivot(int i)
    {
        return pivot.InverseTransformPoint(filtre.transform.TransformPoint(sommets[i]));
    }

    // Le centre de la section : le centre Y-Z de la boîte du bat entier.
    var boite = new Bounds(DansPivot(0), Vector3.zero);

    for (int i = 0; i < sommets.Length; i++) { boite.Encapsulate(DansPivot(i)); }

    float cy = boite.center.y;
    float cz = boite.center.z;

    sb.AppendLine();
    sb.AppendLine("  centre de section (" + (cy * 1000f / U).ToString("F2") + " ; "
                  + (cz * 1000f / U).ToString("F2") + ") mm    rayon max du bat "
                  + (Mathf.Max(boite.extents.y, boite.extents.z) * 1000f / U).ToString("F2") + " mm");

    for (int s = 0; s < maille.subMeshCount; s++)
    {
        int[] tris;

        try { tris = maille.GetTriangles(s); }
        catch { continue; }

        var vus = new System.Collections.Generic.HashSet<int>();

        foreach (var t in tris) { vus.Add(t); }

        if (vus.Count == 0) { continue; }

        var secteurs = new int[8];
        float rMin = float.MaxValue, rMax = float.MinValue, rSomme = 0f;

        foreach (var i in vus)
        {
            var p = DansPivot(i);

            float dy = p.y - cy;
            float dz = p.z - cz;

            // 90° doit tomber vers le HAUT, c'est-à-dire vers z négatif : d'où le −dz.
            float angle = Mathf.Atan2(-dz, dy) * Mathf.Rad2Deg;

            if (angle < 0f) { angle += 360f; }

            int secteur = Mathf.Min(7, (int)(angle / 45f));
            secteurs[secteur]++;

            float r = Mathf.Sqrt(dy * dy + dz * dz);
            rMin = Mathf.Min(rMin, r);
            rMax = Mathf.Max(rMax, r);
            rSomme += r;
        }

        var mat = rendu != null && s < rendu.sharedMaterials.Length ? rendu.sharedMaterials[s] : null;

        sb.AppendLine();
        sb.AppendLine("  [" + s + "] '" + (mat != null ? mat.name : "null") + "'   "
                      + vus.Count + " sommets"
                      + "   rayon min/moy/max " + (rMin * 1000f / U).ToString("F1") + " / "
                      + (rSomme / vus.Count * 1000f / U).ToString("F1") + " / "
                      + (rMax * 1000f / U).ToString("F1") + " mm");

        for (int k = 0; k < 8; k++)
        {
            float part = 100f * secteurs[k] / vus.Count;
            int barres = Mathf.RoundToInt(part / 2.5f);

            sb.AppendLine("      " + noms[k] + "  " + part.ToString("F1").PadLeft(5) + " %  "
                          + new string('#', Mathf.Min(barres, 40)));
        }
    }

    sb.AppendLine();
}

var chemin = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "flipper_section.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(chemin));
System.IO.File.WriteAllText(chemin, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(chemin) + "  (" + sb.Length + " car.)";
