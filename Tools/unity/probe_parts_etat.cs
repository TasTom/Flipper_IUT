// SONDE (lecture seule) — état du dossier `Assets/Models/Parts` vu par Unity, et santé des
// flippers de la scène après le réimport.
//
// Deux vérifications d'un coup :
//   1. les 723 FBX du dépôt `vbousquet/pinball-parts` sont-ils bien importés, et sous quel type ?
//   2. le bat de flipper de la scène pointe-t-il toujours sur un modèle valide — le dossier
//      `Flipper/` a été vidé puis réexporté, donc sa référence méritait un contrôle.

var sb = new System.Text.StringBuilder();

sb.AppendLine("=== Assets/Models/Parts ===");

var dossiers = UnityEditor.AssetDatabase.GetSubFolders("Assets/Models/Parts");
int totalFbx = 0;

foreach (var dossier in dossiers)
{
    // Chemin ABSOLU obligatoire : `Directory.GetFiles` rend un chemin relatif quand on lui passe
    // un chemin relatif, et la reconstruction `"Assets" + relatif.Substring(dataPath.Length)`
    // donnait alors `AssetsFlipper/...` — d'où un relevé « 0 importé » sur 723 pièces pourtant
    // bien importées (vérifié par `probe_parts_visibilite.cs` : `FindAssets` en voit 723).
    var absolu = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", dossier));
    var noms = System.IO.Directory.GetFiles(absolu, "*.fbx", System.IO.SearchOption.AllDirectories);
    int importes = 0;
    int manquants = 0;
    var exemples = new System.Collections.Generic.List<string>();

    foreach (var f in noms)
    {
        var relatif = "Assets" + f.Replace('\\', '/').Substring(Application.dataPath.Length).Replace("//", "/");
        var modele = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(relatif);

        if (modele != null) { importes++; }
        else { manquants++; if (exemples.Count < 3) { exemples.Add(System.IO.Path.GetFileName(f) + " → " + relatif); } }
    }

    totalFbx += noms.Length;

    sb.AppendLine("  " + System.IO.Path.GetFileName(dossier).PadRight(16)
                  + noms.Length.ToString().PadLeft(4) + " fbx"
                  + "   importés " + importes.ToString().PadLeft(4)
                  + (manquants > 0 ? "   ⚠ non chargés " + manquants + " (" + string.Join(", ", exemples) + ")" : ""));
}

sb.AppendLine("  TOTAL " + totalFbx + " fbx");

// --- 2. les flippers de la scène ---------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== flippers de la scène ===");

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
sb.AppendLine("scène : '" + scene.name + "'   modifiée = " + scene.isDirty);

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null) { sb.AppendLine("Gameplay introuvable."); }
else
{
    foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
    {
        var pivot = gameplay.Find(nom);

        if (pivot == null) { sb.AppendLine("  " + nom + " : ABSENT"); continue; }

        var porteur = pivot.Find("Flipper_Bat");
        var filtre = porteur != null ? porteur.GetComponentInChildren<MeshFilter>() : null;
        var collider = porteur != null ? porteur.GetComponentInChildren<BoxCollider>() : null;
        var rendu = porteur != null ? porteur.GetComponentInChildren<MeshRenderer>() : null;
        var joint = pivot.GetComponent<HingeJoint>();

        sb.AppendLine();
        sb.AppendLine("  " + nom);
        sb.AppendLine("    position table     ("
                      + rt.InverseTransformPoint(pivot.position).x.ToString("F4") + " ; "
                      + rt.InverseTransformPoint(pivot.position).y.ToString("F4") + " ; "
                      + rt.InverseTransformPoint(pivot.position).z.ToString("F4") + ")");
        sb.AppendLine("    maillage           " + (filtre != null && filtre.sharedMesh != null
                          ? filtre.sharedMesh.name + "  (" + filtre.sharedMesh.vertexCount + " sommets)"
                          : "⚠ AUCUN"));
        sb.AppendLine("    collider           " + (collider != null
                          ? collider.size.ToString("F4") + "  activé " + collider.enabled
                          : "⚠ AUCUN"));
        sb.AppendLine("    matériaux          " + (rendu != null
                          ? string.Join(", ", System.Array.ConvertAll(rendu.sharedMaterials,
                              m => m != null ? m.name : "null"))
                          : "—"));

        if (joint != null)
        {
            sb.AppendLine("    HingeJoint         ancrage "
                          + (rt.InverseTransformPoint(joint.connectedAnchor)).ToString("F4")
                          + "   écart au pivot "
                          + Vector3.Distance(rt.InverseTransformPoint(joint.connectedAnchor),
                                             rt.InverseTransformPoint(pivot.position)).ToString("F4") + " u"
                          + (Vector3.Distance(rt.InverseTransformPoint(joint.connectedAnchor),
                                              rt.InverseTransformPoint(pivot.position)) < 1e-3f ? "   ✓" : "   ⚠"));
        }
    }
}

var chemin = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "parts_etat.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(chemin));
System.IO.File.WriteAllText(chemin, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(chemin) + "  (" + sb.Length + " car.)";
