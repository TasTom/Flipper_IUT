// Remet `PinballTable/Table` à l'altitude que le FBX réclame.
//
// Mesuré : la face supérieure de la plaque du plateau est à `z = 0,0000 cm` dans le repère du
// FBX — c'est-à-dire exactement dans le plan d'origine du modèle. Le repère du menu 4 le dit
// aussi : « y = 0 à la surface du plateau », et les hôtes qu'il pose (`BallSpawnPoint` à
// `y = 0,392`) n'ont de sens qu'à cette condition.
//
// Or le groupe `Table` est à `y = 2,360` — soit 14,16 cm plus haut. Aucun script ne pose cette
// valeur (`PlaceImportedTable.EnsureGroup` écrit `Vector3.zero` et laisse un groupe existant
// intact) : c'est un réglage fait à la main dans l'éditeur, devenu faux. Conséquence mesurée :
// la bille apparaît à `y 0,5109` alors que le sol du couloir est à `y 2,2251` — elle naît sous
// la table et tombe en chute libre, sans jamais rien toucher.
//
// On ne touche qu'à `y`. `x` et `z` sont laissés tels quels : rien ne dit qu'ils soient faux.

var sb = new System.Text.StringBuilder();

var root = GameObject.Find("PinballTable");
var group = root != null ? root.transform.Find("Table") : null;

if (group == null) { return "PinballTable/Table introuvable"; }

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

sb.AppendLine("=== avant ===");
sb.AppendLine("Table position locale : " + group.localPosition.ToString("F4"));

// --- la correction --------------------------------------------------------------------------

UnityEditor.Undo.RecordObject(group.transform, "Remettre la table à l'altitude du FBX");
UnityEditor.Undo.RecordObject(group.gameObject, "Remettre la table à l'altitude du FBX");

group.localPosition = new Vector3(group.localPosition.x, 0f, group.localPosition.z);

EditorUtility.SetDirty(group);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

sb.AppendLine();
sb.AppendLine("=== après ===");
sb.AppendLine("Table position locale : " + group.localPosition.ToString("F4"));

// --- vérification, par la même mesure que celle qui a révélé le problème ---------------------

Physics.SyncTransforms();

var spawn = GameObject.Find("BallSpawnPoint");

if (spawn != null)
{
    Vector3 bas = spawn.transform.position;
    RaycastHit sol;

    sb.AppendLine();
    sb.AppendLine("=== contrôle ===");
    sb.AppendLine("BallSpawnPoint        : " + bas.ToString("F4"));
    sb.AppendLine("bas de la bille       : " + (bas.y - 0.225f).ToString("F4"));

    // Vers le bas depuis le point d'apparition : le premier solide est ce qui arrêtera la bille.
    if (Physics.Raycast(bas, Vector3.down, out sol, 30f, ~0, QueryTriggerInteraction.Ignore))
    {
        sb.AppendLine("sol sous la bille     : " + sol.collider.gameObject.name
                      + " à y " + sol.point.y.ToString("F4")
                      + "   → la bille tombe de " + (bas.y - 0.225f - sol.point.y).ToString("F4") + " u");
    }
    else
    {
        sb.AppendLine("sol sous la bille     : AUCUN — la bille tomberait toujours dans le vide");
    }
}

sb.AppendLine();
sb.AppendLine("scène modifiée : " + scene.isDirty + "   (Ctrl+Z annule, Ctrl+S conserve)");

return sb.ToString();
