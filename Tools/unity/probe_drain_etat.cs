// Où en est la partie, maintenant ? LECTURE SEULE — aucune écriture, aucune physique.
//
// ── L'hypothèse que ce relevé met à l'épreuve ─────────────────────────────────────────
// `test_drain_play.cs` simule dans une boucle SYNCHRONE : `Physics.Simulate` fait avancer la
// physique, mais les rappels (`OnTriggerEnter`) sont remis aux scripts par la phase de
// script d'Unity — donc à la frame suivante, pas dans la boucle. Le banc lisait le compte
// juste après sa boucle, dans le même appel : il ne pouvait pas voir un décompte simplement
// DIFFÉRÉ.
//
// Si c'est bien cela, alors entre deux appels au CLI — c'est-à-dire entre deux frames — le
// rappel a fini par partir, et le compte a bougé. Il suffit donc de relire l'état pour le
// savoir, sans rien relancer.
//
// Ce que le relevé distingue :
//   • billes < 3 → le déclencheur A FONCTIONNÉ, en différé : le silence du banc était un
//     artefact de la boucle synchrone, pas un défaut de la zone ;
//   • billes = 3 et bille très bas → le rappel n'est jamais parti : c'est bien la remise du
//     message qui est en cause, et non la géométrie (mesurée conforme) ni les couches.

var sb = new System.Text.StringBuilder();

var gm = GameManager.Instance;
var bm = BallManager.Instance;

sb.AppendLine("mode Play : " + Application.isPlaying);
sb.AppendLine();

sb.AppendLine("=== état de la partie ===");
sb.AppendLine("  GameManager  : " + (gm == null ? "⚠ nul" : "présent"));
sb.AppendLine("  BallManager  : " + (bm == null ? "⚠ nul" : "présent"));

if (gm != null)
{
    sb.AppendLine("  billes       : " + gm.BallsRemaining + " / 3");
    sb.AppendLine("  état         : " + gm.State);
}

if (bm != null)
{
    sb.AppendLine("  en jeu       : " + bm.LiveBallCount);
}

sb.AppendLine();

// --- les billes présentes dans la scène -------------------------------------------------
var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;

sb.AppendLine("=== billes de la scène ===");

int n = 0;

foreach (var go in GameObject.FindGameObjectsWithTag("Ball"))
{
    n++;

    var rb = go.GetComponent<Rigidbody>();

    sb.AppendLine("  " + n + ". '" + go.name + "'  actif " + go.activeInHierarchy
        + "   tag " + go.tag
        + (rb == null ? "   ⚠ sans Rigidbody"
            : "   cinématique " + rb.isKinematic
              + "   détecte les collisions " + rb.detectCollisions));

    string pos = rb != null ? rb.position.ToString("F4") : go.transform.position.ToString("F4");

    sb.AppendLine("      position monde : " + pos);

    if (rt != null)
    {
        sb.AppendLine("      position table : " + rt.InverseTransformPoint(go.transform.position).ToString("F4"));
    }
}

if (n == 0) { sb.AppendLine("  (aucune — la bille de scène a pu être détruite)"); }

sb.AppendLine();

// --- verdict -----------------------------------------------------------------------------
sb.AppendLine("=== verdict ===");

if (gm == null)
{
    sb.AppendLine("  GameManager absent : rien à conclure.");
    return sb.ToString();
}

if (gm.BallsRemaining < 3)
{
    sb.AppendLine("  ✓✓ LE DÉCOMPTE A EU LIEU — " + gm.BallsRemaining + " bille(s) restante(s) sur 3.");
    sb.AppendLine("     Le déclencheur fonctionne. Le silence de `test_drain_play.cs` venait de sa");
    sb.AppendLine("     boucle synchrone : les rappels de physique sont remis à la frame suivante,");
    sb.AppendLine("     et le banc relisait le compte avant qu'elle n'arrive.");
    sb.AppendLine("     → la zone de perte est VALIDÉE ; c'est le banc qu'il faut corriger.");
}
else
{
    sb.AppendLine("  ⚠ TOUJOURS 3 BILLES : aucun décompte n'est parti, même en différé.");
    sb.AppendLine("     La géométrie est pourtant conforme (le segment passe dans le volume), le");
    sb.AppendLine("     collider est un déclencheur actif, les couches se parlent, `graceDelay` vaut 0.");
    sb.AppendLine("     Reste la remise du message elle-même sous `Physics.Simulate`.");
}

return sb.ToString();
