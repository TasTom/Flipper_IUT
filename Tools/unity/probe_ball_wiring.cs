// Où en est le câblage de la bille dans `Neutral.unity` ?
//
// `GameManager.SpawnBall()` refuse de créer quoi que ce soit si `ballPrefab` est vide, et
// `BallManager.Respawn` a besoin de `ballSpawnPoint` pour ramener une bille bloquée. Les deux
// champs viennent de `[SerializeField]` : ils se lisent donc dans la scène, pas dans le code.
//
// On relève aussi les gestionnaires présents — la hiérarchie n'en montrait que trois à la
// racine, alors que CLAUDE.md en annonce six.
//
// Note d'API : `FindObjectsByType(Type, FindObjectsInactive, FindObjectsSortMode)` et
// `FindFirstObjectByType<T>()` sont obsolètes en 6000.6 — le premier parce que le tri par
// identifiant d'instance disparaît, le second pour la même raison. On passe donc par
// `FindObjectsByType<MonoBehaviour>(FindObjectsInactive)` et `FindAnyObjectByType<T>()`.

var sb = new System.Text.StringBuilder();

// --- tous les objets racine ----------------------------------------------------------------

sb.AppendLine("=== racines de la scène ===");

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

foreach (var root in scene.GetRootGameObjects())
{
    sb.AppendLine("  " + root.name.PadRight(24) + " actif " + root.activeSelf
                  + "   composants " + root.GetComponents<Component>().Length);
}

// --- les gestionnaires, où qu'ils soient ---------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== gestionnaires présents ===");

var byType = new System.Collections.Generic.SortedDictionary<string, System.Collections.Generic.List<string>>();

foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
{
    if (behaviour == null) { continue; }

    string type = behaviour.GetType().Name;

    if (!byType.ContainsKey(type)) { byType[type] = new System.Collections.Generic.List<string>(); }

    byType[type].Add(behaviour.gameObject.name);
}

foreach (var entry in byType)
{
    sb.AppendLine("  " + entry.Key.PadRight(20) + " ×" + entry.Value.Count
                  + "   " + string.Join(", ", entry.Value.ToArray()));
}

// --- les champs de bille du GameManager ----------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== GameManager : champs de bille ===");

var manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();

if (manager == null)
{
    sb.AppendLine("  AUCUN GameManager dans la scène — la partie ne peut pas démarrer.");
}
else
{
    var serialized = new SerializedObject(manager);

    foreach (string field in new[] { "ballPrefab", "ballSpawnPoint", "startState", "ballsPerGame" })
    {
        var property = serialized.FindProperty(field);

        if (property == null)
        {
            sb.AppendLine("  " + field.PadRight(16) + " : CHAMP INTROUVABLE");
            continue;
        }

        string rendered;

        if (property.propertyType == SerializedPropertyType.ObjectReference)
        {
            var value = property.objectReferenceValue;

            rendered = value != null
                ? value.name + "   (" + AssetDatabase.GetAssetPath(value) + ")"
                : "VIDE";
        }
        else if (property.propertyType == SerializedPropertyType.Enum)
        {
            rendered = ((GameManager.GameState)property.enumValueIndex).ToString();
        }
        else
        {
            rendered = property.intValue.ToString();
        }

        sb.AppendLine("  " + field.PadRight(16) + " : " + rendered);
    }
}

// --- le point d'apparition -----------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== BallSpawnPoint ===");

var spawnObject = GameObject.Find("BallSpawnPoint");

if (spawnObject == null)
{
    sb.AppendLine("  ABSENT");
}
else
{
    sb.AppendLine("  position monde : " + spawnObject.transform.position.ToString("F4"));
    sb.AppendLine("  position locale: " + spawnObject.transform.localPosition.ToString("F4"));
    sb.AppendLine("  rotation       : " + spawnObject.transform.eulerAngles.ToString("F2"));
    sb.AppendLine("  échelle        : " + spawnObject.transform.lossyScale.ToString("F4"));
    sb.AppendLine("  (rappel : rayon de bille 0,225 u)");
}

// --- tag et layers -------------------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== tag / layers ===");
sb.AppendLine("  tag 'Ball'          : "
              + (System.Array.IndexOf(UnityEditorInternal.InternalEditorUtility.tags, "Ball") >= 0));
sb.AppendLine("  layer 'Ball'        : " + (LayerMask.NameToLayer("Ball") >= 0));
sb.AppendLine("  layer 'Table'       : " + (LayerMask.NameToLayer("Table") >= 0));
sb.AppendLine("  layer 'TableElement': " + (LayerMask.NameToLayer("TableElement") >= 0));
sb.AppendLine("  layer 'Environment' : " + (LayerMask.NameToLayer("Environment") >= 0));

return sb.ToString();
