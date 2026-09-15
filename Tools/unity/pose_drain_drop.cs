// Lâche la bille au-dessus de l'ouverture, puis laisse la physique d'Unity faire le reste.
// EN MODE PLAY. Ne détruit rien, ne crée rien : déplace la bille en jeu.
//
// ── Pourquoi pas `Physics.Simulate` ───────────────────────────────────────────────────
// `test_drain_play.cs` pilotait la chute à la main (`Physics.simulationMode = Script` +
// `Physics.Simulate`) : 1500 pas, la bille est descendue à y local −135, et AUCUN décompte.
// Or la même technique avait fait réagir le slingshot. La différence n'est pas la bille,
// c'est QUI fait avancer le monde.
//
// On laisse donc Unity simuler pour de bon. Pour que l'éditeur continue de tourner quand la
// fenêtre n'a pas le focus, `Application.runInBackground` est activé — sans lui, la boucle de
// jeu s'arrête et la coroutine de relance de `GameManager` (1,2 s) ne se termine jamais.
//
// ── Ce que le contrôle suivant lira ───────────────────────────────────────────────────
// `BallManager.Park` ne DÉPLACE pas la bille : elle s'arrête là où elle était. Après le
// décompte, la position de la bille parquée est donc exactement la profondeur à laquelle
// elle a été comptée — c'est elle qui distingue le déclencheur du repli `killHeight`.

var sb = new System.Text.StringBuilder();

if (!Application.isPlaying) { return "Ce script exige le mode Play."; }

Application.runInBackground = true;
Physics.simulationMode = SimulationMode.FixedUpdate;

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "PinballTable introuvable."; }

var rt = racine.transform;
var gm = GameManager.Instance;
var bm = BallManager.Instance;

sb.AppendLine("runInBackground : " + Application.runInBackground);
sb.AppendLine("mode physique   : " + Physics.simulationMode);
sb.AppendLine("état            : billes " + gm.BallsRemaining + " | " + gm.State
    + " | en jeu " + bm.LiveBallCount);

Rigidbody corps = null;

foreach (var go in GameObject.FindGameObjectsWithTag("Ball"))
{
    if (!go.activeInHierarchy) { continue; }

    var rb = go.GetComponent<Rigidbody>();

    if (rb != null && !rb.isKinematic) { corps = rb; break; }
}

if (corps == null)
{
    sb.AppendLine();
    sb.AppendLine("Aucune bille en jeu — rien à lâcher. (Respawn en attente ?)");
    return sb.ToString();
}

Vector3 depart = new Vector3(0f, 0.55f, 0.50f);

corps.position = rt.TransformPoint(depart);
corps.rotation = Quaternion.identity;
corps.linearVelocity = Vector3.zero;
corps.angularVelocity = Vector3.zero;

Physics.SyncTransforms();

sb.AppendLine();
sb.AppendLine("bille lâchée en " + depart.ToString("F3") + " (repère table), immobile.");
sb.AppendLine("La physique d'Unity prend le relais — relire l'état dans un instant.");

return sb.ToString();
