// Combien le slingshot transmet-il VRAIMENT ? EN MODE PLAY.
//
// `test_slingshots_play.cs` a établi que le script entre et chasse la bille (5,36 u/s
// projetés vers l'aire de jeu, miroir exact). Mais `kickForce = 9` devrait donner
// 9 / √(1 + 0,25²) = 8,73 u/s horizontaux. On relève donc le saut pas à pas pour voir
// où passe la différence, plutôt que de le supposer.

var sb = new System.Text.StringBuilder();

const float U = 16.6666667f;
const float RAYON = 0.225f;

if (!Application.isPlaying) { return "Ce banc exige le mode Play."; }

var racine = GameObject.Find("PinballTable");
var rt = racine.transform;
var gameplay = rt.Find("Gameplay");
var bille = gameplay.Find("Ball");
var corps = bille.GetComponent<Rigidbody>();

var hote = gameplay.Find("Slingshot_Right");
var sl = hote.GetComponent("Slingshot");
var type = sl.GetType();

var champCoup = type.GetField("lastKickTime", System.Reflection.BindingFlags.NonPublic
    | System.Reflection.BindingFlags.Instance);
var champForce = type.GetField("kickForce", System.Reflection.BindingFlags.NonPublic
    | System.Reflection.BindingFlags.Instance);

float coup() { return System.Convert.ToSingle(champCoup.GetValue(sl)); }

sb.AppendLine("kickForce = " + champForce.GetValue(sl));

const float TOP_X = 0.1925f * U, TOP_Z = 0.330f * U;
const float BOT_X = 0.088f * U, BOT_Z = 0.085f * U;

Vector3 a = new Vector3(TOP_X, 0f, TOP_Z);
Vector3 b = new Vector3(BOT_X, 0f, BOT_Z);
float dx = b.x - a.x, dz = b.z - a.z;
float len = Mathf.Sqrt(dx * dx + dz * dz);
Vector3 nExt = new Vector3(-dz / len, 0f, dx / len);
Vector3 nInt = -nExt;
Vector3 centreFace = (a + b) * 0.5f;
Vector3 intMonde = (rt.rotation * nInt).normalized;

bille.gameObject.SetActive(true);
corps.isKinematic = false;
corps.linearVelocity = -intMonde * 4f;
corps.angularVelocity = Vector3.zero;
corps.position = rt.TransformPoint(new Vector3(centreFace.x, RAYON, centreFace.z)) + intMonde * 0.60f;
corps.rotation = Quaternion.identity;

Physics.simulationMode = SimulationMode.Script;
Physics.SyncTransforms();

Vector3 lInt = rt.InverseTransformDirection(intMonde);

sb.AppendLine();
sb.AppendLine("pas | v locale (x, y, z)                | |v|    | proj. intérieur | lastKickTime");
sb.AppendLine("----+-----------------------------------+--------+-----------------+-------------");

float precedent = coup();

for (int i = 0; i < 26; i++)
{
    Physics.Simulate(0.004f);

    Vector3 lv = rt.InverseTransformDirection(corps.linearVelocity);
    float maintenant = coup();

    sb.AppendLine(i.ToString("000") + " | (" + lv.x.ToString("F3").PadLeft(7) + ", "
        + lv.y.ToString("F3").PadLeft(7) + ", " + lv.z.ToString("F3").PadLeft(7) + ") | "
        + lv.magnitude.ToString("F3").PadLeft(6) + " | "
        + Vector3.Dot(lv, lInt).ToString("F3").PadLeft(15) + " | "
        + (maintenant != precedent ? "COUP → " + maintenant.ToString("F4") : maintenant.ToString("F4")));

    precedent = maintenant;
}

Physics.simulationMode = SimulationMode.FixedUpdate;

return sb.ToString();
