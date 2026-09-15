// Le lanceur remplit-il son office ?
//
// Il a deux fonctions, et le test les éprouve dans cet ordre :
//
//   1. **Fermer le bas du couloir.** Avant lui, le sol du couloir s'arrêtait à `z 0,3187` et rien
//      ne le prolongeait : la bille partait du point d'apparition, roulait vers -Z et quittait la
//      table par le bas. Elle doit maintenant s'arrêter contre le bouchon.
//   2. **Lancer.** La bille doit finir sa course **dans la boîte de recherche** de `PushBalls`,
//      sinon un relâchement ne pousse rien. C'est mesuré ici avec les valeurs réellement portées
//      par le composant, pas avec celles du script de pose.
//
// La méthode privée `PushBalls` est appelée **par réflexion** plutôt que recopiée : une copie de
// sa requête ne prouverait que ma lecture du code, alors que ce qui doit être vérifié est le
// comportement du composant tel qu'il est. C'est la vraie qui décide si la bille part.
//
// Le test se nettoie : bille remise à sa position et ses vitesses d'origine, mode de simulation
// restauré. Rien n'est enregistré.

var rapport = new System.Text.StringBuilder();

// --- ce qu'il faut sous la main ------------------------------------------------------------------

var racine = GameObject.Find("PinballTable");
var gameplay = racine != null ? racine.transform.Find("Gameplay") : null;
var hote = gameplay != null ? gameplay.Find("Plunger") : null;
var spawn = GameObject.Find("BallSpawnPoint");

if (hote == null) { return "PinballTable/Gameplay/Plunger introuvable"; }
if (spawn == null) { return "BallSpawnPoint introuvable"; }

var lanceur = hote.GetComponent<Plunger>();

if (lanceur == null) { return "L'hôte 'Plunger' ne porte pas le composant Plunger"; }

// Les enfants du lanceur : c'est là que la pièce et son collider sont attendus.
var piece = hote.Find("Plunger_Rod");

rapport.AppendLine("=== lanceur ===");
rapport.AppendLine("hôte        : " + hote.name + "   monde " + hote.position.ToString("F4"));
rapport.AppendLine("forward     : " + hote.forward.ToString("F4"));
rapport.AppendLine("pièce       : " + (piece != null ? piece.name : "ABSENTE — l'hôte est nu"));

if (piece != null)
{
    var colliders = piece.GetComponents<Collider>();

    rapport.AppendLine("colliders de la pièce : " + colliders.Length);

    foreach (var c in colliders)
    {
        rapport.AppendLine("  " + c.GetType().Name + "   déclencheur " + c.isTrigger
                           + "   boîte monde " + c.bounds.ToString("F4"));
    }
}

// Les valeurs d'équilibrage réellement portées par le composant, lues par réflexion : ce sont
// elles qui décident, pas les constantes du script de pose.
float Champ(string nom)
{
    var f = typeof(Plunger).GetField(nom,
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    return f != null && f.GetValue(lanceur) is float v ? v : float.NaN;
}

float pullSpeed = Champ("pullSpeed");
float maxPull = Champ("maxPull");
float launchForce = Champ("launchForce");
float catchHalfWidth = Champ("catchHalfWidth");
float catchOffset = Champ("catchOffset");

rapport.AppendLine();
rapport.AppendLine("=== valeurs du composant (par réflexion) ===");
rapport.AppendLine("pullSpeed      " + pullSpeed);
rapport.AppendLine("maxPull        " + maxPull);
rapport.AppendLine("launchForce    " + launchForce);
rapport.AppendLine("catchHalfWidth " + catchHalfWidth);
rapport.AppendLine("catchOffset    " + catchOffset);

if (float.IsNaN(maxPull) || float.IsNaN(catchOffset))
{
    rapport.AppendLine("⚠ champs introuvables : le test ne peut pas reconstruire la zone de recherche");
    return rapport.ToString();
}

// --- la bille ------------------------------------------------------------------------------------

var bille = gameplay.Find("Ball");

if (bille == null)
{
    var parTag = GameObject.FindGameObjectWithTag("Ball");
    bille = parTag != null ? parTag.transform : null;
}

if (bille == null) { return rapport.ToString() + "\nAucune bille dans la scène."; }

var corps = bille.GetComponent<Rigidbody>();

if (corps == null) { return rapport.ToString() + "\nLa bille n'a pas de Rigidbody."; }

Vector3 pos0 = corps.position;
Quaternion rot0 = corps.rotation;
bool kin0 = corps.isKinematic;
bool actif0 = bille.gameObject.activeSelf;

bille.gameObject.SetActive(true);
corps.isKinematic = false;
corps.position = spawn.transform.position;
corps.rotation = spawn.transform.rotation;
corps.linearVelocity = Vector3.zero;
corps.angularVelocity = Vector3.zero;

var mode0 = Physics.simulationMode;
Physics.simulationMode = SimulationMode.Script;

float dt = 1f / 60f;

// La boîte de `PushBalls`, reconstruite à l'identique depuis les champs du composant. Sert
// uniquement à **dire** si la bille est dedans ; ce n'est pas elle qui pousse.
bool DansLaBoite(Vector3 centreBille)
{
    var avant = hote.position + hote.forward * catchOffset;
    var arriere = hote.position - hote.forward * maxPull;
    var centre = (avant + arriere) * 0.5f;
    var demi = new Vector3(catchHalfWidth, 0.5f, (avant - arriere).magnitude * 0.5f);

    var local = hote.InverseTransformPoint(centreBille) - hote.InverseTransformPoint(centre);

    // La boîte est orientée par l'hôte : on compare donc dans son repère.
    return Mathf.Abs(local.x) <= demi.x
        && Mathf.Abs(local.y) <= demi.y
        && Mathf.Abs(local.z) <= demi.z;
}

// --- 1. la bille tombe-t-elle et s'arrête-t-elle ? ----------------------------------------------

Physics.SyncTransforms();

rapport.AppendLine();
rapport.AppendLine("=== 1. chute depuis le point d'apparition, 12 s ===");
rapport.AppendLine("départ : " + corps.position.ToString("F4"));

Vector3 bas = corps.position;
bool sortie = false;
var trace = new System.Text.StringBuilder();

for (int i = 0; i < 720; i++)
{
    Physics.Simulate(dt);

    if (corps == null) { break; }

    var p = corps.position;
    bas = Vector3.Min(bas, p);

    if (i % 120 == 0)
    {
        trace.AppendLine("  t " + (i / 60f).ToString("F0").PadLeft(2) + " s   "
                         + p.ToString("F4") + "   vitesse " + corps.linearVelocity.magnitude.ToString("F3"));
    }

    if (p.y < -2f || p.z < -12f || p.z > 22f)
    {
        sortie = true;
        trace.AppendLine("  ÉCHAPPÉE à t " + (i / 60f).ToString("F2") + " s   " + p.ToString("F4"));
        break;
    }
}

rapport.AppendLine(trace.ToString());

Vector3 repos = corps.position;
float vitesseRepos = corps.linearVelocity.magnitude;

rapport.AppendLine("arrivée        : " + repos.ToString("F4"));
rapport.AppendLine("vitesse finale : " + vitesseRepos.ToString("F4") + " u/s");
rapport.AppendLine("y le plus bas  : " + bas.y.ToString("F4"));
rapport.AppendLine("sortie de table: " + (sortie ? "OUI ⚠" : "non"));
rapport.AppendLine("dans la boîte de PushBalls : " + (DansLaBoite(repos) ? "OUI ✓" : "NON ⚠"));

// Distance le long de l'axe, pour situer la bille par rapport aux deux bornes.
float avant = Vector3.Dot(repos - hote.position, hote.forward);

rapport.AppendLine("  avance sur l'axe " + avant.ToString("F4") + " u"
                  + "   (zone : -" + maxPull + " à +" + catchOffset + ")");

// --- 2. le lancement ---------------------------------------------------------------------------

rapport.AppendLine();
rapport.AppendLine("=== 2. PushBalls(1) par réflexion, puis 12 s ===");

var methode = typeof(Plunger).GetMethod("PushBalls",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

if (methode == null)
{
    rapport.AppendLine("⚠ méthode privée 'PushBalls' introuvable : le lancement n'a pas pu être éprouvé");
}
else
{
    // `Awake` ne tourne pas hors Play mode : `restWorldPosition`, que `PushBalls` prend pour
    // origine de sa boîte, vaut donc `(0,0,0)`. La boîte se construirait autour de l'origine de la
    // scène, à 4,67 u de la bille, et ne trouverait rien — non parce que le lanceur est mal posé,
    // mais parce qu'on l'interroge dans un état où le jeu ne l'interroge jamais. On lui fournit
    // donc ce qu'`Awake` aurait écrit. C'est la **seule** valeur que ce test injecte.
    var champRepos = typeof(Plunger).GetField("restWorldPosition",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    if (champRepos != null)
    {
        rapport.AppendLine("restWorldPosition avant injection : "
                           + ((Vector3)champRepos.GetValue(lanceur)).ToString("F4"));
        rapport.AppendLine("  → porté à la position de l'hôte " + hote.position.ToString("F4")
                           + " (ce qu'Awake écrit en Play mode)");

        champRepos.SetValue(lanceur, hote.position);
    }
    else
    {
        rapport.AppendLine("⚠ champ 'restWorldPosition' introuvable : la boîte restera à l'origine");
    }

    Physics.SyncTransforms();

    Vector3 avantLancement = corps.position;

    methode.Invoke(lanceur, new object[] { 1f });

    // `AddForce` n'écrit pas dans `linearVelocity` : la force est cumulée et appliquée au pas de
    // simulation suivant. Lire la vitesse juste après l'appel montrerait toujours zéro, quelle que
    // soit la force — c'est pourquoi la trace ci-dessous commence après un pas.

    float haut = corps.position.z;
    var trace2 = new System.Text.StringBuilder();

    for (int i = 0; i < 720; i++)
    {
        Physics.Simulate(dt);

        if (corps == null) { break; }

        haut = Mathf.Max(haut, corps.position.z);

        if (i % 120 == 0)
        {
            trace2.AppendLine("  t " + (i / 60f).ToString("F0").PadLeft(2) + " s   "
                              + corps.position.ToString("F4")
                              + "   vitesse " + corps.linearVelocity.magnitude.ToString("F3"));
        }
    }

    rapport.AppendLine(trace2.ToString());
    rapport.AppendLine("z le plus haut atteint : " + haut.ToString("F4")
                      + "   (départ " + avantLancement.z.ToString("F4")
                      + " → gain " + (haut - avantLancement.z).ToString("F3") + " u)");
    rapport.AppendLine("arrivée : " + corps.position.ToString("F4"));
}

// --- nettoyage ---------------------------------------------------------------------------------

corps.linearVelocity = Vector3.zero;
corps.angularVelocity = Vector3.zero;
corps.position = pos0;
corps.rotation = rot0;
corps.isKinematic = kin0;
bille.gameObject.SetActive(actif0);

Physics.simulationMode = mode0;

rapport.AppendLine();
rapport.AppendLine("nettoyage : bille remise à " + pos0.ToString("F4")
                  + " (active " + actif0 + "), mode de simulation restauré à " + Physics.simulationMode + ".");

return rapport.ToString();
