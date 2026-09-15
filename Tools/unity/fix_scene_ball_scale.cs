// Remet la bille de la scène à l'échelle de son prefab.
//
// Ce que j'ai cassé en la posant : `place_scene_ball.cs` recopiait sur la bille l'échelle du
// point d'apparition — `(1, 1, 1)` — au motif d'« éviter deux valeurs écrites à la main qui
// divergeraient ». Le raisonnement vaut pour une **position**, pas pour une échelle : l'échelle
// n'est pas une coordonnée à recopier, c'est une propriété du prefab. `Ball.prefab` porte
// 0,45 ; la bille posée s'est retrouvée à 1,00, soit **60 mm au lieu de 27 mm**.
//
// Les conséquences mesurées, toutes cohérentes avec ce seul chiffre :
//
//   * le couloir de lancement fait 40,0 mm de large — plus étroit que la bille, qui ne peut
//     donc pas y entrer : en Play mode, la physique l'a éjectée vers le haut (relevée à
//     `y 1,2097` pour un point d'apparition à `y 0,5109`) ;
//   * le sol du couloir est à `y 0,2168` et le bas de la bille à `y 0,0109` : elle naissait
//     **enfoncée de 0,206 u** dans le plateau ;
//   * le rapport « largeur de l'aire de jeu / diamètre », qui vaut 19,06 sur une table réelle,
//     tombait à 8,67 — d'où l'impression très juste qu'elle était trop grosse.
//
// On ne touche qu'à l'échelle. La position, elle, est bonne : elle a été recopiée du point
// d'apparition, et c'est bien ce qu'on voulait.

var sb = new System.Text.StringBuilder();

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

var bille = GameObject.FindGameObjectWithTag("Ball");
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ball.prefab");

if (bille == null) { return "aucune bille taggée 'Ball' dans la scène"; }
if (prefab == null) { return "Assets/Prefabs/Ball.prefab introuvable"; }

const float u = 16.66667f;

sb.AppendLine("=== avant ===");
sb.AppendLine("échelle : " + bille.transform.localScale.ToString("F4"));
sb.AppendLine("diamètre: " + bille.GetComponent<Renderer>().bounds.size.x.ToString("F4") + " u   = "
              + (bille.GetComponent<Renderer>().bounds.size.x / u * 1000f).ToString("F2") + " mm");

// --- la correction ------------------------------------------------------------------------------

UnityEditor.Undo.RecordObject(bille.transform, "Remettre la bille à l'échelle de son prefab");

bille.transform.localScale = prefab.transform.localScale;

EditorUtility.SetDirty(bille);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

// --- contrôles ---------------------------------------------------------------------------------

Physics.SyncTransforms();

var rendu = bille.GetComponent<Renderer>();
var sphere = bille.GetComponent<SphereCollider>();

float diametre = rendu.bounds.size.x;
float rayon = sphere != null ? sphere.radius * bille.transform.lossyScale.x : diametre * 0.5f;

sb.AppendLine();
sb.AppendLine("=== après ===");
sb.AppendLine("échelle : " + bille.transform.localScale.ToString("F4"));
sb.AppendLine("diamètre: " + diametre.ToString("F4") + " u   = "
              + (diametre / u * 1000f).ToString("F2") + " mm   (standard réel : 26,99 mm)");
sb.AppendLine("collider: rayon " + rayon.ToString("F4") + " u");

// Le bas de la bille et le sol sous elle : c'est ce contrôle qui montre si elle est posée
// dessus ou enfoncée dedans.
float bas = bille.transform.position.y - rayon;

sb.AppendLine();
sb.AppendLine("centre         : y " + bille.transform.position.y.ToString("F4"));
sb.AppendLine("bas de la bille: y " + bas.ToString("F4"));

RaycastHit sol;

if (Physics.Raycast(bille.transform.position, Vector3.down, out sol, 30f, ~0, QueryTriggerInteraction.Ignore))
{
    sb.AppendLine("sol dessous    : " + sol.collider.gameObject.name + " à y " + sol.point.y.ToString("F4"));

    float jeu = bas - sol.point.y;

    sb.AppendLine("jeu            : " + jeu.ToString("F4") + " u = "
                  + (jeu / u * 1000f).ToString("F2") + " mm"
                  + (jeu < 0f ? "   ⚠ ENFONCÉE DANS LE SOL" : "   (elle tombe de là avant de rouler)"));
}
else
{
    sb.AppendLine("sol dessous    : AUCUN");
}

// --- les rapports, qui sont le vrai juge --------------------------------------------------------

// L'aire de jeu, mesurée entre les faces des murs à hauteur de bille — au milieu de la table,
// là où aucun couloir ne vient fausser la lecture.
var origine = new Vector3(-6.5f, 8f * 0.1228f + 0.30f, 8f);
var touches = Physics.RaycastAll(origine, Vector3.right, 13f, ~0, QueryTriggerInteraction.Ignore);
System.Array.Sort(touches, (a, b2) => a.point.x.CompareTo(b2.point.x));

sb.AppendLine();
sb.AppendLine("=== rapports ===");

if (touches.Length >= 2)
{
    float largeur = touches[1].point.x - touches[0].point.x;

    sb.AppendLine("aire de jeu (entre Left et Divider) : " + largeur.ToString("F4") + " u = "
                  + (largeur / u * 1000f).ToString("F1") + " mm");
    sb.AppendLine("rapport largeur / diamètre          : " + (largeur / diametre).ToString("F2")
                  + "   (table réelle : 19,06)");
}

sb.AppendLine("rapport plateau / diamètre          : "
              + (9.4392f / diametre).ToString("F2") + "   (table réelle : 19,06)");

sb.AppendLine();
sb.AppendLine("scène modifiée : " + scene.isDirty + "   (Ctrl+Z annule, Ctrl+S conserve)");

return sb.ToString();
