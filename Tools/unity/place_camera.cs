// Pose la caméra de jeu : résout la pose, puis l'applique à `Main Camera`. ÉCRITURE.
//
// ---------------------------------------------------------------------------------------------
// Deux erreurs de la première version, corrigées ici — toutes deux dans le solveur, pas dans la
// scène. `probe_camera_frame.cs` les a mises au jour en interrogeant la vraie `Camera` :
//
//  1. **Repères mélangés.** `Pose()` rend une position MONDE (`rt.TransformPoint`), mais `Cadrage()`
//     la comparait à `centre`, qui est en repère ROOT. Le `avant` qui en sortait ne voulait rien
//     dire, et tous les chiffres de cadrage avec lui. Corrigé en faisant *toute* la géométrie en
//     repère du root : le plateau y est dans le plan y = 0 (cf. CLAUDE.md), donc sa normale est
//     `Vector3.up` et le tilt de −7° n'entre plus nulle part. Un seul `TransformPoint` à la fin.
//
//  2. **Rapport d'écran supposé.** 16/9 était écrit en dur ; la fenêtre Game de l'éditeur mesure
//     1705 × 822, soit 2,074. Le rapport est donc un *paramètre*, et le résultat est reporté pour
//     les deux — celui de la cible (16/9, ce que verra le joueur) et celui de l'éditeur.
//
// Et un levier que la première version n'explorait pas : **la focale**. À cadrage égal, un objectif
// long et loin aplatit la perspective — le rapport « proche/loin » tend vers 1 quand le fov
// diminue, puisqu'on s'éloigne. Le solveur ne balayait que fov 35→60, d'où ses ratios de 2,0+ qu'il
// présentait comme le prix inévitable de l'inclinaison. Ils étaient le prix d'une focale courte.
//
// ---------------------------------------------------------------------------------------------
// Ce que la géométrie impose, elle, et qui ne se corrige pas :
//
// La table est un rectangle de rapport 1,87 (long / large) ; l'écran, un rectangle de rapport 1,78.
// Or l'axe LONG de la table tombe sur l'axe VERTICAL de l'écran — c'est la vue de flipper classique,
// on regarde la table depuis le bas. Le rapport des couvertures vaut donc
//
//     couverture_x / couverture_y  =  (largeur / longueur·cos φ) / rapport_écran
//
// Avec 9,439 de large, 17,465 de long et 1,778 de rapport, il faudrait cos φ < 0,304, soit φ > 72°,
// pour que la largeur cesse d'être le facteur limitant. À tout angle plus doux, cadrer la hauteur
// laisse la largeur à moitié vide — et cette place est celle du caisson et du HUD.
//
// Rien n'est enregistré : `Undo.RecordObject` rend le changement annulable par Ctrl+Z, et la scène
// est marquée modifiée mais **pas** sauvegardée — c'est Ctrl+S qui décide.

var sb = new System.Text.StringBuilder();

const float marge = 0.04f;
const float rapportCible = 16f / 9f;

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "'PinballTable' introuvable dans la scène."; }

var rt = racine.transform;

var camera = GameObject.Find("Main Camera");

if (camera == null) { return "'Main Camera' introuvable dans la scène."; }

var cam = camera.GetComponent<Camera>();

if (cam == null) { return "'Main Camera' ne porte pas de Camera."; }

// --- le volume à cadrer : la TABLE, lue sur ses renderers ------------------------------------------
//
// On ne cadre pas l'union table + caisson. Le caisson (`Pinball_Cabinet`) mesure 10,77 × 12,09 ×
// 20,79 — son bandeau monte à 12 u et il domine tout : le cadrer, c'est rapetisser le plateau. Or
// c'est le *plateau* qu'on joue ; le bandeau est du décor, il a le droit de sortir par le haut.
//
// Les bornes sont LUES sur les `Renderer`, pas recopiées d'un relevé à la main : le relevé à la
// main de la première version donnait 17,64 de long là où le modèle en fait 17,465, et 3,03 de haut
// là où il en fait 2,800.

Bounds BornesDe(string nom)
{
    var table = rt.Find("Table");
    var t = table != null ? table.Find(nom) : null;

    if (t == null) { t = rt.Find(nom); }

    if (t == null) { return new Bounds(); }

    var rendus = t.GetComponentsInChildren<Renderer>(true);

    if (rendus.Length == 0) { return new Bounds(); }

    var b = rendus[0].bounds;

    for (int i = 1; i < rendus.Length; i++) { b.Encapsulate(rendus[i].bounds); }

    return b;
}

Bounds BornesLocales(Bounds monde, Transform espace)
{
    // Une `Bounds` est alignée sur le MONDE : la transformer ne se fait pas en transformant son
    // centre et sa taille (c'est le piège déjà rencontré sur les colliders). On repasse par les 8
    // coins, puis on reconstruit.
    var b = new Bounds(espace.InverseTransformPoint(monde.center), Vector3.zero);

    for (int i = 0; i < 8; i++)
    {
        b.Encapsulate(espace.InverseTransformPoint(new Vector3(
            (i & 1) == 0 ? monde.min.x : monde.max.x,
            (i & 2) == 0 ? monde.min.y : monde.max.y,
            (i & 4) == 0 ? monde.min.z : monde.max.z)));
    }

    return b;
}

var boiteTable = BornesDe("Pinball_Table");
var boiteCaisson = BornesDe("Pinball_Cabinet");

if (boiteTable.size == Vector3.zero) { return "'Pinball_Table' introuvable ou sans renderer."; }

// En repère du root : le plateau est dans le plan y = 0, sa normale est `Vector3.up`.
var tableau = BornesLocales(boiteTable, rt);
var caisson = BornesLocales(boiteCaisson, rt);

var centre = tableau.center;

sb.AppendLine("=== ce qu'on cadre (repère du root : plateau dans le plan y = 0) ===");
sb.AppendLine("  Pinball_Table    centre " + tableau.center.ToString("F3")
              + "   taille " + tableau.size.ToString("F3"));

if (boiteCaisson.size != Vector3.zero)
{
    sb.AppendLine("  Pinball_Cabinet  centre " + caisson.center.ToString("F3")
                  + "   taille " + caisson.size.ToString("F3") + "   (décor : non cadré)");
}

sb.AppendLine("  caméra avant     " + rt.InverseTransformPoint(camera.transform.position).ToString("F3")
              + " (repère root)   fov " + cam.fieldOfView.ToString("F1")
              + "   rapport écran " + cam.aspect.ToString("F3"));
sb.AppendLine("  rapport de la table " + (tableau.size.z / tableau.size.x).ToString("F3")
              + " (long / large)   rapport d'écran cible " + rapportCible.ToString("F3"));
sb.AppendLine();

// Les 8 coins, en repère du root.
var coins = new Vector3[8];

for (int i = 0; i < 8; i++)
{
    coins[i] = new Vector3(
        (i & 1) == 0 ? tableau.min.x : tableau.max.x,
        (i & 2) == 0 ? tableau.min.y : tableau.max.y,
        (i & 4) == 0 ? tableau.min.z : tableau.max.z);
}

// --- la projection, en repère du root ---------------------------------------------------------------

void Cadrage(Vector3 pose, Vector3 vise, float fov, float rapport,
             out float xmin, out float xmax, out float ymin, out float ymax, out bool valide)
{
    xmin = float.MaxValue; xmax = float.MinValue;
    ymin = float.MaxValue; ymax = float.MinValue;
    valide = true;

    var avant = (vise - pose).normalized;
    var droite = Vector3.Cross(Vector3.up, avant).normalized;
    var haut = Vector3.Cross(avant, droite);

    float tanV = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
    float tanH = tanV * rapport;

    foreach (var c in coins)
    {
        var v = c - pose;
        float d = Vector3.Dot(v, avant);

        if (d <= 1e-4f) { valide = false; return; }

        xmin = Mathf.Min(xmin, Vector3.Dot(v, droite) / d / tanH);
        xmax = Mathf.Max(xmax, Vector3.Dot(v, droite) / d / tanH);
        ymin = Mathf.Min(ymin, Vector3.Dot(v, haut) / d / tanV);
        ymax = Mathf.Max(ymax, Vector3.Dot(v, haut) / d / tanV);
    }
}

// `phi` depuis la normale au plateau, en basculant vers le BAS de la table (−Z local) : phi = 0°
// est à la verticale du plateau, phi = 90° regarde la table par la tranche.
//
// ⚠️ La position ne dépend QUE de `phi` et de `L` — jamais du point visé. C'est délibéré : viser
// plus haut en déplaçant aussi la caméra ne changerait rien à la projection (translation du rig),
// donc le recentrage vertical n'aurait aucun effet. Position et visée sont deux réglages distincts.
Vector3 Pose(float phi, float L)
{
    float rad = phi * Mathf.Deg2Rad;

    return new Vector3(centre.x,
                       centre.y + L * Mathf.Cos(rad),
                       centre.z - L * Mathf.Sin(rad));
}

// Le point visé : le centre de la table, décalé verticalement de `offset`.
Vector3 Vise(float offset) { return centre + Vector3.up * offset; }

// Le milieu vertical de la projection, en unités [−1,1]. NaN si un coin passe derrière la caméra.
// Au niveau supérieur et non en fonction locale de `Resoudre` : une fonction locale ne peut pas
// capturer le paramètre `out L`.
float MilieuVertical(Vector3 pose, float offset, float fov, float rapport)
{
    float xmin, xmax, ymin, ymax;
    bool valide;

    Cadrage(pose, Vise(offset), fov, rapport, out xmin, out xmax, out ymin, out ymax, out valide);

    return valide ? (ymin + ymax) * 0.5f : float.NaN;
}

// La taille tient-elle ? On ne juge que l'ÉTENDUE ici ; le centrage est l'affaire de `Resoudre`.
bool Tient(Vector3 vise, float phi, float L, float fov, float rapport)
{
    float xmin, xmax, ymin, ymax;
    bool valide;

    Cadrage(Pose(phi, L), vise, fov, rapport, out xmin, out xmax, out ymin, out ymax, out valide);

    if (!valide) { return false; }

    // ⚠️ Le viewport va de −1 à 1 : l'écran entier vaut 2,0, pas 1,0. La couverture autorisée est
    // donc 2 × (1 − marge). Écrire `(ymax − ymin) * 0.5f <= 0.5f - marge` — ce que faisait la
    // première version — n'impose pas une marge de 4 % mais de 54 %, et pose la caméra deux fois
    // trop loin. C'est cette faute de convention, et non la géométrie, qui rendait la table si
    // petite dans le champ.
    float limite = 2f * (1f - marge);

    return (xmax - xmin) <= limite && (ymax - ymin) <= limite;
}

// Distance ET point visé, pour un couple (phi, fov, rapport) donné.
//
// Deux réglages, deux recherches :
//   1. la distance — la plus petite qui fasse tenir la TAILLE de la projection ;
//   2. le décalage vertical de la visée — la perspective agrandit le bas de la table, donc la
//      projection n'est pas centrée sur le point visé : on remonte la visée jusqu'à ce qu'elle le
//      soit, sans quoi le coin bas sort du champ.
// Les deux interagissent (recentrer change un peu la taille), d'où deux tours.
bool Resoudre(float phi, float fov, float rapport, out float L, out float offset)
{
    L = -1f;
    offset = 0f;

    for (int tour = 0; tour < 3; tour++)
    {
        float bas = 1f, haut = 900f, trouve = -1f;

        for (int i = 0; i < 60; i++)
        {
            float milieu = (bas + haut) * 0.5f;

            if (Tient(Vise(offset), phi, milieu, fov, rapport)) { trouve = milieu; haut = milieu; }
            else { bas = milieu; }
        }

        if (trouve < 0f) { return false; }

        L = trouve;

        // Recentrage : quel décalage de visée amène le milieu vertical de la projection sur 0 ?
        //
        // ⚠️ Le sens de variation, mesuré et non supposé : viser plus haut fait PIVOTER la caméra
        // vers le haut, donc descendre l'image — `ymid` DÉCROÎT avec `offset`. La première version
        // écrivait l'inverse ; elle poussait donc la visée toujours plus haut, buttant sur le
        // plafond de l'encadrement (+6,00) sur les 88 poses, chacune laissant la table hors champ.
        //
        // Le décalage utile dépend de phi (la visée glisse le long de la normale au plateau, dont
        // l'effet vertical vaut cos φ) et du fov : on l'ENCADRE par expansion avant de dichotomiser,
        // plutôt que de le supposer dans [−6, 6].
        float dist = L;
        float oBas = -0.25f, oHaut = 0.25f;
        float mBas = MilieuVertical(Pose(phi, dist), oBas, fov, rapport);
        float mHaut = MilieuVertical(Pose(phi, dist), oHaut, fov, rapport);

        for (int i = 0; i < 24; i++)
        {
            bool basUtilisable = !float.IsNaN(mBas);
            bool hautUtilisable = !float.IsNaN(mHaut);

            // Encadrement trouvé : les deux bouts sont exploitables et de signes opposés.
            if (basUtilisable && hautUtilisable && (mBas > 0f) != (mHaut > 0f)) { break; }

            // Sinon on élargit. Un bout inexploitable (un coin DERRIÈRE la caméra) est resserré :
            // c'est le côté qui a trop pivoté, donc celui dont on s'éloigne.
            if (!basUtilisable) { oBas *= 0.5f; mBas = MilieuVertical(Pose(phi, dist), oBas, fov, rapport); }
            else if (!hautUtilisable) { oHaut *= 0.5f; mHaut = MilieuVertical(Pose(phi, dist), oHaut, fov, rapport); }
            else
            {
                oBas *= 2f; oHaut *= 2f;
                mBas = MilieuVertical(Pose(phi, dist), oBas, fov, rapport);
                mHaut = MilieuVertical(Pose(phi, dist), oHaut, fov, rapport);
            }

            if (Mathf.Abs(oBas) > 500f || Mathf.Abs(oHaut) > 500f) { break; }
        }

        for (int i = 0; i < 60; i++)
        {
            float m = (oBas + oHaut) * 0.5f;
            float v = MilieuVertical(Pose(phi, dist), m, fov, rapport);

            if (float.IsNaN(v)) { break; }

            // `mBas` garde le bout BAS de l'encadrement : si le milieu est du signe du bas, il faut
            // remonter le bas ; sinon descendre le haut.
            if ((v > 0f) == (mBas > 0f)) { oBas = m; mBas = v; } else { oHaut = m; mHaut = v; }
        }

        offset = (oBas + oHaut) * 0.5f;
    }

    return true;
}

// Le cadrage tient-il vraiment — c'est-à-dire la table est-elle ENTIÈREMENT dans le champ ? La
// taille ne suffit pas : une projection centrée sur autre chose que son milieu déborde d'un côté.
bool DansLeChamp(Vector3 vise, float phi, float L, float fov, float rapport)
{
    float xmin, xmax, ymin, ymax;
    bool valide;

    Cadrage(Pose(phi, L), vise, fov, rapport, out xmin, out xmax, out ymin, out ymax, out valide);

    if (!valide) { return false; }

    float limite = 1f - marge;

    return xmin >= -limite && xmax <= limite && ymin >= -limite && ymax <= limite;
}

// Part du plateau réellement visible : on vise une grille du plan de jeu et on compte les points
// dont le premier objet touché est le plateau lui-même. Le rayon part en MONDE.
float Vu(Vector3 pose, out string pires)
{
    var compte = new System.Collections.Generic.Dictionary<string, int>();
    var monde = rt.TransformPoint(pose);

    int caches = 0, total = 0;

    for (int i = 0; i <= 6; i++)
    for (int j = 0; j <= 14; j++)
    {
        var point = rt.TransformPoint(new Vector3(
            Mathf.Lerp(tableau.min.x + 0.4f, tableau.max.x - 0.4f, i / 6f),
            0f,
            Mathf.Lerp(tableau.min.z + 0.6f, tableau.max.z - 1.2f, j / 14f)));

        var vers = point - monde;
        float portee = vers.magnitude;

        total++;

        RaycastHit h;

        if (!Physics.Raycast(monde, vers / portee, out h, portee * 1.001f, ~0, QueryTriggerInteraction.Ignore))
        {
            caches++;
            continue;
        }

        if (h.distance < portee - 0.05f)
        {
            caches++;

            string nom = h.collider.name;

            compte[nom] = compte.ContainsKey(nom) ? compte[nom] + 1 : 1;
        }
    }

    var liste = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(compte);

    liste.Sort(delegate (System.Collections.Generic.KeyValuePair<string, int> a,
                         System.Collections.Generic.KeyValuePair<string, int> b) { return b.Value.CompareTo(a.Value); });

    pires = string.Empty;

    for (int k = 0; k < liste.Count && k < 3; k++)
    {
        pires += (k > 0 ? ", " : "") + liste[k].Key + " x" + liste[k].Value;
    }

    return 1f - (float)caches / total;
}

// --- la résolution ----------------------------------------------------------------------------------

sb.AppendLine("=== poses candidates ===");
sb.AppendLine("  phi  fov      L      caméra        visée   couverture x / y   proche/loin    vu   ce qui cache");
sb.AppendLine("  ---  ---  -------  ------------   ------  -----------------  -----------   ----  -------------");

float meilleurPhi = 0f, meilleurFov = 0f, meilleurL = 0f, meilleurScore = -1f;
float meilleurVu = 0f, meilleurRatio = 0f, meilleurCx = 0f, meilleurOffset = 0f;
Vector3 meilleurePose = Vector3.zero;

var phis = new float[] { 25f, 30f, 35f, 40f, 45f, 50f, 55f, 60f, 65f, 70f, 75f };

// La focale est balayée BAS : c'est elle qui aplatit la perspective, et la première version ne
// descendait pas sous 35°.
var fovs = new float[] { 20f, 25f, 30f, 35f, 40f, 45f, 50f, 60f };

foreach (var phi in phis)
{
    foreach (var fov in fovs)
    {
        float trouve, offset;

        if (!Resoudre(phi, fov, rapportCible, out trouve, out offset))
        {
            sb.AppendLine("  " + phi.ToString("F0").PadLeft(3) + "  " + fov.ToString("F0").PadLeft(3)
                          + "   aucune distance ne cadre");
            continue;
        }

        var pose = Pose(phi, trouve);
        var vise = Vise(offset);

        float xmin, xmax, ymin, ymax;
        bool valide;
        Cadrage(pose, vise, fov, rapportCible, out xmin, out xmax, out ymin, out ymax, out valide);

        // Le recentrage doit avoir ramené la boîte DANS le champ, pas seulement à la bonne taille.
        bool dedans = DansLeChamp(vise, phi, trouve, fov, rapportCible);

        // L'agrandissement du bas de la table : rapport entre le coin le plus lointain et le plus
        // proche. C'est ce que la focale fait baisser.
        float proche = float.MaxValue, loin = 0f;

        foreach (var c in coins)
        {
            float d = (c - pose).magnitude;
            proche = Mathf.Min(proche, d);
            loin = Mathf.Max(loin, d);
        }

        string pires;
        float vu = Vu(pose, out pires);
        float ratio = loin / proche;
        float cx = xmax - xmin;

        sb.AppendLine("  " + phi.ToString("F0").PadLeft(3) + "  " + fov.ToString("F0").PadLeft(3)
                      + "  " + trouve.ToString("F1").PadLeft(7)
                      + "  " + pose.ToString("F2").PadRight(21)
                      + "  " + offset.ToString("+0.00;-0.00").PadLeft(6)
                      + "  " + cx.ToString("F3") + " / " + (ymax - ymin).ToString("F3")
                      + "   " + ratio.ToString("F2").PadLeft(8)
                      + "   " + (vu * 100f).ToString("F0").PadLeft(3) + "%  " + pires
                      + (dedans ? string.Empty : "   ⚠ la boîte sort du champ"));

        // Une pose qui laisse la table déborder n'est pas une pose : on ne la classe pas, même si
        // elle remplit bien l'écran. C'est ce que l'ancien `Tient`, qui ne jugeait que la taille,
        // laissait passer.
        if (!dedans) { continue; }

        // Le compromis : tout le plateau visible d'abord, puis l'écran le mieux rempli, puis la
        // perspective la plus douce. La largeur passe AVANT la perspective — une table large et un
        // peu déformée se joue ; une table étroite au milieu du vide, non.
        // `cx` est en unités [−1,1] : on le ramène en fraction d'écran (×0,5) pour que les trois
        // termes soient comparables — visibilité jusqu'à 5, largeur jusqu'à 3, perspective jusqu'à 2.
        float score = vu * 5f + (cx * 0.5f) * 3f - ratio * 0.8f;

        if (score > meilleurScore)
        {
            meilleurScore = score;
            meilleurPhi = phi; meilleurFov = fov; meilleurL = trouve;
            meilleurVu = vu; meilleurRatio = ratio; meilleurCx = cx;
            meilleurePose = pose; meilleurOffset = offset;
        }
    }
}

sb.AppendLine();

if (meilleurScore < 0f)
{
    sb.AppendLine("Aucune pose ne cadre la table : rien n'a été modifié.");
    return sb.ToString();
}

var visee = Vise(meilleurOffset);
var avantLocal = (visee - meilleurePose).normalized;
var rotation = rt.rotation * Quaternion.LookRotation(avantLocal, Vector3.up);
var position = rt.TransformPoint(meilleurePose);

sb.AppendLine("=== pose retenue : visibilité, puis largeur remplie, puis perspective ===");
sb.AppendLine("  phi " + meilleurPhi.ToString("F0") + "°   fov " + meilleurFov.ToString("F0")
              + "°   distance " + meilleurL.ToString("F2") + " u");
sb.AppendLine("  visée décalée de " + meilleurOffset.ToString("+0.000;-0.000") + " u au-dessus du centre"
              + "   (la perspective agrandit le bas : sans ce décalage, il sort du champ)");
// Les couvertures sont en unités de viewport [−1,1] : ×50 pour un pourcentage d'écran.
sb.AppendLine("  plateau vu " + (meilleurVu * 100f).ToString("F0") + " %"
              + "   agrandissement du bas ×" + meilleurRatio.ToString("F2")
              + "   largeur remplie " + (meilleurCx * 50f).ToString("F0") + " %");
sb.AppendLine("  position MONDE  " + position.ToString("F4"));
sb.AppendLine("  position root   " + meilleurePose.ToString("F4"));
sb.AppendLine("  rotation        " + rotation.eulerAngles.ToString("F3"));
sb.AppendLine("  angle avec la normale du plateau " + Vector3.Angle(avantLocal, Vector3.up).ToString("F2")
              + "°   (0° = à la verticale, 90° = rasant)");
sb.AppendLine();

// --- l'application -----------------------------------------------------------------------------------

var tr = camera.transform;

UnityEditor.Undo.RecordObject(tr, "Poser la caméra de jeu");
UnityEditor.Undo.RecordObject(cam, "Poser la caméra de jeu");

tr.position = position;
tr.rotation = rotation;
cam.fieldOfView = meilleurFov;

// Le `near` doit rester devant le point le plus proche de la table, sinon on voit à travers.
float distMin = float.MaxValue;

foreach (var c in coins) { distMin = Mathf.Min(distMin, (rt.TransformPoint(c) - position).magnitude); }

sb.AppendLine("=== appliqué à 'Main Camera' ===");
sb.AppendLine("  position  " + tr.position.ToString("F4"));
sb.AppendLine("  rotation  " + tr.eulerAngles.ToString("F3"));
sb.AppendLine("  fov       " + cam.fieldOfView.ToString("F1"));
sb.AppendLine("  near      " + cam.nearClipPlane.ToString("F2") + " u   (coin le plus proche à "
              + distMin.ToString("F2") + " u"
              + (cam.nearClipPlane < distMin ? "   ✓ rien de coupé" : "   ⚠ le near coupe la table") + ")");
sb.AppendLine();

// --- contrôle croisé : ma projection contre celle d'Unity ------------------------------------------
//
// C'est ce contrôle qui manquait, et c'est son absence qui a laissé passer les deux erreurs de la
// première version. On projette les mêmes 8 coins par les deux chemins, au MÊME rapport d'écran :
//   • le mien, en repère du root (celui qui a servi à résoudre) ;
//   • celui d'Unity, `WorldToViewportPoint`, sur la vraie caméra.
// S'ils concordent, la résolution est fiable. S'ils divergent, c'est le solveur qui a tort — et non
// la scène, comme le rendu de contrôle l'avait montré.

sb.AppendLine("=== contrôle croisé : projection du solveur contre WorldToViewportPoint ===");
sb.AppendLine("  (les deux au rapport de la fenêtre Game, " + cam.aspect.ToString("F3") + ")");

{
    float ax, bx, ay, by;
    bool ok;

    Cadrage(meilleurePose, visee, meilleurFov, cam.aspect, out ax, out bx, out ay, out by, out ok);

    // Repère viewport du solveur : [−1,1] → [0,1].
    float sXmin = (ax + 1f) * 0.5f, sXmax = (bx + 1f) * 0.5f;
    float sYmin = (ay + 1f) * 0.5f, sYmax = (by + 1f) * 0.5f;

    float uXmin = float.MaxValue, uXmax = float.MinValue;
    float uYmin = float.MaxValue, uYmax = float.MinValue;

    foreach (var c in coins)
    {
        var v = cam.WorldToViewportPoint(rt.TransformPoint(c));

        uXmin = Mathf.Min(uXmin, v.x); uXmax = Mathf.Max(uXmax, v.x);
        uYmin = Mathf.Min(uYmin, v.y); uYmax = Mathf.Max(uYmax, v.y);
    }

    sb.AppendLine("                     x min    x max    y min    y max");
    sb.AppendLine("    solveur       " + sXmin.ToString("F4").PadLeft(8) + " " + sXmax.ToString("F4").PadLeft(8)
                  + " " + sYmin.ToString("F4").PadLeft(8) + " " + sYmax.ToString("F4").PadLeft(8));
    sb.AppendLine("    Unity         " + uXmin.ToString("F4").PadLeft(8) + " " + uXmax.ToString("F4").PadLeft(8)
                  + " " + uYmin.ToString("F4").PadLeft(8) + " " + uYmax.ToString("F4").PadLeft(8));

    float ecart = Mathf.Max(
        Mathf.Max(Mathf.Abs(sXmin - uXmin), Mathf.Abs(sXmax - uXmax)),
        Mathf.Max(Mathf.Abs(sYmin - uYmin), Mathf.Abs(sYmax - uYmax)));

    sb.AppendLine("    écart max     " + ecart.ToString("F5")
                  + (ecart < 0.002f ? "   ✓ le solveur et Unity concordent"
                                    : "   ⚠ DIVERGENCE — le solveur est faux, ne pas se fier à ses chiffres"));
    sb.AppendLine();
}

// Et le cadrage tel qu'il sera vu, aux rapports d'écran probables. Le fov est VERTICAL : un écran
// plus large ajoute de la marge sur les côtés sans rien changer à la hauteur ; un écran plus étroit
// en retire. C'est donc le rapport étroit (4/3) qui contraint.
sb.AppendLine("=== cadrage selon le rapport d'écran (fov vertical : la hauteur ne bouge pas) ===");

foreach (var cas in new float[] { 21f / 9f, 16f / 9f, 16f / 10f, 4f / 3f })
{
    float ax, bx, ay, by;
    bool ok;

    Cadrage(meilleurePose, visee, meilleurFov, cas, out ax, out bx, out ay, out by, out ok);

    // Marge de 1 % : le solveur travaille à 4 %, mais un écran plus étroit que la cible 16/9 rogne
    // la largeur — c'est justement ce que ce tableau sert à voir.
    bool deborde = ax < -1f || bx > 1f || ay < -1f || by > 1f;

    sb.AppendLine("  " + cas.ToString("F3") + "   table "
                  + ((bx - ax) * 50f).ToString("F1").PadLeft(5) + " % de large × "
                  + ((by - ay) * 50f).ToString("F1").PadLeft(5) + " % de haut"
                  + (deborde ? "   ⚠ déborde" : "   ✓ dans le champ")
                  + (Mathf.Abs(cas - cam.aspect) < 0.01f ? "   ← la fenêtre Game actuelle" : string.Empty));
}

sb.AppendLine();
sb.AppendLine("Ctrl+Z annule. La scène est marquée modifiée mais n'est **pas** enregistrée : Ctrl+S pour garder.");

UnityEditor.EditorUtility.SetDirty(cam);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
    UnityEngine.SceneManagement.SceneManager.GetActiveScene());

return sb.ToString();
