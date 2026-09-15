// Ce que fait VRAIMENT la bille quand on tient le lanceur — physique pas à pas, hors Play mode.
//
// Trois hypothèses à départager, toutes trois expliqueraient « la collision est des fois bizarre » :
//
//   A. **Téléportation.** `Plunger.Launch` remet `transform.localPosition` à sa position de repos
//      d'un coup : le collider du bouchon saute de 0,8 u à travers la bille, et PhysX résout
//      l'enfoncement par un désenfoncement positionnel — dans un sens qui dépend de quel côté de
//      la boîte tombe le centre de la bille.
//   B. **Le trou sous le bouchon.** Le sol du couloir s'arrête à la cote de la face du bouchon
//      (mesuré : présent à z 0,20, absent à z 0,10 en repère de l'hôte, face avant à 0,1783). Le
//      bouchon *est* le fond du couloir : dès qu'il recule, la bille roule dans le vide.
//   C. **La bille suit le bouchon.** Elle recule de 0,427 u en 1 s sur la pente de 7°, donc
//      au-delà du bord du sol comme dans la course de retour du bouchon.
//
// ⚠️ Piège de mesure, corrigé ici : le repère de l'HÔTE BOUGE (c'est lui qu'on recule). Relever la
// bille « en repère de l'hôte » pendant la charge ne dit donc rien de son déplacement — on croit
// la voir avancer de 0,8 u alors qu'elle n'a pas bougé. Tout est donc exprimé dans le repère
// **du bouchon au repos**, fixe par construction :
//
//     avance  = (p − origine) · avant      + = vers le haut du couloir
//     hauteur = (p − origine) · haut
//     latéral = (p − origine) · droite
//
// Le test rejoue la séquence réelle (charge à la vitesse du composant, maintien, relâchement) et
// compare trois relâchements : A le téléportement actuel, B une course de retour progressive,
// C progressive + un plancher temporaire sous la course. L'impulsion est celle du composant,
// appelée par réflexion.
//
// Rien n'est enregistré. Le test rend à la scène son état exact (bille, hôte, mode de simulation)
// et détruit le plancher temporaire.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "'PinballTable' introuvable dans la scène."; }

var rt = racine.transform;
var gameplay = rt.Find("Gameplay");
var hote = gameplay != null ? gameplay.Find("Plunger") : null;
var bille = gameplay != null ? gameplay.Find("Ball") : null;

if (hote == null || bille == null) { return "'Plunger' ou 'Ball' introuvable sous Gameplay."; }

var rb = bille.GetComponent<Rigidbody>();
var colBille = bille.GetComponent<Collider>();

if (rb == null || colBille == null) { return "la bille n'a pas Rigidbody + Collider."; }

// --- le composant et ses constantes --------------------------------------------------------------

System.Type typePlunger = null;

foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
{
    typePlunger = a.GetType("Plunger");
    if (typePlunger != null) { break; }
}

if (typePlunger == null) { return "le type 'Plunger' est introuvable (compilation ?)."; }

var compPlunger = hote.GetComponent(typePlunger);

if (compPlunger == null) { return "'Plunger' ne porte pas le composant Plunger."; }

var drapeaux = System.Reflection.BindingFlags.Instance
             | System.Reflection.BindingFlags.NonPublic
             | System.Reflection.BindingFlags.Public;

var methodePush = typePlunger.GetMethod("PushBalls", drapeaux);
var champReposMonde = typePlunger.GetField("restWorldPosition", drapeaux);

float maxPull = 0.8f, pullSpeed = 3f;

var champMaxPull = typePlunger.GetField("maxPull", drapeaux);
var champPullSpeed = typePlunger.GetField("pullSpeed", drapeaux);

if (champMaxPull != null) { maxPull = (float)champMaxPull.GetValue(compPlunger); }
if (champPullSpeed != null) { pullSpeed = (float)champPullSpeed.GetValue(compPlunger); }

if (methodePush == null) { return "PushBalls introuvable sur Plunger."; }

const float dt = 0.02f;
const float faceBouchon = 0.1783f;

// --- le repère fixe : le bouchon AU REPOS --------------------------------------------------------

var reposHote = hote.localPosition;
var origine = hote.parent.TransformPoint(reposHote);
var avant = hote.forward;
var haut = hote.up;
var droite = hote.right;

float Rayon() { return colBille.bounds.extents.x; }

float Avance(Vector3 monde) { return Vector3.Dot(monde - origine, avant); }
float Hauteur(Vector3 monde) { return Vector3.Dot(monde - origine, haut); }
float Lateral(Vector3 monde) { return Vector3.Dot(monde - origine, droite); }

float ySol = Hauteur(colBille.bounds.center) - colBille.bounds.extents.x;

sb.AppendLine("=== repère fixe : le bouchon au repos ===");
sb.AppendLine("  origine monde   " + origine.ToString("F4"));
sb.AppendLine("  avant           " + avant.ToString("F4") + "   (vers le haut du couloir)");
sb.AppendLine("  face du bouchon à l'avance " + faceBouchon.ToString("F4")
              + "   rayon de bille " + colBille.bounds.extents.x.ToString("F4"));
sb.AppendLine("  sol du couloir à l'hauteur " + ySol.ToString("F4")
              + "   (bille posée si son centre est à " + (ySol + colBille.bounds.extents.x).ToString("F4") + ")");
sb.AppendLine("  bille au repos : avance " + Avance(colBille.bounds.center).ToString("F4")
              + "   hauteur " + Hauteur(colBille.bounds.center).ToString("F4")
              + "   latéral " + Lateral(colBille.bounds.center).ToString("F4"));
sb.AppendLine("  maxPull " + maxPull.ToString("F3") + "   pullSpeed " + pullSpeed.ToString("F3"));
sb.AppendLine();

// --- état à rendre en sortie ---------------------------------------------------------------------

var posInitiale = rb.position;
var rotInitiale = rb.rotation;
var vitInitiale = rb.linearVelocity;
var vitAngInitiale = rb.angularVelocity;
var modeInitial = Physics.simulationMode;

GameObject plancher = null;

void PoserPlancher()
{
    plancher = new GameObject("~PlancherTemporaire");

    plancher.transform.rotation = hote.rotation;
    plancher.transform.position = origine
                                + droite * (-0.0005f)
                                + haut * (ySol - 0.025f)
                                + avant * (-0.34f);

    var boite = plancher.AddComponent<BoxCollider>();

    boite.size = new Vector3(0.60f, 0.05f, 1.32f);   // couvre l'avance −1,00 → +0,32

    Physics.SyncTransforms();
}

// --- un scénario ---------------------------------------------------------------------------------

string Scenario(string nom, float maintien, bool retourProg, bool avecPlancher, bool trace)
{
    if (avecPlancher) { PoserPlancher(); }

    var journal = new System.Text.StringBuilder();

    hote.localPosition = reposHote;
    Physics.SyncTransforms();

    // Bille posée contre la face du bouchon.
    rb.position = origine + droite * Lateral(colBille.bounds.center)
                         + haut * (ySol + Rayon())
                         + avant * (faceBouchon + Rayon());
    rb.rotation = Quaternion.identity;
    rb.linearVelocity = Vector3.zero;
    rb.angularVelocity = Vector3.zero;
    rb.WakeUp();

    Physics.SyncTransforms();
    Physics.Simulate(dt);

    float depart = Avance(rb.position);

    // Charge.
    float pull = 0f;

    while (pull < maxPull)
    {
        pull = Mathf.Min(pull + pullSpeed * dt, maxPull);
        hote.localPosition = reposHote + Vector3.back * pull;
        Physics.Simulate(dt);
    }

    float apresCharge = Avance(rb.position);

    // Maintien.
    float tenu = 0f;

    while (tenu < maintien)
    {
        hote.localPosition = reposHote + Vector3.back * pull;
        Physics.Simulate(dt);
        tenu += dt;
    }

    float apresMaintien = Avance(rb.position);

    // Relâchement. `restWorldPosition` vaut la pose de repos, comme le lui écrit Awake en jeu —
    // et non la pose reculée, sinon la boîte de capture rate la bille.
    if (champReposMonde != null) { champReposMonde.SetValue(compPlunger, origine); }

    methodePush.Invoke(compPlunger, new object[] { 1f });

    if (!retourProg)
    {
        hote.localPosition = reposHote;
        Physics.Simulate(dt);
    }
    else
    {
        while (pull > 0f)
        {
            pull = Mathf.Max(0f, pull - 5f * dt);
            hote.localPosition = reposHote + Vector3.back * pull;
            Physics.Simulate(dt);
        }
    }

    // On laisse la bille vivre 1 s.
    float apres = 0f, chute = -1f, avanceMax = -99f, avanceFin = 0f;
    float pas = 0f;

    while (apres < 1f)
    {
        Physics.Simulate(dt);
        apres += dt;

        var p = rb.position;
        float av = Avance(p);
        float ha = Hauteur(p);

        avanceMax = Mathf.Max(avanceMax, av);
        avanceFin = av;

        if (ha < ySol - 0.30f && chute < 0f) { chute = apres; }

        if (trace && pas < 0.2f)
        {
            journal.AppendLine("      t+" + apres.ToString("F2")
                               + "   avance " + av.ToString("F4")
                               + "   hauteur " + ha.ToString("F4")
                               + "   vitesse " + rb.linearVelocity.magnitude.ToString("F3"));
            pas += dt;
        }
    }

    if (avecPlancher && plancher != null)
    {
        UnityEngine.Object.DestroyImmediate(plancher);
        plancher = null;
    }

    string verdict;

    if (chute >= 0f) { verdict = "CHUTE dans le logement à t+" + chute.ToString("F2") + " s ⚠"; }
    else if (avanceMax < 2f) { verdict = "PAS LANCÉE (reste dans le couloir) ⚠"; }
    else { verdict = "lancée"; }

    var ligne = "  " + nom.PadRight(32)
              + " avance : départ " + depart.ToString("F3").PadLeft(6)
              + " → charge " + apresCharge.ToString("F3").PadLeft(6)
              + " → maintien " + apresMaintien.ToString("F3").PadLeft(6)
              + "   max " + avanceMax.ToString("F3").PadLeft(7)
              + "   " + verdict;

    return ligne + (journal.Length > 0 ? "\n" + journal.ToString() : string.Empty);
}

sb.AppendLine("=== la bille dans le repère du bouchon au repos ===");
sb.AppendLine("  (avance 0,403 = posée contre la face ; le sol s'arrête vers 0,15)");
sb.AppendLine();

Physics.simulationMode = SimulationMode.Script;

try
{
    foreach (var maintien in new float[] { 0f, 0.3f, 0.6f, 0.9f, 1.2f })
    {
        sb.AppendLine("maintien " + maintien.ToString("F1") + " s :");
        sb.AppendLine(Scenario("A  téléportement (actuel)", maintien, false, false, false));
        sb.AppendLine(Scenario("B  retour progressif 5 u/s", maintien, true, false, false));
        sb.AppendLine(Scenario("C  retour progressif + plancher", maintien, true, true, false));
        sb.AppendLine();
    }

    sb.AppendLine("=== détail pas à pas : A (actuel), maintien 0,6 s ===");

    Physics.simulationMode = modeInitial;

    sb.AppendLine(Scenario("A  téléportement (actuel)", 0.6f, false, false, true));
}
finally
{
    Physics.simulationMode = modeInitial;

    if (plancher != null) { UnityEngine.Object.DestroyImmediate(plancher); }

    hote.localPosition = reposHote;

    rb.position = posInitiale;
    rb.rotation = rotInitiale;
    rb.linearVelocity = vitInitiale;
    rb.angularVelocity = vitAngInitiale;

    Physics.SyncTransforms();
}

sb.AppendLine();
sb.AppendLine("scène rendue à son état d'origine (bille, hôte, mode de simulation).");

return sb.ToString();
