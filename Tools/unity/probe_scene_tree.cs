// Qu'y a-t-il réellement dans `Neutral.unity` ?
//
// Le rayon vers le bas depuis le couloir touche `LaneFloor` à `y 2,59`, alors que la surface du
// plateau devrait y être à `y 0,11` (repère du menu 4 : `z` local 1,0, racine inclinée à −7°).
// Il y a donc, quelque part, de la géométrie que personne n'attend — le mobilier procédural de
// `BuildPinballTable`, probablement, resté d'un essai du menu 2. CLAUDE.md prévient que ce menu
// « régénère la géométrie procédurale » et qu'il ne faut pas le lancer sur une scène à assets
// importés ; encore faut-il vérifier que la scène de travail est propre.
//
// La sonde liste donc l'arbre, puis **tous les colliders qui ne sont pas sous la table
// importée** : ce sont eux, s'ils existent, qui interceptent la bille.

var sb = new System.Text.StringBuilder();

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

sb.AppendLine("=== scène '" + scene.name + "' ===");
sb.AppendLine("modifiée : " + scene.isDirty);

var roots = scene.GetRootGameObjects();

// --- 1. l'arbre, sur deux niveaux ---------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== racines ===");

foreach (var root in roots)
{
    int colliders = root.GetComponentsInChildren<Collider>(true).Length;
    int renderers = root.GetComponentsInChildren<Renderer>(true).Length;
    int enfants = root.transform.childCount;

    sb.AppendLine("  " + root.name.PadRight(22)
                  + " actif " + root.activeInHierarchy
                  + "   enfants " + enfants
                  + "   colliders " + colliders
                  + "   renderers " + renderers);

    // Les enfants directs : c'est là que se lit un groupe oublié.
    foreach (Transform child in root.transform)
    {
        sb.AppendLine("      " + child.name.PadRight(30)
                      + " colliders " + child.GetComponentsInChildren<Collider>(true).Length
                      + "   renderers " + child.GetComponentsInChildren<Renderer>(true).Length
                      + "   pos locale " + child.localPosition.ToString("F3"));
    }
}

// --- 2. qui porte le nom « LaneFloor » ou « Surface » ? -----------------------------------------

sb.AppendLine();
sb.AppendLine("=== objets nommés LaneFloor / Surface ===");

foreach (var root in roots)
{
    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
    {
        if (transform.name != "LaneFloor" && transform.name != "Surface") { continue; }

        // Le chemin complet, pour savoir de quel groupe il vient.
        string chemin = transform.name;
        var parent = transform.parent;

        while (parent != null)
        {
            chemin = parent.name + "/" + chemin;
            parent = parent.parent;
        }

        var renderer = transform.GetComponent<Renderer>();

        sb.AppendLine("  " + chemin);
        sb.AppendLine("      position monde : " + transform.position.ToString("F4"));
        sb.AppendLine("      échelle monde  : " + transform.lossyScale.ToString("F4"));
        sb.AppendLine("      boîte monde    : "
                      + (renderer != null ? renderer.bounds.size.ToString("F4")
                                           + "   centre " + renderer.bounds.center.ToString("F4")
                                          : "aucun renderer"));
    }
}

// --- 3. les colliders hors de la table importée -------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== colliders hors de PinballTable/Table ===");

const string tablePrefix = "PinballTable/Table";
int dehors = 0;

foreach (var root in roots)
{
    foreach (var collider in root.GetComponentsInChildren<Collider>(true))
    {
        string chemin = collider.gameObject.name;
        var parent = collider.transform.parent;

        while (parent != null)
        {
            chemin = parent.name + "/" + chemin;
            parent = parent.parent;
        }

        if (chemin.StartsWith(tablePrefix)) { continue; }

        dehors++;

        if (dehors <= 25)
        {
            sb.AppendLine("  " + chemin.PadRight(46)
                          + collider.GetType().Name.PadRight(16)
                          + " boîte " + collider.bounds.size.ToString("F3")
                          + "   centre " + collider.bounds.center.ToString("F3"));
        }
    }
}

sb.AppendLine("total hors table : " + dehors);

// --- 4. la table importée, groupe par groupe -----------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== groupes sous PinballTable/Table/Pinball_Table ===");

var table = GameObject.Find("PinballTable");

if (table == null)
{
    sb.AppendLine("  PinballTable ABSENT");
    return sb.ToString();
}

var imported = table.transform.Find("Table/Pinball_Table");

if (imported == null)
{
    sb.AppendLine("  Table/Pinball_Table ABSENT");
    return sb.ToString();
}

sb.AppendLine("  transform : pos locale " + imported.localPosition.ToString("F4")
              + "   rot locale " + imported.localEulerAngles.ToString("F2"));

foreach (Transform child in imported)
{
    var renderers = child.GetComponentsInChildren<Renderer>(true);

    if (renderers.Length == 0)
    {
        sb.AppendLine("      " + child.name.PadRight(34) + " (aucun renderer)");
        continue;
    }

    var boite = renderers[0].bounds;

    for (int i = 1; i < renderers.Length; i++) { boite.Encapsulate(renderers[i].bounds); }

    sb.AppendLine("      " + child.name.PadRight(34)
                  + " maillages " + renderers.Length
                  + "   boîte " + boite.size.ToString("F3")
                  + "   y de " + boite.min.y.ToString("F4") + " à " + boite.max.y.ToString("F4"));
}

return sb.ToString();
