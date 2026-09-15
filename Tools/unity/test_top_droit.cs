// Le virage chenal→orbite passe-t-il à toutes les vitesses ? LECTURE SEULE (bille remise).
//
// Retour terrain : la bille « n'arrive pas à passer en haut à droite ». La carte 3D
// (`probe_top_droit_3d.cs`) montre le virage bouché à hauteur de roulement, ouvert seulement
// vers y 0,5-0,6 : une bille lente bute, une bille rapide grimpe et file. Ce test quantifie :
// bille lâchée au bouchon, vitesse imposée le long du chenal (+Z table), 7 vitesses,
// 4 s de `Physics.Simulate` chacune. Verdict par vitesse : ORBITE / RETOMBÉE / COINCÉE.

var rapport = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
var rt = racine.transform;
var gameplay = rt.Find("Gameplay");
var spawn = GameObject.Find("BallSpawnPoint");

if (gameplay == null || spawn == null) { return "Gameplay ou BallSpawnPoint introuvable."; }

var bille = gameplay.Find("Ball");

if (bille == null)
{
    var parTag = GameObject.FindGameObjectWithTag("Ball");
    bille = parTag != null ? parTag.transform : null;
}

if (bille == null) { return "Aucune bille."; }

var corps = bille.GetComponent<Rigidbody>();

if (corps == null) { return "La bille n'a pas de Rigidbody."; }

Vector3 pos0 = corps.position;
Quaternion rot0 = corps.rotation;
bool kin0 = corps.isKinematic;
bool actif0 = bille.gameObject.activeSelf;

bille.gameObject.SetActive(true);
corps.isKinematic = false;

var mode0 = Physics.simulationMode;
Physics.simulationMode = SimulationMode.Script;

float dt = 1f / 60f;
Vector3 dirLancement = rt.forward;   // +Z table (remonte le chenal incliné)

// Mise au bouchon : chute depuis l'apparition, comme en partie.
corps.position = spawn.transform.position;
corps.rotation = spawn.transform.rotation;
corps.linearVelocity = Vector3.zero;
corps.angularVelocity = Vector3.zero;
Physics.SyncTransforms();

for (int i = 0; i < 180; i++) { Physics.Simulate(dt); }

Vector3 repos = corps.position;
rapport.AppendLine("bouchon (monde) : " + repos.ToString("F4"));

foreach (var v in new float[] { 6f, 8f, 10f, 12f, 14f, 16f, 18f })
{
    corps.position = repos;
    corps.linearVelocity = Vector3.zero;
    corps.angularVelocity = Vector3.zero;
    Physics.SyncTransforms();

    corps.linearVelocity = dirLancement * v;

    float maxZ = float.MinValue;
    float minXLaHaut = float.MaxValue;
    float yAuMaxZ = 0f;

    for (int i = 0; i < 240; i++)
    {
        Physics.Simulate(dt);

        Vector3 local = rt.InverseTransformPoint(corps.position);

        if (local.z > maxZ) { maxZ = local.z; yAuMaxZ = local.y; }
        if (local.z > 16.5f && local.x < minXLaHaut) { minXLaHaut = local.x; }
    }

    Vector3 finLocal = rt.InverseTransformPoint(corps.position);
    float vitesseFin = corps.linearVelocity.magnitude;

    string verdict;

    if (minXLaHaut < 3.9f) { verdict = "ORBITE ✓ (x min " + minXLaHaut.ToString("F2") + ")"; }
    else if (vitesseFin < 0.3f && maxZ > 14f) { verdict = "COINCÉE ⚠"; }
    else if (finLocal.z < 3f) { verdict = "retombée au lanceur"; }
    else { verdict = "ailleurs ?"; }

    rapport.AppendLine("v=" + v.ToString("F0") + " u/s : z max " + maxZ.ToString("F2")
        + " (y " + yAuMaxZ.ToString("F2") + ")   fin (" + finLocal.x.ToString("F2")
        + ", " + finLocal.z.ToString("F2") + ") v=" + vitesseFin.ToString("F2")
        + "   → " + verdict);
}

// Nettoyage : la bille retrouve son état parqué.
corps.linearVelocity = Vector3.zero;
corps.angularVelocity = Vector3.zero;
corps.position = pos0;
corps.rotation = rot0;
corps.isKinematic = kin0;
bille.gameObject.SetActive(actif0);

Physics.simulationMode = mode0;

rapport.AppendLine("nettoyage : bille remise (active " + actif0 + "), simulation restaurée.");

return rapport.ToString();
