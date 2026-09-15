// Cherche une pose de caméra qui cadre la table, **sans rien modifier dans la scène**.
//
// La caméra est décrite dans le repère du root `PinballTable`, qui est incliné de −7° : c'est le
// repère dans lequel le plateau est un rectangle plat, donc celui dans lequel un cadrage se
// raisonne. Elle est définie par trois nombres :
//
//   • `phi`   — l'angle entre la normale du plateau et l'axe de visée. 0° = à la verticale du
//               plateau, 90° = rasante. C'est l'inclinaison de la vue.
//   • `fov`   — le champ vertical.
//   • `L`     — la distance du point visé.
//
// Pour chaque couple (`phi`, `fov`) on cherche la plus petite distance `L` qui fait tenir les huit
// coins de la table dans le cadre, avec une marge. La projection est calculée à la main (produit
// scalaire sur la base de la caméra) plutôt qu'avec `Camera.WorldToViewportPoint` : cela évite de
// créer un objet temporaire dans la scène pour mesurer, donc de la marquer modifiée.
//
// Le cadre de référence est 16/9 — à corriger si la vue de jeu est dans un autre format.

var sb = new System.Text.StringBuilder();

const float marge = 0.04f;      // bord laissé libre de chaque côté, en fraction de l'écran
const float aspect = 16f / 9f;

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "'PinballTable' introuvable."; }

var rt = racine.transform;

// Le volume à cadrer, en repère du root. On écarte ce qui est SOUS le plateau (le maillage de la
// table descend à y −2,377) : cette partie est de toute façon cachée par le plateau lui-même, et
// la cadrer ferait reculer la caméra pour du vide.
var boiteMin = new Vector3(-4.386f, -0.60f, 0.285f);
var boiteMax = new Vector3(5.053f, 2.426f, 17.928f);

var centre = (boiteMin + boiteMax) * 0.5f;

// Les huit coins, en MONDE.
var cibles = new Vector3[8];
int n = 0;

for (int x = 0; x <= 1; x++)
for (int y = 0; y <= 1; y++)
for (int z = 0; z <= 1; z++)
{
    var local = new Vector3(
        x == 0 ? boiteMin.x : boiteMax.x,
        y == 0 ? boiteMin.y : boiteMax.y,
        z == 0 ? boiteMin.z : boiteMax.z);

    cibles[n++] = rt.TransformPoint(local);
}

sb.AppendLine("=== volume à cadrer (repère du root) ===");
sb.AppendLine("  min " + boiteMin.ToString("F3") + "   max " + boiteMax.ToString("F3"));
sb.AppendLine("  taille " + (boiteMax - boiteMin).ToString("F3"));
sb.AppendLine("  centre " + centre.ToString("F3"));
sb.AppendLine("  cadre de référence " + aspect.ToString("F3") + " (16/9), marge "
              + (marge * 100f).ToString("F0") + " %");
sb.AppendLine();

// Couverture du cadre obtenue pour une pose donnée. Renvoie aussi la plus petite et la plus grande
// coordonnée de viewport, pour juger du centrage.
bool Cadrage(Vector3 position, Vector3 pointVise, float fov, out float xmin, out float xmax,
             out float ymin, out float ymax)
{
    xmin = float.MaxValue; xmax = float.MinValue;
    ymin = float.MaxValue; ymax = float.MinValue;

    var avant = (pointVise - position).normalized;

    // Le « haut » de l'écran est celui du plateau : la vue reste d'aplomb sur la table.
    var droite = Vector3.Cross(rt.up, avant).normalized;
    var haut = Vector3.Cross(avant, droite);

    float tanV = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
    float tanH = tanV * aspect;

    foreach (var c in cibles)
    {
        var v = c - position;
        float d = Vector3.Dot(v, avant);

        if (d <= 1e-4f) { return false; }        // un coin derrière la caméra : pose invalide

        float vx = Vector3.Dot(v, droite) / d / tanH;
        float vy = Vector3.Dot(v, haut) / d / tanV;

        xmin = Mathf.Min(xmin, vx); xmax = Mathf.Max(xmax, vx);
        ymin = Mathf.Min(ymin, vy); ymax = Mathf.Max(ymax, vy);
    }

    return true;
}

// Position de la caméra pour un angle `phi` et une distance `L` donnés, visant `pointVise`.
// `phi` est compté depuis la normale au plateau, en basculant vers le BAS de la table (−Z local).
Vector3 Pose(float phi, float L, Vector3 pointVise)
{
    float rad = phi * Mathf.Deg2Rad;
    var local = new Vector3(pointVise.x, pointVise.y + L * Mathf.Cos(rad), pointVise.z - L * Mathf.Sin(rad));
    return rt.TransformPoint(local);
}

// Le cadrage tient-il, pour cette pose ?
bool Tient(Vector3 position, Vector3 pointVise, float fov)
{
    float xmin, xmax, ymin, ymax;

    if (!Cadrage(position, pointVise, fov, out xmin, out xmax, out ymin, out ymax)) { return false; }

    float mx = (xmax - xmin) * 0.5f;
    float my = (ymax - ymin) * 0.5f;

    return mx <= (0.5f - marge) && my <= (0.5f - marge);
}

sb.AppendLine("=== poses candidates ===");
sb.AppendLine("  phi   fov     L        caméra (locale)              couverture x / y");
sb.AppendLine("  ---   ---   -------   ---------------------------   ------------------");

var phis = new float[] { 22f, 26f, 30f, 34f, 38f, 42f };
var fovs = new float[] { 35f, 40f, 45f, 50f, 55f, 60f };

foreach (var phi in phis)
{
    foreach (var fov in fovs)
    {
        // Plus petite distance qui fait tenir le volume, par dichotomie.
        float bas = 1f, haut = 400f, trouve = -1f;

        for (int i = 0; i < 60; i++)
        {
            float milieu = (bas + haut) * 0.5f;

            if (Tient(Pose(phi, milieu, centre), centre, fov)) { trouve = milieu; haut = milieu; }
            else { bas = milieu; }
        }

        if (trouve < 0f)
        {
            sb.AppendLine("  " + phi.ToString("F0").PadLeft(3) + "   " + fov.ToString("F0").PadLeft(3)
                          + "   aucune distance ne cadre (coin derrière la caméra)");
            continue;
        }

        var pos = Pose(phi, trouve, centre);
        var posLocale = rt.InverseTransformPoint(pos);

        float xmin, xmax, ymin, ymax;
        Cadrage(pos, centre, fov, out xmin, out xmax, out ymin, out ymax);

        sb.AppendLine("  " + phi.ToString("F0").PadLeft(3) + "   " + fov.ToString("F0").PadLeft(3)
                      + "   " + trouve.ToString("F1").PadLeft(7)
                      + "   " + posLocale.ToString("F2").PadRight(27)
                      + "   " + (xmax - xmin).ToString("F3") + " / " + (ymax - ymin).ToString("F3")
                      + (Tient(pos, centre, fov) ? "  ✓" : "  ⚠"));
    }
}

sb.AppendLine();
sb.AppendLine("Note : la couverture est mesurée dans [−1,1] → une valeur ≤ "
              + ((1f - 2f * marge).ToString("F2")) + " tient dans le cadre avec la marge.");

return sb.ToString();
