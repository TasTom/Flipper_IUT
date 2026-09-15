// Remet les pivots des flippers sur leurs plots. ÉCRITURE (annulable, non enregistrée).
//
// Le balayage simulé (`test_flippers.cs`, ressort 4000 contre le pied du slingshot) a arraché
// les deux Rigidbody de leur ancre (dérive jusqu'à ~1,1 u) et seule la rotation avait été
// restaurée — les mouvements physiques ne passent pas par la pile Undo. Ce script répare :
// positions contractuelles de `PlaceImportedTable` (∓1,428 ; 0 ; 1,533, local Gameplay),
// cadre +90° (Z vers le bas, cf. `place_scene_flippers.cs` §1), vélocités à zéro + `Sleep()`
// pour repartir d'un corps sain. Le reste (ressort, limites ±30, bat, collider, matériaux)
// n'a pas bougé — vérifié par `probe_flipper_jour.cs` après coup.
// Leçon consignée : en édit, on ne pilote JAMAIS un ressort contre un obstacle en
// `Physics.Simulate` — le balayage se fait en pur géométrique (OverlapBox + raycasts),
// sans toucher aux corps. `test_flippers.cs` est à réécrire sur ce principe (ou à jeter).

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null) { return "'PinballTable/Gameplay' introuvable."; }

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var cadre = rt.rotation * Quaternion.Euler(90f, 0f, 0f);

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    float sx = nom.Contains("Left") ? -1.428f : 1.428f;

    var pivot = gameplay.Find(nom);

    if (pivot == null) { sb.AppendLine(nom + " : ABSENT"); continue; }

    UnityEditor.Undo.RecordObject(pivot, "Remettre le pivot du flipper sur son plot");

    pivot.localPosition = new Vector3(sx, 0f, 1.533f);
    pivot.rotation = cadre;

    var corps = pivot.GetComponent<Rigidbody>();

    if (corps != null)
    {
        corps.linearVelocity = Vector3.zero;
        corps.angularVelocity = Vector3.zero;
        corps.Sleep();
    }

    var joint = pivot.GetComponent<HingeJoint>();

    sb.AppendLine(nom + " : pos " + pivot.localPosition.ToString("F4")
                  + "   rot locale " + pivot.localEulerAngles.ToString("F2")
                  + "   hinge.angle = " + (joint != null ? joint.angle.ToString("F1") + "°" : "?"));
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
sb.AppendLine("scène modifiée : " + scene.isDirty + "   (Ctrl+Z annule, Ctrl+S conserve)");

return sb.ToString();
