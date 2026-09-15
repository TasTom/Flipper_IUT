// Le banc du drain, en UN SEUL appel, et il observe TOUT au lieu de le déduire.
// EN MODE PLAY. Déplace la bille en jeu, ne détruit rien.
//
// ── Pourquoi réécrire le banc ─────────────────────────────────────────────────────────
// Les relevés précédents ont écarté, un par un, tout ce qu'on soupçonnait :
//   • `graceDelay` = 0        → pas d'`Invoke`, donc pas d'attente de frame ;
//   • collider présent, `enabled`, `isTrigger` → la zone est bien armée ;
//   • `GetIgnoreLayerCollision(8, 0)` = faux → les couches se parlent ;
//   • `detectCollisions` vrai, `excludeLayers` 0 → la bille n'ignore rien ;
//   • et les rappels de physique sont remis À L'INTÉRIEUR de `Physics.Simulate`, en
//     dernière étape — vérifié : ils ne sont pas différés à la frame suivante.
//
// Reste donc à mesurer, pas à déduire. Ce banc relève à chaque pas, dans le même appel :
//   1. si la sphère de la bille recouvre le collider de la zone ;
//   2. le compte de billes ;
//   3. combien de fois `BallManager.BallDrained` a été émis — en s'y abonnant soi-même.
// Le point 3 est le neuf : il distingue « le déclencheur n'a pas vu la bille » de « il l'a
// vue mais l'événement ne va nulle part ». C'est exactement ce qu'on ne pouvait pas savoir.
//
// ── L'échec qu'on cherche à ne pas reproduire ─────────────────────────────────────────
// Un banc qui lit l'état APRÈS sa boucle ne peut pas voir un décompte qui arrive pendant.
// Ici tout est relevé pas à pas, et l'écart entre deux pas est journalisé tel quel.

var sb = new System.Text.StringBuilder();

if (!Application.isPlaying) { return "Ce banc exige le mode Play."; }

Application.runInBackground = true;

var gm = GameManager.Instance;
var bm = BallManager.Instance;

if (gm == null) { return "GameManager.Instance est nul."; }
if (bm == null) { return "BallManager.Instance est nul."; }

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "PinballTable introuvable."; }

var rt = racine.transform;

// --- on compte nous-mêmes les émissions de l'événement ---------------------------------
int drains = 0;
System.Action<Rigidbody> espion = _ => drains++;

bm.BallDrained += espion;

{
    sb.AppendLine("=== en arrivant ===");
    sb.AppendLine("  billes " + gm.BallsRemaining + " | état " + gm.State + " | en jeu " + bm.LiveBallCount);
    sb.AppendLine();

    // --- s'assurer qu'une bille est en jeu ---------------------------------------------
    // `StartGame` est la seule voie normale ; s'il n'y a rien à relancer, on le dit.
    Rigidbody corps = null;

    foreach (var go in GameObject.FindGameObjectsWithTag("Ball"))
    {
        var rb = go.GetComponent<Rigidbody>();

        if (rb != null && !rb.isKinematic && go.activeInHierarchy) { corps = rb; break; }
    }

    if (corps == null)
    {
        sb.AppendLine("  aucune bille en jeu — on demande une partie neuve.");
        gm.StartGame();

        foreach (var go in GameObject.FindGameObjectsWithTag("Ball"))
        {
            var rb = go.GetComponent<Rigidbody>();

            if (rb != null && !rb.isKinematic && go.activeInHierarchy) { corps = rb; break; }
        }
    }

    if (corps == null)
    {
        sb.AppendLine();
        sb.AppendLine("  ⚠ TOUJOURS aucune bille en jeu après `StartGame()`.");
        sb.AppendLine("     C'est un défaut du cycle de vie, pas du drain : à traiter avant.");
        return sb.ToString();
    }

    sb.AppendLine("  bille : '" + corps.name + "'  couche " + corps.gameObject.layer
        + "  tag " + corps.gameObject.tag);
    sb.AppendLine();

    // --- la zone ------------------------------------------------------------------------
    Transform dz = null;

    foreach (var t in racine.GetComponentsInChildren<Transform>(true))
    {
        if (t.name == "DrainZone") { dz = t; break; }
    }

    if (dz == null) { return "DrainZone introuvable."; }

    var col = dz.GetComponent<Collider>();

    if (col == null) { return "DrainZone n'a pas de collider."; }

    // --- le lâcher ----------------------------------------------------------------------
    Vector3 depart = new Vector3(0f, 0.55f, 0.50f);

    corps.position = rt.TransformPoint(depart);
    corps.rotation = Quaternion.identity;
    corps.linearVelocity = Vector3.zero;
    corps.angularVelocity = Vector3.zero;

    Physics.simulationMode = SimulationMode.Script;
    Physics.SyncTransforms();

    int billesAvant = gm.BallsRemaining;

    sb.AppendLine("=== lâcher depuis " + depart.ToString("F3") + " (repère table), pas de 4 ms ===");
    sb.AppendLine("  pas | position table                 | recouvre | billes | drains | en jeu");
    sb.AppendLine("  ----+--------------------------------+----------+--------+--------+-------");

    bool dejaDedans = false;
    int pasDedans = 0;
    int premierDedans = -1;
    int pas = 0;

    for (; pas < 1500; pas++)
    {
        Vector3 lp = rt.InverseTransformPoint(corps.position);
        var proches = Physics.OverlapSphere(corps.position, 0.225f, ~0, QueryTriggerInteraction.Collide);

        bool zone = false;

        foreach (var c in proches)
        {
            if (c == col) { zone = true; }
        }

        if (zone && premierDedans < 0) { premierDedans = pas; }
        if (zone) { pasDedans++; }

        // On journalise au changement d'état et tous les 50 pas, jamais 1500 lignes.
        if (zone != dejaDedans || pas % 50 == 0)
        {
            sb.AppendLine("  " + pas.ToString("0000") + " | " + lp.ToString("F3").PadRight(30) + " | "
                + (zone ? "OUI" : "non").PadRight(8) + " | "
                + gm.BallsRemaining.ToString().PadRight(6) + " | "
                + drains.ToString().PadRight(6) + " | " + bm.LiveBallCount
                + (zone != dejaDedans ? "   ← la bille entre/sort du volume" : ""));
        }

        dejaDedans = zone;

        Physics.Simulate(0.004f);
    }

    Physics.simulationMode = SimulationMode.FixedUpdate;

    sb.AppendLine();
    sb.AppendLine("=== verdict ===");
    sb.AppendLine("  pas simulés            : " + pas);
    sb.AppendLine("  billes                 : " + billesAvant + " → " + gm.BallsRemaining);
    sb.AppendLine("  émissions BallDrained  : " + drains);
    sb.AppendLine("  état                   : " + gm.State);
    sb.AppendLine("  bille détruite ?       : " + (corps == null ? "OUI (référence perdue)" : "non"));
    sb.AppendLine();

    if (premierDedans < 0)
    {
        sb.AppendLine("  1. ✗ la bille n'est JAMAIS entrée dans le volume en " + pas + " pas.");
        sb.AppendLine("     dernier point relevé : " + rt.InverseTransformPoint(corps.position).ToString("F3"));
        sb.AppendLine("     → c'est la GÉOMÉTRIE. La boîte est à replacer sur le trajet réel.");
    }
    else
    {
        sb.AppendLine("  1. ✓ la bille est entrée dans le volume au pas " + premierDedans
            + ", et y est restée " + pasDedans + " pas.");

        if (drains > 0)
        {
            sb.AppendLine("  2. ✓ `BallDrained` a été émis " + drains + " fois → le déclencheur FONCTIONNE.");
        }
        else
        {
            sb.AppendLine("  2. ✗ AUCUNE émission de `BallDrained` alors que le recouvrement a duré "
                + pasDedans + " pas.");
            sb.AppendLine("     Le volume est traversé, mais `OnTriggerEnter` n'est pas remis au script.");
            sb.AppendLine("     → c'est la REMISE DU MESSAGE, pas la géométrie.");
        }

        if (gm.BallsRemaining < billesAvant) { sb.AppendLine("  3. ✓ le compte a bien diminué."); }
        else { sb.AppendLine("  3. ✗ le compte n'a pas diminué."); }
    }

    bm.BallDrained -= espion;

    return sb.ToString();
}
