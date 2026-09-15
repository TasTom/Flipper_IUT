// La bille à 8 u/s finit-elle coincée ? + mesure du pincement. LECTURE SEULE.
//
// `test_top_droit.cs` : à 8 u/s la bille reste à (4,36 / 17,44), ni orbite ni retombée.
// Ici : (1) 12 s de simulation à 8 u/s pour trancher stable vs retombée tardive ;
// (2) le jour entre le haut du Divider et la courbe (RailCorner) : si les surfaces
// se touchent, le virage est un bouchon géométrique, pas un réglage.

var rapport = new System.Text.StringBuilder();

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

var corps = bille.GetComponent<Rigidbody>();

Vector3 pos0 = corps.position;
Quaternion rot0 = corps.rotation;
bool kin0 = corps.isKinematic;
bool actif0 = bille.gameObject.activeSelf;

bille.gameObject.SetActive(true);
corps.isKinematic = false;

var mode0 = Physics.simulationMode;
Physics.simulationMode = SimulationMode.Script;

float dt = 1f / 60f;

corps.position = spawn.transform.position;
corps.linearVelocity = Vector3.zero;
corps.angularVelocity = Vector3.zero;
Physics.SyncTransforms();

for (int i = 0; i < 180; i++) { Physics.Simulate(dt); }

Vector3 repos = corps.position;
corps.linearVelocity = rt.forward * 8f;

rapport.AppendLine("=== 8 u/s, 12 s ===");

for (int i = 0; i < 720; i++)
{
    Physics.Simulate(dt);

    if (i % 120 == 0)
    {
        Vector3 l = rt.InverseTransformPoint(corps.position);
        rapport.AppendLine("  t " + (i / 60).ToString() + " s : (" + l.x.ToString("F2")
            + ", " + l.z.ToString("F2") + ") v=" + corps.linearVelocity.magnitude.ToString("F2"));
    }
}

{
    Vector3 l = rt.InverseTransformPoint(corps.position);
    rapport.AppendLine("fin : (" + l.x.ToString("F3") + ", " + l.z.ToString("F3")
        + ") v=" + corps.linearVelocity.magnitude.ToString("F3")
        + (corps.linearVelocity.magnitude < 0.3f && l.z > 14f ? "   → COINCÉE, stable ⚠"
        : l.z < 3f ? "   → retombée (comportement normal)" : "   → encore en mouvement"));
}

corps.linearVelocity = Vector3.zero;
corps.angularVelocity = Vector3.zero;
corps.position = pos0;
corps.rotation = rot0;
corps.isKinematic = kin0;
bille.gameObject.SetActive(actif0);
Physics.simulationMode = mode0;

// --- le pincement Divider / RailCorner -------------------------------------------------
rapport.AppendLine();
rapport.AppendLine("=== jour Divider ↔ courbe ===");

Transform divider = null, courbe = null;

foreach (var f in racine.GetComponentsInChildren<MeshFilter>())
{
    if (f.name.StartsWith("Divider")) { divider = f.transform; }
    if (f.name.StartsWith("RailCorner")) { courbe = f.transform; }
}

rapport.AppendLine("Divider : " + (divider != null ? divider.name : "ABSENT"));
rapport.AppendLine("RailCorner : " + (courbe != null ? courbe.name : "ABSENT"));

if (divider != null && courbe != null)
{
    var fm = divider.GetComponent<MeshFilter>();
    var colCourbe = courbe.GetComponent<Collider>();
    var maille = fm.sharedMesh;

    float zMaxDiv = float.MinValue;
    float jourMin = float.MaxValue;
    Vector3 ouMin = Vector3.zero;

    foreach (var v in maille.vertices)
    {
        Vector3 monde = fm.transform.TransformPoint(v);
        Vector3 local = rt.InverseTransformPoint(monde);

        if (local.z > zMaxDiv) { zMaxDiv = local.z; }

        if (local.z > 16f && local.x > 3f)
        {
            Vector3 proche = colCourbe.ClosestPoint(monde);
            float d = Vector3.Distance(monde, proche);

            if (d < jourMin) { jourMin = d; ouMin = local; }
        }
    }

    rapport.AppendLine("haut du Divider : z = " + zMaxDiv.ToString("F3"));
    rapport.AppendLine("jour min Divider→RailCorner : " + jourMin.ToString("F4") + " u = "
        + (jourMin * 1000f / 16.66667f).ToString("F1") + " mm à ("
        + ouMin.x.ToString("F2") + ", " + ouMin.z.ToString("F2") + ")"
        + (jourMin < 0.45f ? "   ⚠ < bille Ø 0,45" : "   ✓"));
}

return rapport.ToString();
