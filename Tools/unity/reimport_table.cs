// Réimporte le FBX de la table et rend compte de ce qui a bougé. ÉCRITURE (import d'asset).
//
// Le FBX a été régénéré sans la section `Slingshots` (39 maillages au lieu de 41). Cet appel le
// fait relire par Unity, puis relève l'état de la scène — parce que la vraie question n'est pas
// « l'import a-t-il eu lieu » mais « qu'est-il advenu des colliders posés par `PlaceImportedTable` ».
//
// Ceux-ci vivent en overrides sur l'instance de prefab. Un réimport qui ne reconnecterait pas les
// enfants par leur chemin les perdrait — et la table deviendrait traversante sans que rien ne le
// dise. D'où le compte avant/après.

var sb = new System.Text.StringBuilder();

const string chemin = "Assets/Models/Table/Pinball_Table.fbx";

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

// --- avant ---------------------------------------------------------------------------------------

var table = GameObject.Find("PinballTable/Table/Pinball_Table");

int collidersAvant = 0;
int maillagesAvant = 0;
bool slingshotsAvant = false;

if (table != null)
{
    collidersAvant = table.GetComponentsInChildren<Collider>(true).Length;
    maillagesAvant = table.GetComponentsInChildren<MeshFilter>(true).Length;
    slingshotsAvant = table.transform.Find("Slingshots") != null;
}

sb.AppendLine("=== avant réimport ===");
sb.AppendLine("  " + maillagesAvant + " maillage(s)   " + collidersAvant + " collider(s)"
              + "   section 'Slingshots' : " + (slingshotsAvant ? "présente" : "absente"));

// --- le réimport ---------------------------------------------------------------------------------

UnityEditor.AssetDatabase.ImportAsset(chemin,
    UnityEditor.ImportAssetOptions.ForceUpdate |
    UnityEditor.ImportAssetOptions.ForceSynchronousImport);

UnityEditor.AssetDatabase.Refresh();

// --- après ---------------------------------------------------------------------------------------

table = GameObject.Find("PinballTable/Table/Pinball_Table");

int collidersApres = 0;
int maillagesApres = 0;
bool slingshotsApres = false;

if (table != null)
{
    collidersApres = table.GetComponentsInChildren<Collider>(true).Length;
    maillagesApres = table.GetComponentsInChildren<MeshFilter>(true).Length;
    slingshotsApres = table.transform.Find("Slingshots") != null;
}

sb.AppendLine();
sb.AppendLine("=== après réimport ===");
sb.AppendLine("  " + maillagesApres + " maillage(s)   " + collidersApres + " collider(s)"
              + "   section 'Slingshots' : " + (slingshotsApres ? "présente" : "absente"));

// --- le détail : chaque maillage a-t-il son collider ? --------------------------------------------

if (table != null)
{
    var sansCollider = new System.Collections.Generic.List<string>();

    foreach (var f in table.GetComponentsInChildren<MeshFilter>(true))
    {
        if (f.sharedMesh == null) { continue; }

        if (f.GetComponent<Collider>() == null && f.GetComponentInParent<Collider>() == null)
        {
            sansCollider.Add(f.name);
        }
    }

    sb.AppendLine();
    sb.AppendLine("  maillages sans collider : " + (sansCollider.Count == 0
        ? "aucun ✓"
        : sansCollider.Count + "  →  " + string.Join(", ", sansCollider.ToArray(), 0,
            Mathf.Min(12, sansCollider.Count))));
}

// --- les enfants directs, pour voir la structure ----------------------------------------------------

if (table != null)
{
    sb.AppendLine();
    sb.AppendLine("=== enfants de Pinball_Table ===");

    for (int i = 0; i < table.transform.childCount; i++)
    {
        var e = table.transform.GetChild(i);

        sb.AppendLine("  " + e.name.PadRight(40)
                      + " maillages " + e.GetComponentsInChildren<MeshFilter>().Length
                      + "   colliders " + e.GetComponentsInChildren<Collider>().Length);
    }
}

// --- les hôtes de slingshot -------------------------------------------------------------------------

var gameplay = GameObject.Find("PinballTable/Gameplay");

if (gameplay != null)
{
    sb.AppendLine();
    sb.AppendLine("=== hôtes de slingshot ===");

    foreach (var nom in new string[] { "Slingshot_Left", "Slingshot_Right" })
    {
        var hote = gameplay.transform.Find(nom);
        sb.AppendLine("  " + nom + " : " + (hote != null ? "présent   " + hote.position.ToString("F4") : "ABSENT"));
    }
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

var sortie = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "reimport_table.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(sortie));
System.IO.File.WriteAllText(sortie, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(sortie) + "  (" + sb.Length + " car.)";
