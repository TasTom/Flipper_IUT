// Pourquoi le déclencheur n'a-t-il rien attrapé ? EN MODE PLAY, LECTURE SEULE.
//
// `test_drain_play.cs` a lâché la bille au-dessus de l'ouverture : 6 s simulées, 1500 pas,
// et AUCUN décompte — la bille est descendue à y local −135. Le banc a donc fait son travail
// (il a révélé le défaut), mais il ne dit pas POURQUOI.
//
// Trois causes possibles, qu'on distingue ici au lieu de les supposer :
//   A. la bille n'entre jamais dans le volume — la boîte est mal placée ou mal orientée ;
//   B. elle y entre, mais le message `OnTriggerEnter` n'est pas remis au script ;
//   C. la couche de la bille et celle de la zone ne se parlent pas (matrice de collision).
//
// La sonde relève d'abord l'état (collider, couches, matrice), puis rejoue la chute en
// demandant À CHAQUE PAS si la sphère de la bille recouvre le collider de la zone. C'est ce
// qui sépare A des deux autres.

var sb = new System.Text.StringBuilder();

if (!Application.isPlaying) { return "Cette sonde exige le mode Play."; }

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "PinballTable introuvable."; }

var rt = racine.transform;

Transform dz = null;

foreach (var t in racine.GetComponentsInChildren<Transform>(true))
{
    if (t.name == "DrainZone") { dz = t; break; }
}

sb.AppendLine("=== le déclencheur ===");

if (dz == null) { return sb.AppendLine("  ABSENT de la scène.").ToString(); }

var col = dz.GetComponent<Collider>();

sb.AppendLine("  GameObject actif : " + dz.gameObject.activeInHierarchy);
sb.AppendLine("  couche           : " + dz.gameObject.layer + " (" + LayerMask.LayerToName(dz.gameObject.layer) + ")");

if (col == null)
{
    sb.AppendLine("  ⚠ AUCUN COLLIDER sur l'hôte — c'est la cause.");
    return sb.ToString();
}

sb.AppendLine("  collider         : " + col.GetType().Name + "   enabled " + col.enabled
    + "   isTrigger " + col.isTrigger);

Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
Vector3 mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);

for (int i = 0; i < 8; i++)
{
    Vector3 coin = col.bounds.center + new Vector3(
        (i & 1) == 0 ? -col.bounds.extents.x : col.bounds.extents.x,
        (i & 2) == 0 ? -col.bounds.extents.y : col.bounds.extents.y,
        (i & 4) == 0 ? -col.bounds.extents.z : col.bounds.extents.z);
    Vector3 l = rt.InverseTransformPoint(coin);
    mn = Vector3.Min(mn, l); mx = Vector3.Max(mx, l);
}

sb.AppendLine("  bornes repère table (englobante) : x [" + mn.x.ToString("F3") + " → " + mx.x.ToString("F3")
    + "]  y [" + mn.y.ToString("F3") + " → " + mx.y.ToString("F3")
    + "]  z [" + mn.z.ToString("F3") + " → " + mx.z.ToString("F3") + "]");

// --- la bille -------------------------------------------------------------------------
Rigidbody corps = null;

foreach (var go in GameObject.FindGameObjectsWithTag("Ball"))
{
    if (!go.activeInHierarchy) { continue; }

    var rb = go.GetComponent<Rigidbody>();

    if (rb != null && !rb.isKinematic) { corps = rb; break; }
}

if (corps == null) { return sb.AppendLine("  (aucune bille en jeu)").ToString(); }

sb.AppendLine();
sb.AppendLine("=== la bille ===");
sb.AppendLine("  couche " + corps.gameObject.layer + " (" + LayerMask.LayerToName(corps.gameObject.layer) + ")");
sb.AppendLine("  detectCollisions " + corps.detectCollisions);
sb.AppendLine("  excludeLayers    " + corps.excludeLayers.value);
sb.AppendLine("  includeLayers    " + corps.includeLayers.value);
sb.AppendLine();

// C : la matrice de collision
bool ignore = Physics.GetIgnoreLayerCollision(corps.gameObject.layer, dz.gameObject.layer);
sb.AppendLine("=== C. matrice de collision ===");
sb.AppendLine("  GetIgnoreLayerCollision(bille " + corps.gameObject.layer + ", zone " + dz.gameObject.layer
    + ") = " + ignore + (ignore ? "   ⚠ LES DEUX COUCHES NE SE PARLENT PAS" : "   ✓ elles se parlent"));
sb.AppendLine();

// --- A : la bille entre-t-elle dans le volume ? ---------------------------------------
Vector3 depart = new Vector3(0f, 0.55f, 0.50f);

corps.position = rt.TransformPoint(depart);
corps.rotation = Quaternion.identity;
corps.linearVelocity = Vector3.zero;
corps.angularVelocity = Vector3.zero;

Physics.simulationMode = SimulationMode.Script;
Physics.SyncTransforms();

int billesAvant = GameManager.Instance.BallsRemaining;

sb.AppendLine("=== A. la bille entre-t-elle dans le volume ? ===");
sb.AppendLine("  pas | position locale (x, y, z)          | recouvre la zone | billes");
sb.AppendLine("  ----+------------------------------------+------------------+-------");

bool dejaDedans = false;
bool jamais = true;
int premierRecouvrement = -1;
Vector3 posPremier = Vector3.zero;
int pas = 0;

for (; pas < 400; pas++)
{
    Vector3 lp = rt.InverseTransformPoint(corps.position);
    var touche = Physics.OverlapSphere(corps.position, 0.225f, ~0, QueryTriggerInteraction.Collide);

    bool dedans = false;

    foreach (var t in touche) { if (t == col) { dedans = true; } }

    if (dedans && !dejaDedans && premierRecouvrement < 0)
    {
        premierRecouvrement = pas;
        posPremier = lp;
    }

    if (dedans) { jamais = false; }

    if (dedans != dejaDedans || pas % 25 == 0)
    {
        sb.AppendLine("  " + pas.ToString("000") + " | " + lp.ToString("F3").PadRight(34) + " | "
            + (dedans ? "OUI" : "non").PadRight(16) + " | " + GameManager.Instance.BallsRemaining
            + (dedans != dejaDedans ? "   ← changement" : ""));
    }

    dejaDedans = dedans;

    Physics.Simulate(0.004f);
}

Physics.simulationMode = SimulationMode.FixedUpdate;

sb.AppendLine();
sb.AppendLine("=== verdict ===");
sb.AppendLine("  billes " + billesAvant + " → " + GameManager.Instance.BallsRemaining);

if (jamais)
{
    sb.AppendLine("  A. ✗ la bille n'est JAMAIS entrée dans le volume — la boîte est mal placée.");
    sb.AppendLine("     Dernière position locale : " + rt.InverseTransformPoint(corps.position).ToString("F3"));
}
else
{
    sb.AppendLine("  A. ✓ la bille est entrée dans le volume au pas " + premierRecouvrement
        + ", à " + posPremier.ToString("F3") + " (repère table)");

    if (GameManager.Instance.BallsRemaining < billesAvant)
    {
        sb.AppendLine("  ✓✓ le déclencheur a fonctionné ici — l'échec du banc vient d'ailleurs.");
    }
    else
    {
        sb.AppendLine("  B. ⚠ la bille est entrée DANS le volume sans que le compte change :");
        sb.AppendLine("     le recouvrement existe, mais `OnTriggerEnter` n'est pas remis au script.");
        sb.AppendLine("     C'est la même famille que les rappels de collision en mode éditeur.");
    }
}

return sb.ToString();
