// Où la bille peut-elle SORTIR ? LECTURE SEULE.
//
// `DrainZone` n'a toujours pas de collider, et sa taille « décide des billes comptées
// perdues » (CLAUDE.md) — c'est un choix de jeu, mais il est contraint par la géométrie :
// la zone doit couvrir l'ouverture basse, et RIEN de plus, sinon elle compte des billes
// qui roulaient encore sur le plateau.
//
// Plutôt que de lire des bornes et d'en déduire une ouverture, on la mesure : un balayage
// de rayons verticaux qui répond, point par point, « y a-t-il un sol ici ? ».
// Le rayon part de 3 u au-dessus du plateau et descend le long de `-up` DE LA TABLE (et non
// du monde) : la table est inclinée de 7°, un rayon vertical monde ne lui serait pas
// perpendiculaire et traverserait le plateau en biais.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
var rt = racine.transform;
var up = rt.up;
var bas = -up;

// Un sol « de plateau » : la surface est à y = 0 dans le repère de la table.
// En deçà, c'est un trou (on tombe sur la cuve, bien plus bas).
float x0 = -4.6f, x1 = 5.3f, z0 = -0.4f, z1 = 1.6f;
float pas = 0.15f;

int nx = Mathf.CeilToInt((x1 - x0) / pas) + 1;
int nz = Mathf.CeilToInt((z1 - z0) / pas) + 1;

// en-tête : les x, tous les 5 points pour rester lisible
sb.AppendLine("sol du plateau vu de dessus — '#' sol à y≈0, ' ' TROU, 'o' autre (mur, pièce)");
sb.AppendLine("x de " + x0.ToString("F2") + " à " + x1.ToString("F2") + " pas " + pas.ToString("F3")
    + " ; z de " + z0.ToString("F2") + " à " + z1.ToString("F2"));
sb.AppendLine();

var ligneX = new System.Text.StringBuilder("  z\\x  ");
for (int i = 0; i < nx; i++) { ligneX.Append(i % 5 == 0 ? "|" : " "); }
sb.AppendLine(ligneX.ToString());

for (int j = nz - 1; j >= 0; j--)
{
    float z = z0 + j * pas;
    var ligne = new System.Text.StringBuilder(z.ToString("F2").PadLeft(5) + "  ");

    for (int i = 0; i < nx; i++)
    {
        float x = x0 + i * pas;
        Vector3 origine = rt.TransformPoint(new Vector3(x, 3f, z));

        if (Physics.Raycast(origine, bas, out RaycastHit h, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            float ly = rt.InverseTransformPoint(h.point).y;
            ligne.Append(Mathf.Abs(ly) < 0.15f ? '#' : 'o');
        }
        else
        {
            ligne.Append(' ');
        }
    }

    sb.AppendLine(ligne.ToString());
}

// --- les murs du bas, pour nommer les bords ----------------------------------------------
sb.AppendLine();
sb.AppendLine("=== maillages de 'Walls' dont la boîte descend sous z = 1,5 (repère table) ===");

Transform pt = null;
var table = rt.Find("Table");

if (table != null) { foreach (Transform e in table) { if (e.name.StartsWith("Pinball_Table")) { pt = e; } } }

if (pt != null)
{
    foreach (var f in pt.GetComponentsInChildren<MeshFilter>(true))
    {
        if (f.transform.parent == null || f.transform.parent.name != "Walls") { continue; }

        var mr = f.GetComponent<MeshRenderer>();
        if (mr == null) { continue; }

        Bounds b = mr.bounds;
        Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (int i = 0; i < 8; i++)
        {
            Vector3 coin = b.center + new Vector3(
                (i & 1) == 0 ? -b.extents.x : b.extents.x,
                (i & 2) == 0 ? -b.extents.y : b.extents.y,
                (i & 4) == 0 ? -b.extents.z : b.extents.z);
            Vector3 ll = rt.InverseTransformPoint(coin);
            mn = Vector3.Min(mn, ll); mx = Vector3.Max(mx, ll);
        }

        if (mn.z > 1.5f) { continue; }

        sb.AppendLine("  '" + f.name + "'  x [" + mn.x.ToString("F3") + " → " + mx.x.ToString("F3")
            + "]  y [" + mn.y.ToString("F3") + " → " + mx.y.ToString("F3")
            + "]  z [" + mn.z.ToString("F3") + " → " + mx.z.ToString("F3") + "]");
    }
}

return sb.ToString();
