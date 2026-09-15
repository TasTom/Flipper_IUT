// Où sont le caoutchouc et le plastique de chaque bat DANS LE MONDE ? LECTURE SEULE.
//
// Le retour terrain dit : bat gauche mal assis (à rouler de 90° + effet miroir).
// Cette sonde mesure, par sous-maillage (matériau) : le centroïde monde des sommets,
// et les axes locaux de la pièce exprimés en axes table (X = gauche-droite,
// Y = normale au plateau, Z = bas-haut vers le fond). Si le caoutchouc n'est pas
// au-dessus du plastique des deux côtés, l'assise est à reprendre.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null) { return "'PinballTable/Gameplay' introuvable."; }

sb.AppendLine("axes table monde : X " + rt.right.ToString("F3"));
sb.AppendLine("                   Y(normale) " + rt.up.ToString("F3"));
sb.AppendLine("                   Z(fond) " + rt.forward.ToString("F3"));

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    sb.AppendLine();
    sb.AppendLine("=== " + nom + " ===");

    var bat = gameplay.Find(nom + "/Flipper_Bat/Bat_Mesh");

    if (bat == null) { sb.AppendLine("Bat_Mesh absent"); continue; }

    var filtre = bat.GetComponentInChildren<MeshFilter>();
    var rendu = bat.GetComponentInChildren<MeshRenderer>();

    if (filtre == null || filtre.sharedMesh == null) { sb.AppendLine("pas de mesh"); continue; }

    var maille = filtre.sharedMesh;
    var sommets = maille.vertices;

    for (int s = 0; s < maille.subMeshCount; s++)
    {
        string mat = (rendu != null && s < rendu.sharedMaterials.Length && rendu.sharedMaterials[s] != null)
            ? rendu.sharedMaterials[s].name : "?";

        var tris = maille.GetTriangles(s);
        var vus = new System.Collections.Generic.HashSet<int>();

        foreach (var t in tris) { vus.Add(t); }

        Vector3 somme = Vector3.zero;

        foreach (var i in vus) { somme += sommets[i]; }

        var centroideLocal = somme / Mathf.Max(vus.Count, 1);
        var centroideMonde = filtre.transform.TransformPoint(centroideLocal);

        // Hauteur au-dessus du plateau (Y table) et position le long de la table.
        Vector3 relatif = centroideMonde - rt.position;
        float hauteur = Vector3.Dot(relatif, rt.up);
        float travers = Vector3.Dot(relatif, rt.right);

        sb.AppendLine("  sous-maillage " + s + " (" + mat + ", " + vus.Count + " sommets) : "
            + "hauteur " + hauteur.ToString("F4") + " u   travers " + travers.ToString("F4") + " u");
    }

    var pieceT = bat.transform;
    sb.AppendLine("  axes pièce en monde : X " + pieceT.right.ToString("F3")
                  + "   Y " + pieceT.up.ToString("F3") + "   Z " + pieceT.forward.ToString("F3"));
    sb.AppendLine("  échelle locale : " + pieceT.localScale.ToString("F4"));
}

return sb.ToString();
