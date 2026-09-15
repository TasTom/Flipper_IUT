// TEST (écrit des positions — annulé en fin de script) — les flippers tiennent-ils leur ancre ?
//
// Le doute à lever : le coin du talon du flipper pénètre le corps du slingshot (calcul
// analytique : ~7 mm sous la face, à l'angle de repos). Si ce recouvrement pousse vraiment le
// `Rigidbody`, les pivots vont dériver en quelques secondes de simulation — c'est exactement ce
// qui les avait arrachés de leur ancre (le pivot droit s'était retrouvé au centre de la table).
//
// Méthode : `Physics.simulationMode = Script` + `Physics.Simulate(dt)` en boucle. L'éditeur
// n'avance que ~2 images quand la fenêtre n'a pas le focus, donc c'est le seul moyen de faire
// tourner la physique pour de bon.
//
// Le test REMET les poses d'origine à la fin (y compris par la pile Undo) : il ne laisse pas la
// scène modifiée. Il ne touche ni au ressort ni aux limites — on observe, on ne pilote pas.

var sb = new System.Text.StringBuilder();

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null) { return "'PinballTable/Gameplay' introuvable."; }

var noms = new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" };
var pivots = new Transform[noms.Length];
var departs = new Vector3[noms.Length];
var departsRot = new Quaternion[noms.Length];

for (int i = 0; i < noms.Length; i++)
{
    pivots[i] = gameplay.Find(noms[i]);

    if (pivots[i] == null) { return noms[i] + " introuvable."; }

    departs[i] = pivots[i].position;
    departsRot[i] = pivots[i].rotation;
}

// La bille parquée ne doit pas interférer : on la laisse où elle est, mais on vérifie qu'elle
// est bien inactive.
var bille = gameplay.Find("Ball");

sb.AppendLine("=== test de tenue d'ancre — scène '" + scene.name + "' ===");
sb.AppendLine("bille : " + (bille == null ? "absente"
                      : (bille.gameObject.activeInHierarchy ? "ACTIVE (elle peut perturber)" : "parquée ✓")));

var modeAvant = Physics.simulationMode;

Physics.simulationMode = SimulationMode.Script;

const float dt = 1f / 50f;
const int pas = 250;   // 5 secondes

sb.AppendLine("simulation : " + pas + " pas de " + (dt * 1000f).ToString("F1") + " ms (5 s)");

float pire = 0f;

try
{
    for (int i = 0; i < pas; i++)
    {
        Physics.Simulate(dt);
    }
}
finally
{
    Physics.simulationMode = modeAvant;
}

sb.AppendLine();

for (int i = 0; i < noms.Length; i++)
{
    var t = pivots[i];
    float d = Vector3.Distance(departs[i], t.position);
    float r = Quaternion.Angle(departsRot[i], t.rotation);

    pire = Mathf.Max(pire, d);

    var joint = t.GetComponent<HingeJoint>();

    sb.AppendLine("  " + noms[i]);
    sb.AppendLine("    dérive de position  " + d.ToString("F6") + " u = "
                  + (d * 1000f / 16.66667f).ToString("F3") + " mm");
    sb.AppendLine("    dérive de rotation  " + r.ToString("F4") + "°"
                  + (joint != null ? "   hinge.angle = " + joint.angle.ToString("F2") + "°" : ""));
    sb.AppendLine("    vitesse linéaire     " + t.GetComponent<Rigidbody>().linearVelocity.magnitude.ToString("F6")
                  + " u/s   (0 = au repos, l'ancre tient)");
}

// Remise en place — par Undo, pour que Ctrl+Z reste cohérent même si ce script en a fait une.
for (int i = 0; i < noms.Length; i++)
{
    UnityEditor.Undo.RecordObject(pivots[i], "Test de tenue d'ancre des flippers");

    pivots[i].position = departs[i];
    pivots[i].rotation = departsRot[i];

    var corps = pivots[i].GetComponent<Rigidbody>();

    if (corps != null)
    {
        corps.linearVelocity = Vector3.zero;
        corps.angularVelocity = Vector3.zero;
        corps.Sleep();
    }
}

sb.AppendLine();
sb.AppendLine(pire < 1e-4f
    ? "VERDICT : les deux pivots tiennent leur ancre sous 5 s de simulation ✓"
    : "VERDICT : dérive de " + (pire * 1000f / 16.66667f).ToString("F2")
      + " mm — un collider pousse le flipper.");

var chemin = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "flippers_tenue.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(chemin));
System.IO.File.WriteAllText(chemin, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(chemin) + "  (" + sb.Length + " car.)";
