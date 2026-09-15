// Remet les deux flippers d'aplomb ET répare l'ancrage de leur HingeJoint. ÉCRITURE (annulable).
//
// Deux défauts, tous les deux mesurés le 2026-09-15 :
//
// 1. LES PIVOTS ONT DÉRIVÉ. Les essais `Physics.simulate` (mode Script, en édition) les ont
//    déplacés — la remise en place par `transform`/`corps.position` ne tient pas, le moteur
//    reste maître du `Rigidbody`. Relevé : gauche à (−0,9178 ; −0,0341 ; 0,8024) et tourné de
//    153°, droite à (0,3607 ; 0,0397 ; 0,8706). Cotes contractuelles : ∓1,5093 ; 0 ; 1,533.
//
// 2. L'ANCRAGE DU JOINT EST FAUX. `EnsureMvpStructure.EnsureFlipper` écrit
//    `autoConfigureConnectedAnchor = false` et `connectedAnchor = (0,0,0)`. Or le joint n'a
//    **pas** de `connectedBody` : Unity lit alors `connectedAnchor` **en monde**. Le flipper se
//    retrouve donc ancré à l'ORIGINE DE LA SCÈNE, à 2,15 u de son propre pivot. La position
//    étant gelée par les contraintes du `Rigidbody` (62 = position + rotations X et Y), la
//    seule liberté qui reste est la rotation autour de Z — et c'est par là que l'erreur
//    d'ancrage se décharge : le joint tord le flipper au lieu de le tenir.
//
// Ce script répare les deux, et rien d'autre : ni le `Flipper` (angles, ressorts), ni le
// `Flipper_Bat` (maillage, collider, matériaux), ni les limites du joint.

var sb = new System.Text.StringBuilder();

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null) { return "'PinballTable/Gameplay' introuvable."; }

// Le cadre de référence : ±90° autour de l'axe X de la table (Z local vers le bas, cf.
// `place_scene_flippers.cs` §1).
var cadre = rt.rotation * Quaternion.Euler(90f, 0f, 0f);

// La cote du pivot déduite de la pièce (cf. `place_scene_flippers.cs` §9) : la pointe du bat
// de 80 mm doit retomber sur le drain gap du générateur, 0,6570 u.
const float COTE = 1.5093f;
const float Z = 1.533f;

sb.AppendLine("=== scène '" + scene.name + "' ===");

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    sb.AppendLine();
    sb.AppendLine("### " + nom);

    var pivot = gameplay.Find(nom);

    if (pivot == null) { sb.AppendLine("  ABSENT"); continue; }

    float sx = nom.Contains("Left") ? -COTE : COTE;

    sb.AppendLine("  avant : local " + pivot.localPosition.ToString("F4")
                  + "   rot monde " + pivot.rotation.eulerAngles.ToString("F2"));

    UnityEditor.Undo.RecordObject(pivot, "Remettre le flipper d'aplomb");

    pivot.localPosition = new Vector3(sx, 0f, Z);
    pivot.rotation = cadre;
    pivot.localScale = Vector3.one;

    // 1. Le corps : le moteur est maître du Rigidbody, on écrit donc aussi ses propres champs.
    var corps = pivot.GetComponent<Rigidbody>();

    if (corps != null)
    {
        corps.position = pivot.position;
        corps.rotation = pivot.rotation;
        corps.linearVelocity = Vector3.zero;
        corps.angularVelocity = Vector3.zero;
        corps.Sleep();
    }

    // 2. L'ancrage du joint : le point du MONDE autour duquel le flipper tourne. Le pivot, pas
    //    l'origine de la scène. `autoConfigure` remis à true pour qu'Unity le recalcule si le
    //    pivot bouge un jour.
    var joint = pivot.GetComponent<HingeJoint>();

    if (joint != null)
    {
        sb.AppendLine("  ancrage avant : " + joint.connectedAnchor.ToString("F4")
                      + "   (écart au pivot "
                      + Vector3.Distance(joint.connectedAnchor, pivot.position).ToString("F4") + " u)");

        UnityEditor.Undo.RecordObject(joint, "Réparer l'ancrage du HingeJoint");

        joint.autoConfigureConnectedAnchor = true;
    }
    else { sb.AppendLine("  ⚠ pas de HingeJoint sur cet hôte"); }

    var joint2 = pivot.GetComponent<HingeJoint>();

    sb.AppendLine("  après : local " + pivot.localPosition.ToString("F4")
                  + "   rot monde " + pivot.rotation.eulerAngles.ToString("F2")
                  + "   écart au cadre " + Quaternion.Angle(pivot.rotation, cadre).ToString("F3") + "°"
                  + "   axe du joint vs −up table "
                  + (joint2 != null ? Vector3.Angle(pivot.forward, -rt.up).ToString("F3") + "°" : "?"));
}

// Les ancrages, en SECONDE passe : le point est écrit depuis la COTE, pas depuis
// `Transform.position`.
//
// Mesuré : sur un objet porteur d'un `Rigidbody`, `Transform.position` peut rendre une matrice
// monde en CACHE, détenue par le moteur — remonter les cotes par ce chemin donnait l'ancienne
// pose (écart résiduel 0,33 et 1,48 u). La cote, elle, est connue : le repère de `Gameplay` est
// celui de la table (x = 0 au centre de l'aire, z = 0 au bord bas, y = 0 au plateau).
Physics.SyncTransforms();

sb.AppendLine();
sb.AppendLine("### ancrage des joints (seconde passe)");

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    var pivot = gameplay.Find(nom);

    if (pivot == null) { continue; }

    var joint = pivot.GetComponent<HingeJoint>();

    if (joint == null) { continue; }

    float sx = nom.Contains("Left") ? -COTE : COTE;
    var monde = rt.TransformPoint(new Vector3(sx, 0f, Z));

    UnityEditor.Undo.RecordObject(joint, "Réparer l'ancrage du HingeJoint");

    joint.autoConfigureConnectedAnchor = false;
    joint.connectedAnchor = monde;

    var relu = rt.InverseTransformPoint(joint.connectedAnchor);
    var attendu = new Vector3(sx, 0f, Z);

    sb.AppendLine("  " + nom.PadRight(22)
                  + " ancrage table (" + relu.x.ToString("F4") + " ; " + relu.y.ToString("F4")
                  + " ; " + relu.z.ToString("F4") + ")"
                  + "   écart " + Vector3.Distance(relu, attendu).ToString("F5") + " u"
                  + (Vector3.Distance(relu, attendu) < 1e-4f ? "   ✓" : "   ⚠"));
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

sb.AppendLine();
sb.AppendLine("scène modifiée : " + scene.isDirty + "   (Ctrl+Z annule, Ctrl+S conserve)");

var chemin = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "fix_flipper_hinges.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(chemin));
System.IO.File.WriteAllText(chemin, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(chemin) + "  (" + sb.Length + " car.)";
