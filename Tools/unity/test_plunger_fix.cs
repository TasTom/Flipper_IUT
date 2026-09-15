// Ce qui perd la bille au lanceur — et la correction complète, éprouvée en physique.
//
// `test_plunger_hold.cs` a établi le symptôme : dès que le lanceur est tenu plus de ~0,4 s, la bille
// recule **librement** sur la pente de 7° (le bouchon est à 0,8 u, il ne la touche plus), franchit
// le bord du sol du couloir — qui s'arrête à la face du bouchon, avance ≈ 0,18 — et tombe dans le
// logement. Un relâchement immédiat, lui, lance parfaitement (17,3 u). Le défaut n'est pas le
// relâchement : c'est le TROU.
//
// Ce fichier mesure trois choses, dans cet ordre :
//
//   1. **Le plancher de comblement** — ses coins réels et ce qu'un rayon vers le bas touche à chaque
//      cote, avec et sans lui. Il faut savoir si le trou est bien là où on le croit.
//   2. **La correction, croisée** : plancher × course de retour progressive × durée de maintien.
//      C'est là qu'apparaît le second défaut — corriger le trou *révèle* l'encoche du bouchon.
//   3. **Le bouchon élargi à la largeur du couloir**, qui ferme cette encoche.
//
// Les trois défauts mesurés, dans l'ordre où ils se manifestent :
//
//   a. **Le trou.** Le sol du couloir s'arrête à la face du bouchon. Dès qu'il recule de 0,8 u,
//      rien ne porte plus la bille, qui roule jusqu'à tomber dans le logement. C'est le symptôme
//      dominant, et le plus visible : tenir le lanceur perd la bille.
//   b. **L'encoche.** Le bouchon fait 0,5000 de large dans un couloir de 0,5667 : la bille déborde
//      de 0,034 u de chaque côté. Un sol sous elle, et le bouchon la rattrape et la pousse par
//      cette arête — contact de biais, éjection latérale à ~65 u/s, à travers la paroi.
//   c. **Le téléportement.** Le retour du bouchon au repos se fait d'un coup, à travers la bille.
//      PhysX résout un enfoncement profond par un désenfoncement positionnel, dans un sens qui
//      dépend de quel côté de la boîte tombe le centre de la bille.
//
// Repère de mesure : **le bouchon au repos**, fixe par construction.
//     avance  = (p − origine) · avant      + = vers le haut du couloir
//     hauteur = (p − origine) · haut
//     latéral = (p − origine) · droite
//
// Rien n'est enregistré. Le fichier rend à la scène son état exact (bille, hôte, bouchon, mode de
// simulation) et détruit le plancher temporaire.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "'PinballTable' introuvable dans la scène."; }

var rt = racine.transform;
var gameplay = rt.Find("Gameplay");
var hote = gameplay != null ? gameplay.Find("Plunger") : null;
var piece = hote != null ? hote.Find("Plunger_Rod") : null;
var bille = gameplay != null ? gameplay.Find("Ball") : null;

if (hote == null || bille == null) { return "'Plunger' ou 'Ball' introuvable sous Gameplay."; }

var rb = bille.GetComponent<Rigidbody>();
var colBille = bille.GetComponent<Collider>();

if (rb == null || colBille == null) { return "la bille n'a pas Rigidbody + Collider."; }

System.Type typePlunger = null;

foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
{
    typePlunger = a.GetType("Plunger");
    if (typePlunger != null) { break; }
}

if (typePlunger == null) { return "le type 'Plunger' est introuvable (compilation ?)."; }

var comp = hote.GetComponent(typePlunger);

if (comp == null) { return "'Plunger' ne porte pas le composant Plunger."; }

var flags = System.Reflection.BindingFlags.Instance
          | System.Reflection.BindingFlags.NonPublic
          | System.Reflection.BindingFlags.Public;

var methodePush = typePlunger.GetMethod("PushBalls", flags);
var champRepos = typePlunger.GetField("restWorldPosition", flags);
var champMaxPull = typePlunger.GetField("maxPull", flags);
var champPullSpeed = typePlunger.GetField("pullSpeed", flags);
var champReturnSpeed = typePlunger.GetField("returnSpeed", flags);
var champReturnStep = typePlunger.GetField("ReturnStep",
    flags | System.Reflection.BindingFlags.Static);

if (methodePush == null) { return "PushBalls introuvable sur Plunger."; }

float maxPull = champMaxPull != null ? (float)champMaxPull.GetValue(comp) : 0.8f;
float pullSpeed = champPullSpeed != null ? (float)champPullSpeed.GetValue(comp) : 3f;
float returnSpeed = champReturnSpeed != null ? (float)champReturnSpeed.GetValue(comp) : 5f;
float returnStep = champReturnStep != null
    ? System.Convert.ToSingle(champReturnStep.GetRawConstantValue())
    : 0.1f;

const float dt = 0.02f;
const float faceBouchon = 0.1783f;

// On rejoue *la formule du composant* — `ReturnSpeed` avec Time.deltaTime = dt — et non une vitesse
// recopiée à la main : le test ne peut donc pas diverger du code qu'il éprouve.
float retour = Mathf.Min(returnSpeed, returnStep / dt);

// --- repère fixe ----------------------------------------------------------------------------------

var reposHote = hote.localPosition;
var origine = hote.parent.TransformPoint(reposHote);
var avant = hote.forward;
var haut = hote.up;
var droite = hote.right;

float rayon = colBille.bounds.extents.x;

float Avance(Vector3 p) { return Vector3.Dot(p - origine, avant); }
float Hauteur(Vector3 p) { return Vector3.Dot(p - origine, haut); }
float Lateral(Vector3 p) { return Vector3.Dot(p - origine, droite); }
Vector3 Point(float a, float h, float l) { return origine + avant * a + haut * h + droite * l; }

float ySol = Hauteur(colBille.bounds.center) - rayon;
float latBille = Lateral(colBille.bounds.center);
float hautBille = Hauteur(colBille.bounds.center);

sb.AppendLine("=== repère fixe : le bouchon au repos ===");
sb.AppendLine("  origine " + origine.ToString("F4") + "   sol à l'hauteur " + ySol.ToString("F4")
              + "   rayon " + rayon.ToString("F4") + "   face du bouchon à l'avance " + faceBouchon.ToString("F4"));
sb.AppendLine("  bille au repos : avance " + Avance(colBille.bounds.center).ToString("F4")
              + "   hauteur " + hautBille.ToString("F4")
              + "   latéral " + latBille.ToString("F4"));
sb.AppendLine("  lanceur : maxPull " + maxPull.ToString("F3") + "   pullSpeed " + pullSpeed.ToString("F3"));
sb.AppendLine("  retour  : returnSpeed " + returnSpeed.ToString("F3")
              + "   ReturnStep " + returnStep.ToString("F3")
              + "   → vitesse rejouée " + retour.ToString("F3") + " u/s à dt " + dt.ToString("F2")
              + "   (soit " + (retour * dt).ToString("F4") + " u par pas)");
sb.AppendLine();

// --- état à rendre --------------------------------------------------------------------------------

var posInitiale = rb.position;
var rotInitiale = rb.rotation;
var vitInitiale = rb.linearVelocity;
var vitAngInitiale = rb.angularVelocity;
var modeInitial = Physics.simulationMode;

// --- le bouchon : quel composant de `size` porte la largeur ? -------------------------------------

var boiteBouchon = piece != null ? piece.GetComponent<BoxCollider>() : null;
var tailleOrigine = boiteBouchon != null ? boiteBouchon.size : Vector3.zero;

int axeLargeur = -1;
float largeurBouchon = 0f;

if (boiteBouchon != null)
{
    // On avance le centre d'un demi-composant le long de chaque axe local, et on regarde sur quel
    // axe de l'hôte le déplacement se projette : c'est ce qui nomme l'axe, sans rien supposer de
    // l'orientation de la pièce.
    var c0 = piece.TransformPoint(boiteBouchon.center);

    for (int i = 0; i < 3; i++)
    {
        var d = Vector3.zero;
        d[i] = boiteBouchon.size[i] * 0.5f;

        var c1 = piece.TransformPoint(boiteBouchon.center + d);

        float lat = Mathf.Abs(Lateral(c1) - Lateral(c0));
        float hau = Mathf.Abs(Hauteur(c1) - Hauteur(c0));
        float ava = Mathf.Abs(Avance(c1) - Avance(c0));

        if (lat > hau && lat > ava)
        {
            axeLargeur = i;
            largeurBouchon = lat * 2f;
            sb.AppendLine("  le composant size[" + i + "] porte la largeur du bouchon : "
                          + largeurBouchon.ToString("F4") + " u");
        }
    }

    if (axeLargeur < 0) { sb.AppendLine("  ⚠ aucun composant de size ne porte la largeur"); }
    else { sb.AppendLine("  size d'origine " + tailleOrigine.ToString("F4")); }
}

sb.AppendLine();

// --- la largeur du couloir, mesurée à la cote du bouchon ------------------------------------------

// ⚠️ Piège de mesure corrigé ici : la première version tirait les rayons à l'avance 0,13 — c'est-à-
// dire EN ARRIÈRE de la face du bouchon (0,1783). Or à cette cote le couloir n'a plus de parois :
// le bouchon *est* le fond du couloir, et la mesure rendait `float.MaxValue` (« rien touché »), ce
// qui rendait `largeurVoulue` non calculable et `Elargir(true)` sans effet. Les parois ne se
// mesurent qu'AU-DELÀ de la face du bouchon, là où elles existent.
//
// On échantillonne donc vers l'avant et on retient la largeur **la plus étroite** : c'est elle qui
// contraint, le bouchon devant passer partout où il coulisse.
float largeurCouloir = 0f;
float largeurG = 0f, largeurD = 0f;
float coteMesure = 0f;

{
    sb.AppendLine("  largeur du couloir, échantillonnée vers l'avant :");
    sb.AppendLine("     avance    gauche   droite    largeur");

    foreach (var a in new float[] { 0.22f, 0.30f, 0.40f, 0.55f, 0.80f, 1.10f })
    {
        var o = Point(a, hautBille, 0f);

        float mieuxG = float.MaxValue, mieuxD = float.MaxValue;

        foreach (var t in Physics.RaycastAll(o, -droite, 1.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (t.collider == colBille || t.collider == boiteBouchon) { continue; }
            if (t.distance < mieuxG) { mieuxG = t.distance; }
        }

        foreach (var t in Physics.RaycastAll(o, droite, 1.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (t.collider == colBille || t.collider == boiteBouchon) { continue; }
            if (t.distance < mieuxD) { mieuxD = t.distance; }
        }

        bool ok = mieuxG < float.MaxValue && mieuxD < float.MaxValue;

        sb.AppendLine("    " + a.ToString("F3").PadLeft(7) + "   "
                      + (ok ? mieuxG.ToString("F4").PadLeft(7) : "  (rien)").PadRight(9) + " "
                      + (ok ? mieuxD.ToString("F4").PadLeft(7) : "  (rien)").PadRight(9) + " "
                      + (ok ? (mieuxG + mieuxD).ToString("F4") : "⚠ pas de paroi"));

        if (ok && (largeurCouloir <= 0f || mieuxG + mieuxD < largeurCouloir))
        {
            largeurCouloir = mieuxG + mieuxD;
            largeurG = mieuxG;
            largeurD = mieuxD;
            coteMesure = a;
        }
    }

    if (largeurCouloir <= 0f)
    {
        sb.AppendLine("  ⚠ aucune paroi trouvée vers l'avant : largeur non mesurable");
    }
    else
    {
        sb.AppendLine("  → largeur retenue " + largeurCouloir.ToString("F4")
                      + " u (gauche " + largeurG.ToString("F4") + ", droite " + largeurD.ToString("F4")
                      + ") à l'avance " + coteMesure.ToString("F3"));
    }

    if (largeurBouchon > 0f && largeurCouloir > 0f)
    {
        float encocheG = largeurG - largeurBouchon * 0.5f;
        float encocheD = largeurD - largeurBouchon * 0.5f;

        sb.AppendLine("  encoche de chaque côté : gauche " + encocheG.ToString("F4")
                      + "   droite " + encocheD.ToString("F4")
                      + (Mathf.Max(encocheG, encocheD) > 0.005f ? "   ⚠ concave" : "   ✓ fermée"));

        if (rayon > largeurBouchon * 0.5f && latBille + rayon > largeurBouchon * 0.5f)
        {
            sb.AppendLine("  ⚠ la bille déborde du bouchon : bord droit de la bille à "
                          + (latBille + rayon).ToString("F4")
                          + ", bord droit du bouchon à " + (largeurBouchon * 0.5f).ToString("F4")
                          + " — un contact d'arête au lieu d'un contact de face");
        }
    }
}

// Largeur visée : les deux parois moins 0,005 u, pour que le bouchon ne les chevauche pas — deux
// murs qui se chevauchent créent un coin interne, cf. CLAUDE.md.
float largeurVoulue = largeurCouloir > 0f ? largeurCouloir - 0.005f : 0f;

sb.AppendLine("  largeur visée pour le bouchon : "
              + (largeurVoulue > 0f ? largeurVoulue.ToString("F4") : "non calculable")
              + " u   (actuelle " + largeurBouchon.ToString("F4") + " u)");
sb.AppendLine();

void Elargir(bool oui)
{
    if (boiteBouchon == null || axeLargeur < 0 || largeurVoulue <= 0f) { return; }

    var s = tailleOrigine;

    s[axeLargeur] = oui && largeurBouchon > 0f
        ? tailleOrigine[axeLargeur] * largeurVoulue / largeurBouchon
        : tailleOrigine[axeLargeur];

    boiteBouchon.size = s;
    Physics.SyncTransforms();
}

// --- le plancher de comblement --------------------------------------------------------------------

GameObject plancher = null;

void PoserPlancher()
{
    plancher = new GameObject("~PlancherTemporaire");
    plancher.transform.rotation = hote.rotation;
    plancher.transform.position = Point(-0.34f, ySol - 0.025f, -0.0005f);

    var b = plancher.AddComponent<BoxCollider>();
    b.size = new Vector3(0.60f, 0.05f, 1.32f);

    Physics.SyncTransforms();
}

void OterPlancher()
{
    if (plancher != null)
    {
        UnityEngine.Object.DestroyImmediate(plancher);
        plancher = null;
        Physics.SyncTransforms();
    }
}

// Premier obstacle vers le bas, à une avance donnée, au centre du couloir.
string Sonde(float a, out float hauteur)
{
    var o = Point(a, 0.10f, 0f);

    float mieux = float.MaxValue;
    string nom = "RIEN";
    hauteur = 0f;

    foreach (var t in Physics.RaycastAll(o, -haut, 1.5f, ~0, QueryTriggerInteraction.Ignore))
    {
        if (t.collider == colBille) { continue; }
        if (t.distance >= mieux) { continue; }

        mieux = t.distance;
        nom = t.collider.name;
        hauteur = 0.10f - t.distance;
    }

    return nom;
}

Physics.simulationMode = SimulationMode.Script;
Physics.SyncTransforms();

// --- 1. le plancher, mesuré -----------------------------------------------------------------------

sb.AppendLine("=== 1. le plancher de comblement, mesuré ===");

PoserPlancher();

var boite = plancher != null ? plancher.GetComponent<BoxCollider>() : null;

if (boite == null)
{
    sb.AppendLine("  le plancher n'a pas été créé ⚠");
}
else
{
    var mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
    var mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);

    for (int x = -1; x <= 1; x += 2)
    for (int y = -1; y <= 1; y += 2)
    for (int z = -1; z <= 1; z += 2)
    {
        var coin = boite.center + Vector3.Scale(boite.size * 0.5f, new Vector3(x, y, z));
        var w = plancher.transform.TransformPoint(coin);

        mn = Vector3.Min(mn, new Vector3(Lateral(w), Hauteur(w), Avance(w)));
        mx = Vector3.Max(mx, new Vector3(Lateral(w), Hauteur(w), Avance(w)));
    }

    sb.AppendLine("  coins en (latéral, hauteur, avance) :");
    sb.AppendLine("    min " + mn.ToString("F4") + "   max " + mx.ToString("F4"));
    sb.AppendLine("    → couvre l'avance " + mn.z.ToString("F3") + " → " + mx.z.ToString("F3")
                  + "   sommet à l'hauteur " + mx.y.ToString("F4")
                  + "  (sol mesuré " + ySol.ToString("F4") + ")");
}

sb.AppendLine();
sb.AppendLine("  rayon vers le bas, au centre du couloir, à chaque cote :");
sb.AppendLine("     avance     sol SANS plancher            sol AVEC plancher");

var cotes = new float[] { 0.45f, 0.35f, 0.25f, 0.20f, 0.16f, 0.10f, 0.00f, -0.20f, -0.50f, -0.80f, -1.05f };

float hSans, hAvec;

foreach (var a in cotes)
{
    var nomAvec = Sonde(a, out hAvec);

    OterPlancher();
    var nomSans = Sonde(a, out hSans);
    PoserPlancher();

    sb.AppendLine("    " + a.ToString("F3").PadLeft(7) + "     "
                  + (nomSans + " à " + hSans.ToString("F4")).PadRight(30) + "   "
                  + nomAvec + " à " + hAvec.ToString("F4"));
}

OterPlancher();

// --- 2 et 3. la correction, éprouvée --------------------------------------------------------------

string Scenario(bool avecPlancher, bool prog, bool elargi, float maintien, bool trace)
{
    if (avecPlancher) { PoserPlancher(); }
    Elargir(elargi);

    var journal = new System.Text.StringBuilder();

    hote.localPosition = reposHote;
    Physics.SyncTransforms();

    // Bille posée contre la face du bouchon.
    rb.position = Point(faceBouchon + rayon, ySol + rayon, latBille);
    rb.rotation = Quaternion.identity;
    rb.linearVelocity = Vector3.zero;
    rb.angularVelocity = Vector3.zero;
    rb.WakeUp();
    Physics.SyncTransforms();
    Physics.Simulate(dt);

    // Charge.
    float pull = 0f;

    while (pull < maxPull)
    {
        pull = Mathf.Min(pull + pullSpeed * dt, maxPull);
        hote.localPosition = reposHote + Vector3.back * pull;
        Physics.Simulate(dt);
    }

    // Maintien.
    float tenu = 0f;

    while (tenu < maintien)
    {
        hote.localPosition = reposHote + Vector3.back * pull;
        Physics.Simulate(dt);
        tenu += dt;
    }

    float finMaintien = Avance(rb.position);
    float hauteurMini = Hauteur(rb.position);
    float lateralMax = Mathf.Abs(Lateral(rb.position) - latBille);

    // Relâchement. `restWorldPosition` vaut la pose de repos, comme le lui écrit Awake en jeu.
    if (champRepos != null) { champRepos.SetValue(comp, origine); }

    methodePush.Invoke(comp, new object[] { 1f });

    if (!prog)
    {
        hote.localPosition = reposHote;
        Physics.Simulate(dt);
    }
    else
    {
        while (pull > 0f)
        {
            pull = Mathf.Max(0f, pull - retour * dt);
            hote.localPosition = reposHote + Vector3.back * pull;
            Physics.Simulate(dt);
        }
    }

    float apres = 0f, chute = -1f, avanceMax = -99f;
    float pas = 0f;
    float prochainPas = 0.20f;

    // Où et quand l'écart latéral apparaît. C'est la question ouverte : le tracé pas à pas montre un
    // lancement parfaitement droit, donc l'éjection est *ailleurs* — et la dire « ailleurs » ne
    // suffit pas, il faut la dater et la situer sur l'axe du couloir.
    float latMaxT = -1f, latMaxAvance = 0f, latMaxHauteur = 0f;
    float latCouloirMax = 0f;   // tant que la bille est encore dans le couloir (hauteur ≈ sol)

    while (apres < 3f)
    {
        Physics.Simulate(dt);
        apres += dt;

        float av = Avance(rb.position);
        float ha = Hauteur(rb.position);
        float ecart = Mathf.Abs(Lateral(rb.position) - latBille);

        avanceMax = Mathf.Max(avanceMax, av);
        hauteurMini = Mathf.Min(hauteurMini, ha);

        if (Mathf.Abs(ha - (ySol + rayon)) < 0.05f) { latCouloirMax = Mathf.Max(latCouloirMax, ecart); }

        if (ecart > lateralMax)
        {
            lateralMax = ecart;
            latMaxT = apres;
            latMaxAvance = av;
            latMaxHauteur = ha;
        }

        if (ha < ySol - 0.30f && chute < 0f) { chute = apres; }

        // Tracé fin pendant le lancement, puis grossier jusqu'au bout : c'est la SORTIE du couloir
        // (avance ≈ 17) qui porte l'écart latéral, pas le lancement — le tracé fin ne la voyait pas.
        if (trace)
        {
            bool fin = apres <= 0.22f && pas < 0.22f;

            if (fin || apres >= prochainPas)
            {
                journal.AppendLine("        t+" + apres.ToString("F2")
                                   + "   avance " + av.ToString("F3")
                                   + "   hauteur " + ha.ToString("F3")
                                   + "   latéral " + Lateral(rb.position).ToString("F3")
                                   + "   |v| " + rb.linearVelocity.magnitude.ToString("F3")
                                   + (av > 16.5f && av < 17.6f ? "   ← sortie du couloir" : string.Empty));

                if (fin) { pas += dt; }
                else { prochainPas = apres + 0.20f; }
            }
        }
    }

    if (avecPlancher) { OterPlancher(); }
    Elargir(false);

    string verdict;
    string ou = string.Empty;

    if (chute >= 0f) { verdict = "CHUTE à t+" + chute.ToString("F2") + " ⚠"; }
    else if (lateralMax > 0.10f) { verdict = "écart latéral de " + lateralMax.ToString("F3") + " ⚠"; }
    else if (avanceMax < 2f) { verdict = "PAS LANCÉE ⚠"; }
    else { verdict = "lancée"; }

    if (lateralMax > 0.10f)
    {
        // Dans le couloir ou hors du couloir ? La hauteur tranche : au sol = encore dans le couloir.
        bool couloir = Mathf.Abs(latMaxHauteur - (ySol + rayon)) < 0.10f;

        ou = "\n        ⤷ écart max à t+" + latMaxT.ToString("F2")
           + "   avance " + latMaxAvance.ToString("F3")
           + "   hauteur " + latMaxHauteur.ToString("F3")
           + "   → " + (couloir ? "DANS le couloir" : "HORS du couloir (aire de jeu)")
           + "   | écart max tant que la bille est au sol du couloir : "
           + latCouloirMax.ToString("F3");
    }

    return "    plancher " + (avecPlancher ? "oui" : "non")
         + "   bouchon " + (elargi ? "élargi " : "0,5000")
         + "   retour " + (prog ? "progressif" : "téléporté ")
         + "   maintien " + maintien.ToString("F1") + " s"
         + "   | fin maintien " + finMaintien.ToString("F3").PadLeft(6)
         + "   latéral max " + lateralMax.ToString("F3").PadLeft(6)
         + "   avance max " + avanceMax.ToString("F3").PadLeft(7)
         + "   " + verdict
         + ou
         + (journal.Length > 0 ? "\n" + journal.ToString() : string.Empty);
}

sb.AppendLine();
sb.AppendLine("=== 2. la correction, croisée ===");
sb.AppendLine("  (bille posée contre la face du bouchon, avance 0,403)");

try
{
    foreach (var maintien in new float[] { 0.3f, 0.6f, 1.0f })
    {
        sb.AppendLine();
        sb.AppendLine("  maintien " + maintien.ToString("F1") + " s :");
        sb.AppendLine(Scenario(false, false, false, maintien, false));
        sb.AppendLine(Scenario(true, false, false, maintien, false));
        sb.AppendLine(Scenario(false, true, false, maintien, false));
        sb.AppendLine(Scenario(true, true, false, maintien, false));
        sb.AppendLine(Scenario(true, true, true, maintien, false));
    }

    sb.AppendLine();
    sb.AppendLine("=== 3. détail pas à pas : plancher + bouchon élargi + retour progressif, maintien 0,6 s ===");

    sb.AppendLine(Scenario(true, true, true, 0.6f, true));
}
finally
{
    Physics.simulationMode = modeInitial;

    OterPlancher();
    Elargir(false);

    hote.localPosition = reposHote;

    rb.position = posInitiale;
    rb.rotation = rotInitiale;
    rb.linearVelocity = vitInitiale;
    rb.angularVelocity = vitAngInitiale;

    Physics.SyncTransforms();
}

sb.AppendLine();
sb.AppendLine("scène rendue à son état d'origine (bille, hôte, bouchon, mode de simulation).");

return sb.ToString();
