// SONDE (lecture seule) — où tombe le caoutchouc du bat dans la SECTION du flipper ?
//
// Retour terrain : « le côté rouge doit être sur le côté, pas vers le haut ». Le mécanisme est
// connu d'avance : la recherche d'orientation de `place_scene_flippers.cs` §4 ne tourne qu'autour
// de l'AXE DU JOINT (quatre quarts verticaux). Aucun de ces quatre candidats ne change la face
// qui regarde le ciel — si le FBX arrive avec le caoutchouc en haut, aucun ne peut le corriger.
//
// Cette sonde relève, SOUS-MAILLAGE PAR SOUS-MAILLAGE (donc matériau par matériau), la boîte et
// le centroïde des sommets ramenés dans le repère du pivot :
//
//   X = travers de la table   Y = vers le fond   Z = VERS LE BAS
//
// Donc z < 0 = au-dessus du plateau. Le rouge « vers le haut » se lit : z très négatif.
//
// La lecture qui décide : dans la SECTION (le plan Y-Z), le caoutchouc est-il décalé sur un côté
// (|y| marqué) ou vers le haut/bas (|z| marqué) ? C'est ce décalage qui dit de quel quart de tour
// il faut faire rouler le bat autour de son grand axe.

var sb = new System.Text.StringBuilder();

const float U = 16.6666667f;   // unités Unity par mètre

var racine = GameObject.Find("PinballTable");
if (racine == null) { return "PinballTable introuvable."; }

var rt = racine.transform;
var gameplay = rt.Find("Gameplay");

if (gameplay == null) { return "Gameplay introuvable."; }

sb.AppendLine("repère du pivot : X = travers de la table, Y = vers le fond, Z = VERS LE BAS");
sb.AppendLine("                  z < 0 = au-dessus du plateau (le « haut » que voit le joueur)");
sb.AppendLine("une bille = 0,450 u = 27,0 mm");

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    sb.AppendLine();
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

    sb.AppendLine("  maillage '" + maille.name + "'   " + maille.subMeshCount + " sous-maillages   "
                  + maille.vertexCount + " sommets");
    sb.AppendLine();

    // La boîte de TOUT le bat, en premier : c'est la référence à laquelle comparer les sous-maillages.
    var tout = new Bounds(DansPivot(0), Vector3.zero);

    for (int i = 0; i < sommets.Length; i++) { tout.Encapsulate(DansPivot(i)); }

    sb.AppendLine("  TOUT LE BAT");
    sb.AppendLine("    taille   " + (tout.size.x * 1000f / U).ToString("F1") + " × "
                  + (tout.size.y * 1000f / U).ToString("F1") + " × "
                  + (tout.size.z * 1000f / U).ToString("F1") + " mm   (X long × Y travers × Z hauteur)");
    sb.AppendLine("    centre   " + tout.center.ToString("F4"));
    sb.AppendLine("    X ∈ [" + tout.min.x.ToString("F4") + " ; " + tout.max.x.ToString("F4") + "]"
                  + "   Y ∈ [" + tout.min.y.ToString("F4") + " ; " + tout.max.y.ToString("F4") + "]"
                  + "   Z ∈ [" + tout.min.z.ToString("F4") + " ; " + tout.max.z.ToString("F4") + "]");
    sb.AppendLine();

    for (int s = 0; s < maille.subMeshCount; s++)
    {
        int[] tris;

        try { tris = maille.GetTriangles(s); }
        catch { sb.AppendLine("  [" + s + "] triangles illisibles"); continue; }

        var vus = new System.Collections.Generic.HashSet<int>();

        foreach (var t in tris) { vus.Add(t); }

        if (vus.Count == 0) { sb.AppendLine("  [" + s + "] vide"); continue; }

        var b = new Bounds(DansPivot(0), Vector3.zero);
        var centre = Vector3.zero;
        bool premier = true;
        int n = 0;

        foreach (var i in vus)
        {
            var p = DansPivot(i);

            if (premier) { b = new Bounds(p, Vector3.zero); premier = false; }
            else { b.Encapsulate(p); }

            centre += p;
            n++;
        }

        centre /= n;

        var mat = rendu != null && s < rendu.sharedMaterials.Length ? rendu.sharedMaterials[s] : null;

        sb.AppendLine("  [" + s + "] matériau '" + (mat != null ? mat.name : "null") + "'"
                      + "   " + vus.Count + " sommets");
        sb.AppendLine("      taille   " + (b.size.x * 1000f / U).ToString("F1") + " × "
                      + (b.size.y * 1000f / U).ToString("F1") + " × "
                      + (b.size.z * 1000f / U).ToString("F1") + " mm");
        sb.AppendLine("      centre   (" + (centre.x * 1000f / U).ToString("F2") + " ; "
                      + (centre.y * 1000f / U).ToString("F2") + " ; "
                      + (centre.z * 1000f / U).ToString("F2") + ") mm");
        sb.AppendLine("      Y ∈ [" + (b.min.y * 1000f / U).ToString("F2") + " ; "
                      + (b.max.y * 1000f / U).ToString("F2") + "] mm"
                      + "   Z ∈ [" + (b.min.z * 1000f / U).ToString("F2") + " ; "
                      + (b.max.z * 1000f / U).ToString("F2") + "] mm");
        sb.AppendLine("      écart du centre au bat : ΔY "
                      + ((b.center.y - tout.center.y) * 1000f / U).ToString("F2") + " mm"
                      + "   ΔZ " + ((b.center.z - tout.center.z) * 1000f / U).ToString("F2") + " mm");
    }
}

// L'orientation du maillage, telle que le script de pose l'a écrite : c'est elle que la
// correction devra changer.
sb.AppendLine();
sb.AppendLine("=== orientation posée par le script (à corriger) ===");

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    var pivot = gameplay.Find(nom);
    var piece = pivot != null ? pivot.Find("Flipper_Bat/Bat_Mesh") : null;

    sb.AppendLine("  " + nom.PadRight(22)
                  + (piece != null
                      ? "localRotation " + piece.localEulerAngles.ToString("F2")
                        + "   localScale " + piece.localScale.ToString("F3")
                      : "pas de 'Bat_Mesh'"));
}

var chemin = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "flipper_roulis.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(chemin));
System.IO.File.WriteAllText(chemin, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(chemin) + "  (" + sb.Length + " car.)";
