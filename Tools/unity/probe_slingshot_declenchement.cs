// Pourquoi `Slingshot.cs` ne se déclenche-t-il pas ? LECTURE SEULE.
//
// Le banc de `test_slingshots.cs` a montré un simple rebond (3,996 u/s entrant →
// 2,169 u/s sortant, plat sur 40 pas) : aucune impulsion. Le script est donc sorti
// avant son `AddForce`. Trois sorties possibles, dans l'ordre du code :
//
//   1. `collision.collider.CompareTag("Ball")` — le tag de la bille de scène ;
//   2. `collision.rigidbody` / `attachedRigidbody` nuls ;
//   3. `Time.time - lastKickTime < cooldown` — et en ÉDITEUR, `Time.time` n'avance
//      pas : il vaut 0, `lastKickTime` vaut 0, donc 0 < 0,12 est vrai au PREMIER
//      contact et le script sort. Ce serait un artefact du banc, pas un défaut du jeu.
//
// On ne devine pas : on relève `lastKickTime` par réflexion avant et après le choc.
// S'il change, le script est entré (et le problème est ailleurs) ; s'il ne change pas,
// la garde est la cause.

var sb = new System.Text.StringBuilder();

const float U = 16.6666667f;
const float RAYON = 0.225f;

var racine = GameObject.Find("PinballTable");
var rt = racine.transform;
var gameplay = rt.Find("Gameplay");
var bille = gameplay.Find("Ball");
var corps = bille.GetComponent<Rigidbody>();

sb.AppendLine("=== 1. la bille ===");
sb.AppendLine("  tag   : '" + bille.tag + "'" + (bille.CompareTag("Ball") ? "   ✓" : "   ✗ CompareTag(\"Ball\") est FAUX"));
sb.AppendLine("  layer : " + bille.gameObject.layer + " (" + LayerMask.LayerToName(bille.gameObject.layer) + ")");
sb.AppendLine("  tag du prefab : "
    + UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ball.prefab").tag);

sb.AppendLine();
sb.AppendLine("=== 2. l'horloge de l'éditeur ===");
sb.AppendLine("  Time.time                  : " + Time.time.ToString("F4"));
sb.AppendLine("  Time.timeSinceLevelLoad    : " + Time.timeSinceLevelLoad.ToString("F4"));
sb.AppendLine("  Time.realtimeSinceStartup  : " + Time.realtimeSinceStartup.ToString("F4"));
sb.AppendLine("  Time.fixedTime             : " + Time.fixedTime.ToString("F4"));

// --- 3. les champs du Slingshot, avant le choc --------------------------------------------
var hote = rt.Find("Gameplay/" + "Slingshot_Right");

if (hote == null) { foreach (Transform e in gameplay) { if (e.name == "Slingshot_Right") { hote = e; } } }
if (hote == null) { return sb.ToString() + "\nSlingshot_Right introuvable sous Gameplay."; }

var sl = hote.GetComponent("Slingshot");

if (sl == null) { return sb.ToString() + "\nLe script Slingshot n'est pas sur l'hôte."; }

var type = sl.GetType();

object Lire(string nom)
{
    var c = type.GetField(nom, System.Reflection.BindingFlags.Public
        | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    return c != null ? c.GetValue(sl) : "(champ absent)";
}

sb.AppendLine();
sb.AppendLine("=== 3. le Slingshot, avant le choc ===");
sb.AppendLine("  kickForce    : " + Lire("kickForce"));
sb.AppendLine("  upwardBias   : " + Lire("upwardBias"));
sb.AppendLine("  cooldown     : " + Lire("cooldown"));
sb.AppendLine("  lastKickTime : " + Lire("lastKickTime"));
sb.AppendLine("  actif        : " + hote.gameObject.activeInHierarchy
    + "   script activé : " + (sl as Behaviour).enabled);
sb.AppendLine("  collider sur l'hôte : " + (hote.GetComponent<Collider>() != null
    ? hote.GetComponent<Collider>().GetType().Name : "AUCUN"));

// --- 4. le même choc que le banc, et on relit ---------------------------------------------
const float TOP_X = 0.1925f * U;
const float TOP_Z = 0.330f * U;
const float BOT_X = 0.088f * U;
const float BOT_Z = 0.085f * U;

Vector3 a = new Vector3(TOP_X, 0f, TOP_Z);
Vector3 b = new Vector3(BOT_X, 0f, BOT_Z);
float dx = b.x - a.x, dz = b.z - a.z;
float len = Mathf.Sqrt(dx * dx + dz * dz);
Vector3 nExt = new Vector3(-dz / len, 0f, dx / len);
Vector3 nInt = -nExt;
Vector3 centreFace = (a + b) * 0.5f;
Vector3 intMonde = (rt.rotation * nInt).normalized;

var ancienMode = Physics.simulationMode;

bille.gameObject.SetActive(true);
corps.isKinematic = false;
corps.linearVelocity = -intMonde * 4f;
corps.angularVelocity = Vector3.zero;
corps.position = rt.TransformPoint(new Vector3(centreFace.x, RAYON, centreFace.z)) + intMonde * 0.60f;
corps.rotation = Quaternion.identity;

Physics.simulationMode = SimulationMode.Script;
Physics.SyncTransforms();

for (int i = 0; i < 30; i++)
{
    Physics.Simulate(0.004f);

    if (i == 14 || i == 16 || i == 18)
    {
        sb.AppendLine("  pas " + i + " : Time.time " + Time.time.ToString("F4")
            + "  lastKickTime " + Lire("lastKickTime")
            + "  |v| " + corps.linearVelocity.magnitude.ToString("F3"));
    }
}

sb.AppendLine();
sb.AppendLine("=== 4. le Slingshot, après le choc ===");
sb.AppendLine("  lastKickTime : " + Lire("lastKickTime"));
sb.AppendLine("  → " + (System.Convert.ToSingle(Lire("lastKickTime")) > 0f
    ? "le script EST entré : la garde n'est pas la cause."
    : "le script n'est JAMAIS entré : sorti avant `lastKickTime = Time.time` "
      + "(tag, rigidbody, ou garde de cooldown à Time.time = 0)."));

// --- remise au parc ------------------------------------------------------------------------
Physics.simulationMode = ancienMode;

var spawn = GameObject.Find("BallSpawnPoint");
Vector3 pose = spawn != null ? spawn.transform.position : bille.position;

corps.isKinematic = true;
corps.linearVelocity = Vector3.zero;
corps.angularVelocity = Vector3.zero;
bille.SetPositionAndRotation(pose, spawn != null ? spawn.transform.rotation : bille.rotation);
corps.position = pose;
Physics.SyncTransforms();
bille.gameObject.SetActive(false);

sb.AppendLine();
sb.AppendLine("bille remise au parc : " + bille.gameObject.activeSelf);

return sb.ToString();
