// La sortie basse est-elle VRAIMENT comptée ? EN MODE PLAY.
//
// ── Le piège que ce banc doit fermer ──────────────────────────────────────────────────
// `BallManager.CheckFellThroughWorld` compte déjà une bille perdue dès qu'elle passe sous
// `killHeight` (y = −5 en monde). Un déclencheur absent, trop petit ou mal placé serait donc
// MASQUÉ : la bille tomberait 5 u dans le vide, puis serait décomptée quand même, et le banc
// conclurait « ça marche ».
//
// Le banc relève donc la PROFONDEUR à laquelle le compte change. Au déclencheur, la bille
// est encore dans l'ouverture (y local ≈ 0) ; au repli, elle est 5 u plus bas. C'est cette
// mesure, et non le simple décrément, qui prouve que la détection fonctionne.
//
// ── Pourquoi c'est un banc à appels répétés ───────────────────────────────────────────
// Entre deux billes, `GameManager` attend `respawnDelay` (1,2 s) dans une coroutine, et une
// coroutine a besoin que l'éditeur avance des frames — ce qu'un script synchrone ne peut pas
// provoquer. Chaque appel traite donc UNE bille : on rappelle le script jusqu'à `GameOver`.
// S'il n'y a pas encore de bille en jeu, l'appel se contente de le dire et ne fait rien.

var sb = new System.Text.StringBuilder();

if (!Application.isPlaying) { return "Ce banc exige le mode Play (editor_play d'abord)."; }

var gm = GameManager.Instance;
var bm = BallManager.Instance;

if (gm == null) { return "GameManager.Instance est nul."; }
if (bm == null) { return "BallManager.Instance est nul."; }

var racine = GameObject.Find("PinballTable");
var rt = racine.transform;

// --- la bille en jeu (l'active : la bille de scène parquée est désactivée) --------------
Rigidbody corps = null;

foreach (var go in GameObject.FindGameObjectsWithTag("Ball"))
{
    if (!go.activeInHierarchy) { continue; }

    var rb = go.GetComponent<Rigidbody>();

    if (rb != null && !rb.isKinematic) { corps = rb; break; }
}

string Etat()
{
    return "billes " + gm.BallsRemaining + " | état " + gm.State
        + " | en jeu " + bm.LiveBallCount;
}

sb.AppendLine("=== état en arrivant ===");
sb.AppendLine("  " + Etat());
sb.AppendLine("  score : " + Score());
sb.AppendLine("  bille en jeu : " + (corps != null ? "oui" : "non"));

if (gm.State == GameManager.GameState.GameOver)
{
    sb.AppendLine();
    sb.AppendLine("  ✓ PARTIE TERMINÉE — plus de bille. C'est la fin attendue du cycle de 3 billes.");
    return sb.ToString();
}

if (corps == null)
{
    sb.AppendLine();
    sb.AppendLine("  En attente de la bille suivante (respawnDelay) — rappeler ce script.");
    return sb.ToString();
}

// --- on lâche la bille au-dessus de l'ouverture ----------------------------------------
// Repère de la table : le sol s'arrête à z = 0,330. La bille est posée un peu au-dessus et
// juste avant le bord : elle tombe, roule, et passe le bord — le trajet réel d'une bille
// perdue, plutôt qu'un largage à l'aplomb du déclencheur.
Vector3 depart = new Vector3(0f, 0.55f, 0.50f);

corps.position = rt.TransformPoint(depart);
corps.rotation = Quaternion.identity;
corps.linearVelocity = Vector3.zero;
corps.angularVelocity = Vector3.zero;

Physics.simulationMode = SimulationMode.Script;
Physics.SyncTransforms();

int billesAvant = gm.BallsRemaining;
var etatAvant = gm.State;
string scoreAvant = Score();

int pas = 0;
Vector3 dernier = corps.position;

for (; pas < 1500; pas++)
{
    dernier = corps.position;

    Physics.Simulate(0.004f);

    if (gm.BallsRemaining < billesAvant) { break; }
}

Physics.simulationMode = SimulationMode.FixedUpdate;

sb.AppendLine();
sb.AppendLine("=== lâcher dans l'ouverture ===");
sb.AppendLine("  départ (repère table) " + depart.ToString("F3"));
sb.AppendLine("  pas simulés : " + pas + "   (" + (pas * 0.004f).ToString("F3") + " s simulées)");

Vector3 dernierLocal = rt.InverseTransformPoint(dernier);

sb.AppendLine("  dernière position de la bille EN JEU (repère table) : " + dernierLocal.ToString("F4"));
sb.AppendLine("  dernière position de la bille EN JEU (monde)        : " + dernier.ToString("F4"));
sb.AppendLine();
sb.AppendLine("  " + Etat());
sb.AppendLine("  score : " + scoreAvant + " → " + Score());

if (gm.BallsRemaining >= billesAvant)
{
    sb.AppendLine();
    sb.AppendLine("  ✗ AUCUN DÉCOMPTE en " + pas + " pas : la bille n'a pas atteint le déclencheur.");
    return sb.ToString();
}

// --- le verdict : déclencheur ou repli ? -----------------------------------------------
sb.AppendLine();
sb.AppendLine("=== d'où vient le décompte ? ===");
sb.AppendLine("  seuil du repli `killHeight` : y MONDE = −5,000");
sb.AppendLine("  profondeur atteinte         : y MONDE = " + dernier.y.ToString("F3")
    + "   (y local " + dernierLocal.y.ToString("F3") + ")");

if (dernier.y > -1f)
{
    sb.AppendLine("  ✓ DÉCLENCHEUR — la bille est comptée dans l'ouverture (z local "
        + dernierLocal.z.ToString("F3") + "), pas après une chute de 5 u.");
    sb.AppendLine("    Un collider absent ou mal placé aurait laissé la bille tomber jusqu'à −5.");
}
else
{
    sb.AppendLine("  ⚠ REPLI `killHeight` — la bille est tombée " + (-dernier.y).ToString("F2")
        + " u avant d'être comptée.");
    sb.AppendLine("    Le déclencheur n'a PAS fonctionné : vérifier son collider (isTrigger, taille,");
    sb.AppendLine("    couche) et que `DrainZone` est bien sur le même GameObject que le collider.");
}

sb.AppendLine();
sb.AppendLine("  état : " + etatAvant + " → " + gm.State
    + (gm.State == GameManager.GameState.BallDrained || gm.State == GameManager.GameState.GameOver
        ? "  ✓" : "  ⚠ attendu BallDrained ou GameOver"));

return sb.ToString();

// --- helpers ---------------------------------------------------------------------------
// Le nom de la propriété de score n'est pas supposé : on lit celle qui existe.
string Score()
{
    var sm = ScoreManager.Instance;

    if (sm == null) { return "(pas de ScoreManager)"; }

    foreach (var p in sm.GetType().GetProperties())
    {
        if (p.PropertyType == typeof(int)
            && (p.Name == "Score" || p.Name == "CurrentScore" || p.Name == "Points"))
        {
            return p.Name + " = " + p.GetValue(sm);
        }
    }

    return "(propriété de score introuvable)";
}
