// Trois écarts à trancher avant de poser quoi que ce soit. LECTURE SEULE.
//
// 1. `parquer_bille.cs` a écrit la bille à (-3,0675 / 0,2250 / 17,4014) en croyant
//    l'écrire au point d'apparition, qui est à (4,670 / 0,392 / 1,000) sous Gameplay.
//    Donc `GameObject.Find("BallSpawnPoint")` a renvoyé un AUTRE objet. Combien y en a-t-il ?
// 2. Le Canvas est en ScreenSpaceOverlay et n'est pas sous PinballTable. Où est-il, et
//    quelle est sa taille ?
// 3. Les hôtes Slingshot sont à x ±0,733 alors que les corps engendrés sont à x ±2,51.
//    Lequel des deux est le bon ? On mesure la géométrie réelle.

var sb = new System.Text.StringBuilder();

Transform Trouver(Transform r, string nom)
{
    if (r.name == nom) { return r; }
    for (int i = 0; i < r.childCount; i++)
    {
        var t = Trouver(r.GetChild(i), nom);
        if (t != null) { return t; }
    }
    return null;
}

string Chemin(Transform t)
{
    var s = t.name;
    while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
    return s;
}

var racine = GameObject.Find("PinballTable");
var rt = racine.transform;

// --- 1. combien de BallSpawnPoint ? -----------------------------------------------------
sb.AppendLine("=== objets nommés 'BallSpawnPoint' (scène entière, inactifs compris) ===");

int n = 0;

foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>(true))
{
    if (t.name != "BallSpawnPoint") { continue; }
    n++;
    Vector3 l = rt.InverseTransformPoint(t.position);
    sb.AppendLine("  " + Chemin(t)
        + "  monde " + t.position.ToString("F4")
        + "  local-table (" + l.x.ToString("F4") + ", " + l.y.ToString("F4") + ", " + l.z.ToString("F4") + ")");
}

sb.AppendLine("  total : " + n);

// --- 2. le Canvas ------------------------------------------------------------------------
sb.AppendLine();
sb.AppendLine("=== Canvas de la scène ===");

foreach (var cv in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
{
    var rtc = cv.GetComponent<RectTransform>();
    Vector3 l = rt.InverseTransformPoint(cv.transform.position);

    sb.AppendLine("  chemin : " + Chemin(cv.transform));
    sb.AppendLine("    mode : " + cv.renderMode
        + "   sortingOrder " + cv.sortingOrder
        + "   pixelPerfect " + cv.pixelPerfect);
    sb.AppendLine("    monde : " + cv.transform.position.ToString("F3")
        + "   local-table (" + l.x.ToString("F2") + ", " + l.y.ToString("F2") + ", " + l.z.ToString("F2") + ")");
    sb.AppendLine("    échelle : " + cv.transform.lossyScale.ToString("F5"));

    if (rtc != null)
    {
        sb.AppendLine("    RectTransform : sizeDelta " + rtc.sizeDelta.ToString("F1")
            + "  anchorMin " + rtc.anchorMin.ToString("F2") + "  anchorMax " + rtc.anchorMax.ToString("F2"));
    }

    var scaler = cv.GetComponent<UnityEngine.UI.CanvasScaler>();
    if (scaler != null)
    {
        sb.AppendLine("    CanvasScaler : mode " + scaler.uiScaleMode
            + "  référence " + scaler.referenceResolution.ToString("F0")
            + "  match " + scaler.matchWidthOrHeight.ToString("F2"));
    }

    foreach (Transform e in cv.transform)
    {
        var rte = e.GetComponent<RectTransform>();
        sb.AppendLine("    - " + e.name
            + (rte != null ? "  sizeDelta " + rte.sizeDelta.ToString("F0")
                + "  anchoredPos " + rte.anchoredPosition.ToString("F0") : "")
            + "  actif " + e.gameObject.activeInHierarchy);
    }
}

// --- 3. la géométrie réelle des slingshots ------------------------------------------------
sb.AppendLine();
sb.AppendLine("=== 'Slingshots' engendré : maillages et bornes (repère table) ===");

var table = rt.Find("Table");
Transform pt = null;

if (table != null)
{
    foreach (Transform e in table) { if (e.name.StartsWith("Pinball_Table")) { pt = e; } }
}

if (pt == null) { sb.AppendLine("  Pinball_Table introuvable."); }
else
{
    foreach (var f in pt.GetComponentsInChildren<MeshFilter>(true))
    {
        if (f.transform.parent == null || f.transform.parent.name != "Slingshots") { continue; }

        var mr = f.GetComponent<MeshRenderer>();
        Bounds b = mr != null ? mr.bounds : new Bounds();

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

        Vector3 centre = rt.InverseTransformPoint(b.center);

        sb.AppendLine("  '" + f.name + "'  centre (" + centre.x.ToString("F4") + ", " + centre.y.ToString("F4")
            + ", " + centre.z.ToString("F4") + ")");
        sb.AppendLine("      x [" + mn.x.ToString("F3") + " → " + mx.x.ToString("F3")
            + "]  y [" + mn.y.ToString("F3") + " → " + mx.y.ToString("F3")
            + "]  z [" + mn.z.ToString("F3") + " → " + mx.z.ToString("F3") + "]"
            + "  sommets " + (f.sharedMesh != null ? f.sharedMesh.vertexCount : 0));

        var lmn = f.sharedMesh != null ? f.sharedMesh.bounds.min : Vector3.zero;
        var lmx = f.sharedMesh != null ? f.sharedMesh.bounds.max : Vector3.zero;
        sb.AppendLine("      maillage local : min " + lmn.ToString("F4") + "  max " + lmx.ToString("F4"));
        sb.AppendLine("      échelle cumulée : " + f.transform.lossyScale.ToString("F4"));
    }
}

// --- 4. où sont les murs de l'aire de jeu, en bas, pour situer les slingshots -------------
sb.AppendLine();
sb.AppendLine("=== murs bas de l'aire de jeu (bornes, repère table) ===");

if (pt != null)
{
    foreach (var f in pt.GetComponentsInChildren<MeshFilter>(true))
    {
        if (f.transform.parent == null || f.transform.parent.name != "Walls") { continue; }

        var mr = f.GetComponent<MeshRenderer>();
        if (mr == null) { continue; }

        Bounds b = mr.bounds;
        Vector3 centre = rt.InverseTransformPoint(b.center);
        if (centre.z > 8f) { continue; }   // on ne regarde que le bas de la table

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

        sb.AppendLine("  '" + f.name + "'  centre (" + centre.x.ToString("F3") + ", " + centre.y.ToString("F3")
            + ", " + centre.z.ToString("F3") + ")  x [" + mn.x.ToString("F3") + " → " + mx.x.ToString("F3")
            + "]  z [" + mn.z.ToString("F3") + " → " + mx.z.ToString("F3") + "]");
    }
}

return sb.ToString();
