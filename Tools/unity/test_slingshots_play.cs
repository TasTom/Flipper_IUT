// Banc des slingshots EN MODE PLAY. À lancer éditeur déjà en Play.
//
// Pourquoi en Play et pas en éditeur : `probe_slingshot_declenchement.cs` a montré que
// `Time.time` vaut bien 116 s (donc la garde de cooldown n'est pas en cause), que la bille
// porte le tag `Ball`, que le script est activé et que le collider est bien sur le MÊME
// objet — et pourtant `lastKickTime` reste à -Infini après un choc pourtant réel (la bille
// rebondit à 0,345 u de la face, soit exactement l'épaisseur de bande 0,12 + le rayon 0,225).
// Autrement dit : Unity ne remet pas `OnCollisionEnter` en éditeur. C'est une limite du
// banc, pas un défaut du jeu — mais elle se lève en Play, et c'est en Play que le jeu tourne.
//
// `Physics.simulationMode = Script` garde la physique déterministe et sous notre contrôle,
// sans dépendre du rythme des frames.

var sb = new System.Text.StringBuilder();

const float U = 16.6666667f;
const float RAYON = 0.225f;

if (!Application.isPlaying) { return "Ce banc exige le mode Play (editor_play d'abord)."; }

var racine = GameObject.Find("PinballTable");
var rt = racine.transform;
var gameplay = rt.Find("Gameplay");
var bille = gameplay != null ? gameplay.Find("Ball") : null;

if (bille == null) { return "La bille de la scène est introuvable (PinballTable/Gameplay/Ball)."; }

var corps = bille.GetComponent<Rigidbody>();

sb.AppendLine("bille de scène : active=" + bille.gameObject.activeSelf + "  cinématique=" + corps.isKinematic
    + "  tag '" + bille.tag + "'");

// --- un hôte et son script ----------------------------------------------------------------
var hoteD = gameplay.Find("Slingshot_Right");
var hoteG = gameplay.Find("Slingshot_Left");
var slD = hoteD != null ? hoteD.GetComponent("Slingshot") : null;
var slG = hoteG != null ? hoteG.GetComponent("Slingshot") : null;

if (slD == null) { return "Le script Slingshot est absent de Slingshot_Right."; }

var type = slD.GetType();

System.Reflection.FieldInfo Champ(string nom)
{
    return type.GetField(nom, System.Reflection.BindingFlags.Public
        | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
}

float DernierCoup(object composant)
{
    var c = Champ("lastKickTime");
    return c != null ? System.Convert.ToSingle(c.GetValue(composant)) : float.NaN;
}

sb.AppendLine("Slingshot_Right : kickForce " + Champ("kickForce").GetValue(slD)
    + "  upwardBias " + Champ("upwardBias").GetValue(slD)
    + "  cooldown " + Champ("cooldown").GetValue(slD)
    + "  lastKickTime " + DernierCoup(slD));

var rendus = Champ("flashRenderers").GetValue(slD) as System.Array;
sb.AppendLine("  rendus de flash : " + (rendus != null ? rendus.Length : -1)
    + (rendus == null || rendus.Length == 0 ? "   (rien à illuminer pour l'instant)" : ""));

// --- les deux faces actives ----------------------------------------------------------------
const float TOP_X = 0.1925f * U;
const float TOP_Z = 0.330f * U;
const float BOT_X = 0.088f * U;
const float BOT_Z = 0.085f * U;

for (int k = 0; k < 2; k++)
{
    int s = k == 0 ? 1 : -1;
    string nom = k == 0 ? "Slingshot_Right" : "Slingshot_Left";
    object sl = k == 0 ? (object)slD : slG;

    sb.AppendLine();

    if (sl == null) { sb.AppendLine("=== " + nom + " : script absent ==="); continue; }

    Vector3 a = new Vector3(s * TOP_X, 0f, TOP_Z);
    Vector3 b = new Vector3(s * BOT_X, 0f, BOT_Z);
    float dx = b.x - a.x, dz = b.z - a.z;
    float len = Mathf.Sqrt(dx * dx + dz * dz);
    Vector3 nExt = new Vector3(s * (-dz / len), 0f, s * (dx / len));
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

    float avant = DernierCoup(sl);
    float vMax = 0f;
    Vector3 lFinale = Vector3.zero;
    int pasDuCoup = -1;

    for (int i = 0; i < 60; i++)
    {
        Physics.Simulate(0.004f);

        float maintenant = DernierCoup(sl);
        if (pasDuCoup < 0 && maintenant != avant) { pasDuCoup = i; }

        if (corps.linearVelocity.magnitude > vMax) { vMax = corps.linearVelocity.magnitude; }
        lFinale = rt.InverseTransformDirection(corps.linearVelocity);
    }

    float apres = DernierCoup(sl);
    Vector3 lInt = rt.InverseTransformDirection(intMonde);

    sb.AppendLine("=== " + nom + " ===");
    sb.AppendLine("  lastKickTime : " + avant + "  →  " + apres
        + (apres > avant ? "   ✓ LE SCRIPT EST ENTRÉ" : "   ✗ jamais entré"));
    sb.AppendLine("  pas du coup  : " + pasDuCoup);
    sb.AppendLine("  vitesse finale (repère table) : (" + lFinale.x.ToString("F3") + ", "
        + lFinale.y.ToString("F3") + ", " + lFinale.z.ToString("F3") + ")   |v| " + lFinale.magnitude.ToString("F3"));
    sb.AppendLine("  vitesse maximale atteinte    : " + vMax.ToString("F3") + "   (approche à 4, départ)");
    sb.AppendLine("  projection sur l'intérieur de l'aire de jeu : " + Vector3.Dot(lFinale, lInt).ToString("F3")
        + (Vector3.Dot(lFinale, lInt) > 0f ? "   ✓ CHASSÉE vers le jeu" : "   ✗ PLAQUÉE vers le mur"));
    sb.AppendLine("  position finale : " + rt.InverseTransformPoint(bille.position).ToString("F3"));
}

Physics.simulationMode = SimulationMode.FixedUpdate;

sb.AppendLine();
sb.AppendLine("(rien à remettre en état : la sortie du mode Play annule tout)");

return sb.ToString();
