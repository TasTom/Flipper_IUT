// SONDE (lecture seule sur la scène) — la pièce `Plastic_with_decal`, et l'état du bas de table.
//
// Trois questions, une seule passe :
//   1. Que vaut `Plastic_with_decal.fbx` une fois importé par Unity — cotes réelles, orientation,
//      sous-maillages, matériaux ? L'inventaire du dépôt donne le repère BLENDER (Z vertical),
//      pas celui d'Unity : seule la mesure dans Unity tranche.
//   2. Que portent aujourd'hui les deux hôtes de slingshot (collider réactif, position, rotation) ?
//   3. Où sont les slingshots ENGENDRÉS (`Pinball_Table/Slingshots`) que l'utilisateur veut voir
//      disparaître, et quelle place reste-t-il entre eux et les flippers ?
//
// Une image de la pièce part aussi sur disque : `AssetPreview` la rend sans toucher à la scène,
// ce qu'un instantané temporaire ne permettrait pas.

var sb = new System.Text.StringBuilder();

const float U = 16.6666667f;   // unités Unity par mètre (ancre : bille Ø 0,45 = 27 mm)

string Mm(float u) { return (u * 1000f / U).ToString("F2") + " mm"; }

// --- 1. la pièce --------------------------------------------------------------------------------

const string cheminPiece = "Assets/Models/Parts/Miscellaneous/Plastic_with_decal.fbx";

sb.AppendLine("=== " + cheminPiece + " ===");

var piece = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(cheminPiece);

if (piece == null) { sb.AppendLine("  INTROUVABLE"); }
else
{
    sb.AppendLine("  échelle de racine du prefab : " + piece.transform.localScale.ToString("F6"));

    var filtres = piece.GetComponentsInChildren<MeshFilter>();

    sb.AppendLine("  " + filtres.Length + " maillage(s)");

    var boite = new Bounds();
    bool premier = true;

    foreach (var f in filtres)
    {
        if (f.sharedMesh == null) { continue; }

        // `localToWorldMatrix` depuis la racine du prefab (qui est à l'identité) donne la taille
        // telle qu'elle sortirait d'une instanciation : c'est la seule mesure qui vaille.
        var m = f.sharedMesh.bounds;

        for (int k = 0; k < 8; k++)
        {
            var p = f.transform.localToWorldMatrix.MultiplyPoint(new Vector3(
                (k & 1) == 0 ? m.min.x : m.max.x,
                (k & 2) == 0 ? m.min.y : m.max.y,
                (k & 4) == 0 ? m.min.z : m.max.z));

            if (premier) { boite = new Bounds(p, Vector3.zero); premier = false; }
            else { boite.Encapsulate(p); }
        }

        var rendu = f.GetComponent<MeshRenderer>();
        var mats = rendu != null ? rendu.sharedMaterials : new Material[0];

        sb.AppendLine();
        sb.AppendLine("  maillage '" + f.sharedMesh.name + "'   "
                      + f.sharedMesh.vertexCount + " sommets   "
                      + f.sharedMesh.subMeshCount + " sous-maillages");
        sb.AppendLine("    chemins matériaux : "
                      + string.Join(" | ", System.Array.ConvertAll(mats,
                          x => x != null ? x.name : "null")));
        sb.AppendLine("    échelle cumulée   : " + f.transform.lossyScale.ToString("F6"));
    }

    if (!premier)
    {
        sb.AppendLine();
        sb.AppendLine("  --- boîte assemblée, telle qu'instanciée (repère Unity) ---");
        sb.AppendLine("    taille  X " + Mm(boite.size.x) + "   Y " + Mm(boite.size.y)
                      + "   Z " + Mm(boite.size.z));
        sb.AppendLine("    centre  (" + Mm(boite.center.x) + " ; " + Mm(boite.center.y)
                      + " ; " + Mm(boite.center.z) + ")");
        sb.AppendLine("    bornes  X [" + Mm(boite.min.x) + " ; " + Mm(boite.max.x) + "]"
                      + "   Y [" + Mm(boite.min.y) + " ; " + Mm(boite.max.y) + "]"
                      + "   Z [" + Mm(boite.min.z) + " ; " + Mm(boite.max.z) + "]");
        sb.AppendLine("    (rappel : une bille = 0,45 u = 27,00 mm ; la pièce fait "
                      + (boite.size.magnitude / 0.45f).ToString("F2") + " bille de diagonale)");
    }

    // L'aperçu, pour voir de quoi il s'agit sans rien poser dans la scène.
    var apercu = UnityEditor.AssetPreview.GetAssetPreview(piece);

    if (apercu != null)
    {
        var pixels = apercu.GetPixels32();
        var tex = new Texture2D(apercu.width, apercu.height, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply();

        var fichier = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                             "plastique_apercu.png");

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fichier));
        System.IO.File.WriteAllBytes(fichier, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        sb.AppendLine("  aperçu écrit : " + fichier + "  (" + apercu.width + "×" + apercu.height + ")");
    }
    else { sb.AppendLine("  (pas d'aperçu disponible pour cette pièce)"); }
}

// --- 2. les hôtes de slingshot dans la scène ----------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== hôtes de slingshot (scène) ===");

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null) { sb.AppendLine("  Gameplay introuvable"); }
else
{
    foreach (var nom in new string[] { "Slingshot_Left", "Slingshot_Right" })
    {
        var hote = gameplay.Find(nom);

        sb.AppendLine();
        sb.AppendLine("  " + nom);

        if (hote == null) { sb.AppendLine("    ABSENT"); continue; }

        var table = rt.InverseTransformPoint(hote.position);

        sb.AppendLine("    position table (" + table.x.ToString("F4") + " ; " + table.y.ToString("F4")
                      + " ; " + table.z.ToString("F4") + ")");
        sb.AppendLine("    rotation locale " + hote.localEulerAngles.ToString("F2")
                      + "   échelle " + hote.localScale.ToString("F4"));
        sb.AppendLine("    enfants : " + hote.childCount);

        for (int i = 0; i < hote.childCount; i++)
        {
            var e = hote.GetChild(i);
            sb.AppendLine("      [" + i + "] '" + e.name + "'  maillages "
                          + e.GetComponentsInChildren<MeshFilter>().Length
                          + "   colliders " + e.GetComponentsInChildren<Collider>().Length);
        }

        var col = hote.GetComponent<BoxCollider>();

        if (col != null)
        {
            sb.AppendLine("    BoxCollider : centre " + col.center.ToString("F4")
                          + "   taille " + col.size.ToString("F4")
                          + "   trigger " + col.isTrigger);
        }
        else { sb.AppendLine("    ⚠ pas de BoxCollider sur l'hôte"); }

        var script = hote.GetComponent("Slingshot");
        sb.AppendLine("    script Slingshot : " + (script != null ? "présent" : "ABSENT"));
    }
}

// --- 3. les slingshots engendrés, et l'espace disponible ------------------------------------------

sb.AppendLine();
sb.AppendLine("=== slingshots engendrés (Pinball_Table/Slingshots) ===");

var engendres = GameObject.Find("PinballTable/Table/Pinball_Table/Slingshots");

if (engendres == null) { sb.AppendLine("  groupe introuvable"); }
else
{
    sb.AppendLine("  " + engendres.transform.childCount + " enfant(s)");

    for (int i = 0; i < engendres.transform.childCount; i++)
    {
        var e = engendres.transform.GetChild(i);
        var r = e.GetComponent<Renderer>();
        var b = r != null ? r.bounds : new Bounds();

        sb.AppendLine("    '" + e.name + "'   colliders "
                      + e.GetComponents<Collider>().Length
                      + (r != null ? "   bornes monde " + b.size.ToString("F4") : ""));
    }

    var rb = engendres.GetComponent<Renderer>();

    if (rb != null)
    {
        sb.AppendLine("  groupe : centre " + rb.bounds.center.ToString("F4")
                      + "   taille " + rb.bounds.size.ToString("F4"));
    }
}

// --- 4. le bas de table : flippers, drain gap ------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== flippers et drain gap ===");

if (gameplay != null)
{
    float[] pointes = new float[2];
    var noms = new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" };

    for (int i = 0; i < 2; i++)
    {
        bool gauche = i == 0;
        var pivot = gameplay.Find(noms[i]);

        if (pivot == null) { sb.AppendLine("  " + noms[i] + " absent"); continue; }

        var filtre = pivot.GetComponentInChildren<MeshFilter>();

        if (filtre == null) { sb.AppendLine("  " + noms[i] + " : pas de bat"); continue; }

        var b = filtre.sharedMesh.bounds;
        var boite = new Bounds();
        bool premier = true;

        for (int k = 0; k < 8; k++)
        {
            var p = pivot.InverseTransformPoint(filtre.transform.TransformPoint(new Vector3(
                (k & 1) == 0 ? b.min.x : b.max.x,
                (k & 2) == 0 ? b.min.y : b.max.y,
                (k & 4) == 0 ? b.min.z : b.max.z)));

            if (premier) { boite = new Bounds(p, Vector3.zero); premier = false; }
            else { boite.Encapsulate(p); }
        }

        // Pointe au repos : coin le plus éloigné de l'axe, tourné de l'angle de repos.
        var q = Quaternion.AngleAxis(gauche ? -30f : 30f, Vector3.forward);
        float hx = gauche ? Mathf.Max(Mathf.Abs(boite.min.x), Mathf.Abs(boite.max.x))
                          : -Mathf.Max(Mathf.Abs(boite.min.x), Mathf.Abs(boite.max.x));

        var monde = pivot.TransformPoint(q * new Vector3(hx, boite.center.y, boite.center.z));
        var table = rt.InverseTransformPoint(monde);

        pointes[i] = table.x;

        sb.AppendLine("  " + noms[i].PadRight(22)
                      + " pivot x = " + rt.InverseTransformPoint(pivot.position).x.ToString("F4")
                      + "   pointe au repos x = " + table.x.ToString("F4")
                      + "   z = " + table.z.ToString("F4")
                      + "   rayon " + Mathf.Max(Mathf.Abs(boite.min.x), Mathf.Abs(boite.max.x)).ToString("F4"));
    }

    float gap = pointes[1] - pointes[0];

    sb.AppendLine();
    sb.AppendLine("  DRAIN GAP actuel : " + gap.ToString("F4") + " u = " + Mm(gap)
                  + " = " + (gap / 0.45f).ToString("F2") + " bille(s)");
}

var sortie = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "plastique_etat.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(sortie));
System.IO.File.WriteAllText(sortie, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(sortie) + "  (" + sb.Length + " car.)";
