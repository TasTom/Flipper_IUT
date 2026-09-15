// Mesure les deux FBX de table tels qu'Unity les a importés.
//
// La seule vérification qui vaille pour l'échelle : `--verify` de Blender ne teste que
// l'aller-retour Blender, alors que la conversion d'unités (fileScale 0,01 : cm -> m) est
// l'affaire de l'importeur Unity. On lit donc les `Renderer.bounds` réels.
//
// Un FBX importé n'est pas un prefab : `PrefabUtility.LoadPrefabContents` le refuse. On
// instancie donc dans une scène de prévisualisation, refermée aussitôt — la scène active n'est
// pas touchée.
//
// Le réimport est **forcé** chemin par chemin : `AssetDatabase.Refresh` seul se fie à
// l'horodatage et peut ne rien faire, ce qui avait produit deux relevés identiques sur deux
// fichiers différents. Et une empreinte des sommets est reportée, pour que la mesure porte sur
// ce qu'Unity a réellement en mémoire plutôt que sur ce qu'on suppose qu'il a relu.

string[] paths =
{
    "Assets/Models/Table/Pinball_Table.fbx",
    "Assets/Models/Table/Pinball_Cabinet.fbx",
};

foreach (string path in paths)
{
    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
}

var sb = new System.Text.StringBuilder();

foreach (string path in paths)
{
    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

    if (asset == null)
    {
        sb.AppendLine(path + " : INTROUVABLE");
        continue;
    }

    var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
    var contents = PrefabUtility.InstantiatePrefab(asset, preview) as GameObject;

    if (contents == null)
    {
        sb.AppendLine(path + " : INSTANCIATION IMPOSSIBLE");
        UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
        continue;
    }

    var renderers = contents.GetComponentsInChildren<Renderer>(true);
    var filters = contents.GetComponentsInChildren<MeshFilter>(true);

    bool has = false;
    Bounds bounds = default;

    foreach (var renderer in renderers)
    {
        if (!has) { bounds = renderer.bounds; has = true; }
        else { bounds.Encapsulate(renderer.bounds); }
    }

    sb.AppendLine("--- " + path);
    sb.AppendLine("    racine importee  : " + contents.name
                  + "   echelle locale " + contents.transform.localScale.x.ToString("F2"));
    sb.AppendLine("    maillages        : " + filters.Length
                  + "   renderers " + renderers.Length
                  + "   enfants directs " + contents.transform.childCount);

    // Empreinte des sommets : atteste de ce qu'Unity a en memoire. Deux fichiers differents
    // doivent donner deux empreintes differentes - sinon la mesure ne prouve rien.
    long empreinte = 0;
    int sommets = 0;

    foreach (var filter in filters)
    {
        var mesh = filter.sharedMesh;

        if (mesh == null) { continue; }

        var vertices = mesh.vertices;
        sommets += vertices.Length;

        for (int i = 0; i < vertices.Length; i++)
        {
            var v = vertices[i];
            empreinte = unchecked(empreinte * 31 + (long)(v.x * 10000f));
            empreinte = unchecked(empreinte * 31 + (long)(v.y * 10000f));
            empreinte = unchecked(empreinte * 31 + (long)(v.z * 10000f));
        }
    }

    sb.AppendLine("    sommets          : " + sommets
                  + "   empreinte " + empreinte.ToString("x16"));

    if (has)
    {
        sb.AppendLine("    bounds Unity     : "
                      + bounds.size.x.ToString("F3") + " large x "
                      + bounds.size.y.ToString("F3") + " haut  x "
                      + bounds.size.z.ToString("F3") + " long");
        sb.AppendLine("    bounds min/max   : x " + bounds.min.x.ToString("F3")
                      + " -> " + bounds.max.x.ToString("F3")
                      + "   y " + bounds.min.y.ToString("F3")
                      + " -> " + bounds.max.y.ToString("F3")
                      + "   z " + bounds.min.z.ToString("F3")
                      + " -> " + bounds.max.z.ToString("F3"));
    }

    foreach (Transform child in contents.transform)
    {
        var sub = child.GetComponentsInChildren<Renderer>(true);

        if (sub.Length == 0) { continue; }

        Bounds b = sub[0].bounds;

        for (int i = 1; i < sub.Length; i++) { b.Encapsulate(sub[i].bounds); }

        sb.AppendLine("      " + child.name.PadRight(14)
                      + " x " + b.min.x.ToString("F3").PadLeft(8)
                      + " -> " + b.max.x.ToString("F3").PadLeft(8)
                      + "  z " + b.min.z.ToString("F3").PadLeft(8)
                      + " -> " + b.max.z.ToString("F3").PadLeft(8)
                      + "  y " + b.min.y.ToString("F3").PadLeft(7)
                      + " -> " + b.max.y.ToString("F3").PadLeft(7));
    }

    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
}

return sb.ToString();
