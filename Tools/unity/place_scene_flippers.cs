// Habille les deux flippers dans `Neutral.unity`. ÉCRITURE (annulable, non enregistrée).
//
// v2 — corrige le sens du cadre (cf. §1) et la détection du talon (cf. §2).
// v3 — jour anti-frottement de 0,03 u côté centre (cf. §5).
// v4 — le talon se repère par l'axe +X du maillage, pas par le profil (cf. §2) : la v3
//      posait le bat gauche talon vers le centre (copie du droit au lieu du miroir).
// v5 — le ROULIS (cf. §4bis). §4 ne tourne qu'autour de l'axe du JOINT : aucun de ses quatre
//      quarts ne change la face qui regarde le ciel, si bien qu'un bat couché sur le flanc
//      passait inaperçu — relevé 2026-09-15, le caoutchouc rouge apparaissait en bande centrale
//      sur le DESSUS du bat au lieu de border le plastique sur les côtés.
//
// Motif (cf. `place_scene_plunger.cs`) : la pièce va sur `Flipper_Bat`, l'enfant qui porte le
// collider — jamais sur le pivot, que `NeutralScene.Strip` dépouillerait.
//
// 1. **Le cadre du pivot : `root × Euler(+90,0,0)`, Z local VERS LE BAS.**
//    Le `HingeJoint` tourne autour du Z local et `Flipper.cs` écrit `restAngle × AngleSign`
//    (−30 à gauche, +30 à droite avec `mirrorAngles`) puis `activeAngle × AngleSign` (+30 / −30).
//    Avec Z vers le bas, le +Y local vaut +Z table (vers le haut) : une rotation +30° envoie
//    +X vers +Y, donc la pointe vers le haut. Concrètement :
//      gauche, bat selon +X : repos −30° = pointe 30° vers le drain ✓, actif +30° = vers le haut ✓ ;
//      droite, bat selon −X : repos +30° = (−cos30, −sin30) = centre + drain ✓, actif −30° = haut ✓.
//    La v1 mettait −90° (Z vers le haut) : même plan de balayage, mais repos/actif inversés —
//    le flipper aurait cogné vers le drain au lieu de relever la bille. L'écart est MESURÉ
//    avant/après. Rien d'autre n'est touché : axe du joint, ressorts, limites (réécrites par
//    `Flipper.Update` de toute façon) et côté `Auto` restent tels quels.
//    Corollaire : la hauteur se lit à l'envers (au-dessus du plateau = z < 0), la semelle vise
//    max.z = −0,02. Et les quarts de tour d'orientation se prennent autour de `pivot.forward`
//    (= l'axe du joint), pas `pivot.up` — la v1 tournait autour d'un axe du plan, ses quarts
//    q>0 auraient mis le bat sur la tranche (sans effet : q=0 gagnait toujours).
//
// 2. **Le sens du bat : le talon (+X du maillage) côté pivot — règle mesurée, pas devinée.**
//    Le FBX arrive miroité en x et centré au milieu du bat. Le profil du nuage complet est
//    inutilisable : le manchon de caoutchouc (tube régulier, 0,424 u de large partout) noie la
//    conicité du plastique. La sonde chirale (`probe_flipper_chiral.cs`, 2026-09-14) a tranché
//    autrement : le centroïde du plastique est décalé de ~0,2 u vers +X du maillage des deux
//    côtés — le talon (l'embase plastique) est côté +X. Le quart retenu est donc celui dont
//    l'axe +X, exprimé en repère pivot, pointe côté pivot (−sens). Leçon : la v3 prenait le
//    premier quart « à plat » des deux côtés (profil indécidable → même orientation partout),
//    si bien que le bat GAUCHE est sorti talon vers le centre — une copie du droit, pas son
//    miroir (retour terrain 2026-09-14). Pour cette pièce quasi-symétrique, le 180° autour de
//    l'axe du joint EST le miroir effectif : pas d'échelle négative (normales cassées).
//    `SensTalon` ne sert plus qu'au rapport (profil loggé, décision par l'axe +X).
//
// 3. Talon sur l'axe, semelle à 0,02 u, `BoxCollider` exact sur la pièce, matériaux URP
//    (`FlipperOrange_Mat`, `BumperRed_Mat`), balayage (neutre/repos/actif) testé contre les
//    colliders existants AVANT d'ajouter le collider, PLUS mesure de garde aux coins du talon
//    (`ClosestPoint` vers `Body_L`/`Body_R`, en mm) : le talon frôle le corps du slingshot.
//    Si le cadre a changé depuis une pose précédente, le bat est repris (détruit + reposé),
//    sinon un bat déjà habillé est laissé intact (idempotence).

var sb = new System.Text.StringBuilder();

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null) { return "'PinballTable/Gameplay' introuvable — la scène n'est pas celle du menu 4."; }

var modele = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
    "Assets/Models/Parts/Flipper/Flipper_Williams_3.fbx");

if (modele == null) { return "Assets/Models/Parts/Flipper/Flipper_Williams_3.fbx introuvable."; }

var matPlastique = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
    "Assets/Materials/FlipperOrange_Mat.mat");
var matCaoutchouc = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
    "Assets/Materials/BumperRed_Mat.mat");

var corpsL = GameObject.Find("PinballTable/Table/Pinball_Table/Slingshots/Body_L");
var corpsR = GameObject.Find("PinballTable/Table/Pinball_Table/Slingshots/Body_R");

sb.AppendLine("=== scène '" + scene.name + "' ===");
sb.AppendLine("modèle : " + modele.name
              + "   plastique→" + (matPlastique != null ? matPlastique.name : "INTROUVABLE")
              + "   caoutchouc→" + (matCaoutchouc != null ? matCaoutchouc.name : "INTROUVABLE"));
sb.AppendLine("murs slingshot : Body_L " + (corpsL != null ? "✓" : "ABSENT")
              + "   Body_R " + (corpsR != null ? "✓" : "ABSENT"));

// --- helpers ----------------------------------------------------------------------------------------

// Boîte des maillages sous `objet`, exprimée dans le repère de `repere`, par les 8 coins.
Bounds BoiteLocale(Transform objet, Transform repere)
{
    Bounds boite = default(Bounds);
    bool premier = true;

    foreach (var filtre in objet.GetComponentsInChildren<MeshFilter>())
    {
        if (filtre.sharedMesh == null) { continue; }

        var m = filtre.sharedMesh.bounds;

        for (int i = 0; i < 8; i++)
        {
            var p = repere.InverseTransformPoint(filtre.transform.TransformPoint(new Vector3(
                (i & 1) == 0 ? m.min.x : m.max.x,
                (i & 2) == 0 ? m.min.y : m.max.y,
                (i & 4) == 0 ? m.min.z : m.max.z)));

            if (premier) { boite = new Bounds(p, Vector3.zero); premier = false; }
            else { boite.Encapsulate(p); }
        }
    }

    return boite;
}

// Une taille écrite dans les axes de `hote`, reportée dans ceux de `piece` (cf. place_scene_plunger).
Vector3 TailleDansAxesPiece(Transform piece, Transform hote, Vector3 enAxesHote)
{
    Vector3 droite = piece.InverseTransformDirection(hote.right);
    Vector3 haut = piece.InverseTransformDirection(hote.up);
    Vector3 avant = piece.InverseTransformDirection(hote.forward);

    return new Vector3(
        Mathf.Abs(droite.x) * enAxesHote.x + Mathf.Abs(haut.x) * enAxesHote.y + Mathf.Abs(avant.x) * enAxesHote.z,
        Mathf.Abs(droite.y) * enAxesHote.x + Mathf.Abs(haut.y) * enAxesHote.y + Mathf.Abs(avant.y) * enAxesHote.z,
        Mathf.Abs(droite.z) * enAxesHote.x + Mathf.Abs(haut.z) * enAxesHote.y + Mathf.Abs(avant.z) * enAxesHote.z);
}

// L'ancrage du `HingeJoint`, écrit depuis la COTE et non depuis `Transform.position` : sur un
// objet porteur d'un `Rigidbody`, `position` peut rendre une matrice en cache et l'ancrage
// recevrait l'ancienne pose (mesuré : 0,33 et 1,48 u d'écart).
void AncrerJoint(Transform pivot, Transform racine, float cote, System.Text.StringBuilder rapport)
{
    var joint = pivot.GetComponent<HingeJoint>();

    if (joint == null) { return; }

    UnityEditor.Undo.RecordObject(joint, "Reposer l'ancrage du HingeJoint");

    joint.autoConfigureConnectedAnchor = false;
    joint.connectedAnchor = racine.TransformPoint(new Vector3(cote, 0f, pivot.localPosition.z));

    float ecart = Mathf.Abs(racine.InverseTransformPoint(joint.connectedAnchor).x - cote);

    rapport.AppendLine("    ancrage du joint          " + ecart.ToString("F5")
                       + " u d'écart à la cote" + (ecart < 1e-4f ? "  ✓" : "  ⚠"));
}

// Le drain gap voulu, en unités Unity : 1,75 bille — le milieu de la fourchette des tables
// réelles (1,65 à 1,88 bille entre les pointes au repos). Le générateur visait 1,46 bille, ce qui
// est serré : une bille y passe plus difficilement qu'elle ne le devrait, et le retour terrain du
// 2026-09-15 demandait de l'élargir « un peu ».
const float DRAIN_GAP_U = 1.75f * 0.45f;   // 0,7875 u = 47,25 mm

// La boîte du collider, rétractée de `r` côté TALON (le côté qui mord le slingshot). Le côté
// pointe est laissé intact : c'est lui qui touche la bille.
Bounds RetracterTalon(Bounds boite, float sens, float r)
{
    if (r <= 0f) { return boite; }

    float tailleX = Mathf.Max(boite.size.x - r, 0.02f);
    float centreX = sens > 0f
        ? boite.min.x + r + tailleX * 0.5f
        : boite.max.x - r - tailleX * 0.5f;

    return new Bounds(new Vector3(centreX, boite.center.y, boite.center.z),
                      new Vector3(tailleX, boite.size.y, boite.size.z));
}

// La cote du pivot — déduite de la PIÈCE, pas écrite d'avance. Voir §9.
float CoterPivot(Transform porteur, Transform pivot, float sens, float reposDeg, out float rayon)
{
    var boite = BoiteLocale(porteur, pivot);

    // Distance de l'axe du joint à la pointe : le plus grand |x| de la boîte assise.
    rayon = sens > 0f ? boite.max.x : -boite.min.x;

    // La pointe, au repos, tombe à ±DRAIN_GAP/2 du centre.
    float pointe = DRAIN_GAP_U * 0.5f;

    return -sens * (pointe + rayon * Mathf.Cos(reposDeg * Mathf.Deg2Rad));
}

// Conicité sur le PLASTIQUE seul : le manchon (tube régulier) noie le profil du nuage complet.
// Rend +1 si le gros bout est côté x petits, −1 s'il est côté x grands, 0 si indécidable.
// `profil` reçoit les 9 largeurs pour le rapport.
int SensTalon(Transform piece, Transform pivot, out string profil)
{
    profil = "—";

    var filtre = piece.GetComponentInChildren<MeshFilter>();
    var rendu = piece.GetComponentInChildren<MeshRenderer>();

    if (filtre == null || filtre.sharedMesh == null) { return 0; }

    var maille = filtre.sharedMesh;

    // Le sous-maillage du plastique, repéré par son matériau (repli : sous-maillage 0).
    int sous = 0;

    if (rendu != null && maille.subMeshCount == rendu.sharedMaterials.Length)
    {
        for (int i = 0; i < rendu.sharedMaterials.Length; i++)
        {
            var m = rendu.sharedMaterials[i];

            if (m != null && !m.name.Contains("Rubber")
                && (m.name.Contains("Bat") || m.name.Contains("Plastic")))
            {
                sous = i;
                break;
            }
        }
    }

    int[] triangles;

    try { triangles = maille.GetTriangles(sous); }
    catch { return 0; }

    if (triangles == null || triangles.Length < 30) { return 0; }

    var sommets = maille.vertices;
    var vus = new System.Collections.Generic.HashSet<int>();

    foreach (var t in triangles) { vus.Add(t); }

    var nuage = new System.Collections.Generic.List<Vector3>(vus.Count);

    foreach (var i in vus)
    {
        nuage.Add(pivot.InverseTransformPoint(filtre.transform.TransformPoint(sommets[i])));
    }

    if (nuage.Count < 30) { return 0; }

    float minX = float.MaxValue, maxX = float.MinValue;

    foreach (var q in nuage)
    {
        minX = Mathf.Min(minX, q.x);
        maxX = Mathf.Max(maxX, q.x);
    }

    if (maxX - minX < 1e-6f) { return 0; }

    int tranches = 9;
    var largeurs = new float[tranches];
    var comptes = new int[tranches];

    for (int i = 0; i < tranches; i++)
    {
        float a = minX + (maxX - minX) * i / tranches;
        float b = minX + (maxX - minX) * (i + 1) / tranches;
        float lo = float.MaxValue, hi = float.MinValue;
        int n = 0;

        foreach (var q in nuage)
        {
            if (q.x >= a && (q.x < b || i == tranches - 1))
            {
                n++;
                lo = Mathf.Min(lo, q.y);
                hi = Mathf.Max(hi, q.y);
            }
        }

        comptes[i] = n;
        largeurs[i] = n >= 10 ? hi - lo : -1f;
    }

    var morceaux = new string[tranches];

    for (int i = 0; i < tranches; i++)
    {
        morceaux[i] = largeurs[i] < 0f ? "—" : largeurs[i].ToString("F3");
    }

    profil = "plastique sous-maillage " + sous + " (" + nuage.Count + " sommets) : "
             + string.Join(" ", morceaux);

    float bas = 0f, haut = 0f;
    int nBas = 0, nHaut = 0;

    for (int i = 0; i < 3; i++)
    {
        if (largeurs[i] >= 0f) { bas += largeurs[i]; nBas++; }
        if (largeurs[tranches - 1 - i] >= 0f) { haut += largeurs[tranches - 1 - i]; nHaut++; }
    }

    if (nBas == 0 || nHaut == 0) { return 0; }

    bas /= nBas;
    haut /= nHaut;

    if (bas > haut * 1.05f) { return 1; }
    if (haut > bas * 1.05f) { return -1; }

    return 0;
}

// --- la pose, un côté après l'autre -------------------------------------------------------------------

var cotes = new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" };
var cadreVoulu = rt.rotation * Quaternion.Euler(90f, 0f, 0f);   // Z local vers le bas (cf. §1)

foreach (var nom in cotes)
{
    bool aGauche = nom.Contains("Left");
    float sens = aGauche ? 1f : -1f;   // la pointe part vers le centre : +X à gauche, −X à droite
    float repos = aGauche ? -30f : 30f;    // restAngle × AngleSign (Flipper.cs, mirrorAngles)
    float actif = aGauche ? 30f : -30f;    // activeAngle × AngleSign

    sb.AppendLine();
    sb.AppendLine("=== " + nom + " (pointe vers " + (sens > 0f ? "+X" : "−X")
                  + "   repos " + repos + "°   actif " + actif + "°) ===");

    var pivot = gameplay.Find(nom);

    if (pivot == null) { sb.AppendLine("pivot absent — rien fait"); continue; }

    // 1. Le cadre du pivot : Z local vers le bas (l'axe du joint pointe dans la table).
    //
    // On écrit la pose de REPOS, pas le cadre nu : dans l'éditeur, le flipper doit se présenter
    // tel qu'il apparaît en jeu. Le cadre nu le laissait à l'horizontale, pointé vers le centre —
    // une pose qui n'existe à aucun moment d'une partie.
    //
    // Le contrôle ci-dessous reste juste : tourner autour du Z local ne change pas le Z local,
    // donc `pivot.rotation * Vector3.forward` vaut la même chose pour le cadre et pour la pose.
    var poseVoulue = cadreVoulu * Quaternion.AngleAxis(repos, Vector3.forward);

    var axeZ = pivot.rotation * Vector3.forward;
    float angleAxe = Vector3.Angle(axeZ, -rt.up);
    bool cadreChange = angleAxe >= 0.5f;

    sb.AppendLine("axe du joint vs −normale : " + angleAxe.ToString("F2") + "°"
                  + (!cadreChange ? "   ✓" : "   ⚠ à réaligner"));

    if (cadreChange)
    {
        UnityEditor.Undo.RecordObject(pivot, "Réaligner le pivot du flipper");

        pivot.rotation = poseVoulue;

        float apres = Vector3.Angle(pivot.rotation * Vector3.forward, -rt.up);

        sb.AppendLine("  → réaligné : écart résiduel " + apres.ToString("F3") + "°"
                      + "   (X local = " + (pivot.right.normalized - rt.right.normalized).magnitude.ToString("F4")
                      + " d'écart au X table)");
    }

    // 2. Le porteur : un `Flipper_Bat` vidé de ses restes procéduraux.
    var porteur = pivot.Find("Flipper_Bat");

    if (porteur == null)
    {
        var neuf = new GameObject("Flipper_Bat");
        UnityEditor.Undo.RegisterCreatedObjectUndo(neuf, "Créer Flipper_Bat");
        neuf.transform.SetParent(pivot, false);
        porteur = neuf.transform;
        sb.AppendLine("porteur 'Flipper_Bat' créé");
    }
    else if (porteur.localPosition != Vector3.zero || porteur.localRotation != Quaternion.identity)
    {
        sb.AppendLine("porteur : pos " + porteur.localPosition.ToString("F4")
                      + " rot " + porteur.localEulerAngles.ToString("F2") + " (restes procéduraux)");

        UnityEditor.Undo.RecordObject(porteur, "Recentrer Flipper_Bat");

        porteur.localPosition = Vector3.zero;
        porteur.localRotation = Quaternion.identity;

        sb.AppendLine("  → recentré sur l'axe");
    }

    // 3. Reprise ou idempotence : un bat assis dans un cadre périmé est reposé, sinon intact.
    var dejaLa = porteur.GetComponentInChildren<MeshFilter>() != null;

    if (dejaLa && cadreChange)
    {
        foreach (Transform enfant in porteur)
        {
            UnityEditor.Undo.DestroyObjectImmediate(enfant.gameObject);
        }

        sb.AppendLine("reprise : bat précédent retiré (assis dans l'ancien cadre) — on repose.");
        dejaLa = false;
    }

    if (dejaLa)
    {
        // Reprise si le talon est sans jour (pose antérieure) : talon à moins de 0,015 de l'axe.
        var boiteLa = BoiteLocale(porteur, pivot);
        float talonLa = sens > 0f ? boiteLa.min.x : boiteLa.max.x;

        // Reprise si le talon (+X maille) ne pointe pas côté pivot (poses v3 et avant :
        // le bat gauche est sorti copie du droit au lieu de son miroir).
        bool talonALEnvers = false;
        var batLa = porteur.Find("Bat_Mesh");

        if (batLa != null)
        {
            var xLa = pivot.InverseTransformDirection(batLa.TransformDirection(Vector3.right));
            talonALEnvers = Vector3.Dot(xLa, new Vector3(-sens, 0f, 0f)) <= 0.9f;
        }

        if (Mathf.Abs(talonLa) < 0.015f || talonALEnvers)
        {
            foreach (Transform enfant in porteur)
            {
                UnityEditor.Undo.DestroyObjectImmediate(enfant.gameObject);
            }

            sb.AppendLine(talonALEnvers
                ? "reprise : talon côté pointe (copie au lieu du miroir) — on repose."
                : "reprise : talon sans jour (à " + talonLa.ToString("F4") + ") — on repose avec jour.");
            dejaLa = false;
        }
    }

    if (dejaLa)
    {
        sb.AppendLine("RIEN FAIT : 'Flipper_Bat' porte déjà un maillage assis dans ce cadre.");

        // §9 vaut aussi ici : la cote se déduit de la pièce, donc une pose antérieure à cette
        // version doit être recotée même si le bat, lui, n'a pas à être reposé. Et l'ancrage du
        // joint suit la cote — c'est un point du MONDE quand le joint n'a pas de `connectedBody`.
        float rayonR;
        float coteR = CoterPivot(porteur, pivot, sens, 30f, out rayonR);

        if (Mathf.Abs(pivot.localPosition.x - coteR) > 1e-4f)
        {
            UnityEditor.Undo.RecordObject(pivot, "Recoter le pivot du flipper");
            pivot.localPosition = new Vector3(coteR, pivot.localPosition.y, pivot.localPosition.z);

            sb.AppendLine("  §9 : pivot recoté à x = " + coteR.ToString("F4")
                          + " (rayon de balayage " + (rayonR * 1000f / 16.66667f).ToString("F2")
                          + " mm, drain gap visé " + DRAIN_GAP_U.ToString("F4") + " u)");

            AncrerJoint(pivot, rt, coteR, sb);
            sb.AppendLine("    ancrage du joint reposté"
                          + (Mathf.Abs(Mathf.Abs(pivot.position.x) - Mathf.Abs(pivot.localPosition.x)) < 0.5f
                             ? "  ✓" : "  ⚠"));
        }

        continue;
    }

    var piece = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(modele, porteur);

    piece.name = "Bat_Mesh";

    UnityEditor.Undo.RegisterCreatedObjectUndo(piece, "Poser le bat de flipper");

    var pieceT = piece.transform;

    // 4. L'orientation : 4 quarts de tour autour de l'AXE DU JOINT, on mesure et on retient.
    // Grand axe sur X, à plat (épaisseur en Z), gros bout côté pivot.
    Quaternion meilleure = pieceT.localRotation;
    bool trouve = false;   // un quart à plat ET talon (+X maille) côté pivot
    bool vuPlat = false;   // repli : au moins un quart à plat
    Quaternion repliPlat = pieceT.localRotation;

    // L'axe du joint est fixe dans le repère du porteur (identité dans le pivot) :
    // on compose chaque quart en ABSOLU autour de cet axe. (v4 : l'incrément relatif
    // mélangeait l'axe local et le repère parent — seul q=0 sortait à plat, la « recherche »
    // n'en était pas une et le bat gauche est resté copie du droit.)
    var axeJoint = porteur.InverseTransformDirection(pivot.forward).normalized;
    var orientationImport = pieceT.localRotation;

    for (int q = 0; q < 4; q++)
    {
        pieceT.localRotation = Quaternion.AngleAxis((float)q * 90f, axeJoint) * orientationImport;

        var boite = BoiteLocale(pieceT, pivot);

        bool grandAxeSurX = boite.size.x >= boite.size.y * 1.5f && boite.size.x >= boite.size.z * 1.5f;
        bool aPlat = boite.size.z <= boite.size.y;

        if (!grandAxeSurX || !aPlat) { continue; }

        if (!vuPlat) { repliPlat = pieceT.localRotation; vuPlat = true; }

        string profil;
        int talon = SensTalon(pieceT, pivot, out profil);

        // Règle mesurée (cf. §2) : le talon est côté +X du maillage ; il doit pointer
        // côté pivot, soit −sens en X pivot.
        var xMaille = pivot.InverseTransformDirection(pieceT.TransformDirection(Vector3.right));
        bool talonCotePivot = Vector3.Dot(xMaille, new Vector3(-sens, 0f, 0f)) > 0.9f;

        sb.AppendLine("  quart " + (q * 90) + "° : boîte " + boite.size.ToString("F4")
                      + "   +X maille→pivot " + xMaille.ToString("F2")
                      + (talonCotePivot ? "   talon côté pivot ✓" : "   talon côté pointe"));

        if (q == 0) { sb.AppendLine("    " + profil); }

        if (talonCotePivot && !trouve) { meilleure = pieceT.localRotation; trouve = true; }
    }

    if (!trouve)
    {
        meilleure = repliPlat;
        sb.AppendLine("  → ⚠ aucun quart n'a le talon côté pivot, premier quart à plat gardé");
    }
    else { sb.AppendLine("  → orientation retenue (talon +X côté pivot)"); }

    pieceT.localRotation = meilleure;

    // --- 4bis. LE ROULIS : quelle face regarde le ciel ---------------------------------------
    //
    // §4 ne tourne qu'autour de l'AXE DU JOINT — quatre quarts dans le plan de la table. Aucun de
    // ces quatre candidats ne change la face qui regarde le ciel, donc un bat couché sur le flanc
    // passe inaperçu.
    //
    // Relevé du 2026-09-15 (retour terrain : « le côté rouge doit être sur le côté, pas vers le
    // haut ») : le « haut » du modèle ressortait sur le +Y du pivot, c'est-à-dire vers le FOND de
    // la table — à l'horizontale. Le caoutchouc rouge apparaissait alors en bande centrale sur le
    // DESSUS du bat, au lieu du bourrelet latéral qui borde le plastique.
    //
    // L'ancre du modèle, pour mémoire : `Tools/pinball-parts-inventory.json` donne
    // `Flipper Williams 3"` pour 80,01 × 21,18 × 25,99 mm en repère Blender, où Z est le haut.
    // Le pipeline mappe Blender Z → Unity Y (cf. `export_parts.py`) : le « haut » du modèle est
    // donc l'axe des 25,99 mm — presque le diamètre d'une bille (27 mm), ce qui est bien la
    // hauteur d'un bat de flipper. C'est la mesure qui tranche, pas l'aspect.
    //
    // Comment on le reconnaît sans rien supposer du FBX : c'est l'axe local du maillage où le
    // PLASTIQUE dépasse le CAOUTCHOUC. Sur un flipper, la hauteur est donnée par le corps
    // plastique ; le caoutchouc est un bourrelet, donc plus court dans cette direction. Mesuré :
    // 26,0 mm contre 12,5 mm — l'écart s'inverse sur les deux autres axes.
    //
    // On cherche le roulis autour du GRAND AXE qui redresse ce « haut » vers le ciel. Le grand
    // axe est le X du pivot : la boîte assemblée fait 1,3335 u sur X contre 0,43 et 0,35 sur les
    // deux autres (§4 l'exige d'ailleurs), et `porteur` est à l'identité dans le pivot — le
    // repère parent de la pièce EST celui du pivot, donc `Vector3.right` est bien ce grand axe.
    var filtreBat = piece.GetComponentInChildren<MeshFilter>();
    var renduBat = piece.GetComponentInChildren<MeshRenderer>();
    var mailleBat = filtreBat != null ? filtreBat.sharedMesh : null;

    if (mailleBat != null && mailleBat.subMeshCount >= 2 && renduBat != null)
    {
        var sommetsBat = mailleBat.vertices;
        var boites = new Bounds[mailleBat.subMeshCount];
        var remplies = new bool[mailleBat.subMeshCount];
        var compte = new int[mailleBat.subMeshCount];

        for (int s = 0; s < mailleBat.subMeshCount; s++)
        {
            int[] tris;

            try { tris = mailleBat.GetTriangles(s); }
            catch { continue; }

            bool premier = true;

            foreach (var t in tris)
            {
                var p = sommetsBat[t];

                if (premier) { boites[s] = new Bounds(p, Vector3.zero); premier = false; }
                else { boites[s].Encapsulate(p); }

                compte[s]++;
            }

            remplies[s] = !premier;
        }

        // Quel sous-maillage est le caoutchouc ? Par son MATÉRIAU — les noms d'origine sont encore
        // en place ici, §7 ne les remplace qu'ensuite. À défaut, par la géométrie : le bourrelet
        // est une bande, le plastique est le corps, donc le plus petit des deux.
        int caout = -1;

        for (int s = 0; s < mailleBat.subMeshCount; s++)
        {
            if (!remplies[s] || s >= renduBat.sharedMaterials.Length) { continue; }

            var m = renduBat.sharedMaterials[s];

            if (m != null && (m.name.IndexOf("Rubber", System.StringComparison.OrdinalIgnoreCase) >= 0
                              || (matCaoutchouc != null && m == matCaoutchouc)))
            {
                caout = s;
                break;
            }
        }

        if (caout < 0)
        {
            int petit = int.MaxValue;

            for (int s = 0; s < mailleBat.subMeshCount; s++)
            {
                if (remplies[s] && compte[s] > 0 && compte[s] < petit)
                {
                    petit = compte[s];
                    caout = s;
                }
            }
        }

        int plast = -1;

        for (int s = 0; s < mailleBat.subMeshCount; s++)
        {
            if (remplies[s] && s != caout) { plast = s; break; }
        }

        if (plast >= 0 && caout >= 0 && caout != plast)
        {
            int axeHaut = -1;
            float ecartMax = 0f;

            for (int a = 0; a < 3; a++)
            {
                float ecart = boites[plast].size[a] - boites[caout].size[a];

                if (ecart > ecartMax) { ecartMax = ecart; axeHaut = a; }
            }

            // Les cotes ci-dessous sont dans les unités LOCALES du maillage, pas dans celles de la
            // scène : le facteur d'échelle du FBX s'interpose, et une conversion en mm par la
            // constante de la table serait fausse (elle annonçait « 0,01 mm » là où l'écart réel
            // vaut les deux tiers de la bande de caoutchouc). On affiche donc les valeurs brutes,
            // plus la marge RELATIVE — un rapport, donc sans unité, et c'est lui qui décide.
            sb.AppendLine();
            sb.AppendLine("  §4bis roulis (repère du MAILLAGE) :");
            sb.AppendLine("    plastique  " + boites[plast].size.ToString("F6")
                          + "   (" + compte[plast] + " triangles)");
            sb.AppendLine("    caoutchouc " + boites[caout].size.ToString("F6")
                          + "   (" + compte[caout] + " triangles)");

            if (axeHaut < 0)
            {
                sb.AppendLine("    ⚠ aucun axe où le plastique dépasse le caoutchouc : roulis non tenté");
            }
            else
            {
                var locale = axeHaut == 0 ? Vector3.right
                           : (axeHaut == 1 ? Vector3.up : Vector3.forward);

                float surAxe = boites[caout].size[axeHaut];
                float marge = surAxe > 1e-9f ? 100f * ecartMax / surAxe : 0f;

                sb.AppendLine("    axe « haut » du modèle : " + "XYZ"[axeHaut]
                              + "   (plastique − caoutchouc = " + ecartMax.ToString("F6")
                              + " u. maillage, soit +" + marge.ToString("F1")
                              + " % sur cet axe)");

                var baseRoulis = pieceT.localRotation;
                var retenu = baseRoulis;
                float mieux = float.MinValue;
                float angleRetenu = 0f;
                var directionRetenue = Vector3.zero;

                for (int q = 0; q < 4; q++)
                {
                    float angle = q * 90f;

                    // Le roulis se prend autour du GRAND AXE, en absolu depuis la pose de §4 :
                    // même motif que §4, où l'incrément relatif avait déjà fait des dégâts.
                    pieceT.localRotation = Quaternion.AngleAxis(angle, Vector3.right) * baseRoulis;

                    var dansPivot = pivot.InverseTransformDirection(
                        pieceT.TransformDirection(locale)).normalized;

                    // −Z du pivot = le CIEL : dans ce cadre la hauteur se lit à l'envers (cf. §1).
                    float alignement = Vector3.Dot(dansPivot, Vector3.back);

                    sb.AppendLine("      roulis " + angle.ToString("F0").PadLeft(3) + "°  →  pivot "
                                  + dansPivot.ToString("F2")
                                  + "   ciel " + alignement.ToString("F3"));

                    if (alignement > mieux)
                    {
                        mieux = alignement;
                        retenu = pieceT.localRotation;
                        angleRetenu = angle;
                        directionRetenue = dansPivot;
                    }
                }

                pieceT.localRotation = retenu;

                sb.AppendLine("    → roulis retenu " + angleRetenu.ToString("F0")
                              + "°   haut du modèle → " + directionRetenue.ToString("F2")
                              + (mieux > 0.99f ? "   ✓" : "   ⚠ alignement imparfait"));
            }
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("  §4bis : sous-maillages non identifiés — roulis non tenté");
        }
    }

    // 5. La position : talon sur l'axe, centré en travers, semelle à 0,02 u du plateau.
    // Cadre +90° : X = gauche-droite table, Y = haut-bas table, Z = VERS LE BAS (cf. §1).
    // Cotes relues APRÈS le roulis de §4bis : la hauteur du bat a changé de face, donc la
    // semelle et le centrage en travers se recalculent sur la nouvelle boîte.
    var placee = BoiteLocale(pieceT, pivot);

    float cibleX = sens * 0.03f + (sens > 0f ? -placee.min.x : -placee.max.x);
    // Talon à 0,03 u (1,8 mm) de l'axe, vers le centre : le jour anti-frottement. Mesuré
    // (probe_flipper_jour) : la boîte frôle le pied du slingshot à 0,00 et passe à 0,03.
    // C'est l'arête sharp de la boîte qui frôlait — le talon arrondi passe à ~5 mm.
    // L'œil (Ø26 mm) excentré de 1,8 mm reste visuellement sur son axe.
    float cibleY = -placee.center.y;                            // talon centré en travers
    float cibleZ = -0.02f - placee.max.z;                       // semelle (face haute) à −0,02

    pieceT.position += pivot.TransformVector(new Vector3(cibleX, cibleY, cibleZ));

    var assise = BoiteLocale(pieceT, pivot);

    sb.AppendLine("assise, axes pivot : centre " + assise.center.ToString("F4")
                  + "   taille " + assise.size.ToString("F4")
                  + "   soit " + (assise.size.x * 1000f / 16.66667f).ToString("F1") + " mm de long");
    sb.AppendLine("  talon à x " + (sens > 0f ? assise.min.x : assise.max.x).ToString("F4")
                  + "   pointe à x " + (sens > 0f ? assise.max.x : assise.min.x).ToString("F4")
                  + "   semelle à z " + assise.max.z.ToString("F4"));

    // 9. LA COTE DU PIVOT — déduite de la pièce, pas écrite d'avance. Avant le balayage : c'est
    // elle qui décide de ce que le flipper peut toucher.
    //
    // Pourquoi ce n'est pas la cote du générateur : `build_table.py` dimensionne le drain gap
    // pour un bat de 3" (`FLIP_LEN` = 76,2 mm) dont le talon est SUR l'axe, et pose les pivots
    // à 1,42833. La pièce du dépôt, elle, mesure **80,0 mm** (relevé : 1,3335 u) et son talon est
    // avancé de 0,03 u pour ne pas frotter. Le rayon réellement balayé est donc
    // 0,03 + 1,3335 = 1,3635 u au lieu de 1,27 : la pointe tombe 5,6 mm trop près du centre, et
    // l'écart entre pointes descend à 29,66 mm = 1,10 bille.
    //
    // Or 1,10 bille est exactement le passage que la règle n°3 de `build_table.py` interdit :
    // « un intervalle est soit plus étroit que le diamètre, soit au moins 1,3 fois plus large ».
    // À 1,10 la bille y entre et s'y coince — c'est le motif que tout le reste du projet évite.
    //
    // On recote donc le pivot à partir du rayon MESURÉ, pour que la POINTE retombe sur la cote
    // que le générateur visait. Les plans de référence (`proto1`/`proto2`) montrent le même jour
    // entre les pointes.
    float rayon;
    float cote = CoterPivot(porteur, pivot, sens, 30f, out rayon);

    sb.AppendLine();
    sb.AppendLine("  §9 cote du pivot :");
    sb.AppendLine("    rayon de balayage mesuré  " + rayon.ToString("F4") + " u = "
                  + (rayon * 1000f / 16.66667f).ToString("F2") + " mm   (générateur : 1,2700 u = 76,20 mm)");
    sb.AppendLine("    cote retenue              x = " + cote.ToString("F4")
                  + "   (générateur : " + (sens > 0f ? -1.428f : 1.428f).ToString("F4") + ")");

    UnityEditor.Undo.RecordObject(pivot, "Recoter le pivot du flipper");

    pivot.localPosition = new Vector3(cote, pivot.localPosition.y, pivot.localPosition.z);

    // L'ancrage du `HingeJoint` suit : c'est un point du MONDE quand le joint n'a pas de
    // `connectedBody`, et il est écrit à zéro par défaut — le flipper se retrouverait accroché à
    // l'origine de la scène au lieu de son pivot. On le réécrit ici parce que §9 DÉPLACE le
    // pivot : un ancrage resté sur l'ancienne cote tordrait le flipper (cf. `fix_flipper_hinges.cs`).
    AncrerJoint(pivot, rt, cote, sb);

    // Le bat suit sa nouvelle cote : la boîte locale ne change pas (une translation du pivot ne
    // touche pas les coordonnées du bat dans son repère), on relit donc `assise` tel quel. La
    // pointe, elle, se lit à l'angle de repos — c'est elle qui décide du drain gap.
    var qRepos = Quaternion.AngleAxis(repos, Vector3.forward);
    var pointeMonde = pivot.TransformPoint(qRepos * new Vector3(sens > 0f ? assise.max.x : assise.min.x,
                                                                assise.center.y, assise.center.z));
    var pointeTable = rt.InverseTransformPoint(pointeMonde);

    sb.AppendLine("    pointe au repos, en table  x = " + pointeTable.x.ToString("F4")
                  + "   z = " + pointeTable.z.ToString("F4")
                  + "   soit " + (Mathf.Abs(pointeTable.x) * 2f * 1000f / 16.66667f).ToString("F2")
                  + " mm d'écart entre les pointes"
                  + (Mathf.Abs(Mathf.Abs(pointeTable.x) * 2f - DRAIN_GAP_U) < 1e-4f ? "  ✓" : "  ⚠"));

    // 6. Le balayage ne doit rien toucher : on teste AVANT d'ajouter le collider.
    // Plus la garde aux 4 coins du talon (ClosestPoint vers les corps de slingshot, en mm).
    var angles = new float[] { 0f, repos, actif };
    var etiquettes = new string[] { "neutre", "repos", "actif" };

    bool touche = false;

    var corps = new GameObject[] { corpsL, corpsR };

    for (int i = 0; i < angles.Length; i++)
    {
        float a = angles[i];
        var q = Quaternion.AngleAxis(a, Vector3.forward);
        var centrePivot = q * assise.center;
        var centreMonde = pivot.TransformPoint(centrePivot);
        var rotMonde = pivot.rotation * q;

        var presents = Physics.OverlapBox(centreMonde, assise.size * 0.5f, rotMonde, ~0,
                                          QueryTriggerInteraction.Ignore);

        if (presents.Length > 0)
        {
            touche = true;

            foreach (var h in presents)
            {
                sb.AppendLine("  ⚠ balayage '" + etiquettes[i] + "' (" + a + "°) : touche '" + h.name + "'");
            }
        }

        // Garde : les 4 coins du talon vers les corps de slingshot.
        float hx = sens > 0f ? assise.min.x : assise.max.x;

        float gardeMin = float.MaxValue;
        string gardeOu = "";

        foreach (var cy in new float[] { assise.min.y, assise.max.y })
        {
            foreach (var cz in new float[] { assise.min.z, assise.max.z })
            {
                var coinMonde = pivot.TransformPoint(q * new Vector3(hx, cy, cz));

                foreach (var c in corps)
                {
                    if (c == null) { continue; }

                    var col = c.GetComponent<Collider>();

                    if (col == null) { continue; }

                    var proche = col.ClosestPoint(coinMonde);
                    float d = Vector3.Distance(coinMonde, proche);

                    if (d < gardeMin) { gardeMin = d; gardeOu = c.name; }
                }
            }
        }

        sb.AppendLine("  garde talon '" + etiquettes[i] + "' : " + (gardeMin * 1000f / 16.66667f).ToString("F1")
                      + " mm (" + gardeOu + ")" + (gardeMin * 1000f / 16.66667f < 0.5f ? "   ⚠" : ""));
    }

    if (!touche) { sb.AppendLine("balayage neutre/repos/actif : rien touché ✓"); }

    // 7. Les matériaux URP : le plastique orange du projet, le rouge pour le caoutchouc.
    foreach (var rendu in piece.GetComponentsInChildren<MeshRenderer>())
    {
        var mats = rendu.sharedMaterials;
        bool change = false;

        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null) { continue; }

            if (mats[i].name.Contains("Rubber") && matCaoutchouc != null && mats[i] != matCaoutchouc)
            {
                sb.AppendLine("matériau '" + mats[i].name + "' → " + matCaoutchouc.name);
                mats[i] = matCaoutchouc;
                change = true;
            }
            else if ((mats[i].name.Contains("Bat") || mats[i].name.Contains("Plastic"))
                     && matPlastique != null && mats[i] != matPlastique)
            {
                sb.AppendLine("matériau '" + mats[i].name + "' → " + matPlastique.name);
                mats[i] = matPlastique;
                change = true;
            }
        }

        if (change)
        {
            UnityEditor.Undo.RecordObject(rendu, "Réassigner les matériaux du bat");
            rendu.sharedMaterials = mats;
        }
    }

    // 8. Le collider : la boîte assise, RÉTRACTÉE côté talon juste ce qu'il faut pour ne plus
    //    mordre le slingshot.
    //
    // Pourquoi rétracter : `build_table.py` pose le pied de la face du slingshot (`SLING_BOT`)
    // SUR le pivot du flipper — (0,088 ; 0,085) m contre `FLIP_PIVOT` (0,0857 ; 0,092) m. Le
    // pivot tombe donc à 0,6 mm du plan de la face, et la base du bat, large de 26 mm, s'étale
    // de part et d'autre. Mesuré : la boîte du collider entre de ~8 mm dans le corps du
    // slingshot. `test_flippers_tenue.cs` a montré ce que cela coûte — 56 mm de dérive du pivot
    // en 5 s de simulation, exactement ce qui les avait arrachés de leur ancre.
    //
    // La rétraction ne coûte rien au jeu : la zone libérée est celle que la bille ne peut pas
    // atteindre, coincée entre le pied du slingshot et la base du bat. On cherche la plus PETITE
    // valeur qui dégage le balayage — par balayage, pas par calcul : le corps est un maillage,
    // et la surface réelle qu'il oppose ne se déduit pas de la cote de son plan.
    var cibles = new System.Collections.Generic.List<Collider>();

    foreach (var cible in new string[]
             {
                 "PinballTable/Gameplay/Slingshot_Left",
                 "PinballTable/Gameplay/Slingshot_Right",
                 "PinballTable/Table/Pinball_Table/Slingshots/Body_L",
                 "PinballTable/Table/Pinball_Table/Slingshots/Body_R",
             })
    {
        var g = GameObject.Find(cible);

        if (g == null) { continue; }

        foreach (var c in g.GetComponents<Collider>())
        {
            if (c != null && !c.isTrigger) { cibles.Add(c); }
        }
    }

    const float pasRetrait = 0.005f;
    const float retraitMax = 0.30f;

    float retrait = 0f;
    string motif = "";
    bool degage = false;

    for (; retrait <= retraitMax; retrait += pasRetrait)
    {
        var essai = RetracterTalon(assise, sens, retrait);

        motif = "";

        foreach (var a in new float[] { 0f, repos, actif })
        {
            var q = Quaternion.AngleAxis(a, Vector3.forward);

            var presents = Physics.OverlapBox(pivot.TransformPoint(q * essai.center),
                                              essai.size * 0.5f, pivot.rotation * q, ~0,
                                              QueryTriggerInteraction.Ignore);

            foreach (var h in presents)
            {
                if (!cibles.Contains(h)) { continue; }

                motif = h.name + " à " + a.ToString("F0") + "°";
                break;
            }

            if (motif.Length > 0) { break; }
        }

        if (motif.Length == 0) { degage = true; break; }
    }

    // Deux pas de marge une fois dégagé : rester au contact exact, c'est laisser la physique
    // hésiter entre « touche » et « touche pas » à chaque image.
    if (degage) { retrait = Mathf.Min(retrait + 2f * pasRetrait, retraitMax); }

    if (!degage || retrait <= 2f * pasRetrait) { retrait = 0f; }

    var boiteColl = RetracterTalon(assise, sens, retrait);

    sb.AppendLine();
    sb.AppendLine("  §8 collider :");

    if (cibles.Count == 0)
    {
        sb.AppendLine("    aucun collider de slingshot trouvé — rétraction non tentée");
    }
    else if (!degage)
    {
        sb.AppendLine("    ⚠ le balayage mord encore (" + motif + ") même à "
                      + (retraitMax * 1000f / 16.66667f).ToString("F0")
                      + " mm de rétraction : boîte laissée entière, c'est à la géométrie de la"
                      + " table d'être revue");
    }
    else if (retrait > 0f)
    {
        sb.AppendLine("    talon rétracté de " + (retrait * 1000f / 16.66667f).ToString("F2")
                      + " mm pour dégager le balayage (il mordait : " + motif + ")");
    }
    else
    {
        sb.AppendLine("    boîte entière conservée : rien à dégager");
    }

    var collider = UnityEditor.Undo.AddComponent<BoxCollider>(piece);

    collider.center = pieceT.InverseTransformPoint(pivot.TransformPoint(boiteColl.center));

    var tailleScene = TailleDansAxesPiece(pieceT, pivot, boiteColl.size);
    var echelle = pieceT.lossyScale;

    collider.size = new Vector3(
        tailleScene.x / Mathf.Max(Mathf.Abs(echelle.x), 1e-6f),
        tailleScene.y / Mathf.Max(Mathf.Abs(echelle.y), 1e-6f),
        tailleScene.z / Mathf.Max(Mathf.Abs(echelle.z), 1e-6f));

    collider.isTrigger = false;

    UnityEditor.EditorUtility.SetDirty(collider);

    // Le contrôle qui compte : la boîte du collider relue dans les axes du pivot.
    Vector3 demi = collider.size * 0.5f;
    Bounds verif = default(Bounds);
    bool prem = true;

    for (int i = 0; i < 8; i++)
    {
        var p = pivot.InverseTransformPoint(collider.transform.TransformPoint(
            collider.center + new Vector3(
                (i & 1) == 0 ? -demi.x : demi.x,
                (i & 2) == 0 ? -demi.y : demi.y,
                (i & 4) == 0 ? -demi.z : demi.z)));

        if (prem) { verif = new Bounds(p, Vector3.zero); prem = false; }
        else { verif.Encapsulate(p); }
    }

    sb.AppendLine("BoxCollider : écart de taille " + (verif.size - boiteColl.size).ToString("F5")
                  + "   écart de centre " + (verif.center - boiteColl.center).ToString("F5"));

    // 10. LES CONTRÔLES QUI MANQUAIENT : où est le bat, et que touche-t-il ?
    //
    // Tout ce script pose le bat d'après le repère du PIVOT — « semelle à 0,02 u », « talon à
    // 0,03 u » — ce qui suppose que le pivot repose exactement sur la surface de jeu ET que le bat
    // est centré sur son axe. Deux hypothèses qui n'étaient jamais vérifiées, et qu'un déplacement
    // à la main fait tomber.
    //
    // Mesuré le 2026-09-15, après un remaniement manuel de la scène : les deux `Bat_Mesh` avaient
    // été déplacés de 0,64 u (38 mm) en travers de l'axe. Le bat entrait alors dans le corps du
    // slingshot — un `MeshCollider` bien plus large que la bande réactive — et PhysX le repoussait
    // en boucle : 328° de débattement pour des limites de ±30°, une vitesse angulaire de
    // 5,9 × 10⁹ °/s, et un déplacement du pivot sur un axe pourtant gelé par `FreezePosition` (la
    // dé-pénétration ne respecte pas les contraintes du corps).
    //
    // Deux mesures, donc, et aucune ne se contente d'une cote supposée :
    //
    //   1. l'écart latéral du bat à son pivot — doit être ~0 ; un écart de plusieurs millimètres
    //      est un déplacement à la main, et tout ce qui suit (jour anti-frottement, rétraction du
    //      collider) a été calculé pour la position d'origine ;
    //   2. la garde sous la semelle, mesurée CONTRE LE PLATEAU — un rayon vers le BAS lancé de
    //      bien au-dessus, ce qui touche toujours la face supérieure, contrairement à un rayon
    //      parti d'en dessous qui peut traverser un maillage non convexe sans rien rencontrer.
    sb.AppendLine();
    sb.AppendLine("  §10 le bat est-il là où le script le croit ?");

    var assise2 = BoiteLocale(pieceT, pivot);

    sb.AppendLine("    écart latéral à l'axe   " + assise2.center.y.ToString("F5") + " u = "
                  + (assise2.center.y * 1000f / 16.66667f).ToString("F2") + " mm"
                  + (Mathf.Abs(assise2.center.y) < 3e-3f ? "   ✓ centré"
                                                          : "   ⚠ DÉCALÉ — la pose a été modifiée à la main"));

    sb.AppendLine("    talon à l'axe           " + (sens > 0f ? assise2.min.x : assise2.max.x).ToString("F5")
                  + " u   (visé " + (sens * 0.03f).ToString("F4") + ")"
                  + (Mathf.Abs((sens > 0f ? assise2.min.x : assise2.max.x) - sens * 0.03f) < 3e-3f
                      ? "   ✓" : "   ⚠"));

    // La garde sous la semelle, mesurée depuis le dessus de la table.
    //
    // Le rayon part de 0,5 u au-dessus et descend : il rencontre d'abord le BAT lui-même, qui
    // occupe 26 mm de haut. Le lire tel quel donnait « -26,18 mm », soit la hauteur du bat et non
    // sa garde — une mesure qui a l'air d'un résultat et n'en est pas un. On écarte donc tout ce
    // qui appartient au flipper, et on ne retient que la première surface ÉTRANGÈRE.
    float dessusTable = rt.TransformPoint(pivot.localPosition).y + 0.5f;
    float gardePlateau = float.MaxValue;
    string sousLeBat = "";

    for (int i = 0; i <= 10; i++)
    {
        var p = pivot.TransformPoint(new Vector3(
            Mathf.Lerp(assise2.min.x, assise2.max.x, i / 10f), assise2.center.y, assise2.max.z));

        var tous = Physics.RaycastAll(new Vector3(p.x, dessusTable, p.z), Vector3.down, 3f, ~0,
                                      QueryTriggerInteraction.Ignore);

        System.Array.Sort(tous, (x, y) => x.distance.CompareTo(y.distance));

        foreach (var t in tous)
        {
            // Ce qui appartient au flipper ne compte pas : c'est le bat qu'on mesure, pas ce
            // qu'il porte.
            if (t.collider.transform.IsChildOf(pivot)) { continue; }

            float garde = p.y - t.point.y;

            if (garde < gardePlateau)
            {
                gardePlateau = garde;
                sousLeBat = t.collider.name;
            }

            break;   // premier sol étranger rencontré sous ce point
        }
    }

    if (gardePlateau > -0.5f && gardePlateau < 0.5f)
    {
        sb.AppendLine("    garde sous la semelle   " + (gardePlateau * 1000f / 16.66667f).ToString("F2")
                      + " mm  (sol : '" + sousLeBat + "')"
                      + (gardePlateau < 0f ? "   ⚠ LE BAT EST ENTERRÉ dans le plateau"
                        : (gardePlateau < 0.0005f ? "   ⚠ collé au plateau"
                                                  : "   ✓ au-dessus du plateau")));
    }
    else
    {
        sb.AppendLine("    garde sous la semelle   non mesurable (aucun sol étranger sous le bat)");
    }

    // Le bat entre-t-il dans le corps du slingshot ? Test par lancer de rayon vers le mur, en
    // comptant les traversées : un nombre IMPAIR signifie que le point est DANS le solide.
    var corpsSling = GameObject.Find("PinballTable/Table/Pinball_Table/Slingshots/Body_"
                                      + (aGauche ? "L" : "R"));

    if (corpsSling != null)
    {
        var colCorps = corpsSling.GetComponent<Collider>();
        int dedans = 0;
        int testes = 0;

        if (colCorps != null)
        {
            for (int i = 0; i <= 10; i++)
            {
                var p = pivot.TransformPoint(new Vector3(
                    Mathf.Lerp(assise2.min.x, assise2.max.x, i / 10f),
                    assise2.center.y, assise2.center.z));

                // Rayon horizontal vers le mur ; on compte les entrées/sorties du maillage.
                var direction = (rt.rotation * new Vector3(aGauche ? -1f : 1f, 0f, 0f)).normalized;

                var travers = Physics.RaycastAll(p, direction, 5f, ~0,
                                                 QueryTriggerInteraction.Ignore);

                int croisements = 0;

                foreach (var t in travers)
                {
                    if (t.collider == colCorps) { croisements++; }
                }

                testes++;

                if (croisements % 2 == 1) { dedans++; }
            }
        }

        sb.AppendLine("    dans le corps du slingshot : " + dedans + " point(s) sur " + testes
                      + (dedans > 0
                          ? "   ⚠ LE BAT EST DANS LE CORPS — c'est ce qui fait exploser le flipper"
                          : "   ✓ aucun"));
    }
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

sb.AppendLine();
sb.AppendLine("scène modifiée : " + scene.isDirty + "   (Ctrl+Z annule, Ctrl+S conserve)");

// Le CLI tronque les longues sorties : le rapport part sur disque, la valeur de retour ne
// sert qu'à dire où.
var chemin = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "place_flippers.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(chemin));
System.IO.File.WriteAllText(chemin, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(chemin) + "  (" + sb.Length + " car.)";
