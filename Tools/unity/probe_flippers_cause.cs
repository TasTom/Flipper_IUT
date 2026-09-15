// SONDE (écrit puis restaure — annulable) — QUI pousse les flippers ?
//
// `test_flippers_tenue.cs` a montré une dérive de 56 mm, puis de 81 mm après rétraction de la
// boîte du collider. Autrement dit la rétraction n'a rien réglé : la cause est ailleurs. Deux
// suspects, et on les met à l'épreuve séparément :
//
//   A) un COLLIDER qui mord — le flipper est repoussé hors de la pénétration ;
//   B) le HINGE JOINT lui-même — un `HingeJoint` SANS `connectedBody` relie le corps au MONDE,
//      au point `connectedAnchor`. Laissé à (0,0,0), il tire le flipper vers l'ORIGINE du monde,
//      c'est-à-dire vers le centre de la table — ce qui ressemble beaucoup à ce qu'on observe.
//
// (`Joint` ne descend pas de `Behaviour` : pas d'`enabled` à couper. On neutralise donc le joint
// en épinglant son `connectedAnchor` sur la position du corps, ce qui revient au même.)
//
// Tout est remis en place à la fin, y compris en cas d'exception.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null) { return "'PinballTable/Gameplay' introuvable."; }

var noms = new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" };
var pivots = new Transform[noms.Length];

for (int i = 0; i < noms.Length; i++)
{
    pivots[i] = gameplay.Find(noms[i]);
    if (pivots[i] == null) { return noms[i] + " introuvable."; }
}

// --- 0. ce que porte le joint -------------------------------------------------------------------

sb.AppendLine("=== le joint, tel qu'il est ===");

foreach (var t in pivots)
{
    var j = t.GetComponent<HingeJoint>();
    var c = t.GetComponent<Rigidbody>();

    sb.AppendLine("  " + t.name);
    sb.AppendLine("    anchor (local)               " + j.anchor.ToString("F4"));
    sb.AppendLine("    connectedAnchor              " + j.connectedAnchor.ToString("F4")
                  + "   (en MONDE quand connectedBody est null)");
    sb.AppendLine("    autoConfigureConnectedAnchor " + j.autoConfigureConnectedAnchor);
    sb.AppendLine("    connectedBody                " + (j.connectedBody != null ? j.connectedBody.name : "null"));
    sb.AppendLine("    position du corps (monde)    " + t.position.ToString("F4"));
    sb.AppendLine("    donc décalage corps ↔ connectedAnchor = "
                  + Vector3.Distance(t.position, j.connectedAnchor).ToString("F4") + " u");
    sb.AppendLine("    spring                       ressort " + j.spring.spring + "  amorti "
                  + j.spring.damper + "  cible " + j.spring.targetPosition);
    sb.AppendLine("    limits                       " + (j.useLimits
                      ? "[" + j.limits.min.ToString("F1") + " ; " + j.limits.max.ToString("F1") + "]"
                      : "désactivées"));
    sb.AppendLine("    rigidbody                    masse " + c.mass + "  gravité " + c.useGravity
                  + "  kinematic " + c.isKinematic + "  contraintes " + c.constraints
                  + "  drag " + c.linearDamping + "  angDrag " + c.angularDamping);
}

// --- l'essai ------------------------------------------------------------------------------------

var modes = new string[] { "A. état réel", "B. collider coupé", "C. collider coupé + ancrage épinglé" };

sb.AppendLine();
sb.AppendLine("=== essai : 250 pas de 20 ms (5 s) par configuration ===");

for (int i = 0; i < noms.Length; i++)
{
    sb.AppendLine();
    sb.AppendLine("### " + noms[i]);

    var t = pivots[i];
    var corps = t.GetComponent<Rigidbody>();
    var joint = t.GetComponent<HingeJoint>();
    var colliders = t.GetComponentsInChildren<Collider>(true);
    var actifs = new bool[colliders.Length];

    for (int k = 0; k < colliders.Length; k++) { actifs[k] = colliders[k].enabled; }

    var depart = t.position;
    var departRot = t.rotation;
    var ancreAvant = joint.connectedAnchor;
    bool autoAvant = joint.autoConfigureConnectedAnchor;

    var modeAvant = Physics.simulationMode;
    Physics.simulationMode = SimulationMode.Script;

    try
    {
        for (int m = 0; m < modes.Length; m++)
        {
            for (int k = 0; k < colliders.Length; k++)
            {
                colliders[k].enabled = actifs[k] && m != 1 && m != 2;
            }

            // L'ancrage : en mode C, on le colle sur le corps pour que le joint ne tire plus.
            joint.autoConfigureConnectedAnchor = m == 2 ? false : autoAvant;
            joint.connectedAnchor = m == 2 ? depart : ancreAvant;

            t.SetPositionAndRotation(depart, departRot);
            corps.position = depart;
            corps.rotation = departRot;
            corps.linearVelocity = Vector3.zero;
            corps.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();

            for (int s = 0; s < 250; s++) { Physics.Simulate(1f / 50f); }

            sb.AppendLine("  " + modes[m].PadRight(36)
                          + " dérive " + (Vector3.Distance(depart, t.position) * 1000f / 16.66667f).ToString("F2").PadLeft(8) + " mm"
                          + "   rot " + Quaternion.Angle(departRot, t.rotation).ToString("F2").PadLeft(7) + "°"
                          + "   hinge.angle " + joint.angle.ToString("F1") + "°");
        }
    }
    finally
    {
        for (int k = 0; k < colliders.Length; k++) { colliders[k].enabled = actifs[k]; }

        joint.autoConfigureConnectedAnchor = autoAvant;
        joint.connectedAnchor = ancreAvant;

        t.SetPositionAndRotation(depart, departRot);
        corps.position = depart;
        corps.rotation = departRot;
        corps.linearVelocity = Vector3.zero;
        corps.angularVelocity = Vector3.zero;

        Physics.simulationMode = modeAvant;
    }
}

sb.AppendLine();
sb.AppendLine("Lecture : si B dérive autant que A, ce n'est pas un collider ;");
sb.AppendLine("          si C ne dérive plus, c'est l'ancrage du joint.");

var chemin = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "flippers_cause.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(chemin));
System.IO.File.WriteAllText(chemin, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(chemin) + "  (" + sb.Length + " car.)";
