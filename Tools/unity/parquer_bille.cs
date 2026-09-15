// Remet la bille au parc — et VÉRIFIE qu'elle y est. ÉCRITURE (annulable).
//
// Deuxième version. La première écrivait `corps.position = spawn.position` sur un
// Rigidbody NON cinématique : en éditeur, la physique ne passee pas, et l'écriture est
// écrasée par la pose interne du corps. Mesuré : annoncé « après local (-3,0675 …) »
// alors que le point d'apparition est à (4,670 / 0,392 / 1,000) — l'écriture n'avait
// rien fait, et la lecture d'« avant » (-1,713) était la pose interne périmée du
// Rigidbody, pas le Transform (-3,067).
//
// D'où l'ordre corrigé : cinématique D'ABORD, puis Transform ET Rigidbody, puis
// SyncTransforms, puis relecture pour constater.

var sb = new System.Text.StringBuilder();

bool enJeu = Application.isPlaying;

sb.AppendLine("éditeur en Play : " + (enJeu ? "OUI" : "non"));

var racine = GameObject.Find("PinballTable");
var rt = racine.transform;
var gameplay = rt.Find("Gameplay");
var spawn = GameObject.Find("BallSpawnPoint");
var bille = gameplay.Find("Ball");

if (bille == null)
{
    var parTag = GameObject.FindGameObjectWithTag("Ball");
    bille = parTag != null ? parTag.transform : null;
}

if (bille == null) { return sb.ToString() + "AUCUNE BILLE ?"; }
if (spawn == null) { return sb.ToString() + "BallSpawnPoint introuvable."; }
if (spawn.transform.parent != gameplay) { sb.AppendLine("ATTENTION : le point d'apparition n'est pas sous Gameplay."); }

var corps = bille.GetComponent<Rigidbody>();

if (corps == null) { return sb.ToString() + "La bille n'a pas de Rigidbody."; }

Vector3 tf0 = rt.InverseTransformPoint(bille.position);
Vector3 rb0 = rt.InverseTransformPoint(corps.position);

sb.AppendLine("avant : Transform local (" + tf0.x.ToString("F4") + ", " + tf0.y.ToString("F4") + ", " + tf0.z.ToString("F4") + ")"
    + "   Rigidbody local (" + rb0.x.ToString("F4") + ", " + rb0.y.ToString("F4") + ", " + rb0.z.ToString("F4") + ")");
sb.AppendLine("        actif=" + bille.gameObject.activeSelf + " cinématique=" + corps.isKinematic
    + (Vector3.Distance(tf0, rb0) > 1e-4f ? "   ⚠ Transform et Rigidbody DIVERGENT" : "   (Transform et Rigidbody d'accord)"));

if (enJeu)
{
    return sb.ToString()
        + "ON NE TOUCHE À RIEN EN PLAY : sortez du mode Play (editor_stop), "
        + "la bille retrouvera sa place parquée toute seule.";
}

UnityEditor.Undo.RecordObject(bille, "Parquer la bille");
UnityEditor.Undo.RecordObject(corps, "Parquer la bille (corps)");

Vector3 but = spawn.transform.position;
Quaternion butRot = spawn.transform.rotation;

// 1. figer d'abord : une écriture sur un corps dynamique en éditeur est écrasée
corps.isKinematic = true;

// 2. écrire les DEUX poses — le Transform fait foi, le Rigidbody suit
corps.linearVelocity = Vector3.zero;
corps.angularVelocity = Vector3.zero;
bille.SetPositionAndRotation(but, butRot);
corps.position = but;
corps.rotation = butRot;

// 3. réconcilier la physique avec les Transforms
Physics.SyncTransforms();

// 4. parquer
bille.gameObject.SetActive(false);

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

// 5. CONSTATER — la première version annonçait un succès qu'elle n'avait pas obtenu
Vector3 tf1 = rt.InverseTransformPoint(bille.position);
Vector3 rb1 = rt.InverseTransformPoint(corps.position);
Vector3 attendu = rt.InverseTransformPoint(but);

sb.AppendLine("voulu : local (" + attendu.x.ToString("F4") + ", " + attendu.y.ToString("F4") + ", " + attendu.z.ToString("F4") + ")");
sb.AppendLine("après : Transform local (" + tf1.x.ToString("F4") + ", " + tf1.y.ToString("F4") + ", " + tf1.z.ToString("F4")
    + ")   Rigidbody local (" + rb1.x.ToString("F4") + ", " + rb1.y.ToString("F4") + ", " + rb1.z.ToString("F4") + ")");
sb.AppendLine("        actif=" + bille.gameObject.activeSelf + " cinématique=" + corps.isKinematic);

float ecart = Vector3.Distance(tf1, attendu);

sb.AppendLine(ecart < 1e-3f
    ? "✓ bille parquée au point d'apparition (écart " + (ecart * 1000f / 16.66667f).ToString("F4") + " mm)"
    : "⚠ ÉCHEC : écart " + (ecart * 1000f / 16.66667f).ToString("F2") + " mm du point d'apparition");
sb.AppendLine("scène modifiée (Ctrl+Z annule, Ctrl+S conserve).");

return sb.ToString();
