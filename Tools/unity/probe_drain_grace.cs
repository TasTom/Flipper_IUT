// Le déclencheur existe-t-il, et la bille le traverse-t-elle ? LECTURE SEULE, aucin pas de physique.
//
// ── Ce que le banc a montré, et ce qu'il ne dit pas ───────────────────────────────────
// `test_drain_play.cs` a lâché la bille depuis (0 ; 0,55 ; 0,50) en repère table : 1500 pas,
// 6 s simulées, aucun décompte — la bille est descendue à y local −135,75.
//
// ── Le champ que personne n'avait lu ──────────────────────────────────────────────────
// `DrainZone.OnTriggerEnter` ne compte PAS tout de suite : il regarde d'abord `graceDelay`.
// Si ce délai est > 0, il arme `Invoke(nameof(CountPendingBallLost), graceDelay)` — et un
// `Invoke` a besoin que des FRAMES s'écoulent. Or le banc simule dans une boucle synchrone :
// `Physics.Simulate` fait avancer la physique, pas le temps de jeu. Zéro frame, donc zéro
// `Invoke` : le compte ne peut pas bouger, quelle que soit la géométrie.
//
// C'est une cause D, et elle se lit dans les champs sérialisés — pas besoin de rejouer quoi
// que ce soit pour la connaître.
//
// ── La géométrie, calculée et non simulée ─────────────────────────────────────────────
// Pour ne pas confondre les causes, on échantillonne le SEGMENT entre le point de lâcher et
// la dernière position mesurée, et on demande à `Physics.OverlapSphere` — une requête pure,
// qui ne modifie rien — si la bille y recouvre le collider. Aucun pas de physique, aucun
// déplacement, aucun changement de mode : la sonde ne peut pas perturber l'état.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "PinballTable introuvable."; }

var rt = racine.transform;

sb.AppendLine("mode Play : " + Application.isPlaying);
sb.AppendLine("runInBackground : " + Application.runInBackground);
sb.AppendLine();

// --- 1. l'hôte et ses champs -----------------------------------------------------------
Transform dz = null;

foreach (var t in racine.GetComponentsInChildren<Transform>(true))
{
    if (t.name == "DrainZone") { dz = t; break; }
}

sb.AppendLine("=== hôte DrainZone ===");

if (dz == null) { return sb.AppendLine("  ABSENT de la scène.").ToString(); }

sb.AppendLine("  actif dans la hiérarchie : " + dz.gameObject.activeInHierarchy);
sb.AppendLine("  couche                   : " + dz.gameObject.layer
    + " (" + LayerMask.LayerToName(dz.gameObject.layer) + ")");
sb.AppendLine("  tag                      : " + dz.tag);
sb.AppendLine();

var sc = dz.GetComponent("DrainZone");

sb.AppendLine("=== champs sérialisés de DrainZone ===");

if (sc == null) { sb.AppendLine("  ⚠ script ABSENT de l'hôte."); }
else
{
    const System.Reflection.BindingFlags TOUT = System.Reflection.BindingFlags.Public
        | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    object grace = null;

    foreach (var f in sc.GetType().GetFields(TOUT))
    {
        object v;

        try { v = f.GetValue(sc); } catch { v = "(illisible)"; }

        if (f.Name == "graceDelay") { grace = v; }

        sb.AppendLine("  " + f.Name.PadRight(16) + " = " + (v == null ? "null" : v.ToString()));
    }

    sb.AppendLine();

    if (grace is float g && g > 0f)
    {
        sb.AppendLine("  ⚠⚠ `graceDelay` = " + g + " s : le compte passe par `Invoke`.");
        sb.AppendLine("     Une boucle synchrone de `Physics.Simulate` n'avance AUCUNE frame, donc");
        sb.AppendLine("     l'`Invoke` ne part jamais pendant le banc. Cause D — et elle suffit à");
        sb.AppendLine("     expliquer le silence, sans que la géométrie soit en cause.");
    }
    else
    {
        sb.AppendLine("  ✓ `graceDelay` = " + grace + " → le compte est immédiat, pas d'`Invoke`.");
        sb.AppendLine("     Le silence du banc ne vient donc PAS du délai : il faut chercher la");
        sb.AppendLine("     géométrie ou la remise du message.");
    }
}

sb.AppendLine();

// --- 2. le collider ---------------------------------------------------------------------
var col = dz.GetComponent<Collider>();

sb.AppendLine("=== le collider de la zone ===");

if (col == null)
{
    sb.AppendLine("  ⚠ AUCUN collider sur l'hôte — c'est la cause, sans aller plus loin.");
    return sb.ToString();
}

sb.AppendLine("  type      : " + col.GetType().Name);
sb.AppendLine("  enabled   : " + col.enabled);
sb.AppendLine("  isTrigger : " + col.isTrigger + (col.isTrigger ? "   ✓" : "   ⚠ IL EN FAUT UN"));

var bc = col as BoxCollider;

if (bc != null)
{
    sb.AppendLine("  center    : " + bc.center.ToString("F4") + "   (repère de l'hôte)");
    sb.AppendLine("  size      : " + bc.size.ToString("F4") + "   (repère de l'hôte)");
}

sb.AppendLine("  bounds monde : centre " + col.bounds.center.ToString("F3")
    + "   taille " + col.bounds.size.ToString("F3"));

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

sb.AppendLine("  relu repère table : x [" + mn.x.ToString("F3") + " → " + mx.x.ToString("F3")
    + "]  y [" + mn.y.ToString("F3") + " → " + mx.y.ToString("F3")
    + "]  z [" + mn.z.ToString("F3") + " → " + mx.z.ToString("F3") + "]");
sb.AppendLine();

// --- 3. les couches ----------------------------------------------------------------------
sb.AppendLine("=== C. matrice de collision ===");

var bille = rt.Find("Gameplay/Ball");

int coucheBille = bille != null ? bille.gameObject.layer : -1;

if (bille == null)
{
    sb.AppendLine("  (bille de scène introuvable sous Gameplay/Ball)");
}
else
{
    sb.AppendLine("  couche de la bille : " + coucheBille
        + " (" + LayerMask.LayerToName(coucheBille) + ")");
    sb.AppendLine("  GetIgnoreLayerCollision(bille " + coucheBille + ", zone "
        + dz.gameObject.layer + ") = "
        + Physics.GetIgnoreLayerCollision(coucheBille, dz.gameObject.layer)
        + (Physics.GetIgnoreLayerCollision(coucheBille, dz.gameObject.layer)
            ? "   ⚠ LES DEUX COUCHES NE SE PARLENT PAS" : "   ✓ elles se parlent"));

    var rbb = bille.GetComponent<Rigidbody>();

    if (rbb != null)
    {
        sb.AppendLine("  détecte les collisions : " + rbb.detectCollisions);
        sb.AppendLine("  excludeLayers          : " + rbb.excludeLayers.value);
        sb.AppendLine("  includeLayers          : " + rbb.includeLayers.value);
    }
}

sb.AppendLine();

// --- 4. la géométrie : le segment du lâcher recouvre-t-il le volume ? --------------------
// Requêtes pures (`OverlapSphere`) : rien n'est déplacé, rien n'est simulé.
Vector3 depart = new Vector3(0f, 0.55f, 0.50f);

// Dernière position relevée par `test_drain_play.cs` après 1500 pas.
Vector3 arrivee = new Vector3(0f, -135.7533f, -20.7211f);

sb.AppendLine("=== A. le segment lâcher → chute recouvre-t-il le volume ? ===");
sb.AppendLine("  de " + depart.ToString("F3") + "  à " + arrivee.ToString("F3") + "  (repère table)");
sb.AppendLine("  pas | centre (repère table)            | distance au collider | recouvre");
sb.AppendLine("  ----+----------------------------------+----------------------+---------");

bool touche = false;
int premier = -1;

const int NB = 3000;

for (int i = 0; i <= NB; i++)
{
    Vector3 lp = Vector3.Lerp(depart, arrivee, (float)i / NB);
    Vector3 monde = rt.TransformPoint(lp);

    var proches = Physics.OverlapSphere(monde, 0.225f, ~0, QueryTriggerInteraction.Collide);

    bool dedans = false;

    foreach (var c in proches) { if (c == col) { dedans = true; break; } }

    if (dedans && !touche) { touche = true; premier = i; }

    // On ne journalise que le voisinage du premier recouvrement : 3000 lignes ne se lisent pas.
    if (dedans && (premier == i || i % 200 == 0))
    {
        Vector3 proche = col.ClosestPoint(monde);

        sb.AppendLine("  " + i.ToString("0000") + " | " + lp.ToString("F3").PadRight(32) + " | "
            + Vector3.Distance(monde, proche).ToString("F4").PadRight(20) + " | OUI");
    }
}

sb.AppendLine();

if (!touche)
{
    sb.AppendLine("  A. ✗ AUCUN recouvrement sur tout le segment — la boîte est hors du trajet.");
    sb.AppendLine("     C'est un défaut de PLACEMENT, pas de remise de message.");
}
else
{
    sb.AppendLine("  A. ✓ recouvrement atteint au pas " + premier + " / " + NB
        + " — la bille traverse bien le volume.");
    sb.AppendLine("     La géométrie n'est donc PAS en cause : le silence vient de la remise");
        sb.AppendLine("     du message (`OnTriggerEnter`) ou du délai `graceDelay` ci-dessus.");
}

return sb.ToString();
