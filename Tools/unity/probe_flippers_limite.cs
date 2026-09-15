// SONDE (écrit puis restaure) — la LIMITE du HingeJoint tient-elle ?
//
// Suite de `probe_flippers_cause.cs` : collider coupé, la rotation du flipper part quand même
// (133° pour un débattement de ±30°). Il reste deux explications, et cette sonde les sépare en
// observant la rotation pas à pas plutôt qu'à l'arrivée :
//
//   i)  la limite du joint ne s'applique pas — la rotation file sans but ;
//   ii) la rotation part d'un coup au premier pas — c'est alors l'état INITIAL du joint
//       (ancrage, angle de repos) qui est en cause, pas la limite.
//
// On relève la rotation toutes les 25 pas (0,5 s) sur 3 s, dans trois configurations :
//   A. tel quel (ressort 1500 → −30°, limites ±30°, ancrage au monde)
//   B. ancrage du joint ramené sur le corps
//   C. ressort ET limites coupés — plus aucun couple, la rotation doit rester à zéro
//
// C est le témoin : si la rotation bouge encore sans ressort ni limite, c'est la méthode de
// simulation qui parle, pas le joint.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null) { return "'PinballTable/Gameplay' introuvable."; }

var pivot = gameplay.Find("Flipper_Left_Pivot");

if (pivot == null) { return "Flipper_Left_Pivot introuvable."; }

var corps = pivot.GetComponent<Rigidbody>();
var joint = pivot.GetComponent<HingeJoint>();
var colliders = pivot.GetComponentsInChildren<Collider>(true);
var actifs = new bool[colliders.Length];

for (int k = 0; k < colliders.Length; k++) { actifs[k] = colliders[k].enabled; colliders[k].enabled = false; }

var depart = pivot.position;
var departRot = pivot.rotation;
var ancreAvant = joint.connectedAnchor;
bool autoAvant = joint.autoConfigureConnectedAnchor;
bool limitesAvant = joint.useLimits;
var ressortAvant = joint.spring;

sb.AppendLine("=== rotation pas à pas (collider coupé) ===");
sb.AppendLine("départ : position " + depart.ToString("F4")
              + "   rot " + departRot.eulerAngles.ToString("F2")
              + "   hinge.angle = " + joint.angle.ToString("F2") + "°");

var modes = new string[] { "A. tel quel", "B. ancrage ramené sur le corps", "C. sans ressort ni limite (témoin)" };

var modeAvant = Physics.simulationMode;
Physics.simulationMode = SimulationMode.Script;

try
{
    foreach (var mode in modes)
    {
        // --- configuration
        joint.autoConfigureConnectedAnchor = autoAvant;
        joint.connectedAnchor = ancreAvant;
        joint.useLimits = limitesAvant;
        joint.spring = ressortAvant;

        if (mode.StartsWith("B"))
        {
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = depart;
        }
        else if (mode.StartsWith("C"))
        {
            joint.useLimits = false;
            joint.spring = new JointSpring { spring = 0f, damper = 0f, targetPosition = 0f };
        }

        pivot.SetPositionAndRotation(depart, departRot);
        corps.position = depart;
        corps.rotation = departRot;
        corps.linearVelocity = Vector3.zero;
        corps.angularVelocity = Vector3.zero;
        Physics.SyncTransforms();

        sb.AppendLine();
        sb.AppendLine("### " + mode);

        for (int bloc = 0; bloc < 6; bloc++)
        {
            for (int s = 0; s < 25; s++) { Physics.Simulate(1f / 50f); }

            sb.AppendLine("    t = " + ((bloc + 1) * 0.5f).ToString("F1") + " s"
                          + "   rot " + Quaternion.Angle(departRot, pivot.rotation).ToString("F2").PadLeft(7) + "°"
                          + "   hinge.angle " + joint.angle.ToString("F1").PadLeft(8) + "°"
                          + "   pos " + (Vector3.Distance(depart, pivot.position) * 1000f / 16.66667f).ToString("F2").PadLeft(7) + " mm"
                          + "   ω " + corps.angularVelocity.magnitude.ToString("F4"));
        }
    }
}
finally
{
    for (int k = 0; k < colliders.Length; k++) { colliders[k].enabled = actifs[k]; }

    joint.autoConfigureConnectedAnchor = autoAvant;
    joint.connectedAnchor = ancreAvant;
    joint.useLimits = limitesAvant;
    joint.spring = ressortAvant;

    pivot.SetPositionAndRotation(depart, departRot);
    corps.position = depart;
    corps.rotation = departRot;
    corps.linearVelocity = Vector3.zero;
    corps.angularVelocity = Vector3.zero;

    Physics.simulationMode = modeAvant;
}

sb.AppendLine();
sb.AppendLine("Lecture : si C bouge, la mesure est faussée par la méthode ; si seul A bouge,");
sb.AppendLine("          l'ancrage du joint est le fautif.");

var chemin = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "flippers_limite.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(chemin));
System.IO.File.WriteAllText(chemin, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(chemin) + "  (" + sb.Length + " car.)";
