// Sonde du PLAN DE POSE — LECTURE SEULE. Constate, ne décide rien.
// A) hiérarchie sous PinballTable : nom, localPosition, mesh, colliders, scripts
// B) chaque maillage de Table : nom, position monde, taille de boîte
// C) caisson : maillages + boîte englobante monde (pour poser le HUD de fronton)
// D) Canvas : mode de rendu, camera, tailles des textes
// E) caméra, tags

var sb = new System.Text.StringBuilder();

System.Action<Transform, string, System.Text.StringBuilder> walk = null;
walk = (t, pad, o) =>
{
    if (pad.Length > 24) { return; }
    foreach (Transform c in t)
    {
        var bits = new System.Text.StringBuilder();
        var mf = c.GetComponent<MeshFilter>();
        if (mf != null) { bits.Append(" [Mesh:" + (mf.sharedMesh != null ? mf.sharedMesh.name : "NULL") + "]"); }
        var col = c.GetComponent<Collider>();
        if (col != null) { bits.Append(" [" + col.GetType().Name + (col.enabled ? "" : " OFF") + "]"); }
        var rr = c.GetComponent<Renderer>();
        if (rr != null && !rr.enabled) { bits.Append(" [rendu OFF]"); }
        if (c.GetComponent<Rigidbody>() != null) { bits.Append(" [RB]"); }
        if (c.GetComponent<HingeJoint>() != null) { bits.Append(" [Hinge]"); }
        foreach (var co in c.GetComponents<Component>())
        {
            if (co == null) { continue; }
            var n = co.GetType().Name;
            if (n == "Transform" || n == "MeshFilter" || n == "MeshRenderer" || n == "Rigidbody"
                || n == "HingeJoint" || n == "BoxCollider" || n == "MeshCollider" || n == "SphereCollider"
                || n == "CapsuleCollider") { continue; }
            bits.Append(" <" + n + ">");
        }
        o.AppendLine(pad + c.name + " loc " + c.localPosition.ToString("F4") + bits);
        walk(c, pad + "  ", o);
    }
};

System.Func<Transform, string> chemin = null;
chemin = (t) =>
{
    var s = t.name;
    var p = t.parent;
    while (p != null) { s = p.name + "/" + s; p = p.parent; }
    return s;
};

var racine = GameObject.Find("PinballTable");
if (racine == null) { return "PAS DE PinballTable dans la scène."; }
var rt = racine.transform;

sb.AppendLine("=== A. ROOT ===");
sb.AppendLine("PinballTable local " + rt.localPosition.ToString("F4") + " euler " + rt.localEulerAngles.ToString("F4") + " scale " + rt.localScale.ToString("F4"));
sb.AppendLine();
sb.AppendLine("=== A. HIÉRARCHIE (profondeur 4) ===");
walk(rt, "", sb);

sb.AppendLine();
sb.AppendLine("=== B. MAILLAGES DE /PinballTable/Table ===");
var table = rt.Find("Table");
if (table == null) { sb.AppendLine("PAS DE Table"); }
else
{
    var tfs = table.GetComponentsInChildren<MeshFilter>(true);
    sb.AppendLine("total maillages " + tfs.Length);
    foreach (var mf in tfs)
    {
        var r = mf.GetComponent<Renderer>();
        var c = mf.GetComponent<Collider>();
        sb.AppendLine(chemin(mf.transform) + " | mesh " + (mf.sharedMesh != null ? mf.sharedMesh.name : "NULL")
            + " | posMonde " + mf.transform.position.ToString("F4")
            + " | tailleMonde " + (r != null ? r.bounds.size.ToString("F4") : "-")
            + " | collider " + (c != null ? c.GetType().Name : "AUCUN"));
    }
    var tot = 0;
    foreach (var mf in tfs) { if (mf.GetComponent<Collider>() != null) { tot++; } }
    sb.AppendLine("maillages avec collider : " + tot + " / " + tfs.Length);
}

sb.AppendLine();
sb.AppendLine("=== C. CAISSON ===");
if (table != null)
{
    var cab = table.Find("Pinball_Cabinet");
    if (cab == null) { sb.AppendLine("PAS DE Pinball_Cabinet"); }
    else
    {
        var rends = cab.GetComponentsInChildren<Renderer>(true);
        var b = new Bounds(cab.position, Vector3.zero);
        var premier = true;
        foreach (var r in rends)
        {
            if (premier) { b = r.bounds; premier = false; } else { b.Encapsulate(r.bounds); }
            sb.AppendLine(chemin(r.transform) + " | " + (r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null ? r.GetComponent<MeshFilter>().sharedMesh.name : "?")
                + " | posMonde " + r.transform.position.ToString("F4") + " | tailleMonde " + r.bounds.size.ToString("F4"));
        }
        sb.AppendLine("CAISSON boite monde centre " + b.center.ToString("F4") + " taille " + b.size.ToString("F4") + " rendus " + rends.Length);
        sb.AppendLine("CAISSON colliders " + cab.GetComponentsInChildren<Collider>(true).Length);
        sb.AppendLine("CAISSON local " + cab.localPosition.ToString("F4") + " euler " + cab.localEulerAngles.ToString("F4") + " scale " + cab.localScale.ToString("F4"));
    }
}

sb.AppendLine();
sb.AppendLine("=== D. HÔTES GAMEPLAY ===");
var gp = rt.Find("Gameplay");
if (gp == null) { sb.AppendLine("PAS DE Gameplay"); }
else
{
    sb.AppendLine("Gameplay local " + gp.localPosition.ToString("F4"));
    foreach (Transform t in gp)
    {
        sb.AppendLine(t.name + " | local " + t.localPosition.ToString("F4")
            + " | monde " + t.position.ToString("F4")
            + " | scale " + t.localScale.ToString("F4")
            + " | colliders " + t.GetComponentsInChildren<Collider>(true).Length
            + " | rendus " + t.GetComponentsInChildren<Renderer>(true).Length);
    }
}

sb.AppendLine();
sb.AppendLine("=== E. CANVAS / HUD ===");
foreach (var cv in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
{
    var sc = cv.GetComponent<UnityEngine.UI.CanvasScaler>();
    sb.AppendLine("Canvas '" + cv.name + "' mode " + cv.renderMode
        + " camera " + (cv.worldCamera != null ? cv.worldCamera.name : "NULL")
        + " planeDistance " + cv.planeDistance
        + " sortOrder " + cv.sortingOrder
        + " scale " + (sc != null ? sc.uiScaleMode + " " + sc.referenceResolution : "-"));
    foreach (Transform t in cv.transform)
    {
        var tmp = t.GetComponent<TMPro.TMP_Text>();
        var r = t as RectTransform;
        sb.AppendLine("   > " + t.name + " | " + (tmp != null ? "TMP" : "pasTMP")
            + " | anchor " + (r != null ? r.anchorMin.ToString("F2") + ".." + r.anchorMax.ToString("F2") : "-")
            + " | posMonde " + t.position.ToString("F2")
            + " | active " + t.gameObject.activeSelf);
    }
}

sb.AppendLine();
sb.AppendLine("=== F. CAMERA / TAGS ===");
var cam = Camera.main;
if (cam != null) { sb.AppendLine("Main Camera pos " + cam.transform.position.ToString("F4") + " euler " + cam.transform.eulerAngles.ToString("F2") + " fov " + cam.fieldOfView + " near " + cam.nearClipPlane + " far " + cam.farClipPlane); }
else { sb.AppendLine("PAS DE Main Camera"); }

var dz = GameObject.Find("DrainZone");
sb.AppendLine("DrainZone : " + (dz != null ? ("tag " + dz.tag + " layer " + LayerMask.LayerToName(dz.layer) + " local " + dz.transform.localPosition.ToString("F4") + " colliders " + dz.GetComponents<Collider>().Length) : "absent"));

foreach (var t in new string[] { "Ball", "Drain", "Flipper", "Pinball", "GameController", "TableElement" })
{
    try
    {
        var trouve = GameObject.FindGameObjectWithTag(t);
        sb.AppendLine("tag '" + t + "' : " + (trouve != null ? "OK sur '" + trouve.name + "' (active " + trouve.activeInHierarchy + ")" : "défini mais AUCUN OBJET"));
    }
    catch (System.Exception e) { sb.AppendLine("tag '" + t + "' : NON DÉFINI (" + e.Message + ")"); }
}

var mgr = GameObject.Find("BallManager");
if (mgr != null)
{
    var bm = mgr.GetComponent<BallManager>();
    if (bm != null) { sb.AppendLine("BallManager : Live " + bm.LiveBallCount + " | sceneBall " + (bm.GetType().GetField("sceneBall", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null ? "champ présent" : "?")); }
}
sb.AppendLine("Physics.gravity " + Physics.gravity.ToString("F4") + " | simulationMode " + Physics.simulationMode);

return sb.ToString();
