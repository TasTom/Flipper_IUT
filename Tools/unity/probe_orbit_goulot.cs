// Où la bille (Ø 0,45 u) peut-elle rouler en haut à droite ? LECTURE SEULE.
//
// Retour terrain : la bille ne passe plus en haut à droite (voir capture).
// Grille de `CheckSphere` (rayon 0,225, centre à y 0,24 = bille qui roule) sur la
// région x 0,5…4,8 / z 13,0…17,6, puis flood-fill depuis le bas : un chemin continu
// jusqu'en haut prouve le passage, son absence le goulot. Les noms des maillages
// touchés désignent les coupables.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;

if (rt == null) { return "PinballTable introuvable."; }

// La table est inclinée (−7° sur X) : la grille est exprimée en LOCAL table
// (y = 0 surface du plateau) puis convertie en monde pour chaque sondage.
float y = 0.24f;
float r = 0.225f;
float pas = 0.15f;
float x0 = 0.5f, x1 = 4.8f, z0 = 13.0f, z1 = 17.6f;

int nx = Mathf.RoundToInt((x1 - x0) / pas) + 1;
int nz = Mathf.RoundToInt((z1 - z0) / pas) + 1;

sb.AppendLine("grille " + nx + "x" + nz + " pas " + pas + " r " + r + " y " + y);

var libre = new bool[nx, nz];
var noms = new System.Collections.Generic.Dictionary<string, int>();

for (int ix = 0; ix < nx; ix++)
{
    for (int iz = 0; iz < nz; iz++)
    {
        Vector3 p = rt.TransformPoint(new Vector3(x0 + ix * pas, y, z0 + iz * pas));
        var hits = Physics.OverlapSphere(p, r, ~0, QueryTriggerInteraction.Ignore);
        libre[ix, iz] = hits.Length == 0;

        if (hits.Length > 0)
        {
            foreach (var h in hits)
            {
                string n = h.name;
                if (!noms.ContainsKey(n)) { noms[n] = 0; }
                noms[n]++;
            }
        }
    }
}

// Flood-fill depuis la rangée basse (z0).
var vu = new bool[nx, nz];
var file = new System.Collections.Generic.Queue<int[]>();

for (int ix = 0; ix < nx; ix++)
{
    if (libre[ix, 0]) { vu[ix, 0] = true; file.Enqueue(new int[] { ix, 0 }); }
}

int[] dx = new int[] { 1, -1, 0, 0 };
int[] dz = new int[] { 0, 0, 1, -1 };

while (file.Count > 0)
{
    var c = file.Dequeue();

    for (int d = 0; d < 4; d++)
    {
        int ax = c[0] + dx[d], az = c[1] + dz[d];

        if (ax >= 0 && ax < nx && az >= 0 && az < nz && libre[ax, az] && !vu[ax, az])
        {
            vu[ax, az] = true;
            file.Enqueue(new int[] { ax, az });
        }
    }
}

bool atteintHaut = false;

for (int ix = 0; ix < nx; ix++)
{
    if (vu[ix, nz - 1]) { atteintHaut = true; }
}

sb.AppendLine(atteintHaut
    ? "→ un chemin CONTINU bas→haut existe ✓ (pas de goulot total)"
    : "→ AUCUN chemin continu bas→haut   ⚠ GOULOT");

// Carte (haut en premier) : '.' = passage, 'o' = libre isolé, '#' = bloqué.
sb.AppendLine("carte (x " + x0 + "→" + x1 + " , z " + z1 + " en haut) :");

for (int iz = nz - 1; iz >= 0; iz--)
{
    var ligne = new System.Text.StringBuilder();
    ligne.Append("z " + (z0 + iz * pas).ToString("F2") + " ");

    for (int ix = 0; ix < nx; ix++)
    {
        ligne.Append(!libre[ix, iz] ? '#' : (vu[ix, iz] ? '.' : 'o'));
    }

    sb.AppendLine(ligne.ToString());
}

sb.AppendLine("maillages touchés (coups de sonde) :");

foreach (var kv in noms)
{
    sb.AppendLine("  " + kv.Key + " : " + kv.Value);
}

return sb.ToString();
