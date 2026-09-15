// Remet les deux flippers « nus », prêts à être habillés. ÉCRITURE (annulable, non enregistrée).
//
// Pourquoi ce script existe : la scène `Neutral.unity` a dérivé. Relevé par
// `probe_flippers_etat.cs` (2026-09-15) :
//
//   Flipper_Left_Pivot   local (−1,4475 ; +0,2976 ; 1,4120)  rot monde (13,38 ; 330,42 ; 350,33)
//   Flipper_Right_Pivot  local (+0,0415 ; +0,0836 ; 1,3320)  rot monde (82,22 ; 305,47 ; 141,90)
//
// Autrement dit : le pivot DROIT est au centre de la table (x 0,04 au lieu de +1,428) et les
// deux cadres sont quelconques. C'est le même dégât que celui décrit dans
// `fix_flipper_pivots.cs` — une simulation de ressort contre obstacle a arraché les `Rigidbody`
// de leur ancre, et les mouvements physiques ne passent pas par la pile Undo.
//
// Ce script fait le ménage AVANT la pose, pas la pose elle-même :
//   1. pivots à leur cote contractuelle (`PlaceImportedTable` : ∓1,428 ; 0 ; 1,533) et au
//      cadre `root × Euler(+90,0,0)` — Z local vers le bas, cf. `place_scene_flippers.cs` §1 ;
//   2. `Rigidbody` remis à zéro et endormi (on repart d'un corps sain) ;
//   3. bats retirés (`Bat_Mesh` et tout autre enfant de `Flipper_Bat`), porteur recentré.
//
// Les retirer est délibéré : `place_scene_flippers.cs` (§3) laisse intact un bat déjà habillé
// tant que le cadre n'a pas bougé. Ici le cadre vient d'être remis d'aplomb, donc la garde
// d'idempotence verrait « rien à faire » sur un bat assis dans un cadre qui, lui, était faux —
// et le bat resterait de travers. Le ménage lève l'ambiguïté.
//
// Le script est idempotent et ne touche à rien d'autre : ni le `Flipper` (angles, ressorts),
// ni le `HingeJoint` (axe, limites), ni les autres hôtes.

var sb = new System.Text.StringBuilder();

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null || rt == null)
{
    return "'PinballTable/Gameplay' introuvable — ce n'est pas la scène du menu 4.";
}

// Le cadre de référence : ±90° autour de l'axe X de la table. Z local vers le bas, Y local
// vers le haut de table (cf. `place_scene_flippers.cs` §1 pour la démonstration).
var cadre = rt.rotation * Quaternion.Euler(90f, 0f, 0f);

sb.AppendLine("=== scène '" + scene.name + "' ===");
sb.AppendLine("cadre de référence : " + cadre.eulerAngles.ToString("F2")
              + "   (−racine.up = direction de l'axe du joint)");

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    sb.AppendLine();
    sb.AppendLine("### " + nom);

    var pivot = gameplay.Find(nom);

    if (pivot == null) { sb.AppendLine("  ABSENT — rien fait"); continue; }

    float sx = nom.Contains("Left") ? -1.428f : 1.428f;

    sb.AppendLine("  avant : local " + pivot.localPosition.ToString("F4")
                  + "   rot monde " + pivot.rotation.eulerAngles.ToString("F2"));

    // 1. La cote et le cadre.
    UnityEditor.Undo.RecordObject(pivot, "Remettre le pivot du flipper à sa cote");

    pivot.localPosition = new Vector3(sx, 0f, 1.533f);
    pivot.rotation = cadre;

    // L'échelle aussi : une échelle héritée d'un essai fausserait toutes les mesures du bat.
    if (pivot.localScale != Vector3.one)
    {
        sb.AppendLine("  échelle " + pivot.localScale.ToString("F4") + " → (1,00 ; 1,00 ; 1,00)");
        pivot.localScale = Vector3.one;
    }

    // 2. Le corps.
    var corps = pivot.GetComponent<Rigidbody>();

    if (corps != null)
    {
        corps.linearVelocity = Vector3.zero;
        corps.angularVelocity = Vector3.zero;
        corps.position = pivot.position;
        corps.rotation = pivot.rotation;
        corps.Sleep();
    }

    // 3. Les bats.
    var porteur = pivot.Find("Flipper_Bat");

    if (porteur == null)
    {
        var neuf = new GameObject("Flipper_Bat");
        UnityEditor.Undo.RegisterCreatedObjectUndo(neuf, "Créer Flipper_Bat");
        neuf.transform.SetParent(pivot, false);
        porteur = neuf.transform;
        sb.AppendLine("  porteur 'Flipper_Bat' créé");
    }

    int retires = 0;

    foreach (Transform enfant in porteur)
    {
        UnityEditor.Undo.DestroyObjectImmediate(enfant.gameObject);
        retires++;
    }

    UnityEditor.Undo.RecordObject(porteur, "Recentrer Flipper_Bat");

    porteur.localPosition = Vector3.zero;
    porteur.localRotation = Quaternion.identity;
    porteur.localScale = Vector3.one;

    sb.AppendLine("  " + retires + " enfant(s) de 'Flipper_Bat' retiré(s)"
                  + (retires == 0 ? " (il était déjà nu)" : ""));

    sb.AppendLine("  après : local " + pivot.localPosition.ToString("F4")
                  + "   rot monde " + pivot.rotation.eulerAngles.ToString("F2")
                  + "   écart au cadre " + Quaternion.Angle(pivot.rotation, cadre).ToString("F3") + "°"
                  + "   axe du joint vs −up table "
                  + Vector3.Angle(pivot.forward, -rt.up).ToString("F3") + "°");
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

sb.AppendLine();
sb.AppendLine("scène modifiée : " + scene.isDirty + "   (Ctrl+Z annule, Ctrl+S conserve)");
sb.AppendLine("suite : `place_scene_flippers.cs` habille les deux bats.");

var chemin = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "repose_flippers.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(chemin));
System.IO.File.WriteAllText(chemin, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(chemin) + "  (" + sb.Length + " car.)";
