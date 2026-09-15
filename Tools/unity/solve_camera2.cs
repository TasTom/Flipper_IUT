// Seconde passe du solveur de caméra : les angles RASANTS, et la visibilité du plateau.
//
// La première passe (`solve_camera.cs`) n'explorait que phi 22° → 42°, et toutes ses poses
// laissaient la table n'occuper que ~30 % de la largeur de l'écran. Ce n'est pas un réglage à
// corriger, c'est une conséquence de la géométrie : la table est un rectangle de rapport 1,87
// (long / large) et l'écran un rectangle de rapport 1,78 (large / haut). L'axe LONG de la table
// tombe donc sur l'axe COURT de l'écran, et cadrer la longueur laisse la largeur vide.
//
// Le seul levier est l'inclinaison : le raccourcissement en perspective réduit la longueur
// projetée sans toucher à la largeur. Le rapport projeté vaut ≈ 1,87 x 1,78 x cos(phi) ; il passe
// sous 1,78 vers phi ≈ 57°, et c'est là que le cadrage cesse d'être limité par la hauteur.
//
// Mais un angle rasant a deux prix, que ce fichier mesure aussi :
//   • la perspective : le bas de la table est beaucoup plus près que le haut, donc agrandi ;
//   • l'occultation : le bandeau, les murs et le caisson peuvent cacher le plateau.
//
// D'où la colonne « vu » : on tire un rayon de la caméra vers une grille de points du plateau
// (le plan y = 0 du root, cf. CLAUDE.md) et on compte ceux dont le premier objet touché n'est pas
// le plateau lui-même. C'est la mesure qui décide — un cadrage parfait d'un plateau caché ne vaut
// rien.
//
// Rien n'est modifié dans la scène : la projection est calculée à la main.

var sb = new System.Text.StringBuilder();

const float marge = 0.04f;
const float aspect = 16f / 9f;

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "'PinballTable' introuvable."; }

var rt = racine.transform;

// Le volume à cadrer : la table, sans ce qui est sous le plateau.
var boiteMin = new Vector3(-4.386f, -0.60f, 0.285f);
var boiteMax = new Vector3(5.053f, 2.426f, 17.928f);

var centre = (boiteMin + boiteMax) * 0.5f;

var cibles = new Vector3[8];
int n = 0;

for (int x = 0; x <= 1; x++)
for (int y = 0; y <= 1; y++)
for (int z = 0; z <= 1; z++)
{
    cibles[n++] = rt.TransformPoint(new Vector3(
        x == 0 ? boiteMin.x : boiteMax.x,
        y == 0 ? boiteMin.y : boiteMax.y,
        z == 0 ? boiteMin.z : boiteMax.z));
}

sb.AppendLine("=== volume à cadrer (repère du root) ===");
sb.AppendLine("  min " + boiteMin.ToString("F3") + "   max " + boiteMax.ToString("F3"));
sb.AppendLine("  centre " + centre.ToString("F3"));
sb.AppendLine("  cadre " + aspect.ToString("F3") + " (16/9), marge " + (marge * 100f).ToString("F0") + " %");
sb.AppendLine();

bool Cadrage(Vector3 position, Vector3 pointVise, float fov,
             out float xmin, out float xmax, out float ymin, out float ymax)
{
    xmin = float.MaxValue; xmax = float.MinValue;
    ymin = float.MaxValue; ymax = float.MinValue;

    var avant = (pointVise - position).normalized;
    var droite = Vector3.Cross(rt.up, avant).normalized;
    var haut = Vector3.Cross(avant, droite);

    float tanV = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
    float tanH = tanV * aspect;

    foreach (var c in cibles)
    {
        var v = c - position;
        float d = Vector3.Dot(v, avant);

        if (d <= 1e-4f) { return false; }

        float vx = Vector3.Dot(v, droite) / d / tanH;
        float vy = Vector3.Dot(v, haut) / d / tanV;

        xmin = Mathf.Min(xmin, vx); xmax = Mathf.Max(xmax, vx);
        ymin = Mathf.Min(ymin, vy); ymax = Mathf.Max(ymax, vy);
    }

    return true;
}

// `phi` est compté depuis la normale au plateau, en basculant vers le BAS de la table (−Z local).
Vector3 Pose(float phi, float L, Vector3 pointVise)
{
    float rad = phi * Mathf.Deg2Rad;

    return rt.TransformPoint(new Vector3(pointVise.x,
                                         pointVise.y + L * Mathf.Cos(rad),
                                         pointVise.z - L * Mathf.Sin(rad)));
}

bool Tient(Vector3 position, Vector3 pointVise, float fov)
{
    float xmin, xmax, ymin, ymax;

    if (!Cadrage(position, pointVise, fov, out xmin, out xmax, out ymin, out ymax)) { return false; }

    return (xmax - xmin) * 0.5f <= (0.5f - marge) && (ymax - ymin) * 0.5f <= (0.5f - marge);
}

// Part du plateau réellement visible depuis cette pose. On vise une grille du plan y = 0 du root.
float Vu(Vector3 position, out int caches, out int total, out string pires)
{
    caches = 0;
    total = 0;

    var compte = new System.Collections.Generic.Dictionary<string, int>();

    for (int i = 0; i <= 6; i++)
    for (int j = 0; j <= 14; j++)
    {
        var local = new Vector3(
            Mathf.Lerp(-3.30f, 3.30f, i / 6f),
            0f,
            Mathf.Lerp(0.60f, 17.40f, j / 14f));

        var point = rt.TransformPoint(local);
        var vers = point - position;
        float portee = vers.magnitude;

        total++;

        RaycastHit h;

        if (!Physics.Raycast(position, vers / portee, out h, portee * 1.001f, ~0, QueryTriggerInteraction.Ignore))
        {
            caches++;   // rien touché : le plateau n'est même pas là
            continue;
        }

        if (h.distance < portee - 0.05f)
        {
            caches++;

            string nom = h.collider.name;

            compte[nom] = compte.ContainsKey(nom) ? compte[nom] + 1 : 1;
        }
    }

    // Les trois noms qui cachent le plus, pour savoir *quoi* bouche la vue.
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

sb.AppendLine("=== poses candidates (angles rasants) ===");
sb.AppendLine("  phi  fov      L     caméra (repère root)          couverture x / y    proche/loin   vu");
sb.AppendLine("  ---  ---   -------  ----------------------------   -----------------   -----------   ----");

float meilleurPhi = 0f, meilleurFov = 0f, meilleurL = 0f, meilleurScore = -1f;
Vector3 meilleurePos = Vector3.zero;

var phis = new float[] { 30f, 35f, 40f, 45f, 50f, 55f, 60f, 65f, 70f, 75f };
var fovs = new float[] { 35f, 40f, 45f, 50f, 55f, 60f };

foreach (var phi in phis)
{
    foreach (var fov in fovs)
    {
        float bas = 1f, haut = 600f, trouve = -1f;

        for (int i = 0; i < 60; i++)
        {
            float milieu = (bas + haut) * 0.5f;

            if (Tient(Pose(phi, milieu, centre), centre, fov)) { trouve = milieu; haut = milieu; }
            else { bas = milieu; }
        }

        if (trouve < 0f)
        {
            sb.AppendLine("  " + phi.ToString("F0").PadLeft(3) + "  " + fov.ToString("F0").PadLeft(3)
                          + "   aucune distance ne cadre");
            continue;
        }

        var pos = Pose(phi, trouve, centre);
        var posLocale = rt.InverseTransformPoint(pos);

        float xmin, xmax, ymin, ymax;
        Cadrage(pos, centre, fov, out xmin, out xmax, out ymin, out ymax);

        // Distances au coin le plus proche et au plus loin : c'est l'écart entre les deux qui dit
        // de combien le bas de la table est agrandi par rapport au haut.
        float proche = float.MaxValue, loin = 0f;

        foreach (var c in cibles)
        {
            float d = (c - pos).magnitude;
            proche = Mathf.Min(proche, d);
            loin = Mathf.Max(loin, d);
        }

        int caches, total;
        string pires;
        float vu = Vu(pos, out caches, out total, out pires);

        sb.AppendLine("  " + phi.ToString("F0").PadLeft(3) + "  " + fov.ToString("F0").PadLeft(3)
                      + "  " + trouve.ToString("F1").PadLeft(8)
                      + "  " + posLocale.ToString("F2").PadRight(28)
                      + "  " + (xmax - xmin).ToString("F3") + " / " + (ymax - ymin).ToString("F3")
                      + "   " + (loin / proche).ToString("F2").PadLeft(6)
                      + "        " + (vu * 100f).ToString("F0").PadLeft(3) + " %"
                      + "  " + pires);

        // Le meilleur compromis : tout le plateau visible d'abord, puis la perspective la plus
        // douce (rapport proche/loin le plus petit), puis l'écran le mieux rempli en largeur.
        float score = vu * 5f - (loin / proche) - (1f - (xmax - xmin)) * 1.5f;

        if (score > meilleurScore)
        {
            meilleurScore = score;
            meilleurPhi = phi; meilleurFov = fov; meilleurL = trouve; meilleurePos = pos;
        }
    }
}

sb.AppendLine();
sb.AppendLine("  (couverture dans [−1,1] ; « proche/loin » = agrandissement du bas de la table ;");
sb.AppendLine("   « vu » = part du plateau dont le premier objet touché est le plateau lui-même)");

if (meilleurScore < 0f)
{
    sb.AppendLine();
    sb.AppendLine("Aucune pose ne cadre la table.");
    return sb.ToString();
}

var avant = (centre - meilleurePos).normalized;
var rotation = Quaternion.LookRotation(avant, rt.up);

sb.AppendLine();
sb.AppendLine("=== meilleur compromis selon le score (visibilité, puis perspective, puis largeur) ===");
sb.AppendLine("  phi " + meilleurPhi.ToString("F0") + "°   fov " + meilleurFov.ToString("F0")
              + "°   L " + meilleurL.ToString("F2"));
sb.AppendLine("  position MONDE  " + meilleurePos.ToString("F4"));
sb.AppendLine("  position root   " + rt.InverseTransformPoint(meilleurePos).ToString("F4"));
sb.AppendLine("  rotation        " + rotation.eulerAngles.ToString("F3"));
sb.AppendLine("  avant (monde)   " + avant.ToString("F4"));
sb.AppendLine("  distance au centre de la table " + (centre - meilleurePos).magnitude.ToString("F3") + " u");
sb.AppendLine("  angle avec la normale du plateau "
              + Vector3.Angle(avant, rt.up).ToString("F2") + "°"
              + "   (0° = à la verticale, 90° = rasant)");
sb.AppendLine("  near actuel 0,3 u — sous la main : à garder si le bord bas de la table reste au-delà");

return sb.ToString();
