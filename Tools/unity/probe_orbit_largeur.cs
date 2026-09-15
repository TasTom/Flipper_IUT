// Largeur libre le long de l'orbite + position des poteaux. LECTURE SEULE.
//
// La première sonde (x 0,5…4,8) a montré un rétrécissement vers z ≈ 15 et une
// arche haute fermée — mais sa grille coupait le virage (x < 0,5 manquant).
// Ici : grille x −3,7…4,8, intervalles libres par rangée (= largeur des couloirs),
// et centroïdes des poteaux suspects (Post_*, plots Ø 0,53 > bille Ø 0,45 : un plot
// au milieu d'un couloir le bouche).

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;

if (rt == null) { return "PinballTable introuvable."; }

float y = 0.24f;
float r = 0.225f;
float pas = 0.15f;
float x0 = -3.7f, x1 = 4.8f, z0 = 13.0f, z1 = 17.6f;

int nx = Mathf.RoundToInt((x1 - x0) / pas) + 1;
int nz = Mathf.RoundToInt((z1 - z0) / pas) + 1;

var libre = new bool[nx, nz];

for (int ix = 0; ix < nx; ix++)
{
    for (int iz = 0; iz < nz; iz++)
    {
        Vector3 p = rt.TransformPoint(new Vector3(x0 + ix * pas, y, z0 + iz * pas));
        libre[ix, iz] = Physics.OverlapSphere(p, r, ~0, QueryTriggerInteraction.Ignore).Length == 0;
    }
}

sb.AppendLine("intervalles libres par rangée (largeur en u, bille = 0,45) :");

for (int iz = nz - 1; iz >= 0; iz--)
{
    var ligne = new System.Text.StringBuilder();
    ligne.Append("z " + (z0 + iz * pas).ToString("F2") + " :");

    int debut = -1;

    for (int ix = 0; ix <= nx; ix++)
    {
        bool l = ix < nx && libre[ix, iz];

        if (l && debut < 0) { debut = ix; }

        if (!l && debut >= 0)
        {
            float xa = x0 + debut * pas;
            float xb = x0 + (ix - 1) * pas;
            float larg = (ix - debut) * pas;
            ligne.Append("  [" + xa.ToString("F2") + "→" + xb.ToString("F2") + " = " + larg.ToString("F2") + "u"
                         + (larg < 0.45f ? " ⚠" : "") + "]");
            debut = -1;
        }
    }

    sb.AppendLine(ligne.ToString());
}

// Poteaux : centroïde monde → local table.
sb.AppendLine("poteaux (local table) :");

foreach (var t in racine.GetComponentsInChildren<Transform>())
{
    if (!t.name.StartsWith("Post")) { continue; }

    Vector3 local = rt.InverseTransformPoint(t.position);
    sb.AppendLine("  " + t.name + " : x " + local.x.ToString("F3")
                  + "  z " + local.z.ToString("F3"));
}

return sb.ToString();
