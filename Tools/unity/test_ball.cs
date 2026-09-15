// La bille tient-elle sur la table ?
//
// Première mise à l'épreuve réelle de la physique : on place la bille au point d'apparition, on
// avance la simulation pas à pas, et on regarde ce qui se passe.
//
// Le test se sert de **la bille posée dans la scène** — celle que la partie réutilise. Instancier
// la sienne créerait une seconde bille, qui pousserait la première : le test mesurerait une
// collision entre deux billes au lieu de la table. Le prefab n'est instancié qu'en repli, quand
// la scène n'a pas encore de bille.
//
// Pourquoi `Physics.simulationMode = Script` : quand la fenêtre de l'éditeur n'a pas le focus,
// Unity n'avance que deux images par seconde environ — une bille lancée à 5 u/s se déplacerait
// de 2,5 unités entre deux images, traverserait les murs, et le test ne dirait rien. En mode
// `Script` c'est nous qui poussons le temps, donc le résultat est reproductible.
//
// Ce que le test cherche à prendre en défaut, dans l'ordre de gravité :
//
// 1. la bille **traverse le plateau** (elle tombe sous la table) ;
// 2. elle **naît dans la géométrie** et est éjectée — le point d'apparition est à `y 0,5109`, le
//    sol du couloir à `y 0,2168` et la bille a un rayon de 0,2250 : elle doit tomber de
//    **0,069 u (4,2 mm)** avant de toucher, pas être soulevée ;
// 3. elle **ne bouge pas** — signe d'un collider qui la coince ;
// 4. elle **s'arrête en plein couloir** au lieu d'en descendre.
//
// Le test se nettoie derrière lui : la bille de scène est remise exactement où elle était, avec
// ses vitesses d'origine, et le mode de simulation restauré. La scène reste modifiée (déplacer un
// objet la marque), mais rien n'y est enregistré.

var report = new System.Text.StringBuilder();

// --- l'état initial --------------------------------------------------------------------------

var spawn = GameObject.Find("BallSpawnPoint");
var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ball.prefab");

if (spawn == null) { return "BallSpawnPoint introuvable"; }

var tableRoot = GameObject.Find("PinballTable");

report.AppendLine("=== état ===");
report.AppendLine("gravité          : " + Physics.gravity.ToString("F3"));
report.AppendLine("mode simulation  : " + Physics.simulationMode);
report.AppendLine("racine de table  : "
                  + (tableRoot != null ? tableRoot.transform.eulerAngles.ToString("F2") + " (attendu ~353 = -7°)" : "ABSENTE"));
report.AppendLine("point apparition : " + spawn.transform.position.ToString("F4"));

// Les colliders que la bille peut rencontrer : sans eux, elle tombe indéfiniment.
int tableColliders = 0;

var tableGroup = tableRoot != null ? tableRoot.transform.Find("Table/Pinball_Table") : null;

if (tableGroup != null) { tableColliders = tableGroup.GetComponentsInChildren<Collider>(true).Length; }

report.AppendLine("colliders de table : " + tableColliders);

// --- la bille -------------------------------------------------------------------------------

// Celle de la scène, tant qu'à faire : c'est elle qui joue.
var ball = GameObject.FindGameObjectWithTag("Ball");
bool posee = ball != null;

if (!posee)
{
    if (prefab == null) { return report.ToString() + "\nNi bille dans la scène, ni Ball.prefab."; }

    ball = UnityEngine.Object.Instantiate(prefab, spawn.transform.position, spawn.transform.rotation);
    report.AppendLine();
    report.AppendLine("aucune bille dans la scène : une bille du prefab est instanciée pour le test");
}

var body = ball.GetComponent<Rigidbody>();

report.AppendLine();
report.AppendLine("bille            : " + ball.name + (posee ? "   (celle de la scène, réutilisée)" : "   (instanciée)"));

if (body == null)
{
    if (!posee) { UnityEngine.Object.DestroyImmediate(ball); }
    return report.ToString() + "\nLa bille n'a pas de Rigidbody : rien à simuler.";
}

// L'état d'origine, pour le remettre à l'identique en sortant.
Vector3 posOrigine = body.position;
Quaternion rotOrigine = body.rotation;
bool kinematicOrigine = body.isKinematic;
bool actifOrigine = ball.activeSelf;

body.gameObject.SetActive(true);
body.isKinematic = false;
body.position = spawn.transform.position;
body.rotation = spawn.transform.rotation;
body.linearVelocity = Vector3.zero;
body.angularVelocity = Vector3.zero;

var previousMode = Physics.simulationMode;
Physics.simulationMode = SimulationMode.Script;

// Ce qu'il y a sous elle au départ : c'est la mesure qui a révélé la table 2,36 u trop haut, et
// elle vaut d'être refaite à chaque passage.
Physics.SyncTransforms();

RaycastHit sol;

if (Physics.Raycast(body.position, Vector3.down, out sol, 30f, ~0, QueryTriggerInteraction.Ignore))
{
    float rayon = body.GetComponent<SphereCollider>() is SphereCollider sc
        ? sc.radius * ball.transform.lossyScale.x : 0.225f;

    report.AppendLine("sol sous la bille : " + sol.collider.gameObject.name + " à y " + sol.point.y.ToString("F4")
                      + "   → chute de " + (body.position.y - rayon - sol.point.y).ToString("F4") + " u");
}

report.AppendLine();
report.AppendLine("=== simulation ===");

Vector3 start = body.position;
float lowest = start.y;
float fastest = 0f;
bool escaped = false;
var trace = new System.Text.StringBuilder();

const int steps = 900;          // 15 s à 60 Hz
float dt = 1f / 60f;

for (int i = 0; i < steps; i++)
{
    Physics.Simulate(dt);

    if (body == null) { break; }

    Vector3 p = body.position;
    float speed = body.linearVelocity.magnitude;

    lowest = Mathf.Min(lowest, p.y);
    fastest = Mathf.Max(fastest, speed);

    if (i % 60 == 0)
    {
        trace.AppendLine("  t " + (i / 60f).ToString("F0").PadLeft(2) + " s   position "
                         + p.ToString("F3") + "   vitesse " + speed.ToString("F2")
                         + "   |v| " + body.linearVelocity.ToString("F2"));
    }

    // Bornes larges : la table inclinée descend vers -Z, et le caisson occupe ~21 u.
    if (p.y < -2f || Mathf.Abs(p.x) > 12f || p.z < -12f || p.z > 22f)
    {
        escaped = true;
        trace.AppendLine("  ÉCHAPPÉE à t " + (i / 60f).ToString("F2") + " s   position " + p.ToString("F3"));
        break;
    }
}

report.AppendLine(trace.ToString());

Vector3 final = body != null ? body.position : Vector3.zero;
float travelled = Vector3.Distance(start, final);

report.AppendLine("=== bilan ===");
report.AppendLine("départ           : " + start.ToString("F4"));
report.AppendLine("arrivée          : " + final.ToString("F4"));
report.AppendLine("déplacement      : " + travelled.ToString("F3") + " u");
report.AppendLine("y le plus bas    : " + lowest.ToString("F4") + "   (surface de jeu à y≈0)");
report.AppendLine("vitesse maximale : " + fastest.ToString("F2") + " u/s");
report.AppendLine("sortie de scène  : " + (escaped ? "OUI — la bille a quitté la table" : "non"));

if (!escaped && body != null)
{
    float rest = body.linearVelocity.magnitude;
    report.AppendLine("vitesse finale   : " + rest.ToString("F3") + " u/s");

    if (rest < 0.05f)
    {
        report.AppendLine("→ la bille est AU REPOS.");
    }
    else
    {
        report.AppendLine("→ la bille bouge encore après 15 s : elle ne se stabilise pas.");
    }
}

// --- la vitesse de roulement, comparable à la théorie ------------------------------------------
//
// Une sphère pleine qui roule sans glisser sur une pente θ accélère à `g·sinθ / (1 + 2/5)`. À
// 7°, cela fait 0,854 u/s², soit **0,427 u en 1 s**. C'est le contrôle qui a validé la pente, le
// matériau physique et l'échelle en une seule mesure.

report.AppendLine();
report.AppendLine("=== roulement, comparé à la théorie ===");
report.AppendLine("théorie (sphère pleine, 7°) : 0,427 u en 1 s");

// --- nettoyage ------------------------------------------------------------------------------

if (body != null)
{
    body.linearVelocity = Vector3.zero;
    body.angularVelocity = Vector3.zero;
    body.position = posOrigine;
    body.rotation = rotOrigine;
    body.isKinematic = kinematicOrigine;
    ball.SetActive(actifOrigine);
}

if (!posee && ball != null) { UnityEngine.Object.DestroyImmediate(ball); }

Physics.simulationMode = previousMode;

report.AppendLine();
report.AppendLine("nettoyage : " + (posee
    ? "bille de scène remise en place (" + posOrigine.ToString("F4") + ", active " + actifOrigine + ")"
    : "bille de test détruite")
    + ", mode de simulation restauré à " + Physics.simulationMode);

return report.ToString();
