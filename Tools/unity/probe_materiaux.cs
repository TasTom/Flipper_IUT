// Couleurs réelles des matériaux candidats pour la bande de slingshot. LECTURE SEULE.
//
// CLAUDE.md prévient que les matériaux venus du FBX gardent leur nom mais perdent leur
// couleur (`Bumper - Cap Red` mesure 0,906, pas du rouge). Le set URP du projet, lui, est
// éditable. On relève donc la couleur effective, pas le nom.

var sb = new System.Text.StringBuilder();

string[] candidats = {
    "BumperRed_Mat", "FlipperOrange_Mat", "TargetYellow_Mat", "Playfield_Mat",
    "Metal_Mat", "TableWood_Mat", "BallChrome_Mat", "Rollover_Mat", "New Material",
};

sb.AppendLine("matériau                  | shader                          | _BaseColor");
sb.AppendLine("--------------------------+---------------------------------+---------------------------");

foreach (var nom in candidats)
{
    var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + nom + ".mat");

    if (m == null) { sb.AppendLine(nom.PadRight(25) + " | INTROUVABLE"); continue; }

    string couleur;

    if (m.HasProperty("_BaseColor"))
    {
        var c = m.GetColor("_BaseColor");
        couleur = "(" + c.r.ToString("F3") + ", " + c.g.ToString("F3") + ", " + c.b.ToString("F3") + ", " + c.a.ToString("F2") + ")";
    }
    else if (m.HasProperty("_Color"))
    {
        var c = m.GetColor("_Color");
        couleur = "(_Color) (" + c.r.ToString("F3") + ", " + c.g.ToString("F3") + ", " + c.b.ToString("F3") + ")";
    }
    else { couleur = "(aucune propriété de couleur)"; }

    sb.AppendLine(nom.PadRight(25) + " | " + m.shader.name.PadRight(31) + " | " + couleur);
}

// ce que la table porte réellement, pour situer
sb.AppendLine();
sb.AppendLine("=== matériaux portés par les maillages de la table ===");

var racine = GameObject.Find("PinballTable");
var rt = racine.transform;
var table = rt.Find("Table/Pinball_Table");

if (table != null)
{
    var vus = new System.Collections.Generic.HashSet<string>();

    foreach (var r in table.GetComponentsInChildren<MeshRenderer>(true))
    {
        foreach (var m in r.sharedMaterials)
        {
            if (m == null) { continue; }
            string cle = m.name;
            if (!vus.Add(cle)) { continue; }

            string c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString("F3") : "(?)";
            sb.AppendLine("  " + m.name.PadRight(38) + " " + c);
        }
    }
}

return sb.ToString();
