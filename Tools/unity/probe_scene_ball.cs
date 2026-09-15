// Y a-t-il une bille dans `Neutral.unity` ?
//
// Question posée telle quelle, et qui mérite une mesure plutôt qu'un souvenir : le test de
// chute a bien instancié une bille au point d'apparition, mais il la détruit en sortant. Reste
// à confirmer que la scène est bien repartie comme elle était.
//
// On regarde aussi ce que la scène porte d'autre sous `Gameplay`, et si `Flipper_Bat` existe —
// le contrat de scène l'annonce comme l'enfant qui doit recevoir le bat.

var sb = new System.Text.StringBuilder();

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

sb.AppendLine("=== scène '" + scene.name + "' ===");

// --- 1. tout ce qui porte le tag Ball -----------------------------------------------------------

var parTag = GameObject.FindGameObjectsWithTag("Ball");

sb.AppendLine();
sb.AppendLine("objets taggés 'Ball' : " + parTag.Length);

foreach (var objet in parTag)
{
    sb.AppendLine("  " + objet.name
                  + "   scène " + objet.scene.name
                  + "   actif " + objet.activeInHierarchy
                  + "   position " + objet.transform.position.ToString("F4"));
}

// --- 2. tout ce qui ressemble à une bille, tag ou pas --------------------------------------------

sb.AppendLine();
sb.AppendLine("=== objets nommés *Ball* dans la scène ===");

int trouves = 0;

foreach (var root in scene.GetRootGameObjects())
{
    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
    {
        if (transform.name.IndexOf("Ball", System.StringComparison.OrdinalIgnoreCase) < 0) { continue; }

        trouves++;

        string chemin = transform.name;
        var parent = transform.parent;

        while (parent != null)
        {
            chemin = parent.name + "/" + chemin;
            parent = parent.parent;
        }

        sb.AppendLine("  " + chemin.PadRight(40)
                      + " colliders " + transform.GetComponents<Collider>().Length
                      + "   renderers " + transform.GetComponents<Renderer>().Length
                      + "   rigidbody " + (transform.GetComponent<Rigidbody>() != null));
    }
}

sb.AppendLine("total : " + trouves + "   (dont BallManager et BallSpawnPoint, qui sont des hôtes)");

// --- 3. les hôtes sous Gameplay ----------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== PinballTable/Gameplay ===");

var root2 = GameObject.Find("PinballTable");
var gameplay = root2 != null ? root2.transform.Find("Gameplay") : null;

if (gameplay == null)
{
    sb.AppendLine("  Gameplay ABSENT");
}
else
{
    for (int i = 0; i < gameplay.childCount; i++)
    {
        var enfant = gameplay.GetChild(i);

        sb.AppendLine("  " + enfant.name.PadRight(22)
                      + " actif " + enfant.gameObject.activeInHierarchy
                      + "   colliders " + enfant.GetComponentsInChildren<Collider>(true).Length
                      + "   renderers " + enfant.GetComponentsInChildren<Renderer>(true).Length
                      + "   enfants " + enfant.childCount);
    }
}

// --- 4. le prefab est-il toujours câblé ? -------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== câblage côté GameManager ===");

var manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();

if (manager == null)
{
    sb.AppendLine("  GameManager ABSENT");
}
else
{
    var serialise = new SerializedObject(manager);

    foreach (string champ in new[] { "ballPrefab", "ballSpawnPoint" })
    {
        var propriete = serialise.FindProperty(champ);

        sb.AppendLine("  " + champ.PadRight(16) + " : "
                      + (propriete != null && propriete.objectReferenceValue != null
                         ? propriete.objectReferenceValue.name
                         : "VIDE"));
    }
}

return sb.ToString();
