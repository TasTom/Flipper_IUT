// Éprouve les slingshots en physique : la bille est-elle CHASSÉE, ou plaquée au mur ?
// La scène est rendue dans l'état où elle était (bille ré-parquée), rien n'est créé ni détruit.
//
// `Slingshot.cs` calcule sa direction ainsi :
//     Vector3 direction = collision.contactCount > 0 ? -collision.GetContact(0).normal : transform.up;
// Tout repose donc sur le SIGNE de la normale de contact, dont la convention se raconte
// dans les deux sens. Un signe faux ne fait pas planter le script : il plaque la bille
// contre le corps, ce qui ressemble à « le slingshot ne fait rien ». C'est exactement ce
// qu'un essai en physique tranche, et rien d'autre.
//
// Le banc : on réveille la bille de la scène devant la face active, on l'envoie dedans à
// 4 u/s, et on relève sa vitesse. Attendue après le coup : ~9 u/s (kickForce), dirigée
// vers l'intérieur de l'aire de jeu. Si la vitesse gagne vers le mur, le signe est inversé.
//
// Pas de création ni de destruction : on emprunte la bille de la scène, puis on la remet
// au parc par la procédure de `parquer_bille.cs` (cinématique d'abord, puis les deux poses,
// puis SyncTransforms, puis relecture pour constater).

var sb = new System.Text.StringBuilder();

const float U = 16.6666667f;
const float RAYON = 0.225f;

var racine = GameObject.Find("PinballTable");
var rt = racine.transform;
var gameplay = rt.Find("Gameplay");

var bille = gameplay != null ? gameplay.Find("Ball") : null;

if (bille == null) { return "La bille de la scène est introuvable (PinballTable/Gameplay/Ball)."; }

var corps = bille.GetComponent<Rigidbody>();

if (corps == null) { return "La bille de la scène n'a pas de Rigidbody."; }
if (Application.isPlaying) { return "ON NE TOUCHE À RIEN EN PLAY — sortez du mode Play d'abord."; }

var spawn = GameObject.Find("BallSpawnPoint");
Vector3 poseParc = spawn != null ? spawn.transform.position : bille.position;
Quaternion rotParc = spawn != null ? spawn.transform.rotation : bille.rotation;
bool parcEtaitActif = bille.gameObject.activeSelf;

sb.AppendLine("avant essai : bille active=" + parcEtaitActif + " cinématique=" + corps.isKinematic);
sb.AppendLine("gravité du projet : " + Physics.gravity.ToString("F3"));

var ancienMode = Physics.simulationMode;

// faces actives — mêmes cotes que place_scene_slingshots.cs
const float TOP_X = 0.1925f * U;
const float TOP_Z = 0.330f * U;
const float BOT_X = 0.088f * U;
const float BOT_Z = 0.085f * U;

bille.gameObject.SetActive(true);
corps.isKinematic = false;

foreach (var cote in new[] { new { nom = "Slingshot_Right", s = 1 }, new { nom = "Slingshot_Left", s = -1 } })
{
    Vector3 a = new Vector3(cote.s * TOP_X, 0f, TOP_Z);
    Vector3 b = new Vector3(cote.s * BOT_X, 0f, BOT_Z);

    float dx = b.x - a.x, dz = b.z - a.z;
    float len = Mathf.Sqrt(dx * dx + dz * dz);
    Vector3 nExt = new Vector3(cote.s * (-dz / len), 0f, cote.s * (dx / len));
    Vector3 nInt = -nExt;

    Vector3 centreFace = (a + b) * 0.5f;
    Vector3 intMonde = (rt.rotation * nInt).normalized;
    Vector3 depart = rt.TransformPoint(new Vector3(centreFace.x, RAYON, centreFace.z)) + intMonde * 0.60f;
    Vector3 versFace = -intMonde * 4f;

    corps.linearVelocity = versFace;
    corps.angularVelocity = Vector3.zero;
    corps.position = depart;
    corps.rotation = Quaternion.identity;

    Physics.simulationMode = SimulationMode.Script;
    Physics.SyncTransforms();

    sb.AppendLine();
    sb.AppendLine("=== " + cote.nom + " ===");
    sb.AppendLine("  départ (" + depart.x.ToString("F3") + ", " + depart.y.ToString("F3") + ", " + depart.z.ToString("F3")
        + ")  vitesse (" + versFace.x.ToString("F3") + ", " + versFace.y.ToString("F3") + ", " + versFace.z.ToString("F3") + ")");
    sb.AppendLine("  sens intérieur (" + intMonde.x.ToString("F3") + ", " + intMonde.y.ToString("F3")
        + ", " + intMonde.z.ToString("F3") + ")");

    // Trace pas à pas. Un `AddForce(..., Impulse)` déclenché depuis `OnCollisionEnter`
    // n'entre pas forcément dans le pas de simulation en cours : mesurer « juste après le
    // rebond » peut manquer l'impulsion et ne montrer que le rebond du mur. On relève donc
    // le PROFIL, et on juge sur la vitesse établie quelques pas plus loin.
    float dt = 0.004f;
    Vector3 lInt = rt.InverseTransformDirection(intMonde);

    var trace = new System.Text.StringBuilder();
    int premierChoc = -1;
    float vMax = 0f;
    Vector3 vEtablie = Vector3.zero;
    int pasEtabli = -1;

    for (int i = 0; i < 60; i++)
    {
        Vector3 avant = rt.InverseTransformDirection(corps.linearVelocity);
        Physics.Simulate(dt);
        Vector3 apres = rt.InverseTransformDirection(corps.linearVelocity);

        if (premierChoc < 0 && Vector3.Distance(avant, apres) > 1f) { premierChoc = i; }

        // « établie » : trois pas loin du choc et hors du contact
        if (premierChoc >= 0 && i == premierChoc + 4) { vEtablie = apres; pasEtabli = i; }

        if (apres.magnitude > vMax) { vMax = apres.magnitude; }

        if (i <= 16)
        {
            trace.AppendLine("      pas " + i.ToString("00")
                + "  v (" + apres.x.ToString("F3") + ", " + apres.y.ToString("F3") + ", " + apres.z.ToString("F3")
                + ")  |v| " + apres.magnitude.ToString("F3")
                + "  proj. intérieur " + Vector3.Dot(apres, lInt).ToString("F3")
                + "  y local " + rt.InverseTransformPoint(bille.position).y.ToString("F3"));
        }
    }

    sb.AppendLine("  profil de vitesse (repère table, 60 pas de 4 ms) :");
    sb.Append(trace.ToString());

    if (premierChoc < 0)
    {
        sb.AppendLine("  ⚠ AUCUN CHOC — la bille n'a pas atteint la face.");
    }
    else
    {
        sb.AppendLine("  premier choc au pas " + premierChoc + " (t = " + (premierChoc * dt).ToString("F3") + " s)");
        sb.AppendLine("  vitesse établie (pas " + pasEtabli + ") : (" + vEtablie.x.ToString("F3") + ", "
            + vEtablie.y.ToString("F3") + ", " + vEtablie.z.ToString("F3") + ")  |v| " + vEtablie.magnitude.ToString("F3"));
        sb.AppendLine("      projection sur l'intérieur de l'aire de jeu : " + Vector3.Dot(vEtablie, lInt).ToString("F3")
            + (Vector3.Dot(vEtablie, lInt) > 0f ? "   ✓ CHASSÉE vers le jeu" : "   ✗ PLAQUÉE vers le mur — signe INVERSÉ"));
        sb.AppendLine("      vitesse maximale atteinte : " + vMax.ToString("F3") + "   (kickForce = 9 → ~9 attendu)");
    }
}

// --- remise au parc : la procédure de parquer_bille.cs -----------------------------------
Physics.simulationMode = ancienMode;

corps.isKinematic = true;
corps.linearVelocity = Vector3.zero;
corps.angularVelocity = Vector3.zero;
bille.SetPositionAndRotation(poseParc, rotParc);
corps.position = poseParc;
corps.rotation = rotParc;
Physics.SyncTransforms();
bille.gameObject.SetActive(false);

Vector3 relu = rt.InverseTransformPoint(bille.position);
Vector3 attendu = rt.InverseTransformPoint(poseParc);
float ecart = Vector3.Distance(relu, attendu);

sb.AppendLine();
sb.AppendLine("après essai : bille active=" + bille.gameObject.activeSelf
    + "  local (" + relu.x.ToString("F4") + ", " + relu.y.ToString("F4") + ", " + relu.z.ToString("F4") + ")");
sb.AppendLine(ecart < 1e-3f
    ? "✓ bille remise au parc (écart " + (ecart * 1000f / U).ToString("F4") + " mm)"
    : "⚠ bille NON remise au parc — écart " + (ecart * 1000f / U).ToString("F2") + " mm");

sb.AppendLine();
sb.AppendLine("scène modifiée (Ctrl+Z annule, Ctrl+S conserve).");

return sb.ToString();
