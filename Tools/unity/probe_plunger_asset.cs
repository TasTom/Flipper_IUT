// SONDE 2 — Plunger.fbx : orientation fine (profil le long de l'axe long).
//
// La sonde 1 a etabli la boite monde (23,5 x 23,5 x 165,1 mm) et l'axe long = Z monde. Mais
// decouper la BOITE d'un maillage en tranches ne dit rien : une boite allongee recouvre toutes
// les tranches avec la meme section. Pour savoir ou est la partie large (poignee / ressort) et
// ou la partie fine (la tige qui pousse la bille), il faut les SOMMETS.
//
// Cette sonde lit donc mesh.vertices et releve, tranche par tranche le long de Z monde, le rayon
// transversal maximal — le profil. Zero ecriture.

const string chemin = "Assets/Models/Parts/Miscellaneous/Plunger.fbx";
const float upm = 16.66667f;

System.Func<Vector3, string> V = v =>
    "(" + v.x.ToString("F4") + ", " + v.y.ToString("F4") + ", " + v.z.ToString("F4") + ")";
System.Func<float, string> mm = f => (f * 1000f / upm).ToString("F2") + " mm";

var sb = new System.Text.StringBuilder();
sb.AppendLine("=== SONDE 2 Plunger : profil le long de l'axe long ===");

var racine = AssetDatabase.LoadAssetAtPath<GameObject>(chemin);
if (racine == null) { return "LoadAssetAtPath -> NULL"; }

var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var copie = PrefabUtility.InstantiatePrefab(racine, preview) as GameObject;
if (copie == null)
{
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
    return "InstantiatePrefab -> NULL";
}

var renderers = copie.GetComponentsInChildren<MeshRenderer>(true);

// --- les sommets sont-ils lisibles ? (isReadable = False dans l'importeur) -------------------

sb.AppendLine();
sb.AppendLine("=== lisibilite des maillages ===");

int lus = 0;

foreach (var mr in renderers)
{
    var mf = mr.GetComponent<MeshFilter>();
    var mesh = mf != null ? mf.sharedMesh : null;
    if (mesh == null) { sb.AppendLine("  '" + mr.name + "' : sans maillage"); continue; }

    int n = -1;
    string etat;

    try { n = mesh.vertices.Length; etat = "vertices OK"; }
    catch (System.Exception e) { etat = "EXCEPTION : " + e.GetType().Name; }

    sb.AppendLine("  '" + mesh.name + "'  isReadable=" + mesh.isReadable
                  + "  subMeshCount=" + mesh.subMeshCount
                  + "  vertexCount=" + mesh.vertexCount
                  + "  -> mesh.vertices.Length = " + n + "   (" + etat + ")");

    if (n > 0) { lus++; }
}

sb.AppendLine("  maillages dont les sommets sont lisibles : " + lus + " / " + renderers.Length);

// --- profil : rayon transversal max par tranche de 5% le long de Z monde ---------------------

sb.AppendLine();
sb.AppendLine("=== profil (Z monde) — rayon transversal max, par tranche de 5% ===");

// Boite totale (8 coins), pour cadrer les tranches.
System.Func<Bounds, Matrix4x4, Bounds> coinsVersMonde = (b, m) =>
{
    var e = b.extents; var c = b.center;
    var r = new Bounds(m.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y, -e.z)), Vector3.zero);
    for (int i = 0; i < 8; i++)
    {
        r.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(
            (i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z)));
    }
    return r;
};

var total = new Bounds();
bool premier = true;

foreach (var mr in renderers)
{
    var mf = mr.GetComponent<MeshFilter>();
    if (mf == null || mf.sharedMesh == null) { continue; }
    var b = coinsVersMonde(mf.sharedMesh.bounds, mr.transform.localToWorldMatrix);
    if (premier) { total = b; premier = false; } else { total.Encapsulate(b); }
}

float z0 = total.min.z, z1 = total.max.z;
float axeX = total.center.x, axeY = total.center.y;

sb.AppendLine("  axe du repere : x = " + axeX.ToString("F5") + "   y = " + axeY.ToString("F5")
              + "   z de " + z0.ToString("F5") + " a " + z1.ToString("F5"));
sb.AppendLine();

const int nTranches = 20;
var maxRayon = new float[nTranches];
var minRayon = new float[nTranches];
var compte = new int[nTranches];

for (int i = 0; i < nTranches; i++) { maxRayon[i] = 0f; minRayon[i] = float.MaxValue; }

// par maillage aussi
var parMailleNom = new System.Collections.Generic.List<string>();
var parMailleMinZ = new System.Collections.Generic.List<float>();
var parMailleMaxZ = new System.Collections.Generic.List<float>();

if (lus > 0)
{
    foreach (var mr in renderers)
    {
        var mf = mr.GetComponent<MeshFilter>();
        var mesh = mf != null ? mf.sharedMesh : null;
        if (mesh == null) { continue; }

        Vector3[] pts;
        try { pts = mesh.vertices; } catch { continue; }
        if (pts == null || pts.Length == 0) { continue; }

        var m = mr.transform.localToWorldMatrix;
        float mn = float.MaxValue, mx = float.MinValue;

        foreach (var p in pts)
        {
            var w = m.MultiplyPoint3x4(p);
            if (w.z < mn) { mn = w.z; }
            if (w.z > mx) { mx = w.z; }

            float t = Mathf.Clamp01((w.z - z0) / Mathf.Max(z1 - z0, 1e-6f));
            int k = Mathf.Min(nTranches - 1, (int)(t * nTranches));
            float r = Mathf.Sqrt((w.x - axeX) * (w.x - axeX) + (w.y - axeY) * (w.y - axeY));

            if (r > maxRayon[k]) { maxRayon[k] = r; }
            if (r < minRayon[k]) { minRayon[k] = r; }
            compte[k]++;
        }

        parMailleNom.Add(mesh.name);
        parMailleMinZ.Add(mn);
        parMailleMaxZ.Add(mx);
    }
}

sb.AppendLine("  tranche   z (mm)        rayon max      diametre max    nb sommets");
for (int k = 0; k < nTranches; k++)
{
    float zc = Mathf.Lerp(z0, z1, (k + 0.5f) / nTranches);
    string d = compte[k] > 0
        ? "  " + mm(maxRayon[k] * 2f) + "     " + compte[k].ToString().PadLeft(5)
        : "  (vide)         0";
    sb.AppendLine("  " + (k * 5).ToString().PadLeft(3) + "-" + ((k + 1) * 5).ToString().PadLeft(3) + "%"
                  + "   " + (zc * 1000f / upm).ToString("F1").PadLeft(8)
                  + "        " + mm(maxRayon[k]).PadLeft(10) + d);
}

sb.AppendLine();
sb.AppendLine("=== emprise de chaque maillage le long de Z monde ===");
for (int i = 0; i < parMailleNom.Count; i++)
{
    sb.AppendLine("  '" + parMailleNom[i] + "'  z de " + parMailleMinZ[i].ToString("F5")
                  + " (" + (parMailleMinZ[i] * 1000f / upm).ToString("F1") + " mm)"
                  + "  a " + parMailleMaxZ[i].ToString("F5")
                  + " (" + (parMailleMaxZ[i] * 1000f / upm).ToString("F1") + " mm)"
                  + "   longueur " + mm(parMailleMaxZ[i] - parMailleMinZ[i]));
}

// --- emprise par SOUS-MAILLAGE (donc par materiau) -------------------------------------------

sb.AppendLine();
sb.AppendLine("=== emprise par sous-maillage (= par materiau) le long de Z ===");

foreach (var mr in renderers)
{
    var mf = mr.GetComponent<MeshFilter>();
    var mesh = mf != null ? mf.sharedMesh : null;
    if (mesh == null) { continue; }

    Vector3[] pts;
    try { pts = mesh.vertices; } catch { continue; }
    if (pts == null || pts.Length == 0) { continue; }

    var m = mr.transform.localToWorldMatrix;
    var mats = mr.sharedMaterials;

    for (int s = 0; s < mesh.subMeshCount; s++)
    {
        int[] tris;
        try { tris = mesh.GetTriangles(s); } catch { continue; }

        float mn = float.MaxValue, mx = float.MinValue, rmax = 0f;

        foreach (var idx in tris)
        {
            if (idx < 0 || idx >= pts.Length) { continue; }
            var w = m.MultiplyPoint3x4(pts[idx]);
            if (w.z < mn) { mn = w.z; }
            if (w.z > mx) { mx = w.z; }
            float r = Mathf.Sqrt((w.x - axeX) * (w.x - axeX) + (w.y - axeY) * (w.y - axeY));
            if (r > rmax) { rmax = r; }
        }

        string nomMat = (mats != null && s < mats.Length && mats[s] != null) ? mats[s].name : "(sans nom)";

        sb.AppendLine("  '" + mesh.name + "' sub " + s + "  materiau '" + nomMat + "'"
                      + "   z " + (mn * 1000f / upm).ToString("F1") + " -> " + (mx * 1000f / upm).ToString("F1") + " mm"
                      + "   longueur " + mm(mx - mn)
                      + "   rayon max " + mm(rmax) + " (" + (rmax * 2f * 1000f / upm).ToString("F1") + " mm de diametre)");
    }
}

// --- verification croisee : Renderer.bounds -------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== controle croise : Renderer.bounds (calcule par Unity) ===");

var rb = new Bounds();
bool p2 = true;
foreach (var r in renderers)
{
    if (p2) { rb = r.bounds; p2 = false; } else { rb.Encapsulate(r.bounds); }
}
sb.AppendLine("  union Renderer.bounds  taille " + V(rb.size) + " = ("
              + (rb.size.x * 1000f / upm).ToString("F1") + ", "
              + (rb.size.y * 1000f / upm).ToString("F1") + ", "
              + (rb.size.z * 1000f / upm).ToString("F1") + ") mm");
sb.AppendLine("  union 8 coins          taille " + V(total.size) + " = ("
              + (total.size.x * 1000f / upm).ToString("F1") + ", "
              + (total.size.y * 1000f / upm).ToString("F1") + ", "
              + (total.size.z * 1000f / upm).ToString("F1") + ") mm");
sb.AppendLine("  Renderer.bounds min/max : " + V(rb.min) + " -> " + V(rb.max));
sb.AppendLine("  8 coins        min/max : " + V(total.min) + " -> " + V(total.max));

// --- ou tombe l'origine du depot, dans le repere monde ---------------------------------------

sb.AppendLine();
sb.AppendLine("=== reperes du depot ramenes dans le repere Unity (mm) ===");
sb.AppendLine("  origine depot (0,2667 ; -0,11876 ; 0,0127) m");
sb.AppendLine("    -> blender(x,y,z) = unity(-x, z, -y) = (-0,26670, 0,01270, 0,11876) m");
sb.AppendLine("    -> en unites du projet : (" + (-0.26670f * upm).ToString("F4") + ", "
              + (0.01270f * upm).ToString("F4") + ", " + (0.11876f * upm).ToString("F4") + ") u"
              + "  = (" + (-266.70f).ToString("F1") + ", " + (12.70f).ToString("F1") + ", "
              + (118.76f).ToString("F1") + ") mm");
sb.AppendLine("  position de la racine dans la preview : " + V(copie.transform.position)
              + "  = " + mm(copie.transform.position.z) + " sur Z");
sb.AppendLine("  boite monde sur Z : " + (z0 * 1000f / upm).ToString("F1") + " -> "
              + (z1 * 1000f / upm).ToString("F1") + " mm");
sb.AppendLine("  l'origine du depot tombe a "
              + (((0.11876f * upm) - z0) / Mathf.Max(z1 - z0, 1e-6f) * 100f).ToString("F1")
              + "% depuis le bas de la boite");

UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);

sb.AppendLine();
sb.AppendLine("=== restauration ===");
sb.AppendLine("  scene ouverte : " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
              + "  rootCount " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().rootCount);
sb.AppendLine("=== FIN SONDE 2 ===");

return sb.ToString();
