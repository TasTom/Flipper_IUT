// Retire l'instance `Plunger_Rod` posée sous l'hôte `Plunger`, pour repasser
// `place_scene_plunger.cs` sur une scène vierge de ce côté.
//
// Pourquoi un script plutôt qu'un simple Ctrl+Z : `place_scene_plunger.cs` est idempotent, donc
// relancé sur une scène qui porte déjà `Plunger_Rod` il ne fait **rien** — y compris ne pas
// corriger un collider faux. Pour rejouer la pose entière, il faut d'abord retirer l'instance.
//
// C'est un outil de mise au point, à n'utiliser que sur une instance qu'on n'a pas retouchée à la
// main : il ne demande rien et détruit. Annulable par Ctrl+Z.

var racine = GameObject.Find("PinballTable");
var gameplay = racine != null ? racine.transform.Find("Gameplay") : null;
var hote = gameplay != null ? gameplay.Find("Plunger") : null;

if (hote == null)
{
    return "PinballTable/Gameplay/Plunger introuvable";
}

var piece = hote.Find("Plunger_Rod");

if (piece == null)
{
    return "Sous 'Plunger' il n'y a pas de 'Plunger_Rod' : rien à retirer.";
}

var sb = new System.Text.StringBuilder();

sb.AppendLine("retrait de : " + piece.name);
sb.AppendLine("  pos locale  " + piece.localPosition.ToString("F4"));
sb.AppendLine("  rotation    " + piece.localEulerAngles.ToString("F2"));
sb.AppendLine("  enfants     " + piece.childCount);
sb.AppendLine("  colliders   " + piece.GetComponents<Collider>().Length);

UnityEditor.Undo.DestroyObjectImmediate(piece.gameObject);

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

sb.AppendLine("→ retiré. Ctrl+Z annule, sinon relancer place_scene_plunger.cs.");

return sb.ToString();
