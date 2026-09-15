// SONDE (lecture seule) — Unity voit-il les 723 FBX exportés ?
//
// `probe_parts_etat.cs` a relevé un contraste net : les fichiers sont bien sur le disque
// (723 comptés par `System.IO.Directory`), mais `AssetDatabase.LoadAssetAtPath<GameObject>`
// en rend ZÉRO. Deux lectures possibles, et elles n'appellent pas le même remède :
//
//   1. l'import n'a pas encore eu lieu — l'AssetDatabase ne connaît pas les fichiers ;
//   2. les chemins construits par la sonde sont faux (chaîne `Assets/...` mal recollée).
//
// On tranche en interrogeant l'AssetDatabase par son propre index (`FindAssets`) et en
// comparant, pour un fichier témoin, le chemin construit par la sonde au chemin réel.

var sb = new System.Text.StringBuilder();

sb.AppendLine("=== ce que l'AssetDatabase connaît sous Assets/Models/Parts ===");

var guides = UnityEditor.AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Models/Parts" });

sb.AppendLine("  FindAssets(t:GameObject) : " + guides.Length + " résultat(s)");

int modeles = 0;
var extensions = new System.Collections.Generic.Dictionary<string, int>();

foreach (var g in guides)
{
    var p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);

    if (p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) { modeles++; }

    var ext = System.IO.Path.GetExtension(p).ToLowerInvariant();
    if (!extensions.ContainsKey(ext)) { extensions[ext] = 0; }
    extensions[ext]++;
}

sb.AppendLine("  dont .fbx                  : " + modeles);

foreach (var kv in extensions)
{
    sb.AppendLine("    " + kv.Key.PadRight(10) + " " + kv.Value);
}

// --- le fichier témoin : le bat de flipper ------------------------------------------------------

const string temoin = "Assets/Models/Parts/Flipper/Flipper_Williams_3.fbx";

sb.AppendLine();
sb.AppendLine("=== fichier témoin ===");
sb.AppendLine("  chemin visé        " + temoin);
sb.AppendLine("  existe sur disque  " + System.IO.File.Exists(System.IO.Path.Combine(
    System.IO.Path.GetDirectoryName(Application.dataPath), temoin)));
sb.AppendLine("  guid               " + UnityEditor.AssetDatabase.AssetPathToGUID(temoin));

var objet = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(temoin);
sb.AppendLine("  chargé comme objet " + (objet != null ? objet.name : "NULL"));

var importeur = UnityEditor.AssetImporter.GetAtPath(temoin) as UnityEditor.ModelImporter;
sb.AppendLine("  importeur          " + (importeur != null
    ? importeur.GetType().Name + "   échelle de fichier " + importeur.useFileScale
    : "AUCUN — l'AssetDatabase n'a pas vu ce fichier"));

// --- le chemin reconstruit, tel que la sonde précédente le fabriquait ---------------------------

sb.AppendLine();
sb.AppendLine("=== reconstruction du chemin (méthode de la sonde précédente) ===");

var dossier = System.IO.Path.Combine(Application.dataPath, "Models", "Parts", "Flipper");
var fichiers = System.IO.Directory.GetFiles(dossier, "*.fbx");

if (fichiers.Length > 0)
{
    var f = fichiers[0];
    var recollé = "Assets" + f.Replace('\\', '/').Substring(Application.dataPath.Length);

    sb.AppendLine("  fichier disque     " + f);
    sb.AppendLine("  dataPath           " + Application.dataPath);
    sb.AppendLine("  chemin recollé     " + recollé);
    sb.AppendLine("  chargé             " + (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(recollé) != null
        ? "✓" : "NULL"));
}

// --- un import est-il encore en cours ? ---------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== état de l'éditeur ===");
sb.AppendLine("  compilation        " + UnityEditor.EditorApplication.isCompiling);
sb.AppendLine("  mise à jour assets " + UnityEditor.EditorApplication.isUpdating);
sb.AppendLine("  en play            " + UnityEditor.EditorApplication.isPlaying);

var chemin = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "parts_visibilite.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(chemin));
System.IO.File.WriteAllText(chemin, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(chemin) + "  (" + sb.Length + " car.)";
