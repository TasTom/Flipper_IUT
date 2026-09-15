// Relance le menu de pose de la table, puis rapporte l'état du groupe `Table`.
//
// Le menu est idempotent : il ne recrée rien et se contente d'ajouter les `MeshCollider` qui
// manquent. Après un réimport du FBX, les pièces du dépôt sont de nouveaux maillages, donc sans
// collider — c'est exactement ce que cette passe leur donne.

PlaceImportedTable.Place();

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var report = new System.Text.StringBuilder();

report.AppendLine("scène : " + scene.name + "   modifiée : " + scene.isDirty +
                  "   Play : " + UnityEditor.EditorApplication.isPlaying);

foreach (var root in scene.GetRootGameObjects())
{
    if (root.name != "PinballTable")
    {
        continue;
    }

    int colliders = 0;
    int meshes = 0;
    var group = root.transform.Find("Table");

    report.AppendLine("PinballTable  rotation " + root.transform.localEulerAngles);

    if (group != null)
    {
        foreach (var filter in group.GetComponentsInChildren<MeshFilter>(true))
        {
            meshes++;
        }

        colliders = group.GetComponentsInChildren<Collider>(true).Length;

        foreach (Transform child in group)
        {
            var sub = child.GetComponentsInChildren<Renderer>(true);
            int subColliders = child.GetComponentsInChildren<Collider>(true).Length;

            report.AppendLine("  " + child.name.PadRight(18) + " maillages " +
                              sub.Length.ToString().PadLeft(3) + "   colliders " +
                              subColliders.ToString().PadLeft(3));
        }
    }

    report.AppendLine("  total : " + meshes + " maillages, " + colliders + " colliders");
}

return report.ToString();
