// Inventaire de l'état de la scène : hôtes, scripts, colliders. LECTURE SEULE.
//
// Avant de poser slingshots, rampes, loop, portes et drain, il faut savoir ce qui
// EXISTE déjà : quels hôtes du contrat de scène sont présents, lesquels portent un
// script, lesquels portent un collider, et lesquels sont vides.

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

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "PinballTable introuvable. Scène : "
    + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; }

var rt = racine.transform;

sb.AppendLine("scène : " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
sb.AppendLine("racine PinballTable : pos " + rt.position.ToString("F3")
    + "  euler " + rt.eulerAngles.ToString("F2") + "  échelle " + rt.localScale.ToString("F4"));
sb.AppendLine();

// --- 1. les enfants directs de la racine (groupes) -------------------------------------
sb.AppendLine("=== groupes sous PinballTable ===");

foreach (Transform g in rt)
{
    sb.AppendLine("  " + g.name + "  (" + g.childCount + " enfants)  actif " + g.gameObject.activeSelf);
}

sb.AppendLine();

// --- 2. hôtes du contrat de scène ------------------------------------------------------
string[] hotes = {
    "Flipper_Left_Pivot", "Flipper_Right_Pivot", "Flipper_Upper_Pivot",
    "Slingshot_Left", "Slingshot_Right",
    "Bumper_01", "Bumper_02", "Bumper_03",
    "Target_Programmation", "Target_Reseau", "Target_Web", "Target_BDD", "Target_Projet",
    "Ramp_Vosges_In", "Ramp_Vosges_Out", "Ramp_IUT_In", "Ramp_IUT_Out",
    "Loop_At_Entry", "Loop_At_Exit", "Door_IUT", "Boss_ProjetFinal",
    "Plunger", "BallSpawnPoint", "Ball", "DrainZone",
    "Flipper_Bat", "Canvas", "GameManager", "ScoreManager", "BallManager",
    "MissionManager", "MultiballManager", "InputRouter",
};

sb.AppendLine("=== hôtes du contrat ===");

foreach (var nom in hotes)
{
    var t = Trouver(rt, nom);

    if (t == null) { sb.AppendLine("  " + nom.PadRight(24) + " ABSENT"); continue; }

    var comps = t.GetComponents<Component>();
    var noms = new System.Text.StringBuilder();

    foreach (var c in comps)
    {
        if (c == null) { noms.Append("(null) "); continue; }
        if (c is Transform) { continue; }
        noms.Append(c.GetType().Name);

        var col = c as Collider;
        if (col != null) { noms.Append(col.isTrigger ? "(trigger)" : "(solide)"); }

        noms.Append(" ");
    }

    // colliders portés par les ENFANTS (le motif du projet : le collider est sur la pièce)
    var colEnfants = t.GetComponentsInChildren<Collider>(true);
    int nEnfants = 0;
    var nomsEnfants = new System.Text.StringBuilder();
    foreach (var c in colEnfants)
    {
        if (c.transform == t) { continue; }
        nEnfants++;
        nomsEnfants.Append(c.name + (c.isTrigger ? "(trig)" : "") + " ");
    }

    Vector3 local = rt.InverseTransformPoint(t.position);

    sb.AppendLine("  " + nom.PadRight(24)
        + " local (" + local.x.ToString("F3") + ", " + local.y.ToString("F3") + ", " + local.z.ToString("F3") + ")"
        + "  actif " + t.gameObject.activeInHierarchy
        + "  | " + noms.ToString().Trim()
        + (nEnfants > 0 ? "  | ENFANTS colliders (" + nEnfants + ") : " + nomsEnfants.ToString().Trim() : "  | aucun collider enfant")
        + "  | enfants " + t.childCount);
}

sb.AppendLine();

// --- 3. tout objet portant un Collider sous PinballTable --------------------------------
sb.AppendLine("=== colliders sous PinballTable (hors MeshCollider de la table importée) ===");

int nTable = 0;

foreach (var c in racine.GetComponentsInChildren<Collider>(true))
{
    if (c is MeshCollider && c.transform.parent != null
        && c.transform.parent.name == "Pinball_Table") { nTable++; continue; }

    Vector3 local = rt.InverseTransformPoint(c.bounds.center);
    sb.AppendLine("  '" + c.name + "' (" + c.GetType().Name + (c.isTrigger ? " trigger" : "")
        + ") sur '" + (c.transform.parent != null ? c.transform.parent.name : "(racine)") + "'"
        + "  centre (" + local.x.ToString("F2") + ", " + local.y.ToString("F2") + ", " + local.z.ToString("F2") + ")"
        + "  taille " + c.bounds.size.ToString("F2")
        + "  layer " + LayerMask.LayerToName(c.gameObject.layer));
}

sb.AppendLine("  (+ " + nTable + " MeshCollider sur Pinball_Table)");

sb.AppendLine();

// --- 4. les Canvas ---------------------------------------------------------------------
sb.AppendLine("=== Canvas ===");

foreach (var cv in UnityEngine.Object.FindObjectsOfType<Canvas>())
{
    Vector3 local = rt.InverseTransformPoint(cv.transform.position);
    sb.AppendLine("  '" + cv.name + "' mode " + cv.renderMode.ToString()
        + "  local (" + local.x.ToString("F2") + ", " + local.y.ToString("F2") + ", " + local.z.ToString("F2") + ")"
        + "  actif " + cv.gameObject.activeInHierarchy
        + "  enfants " + cv.transform.childCount
        + (cv.worldCamera != null ? "  caméra " + cv.worldCamera.name : "  (pas de caméra)"));

    foreach (Transform e in cv.transform)
    {
        sb.AppendLine("      - " + e.name + " actif " + e.gameObject.activeInHierarchy);
    }
}

sb.AppendLine();

// --- 5. ce qui VOIT la bille : la hiérarchie Gameplay -----------------------------------
var gameplay = rt.Find("Gameplay");

sb.AppendLine("=== Gameplay ===");

if (gameplay == null) { sb.AppendLine("  absent"); }
else
{
    foreach (Transform e in gameplay)
    {
        var comps = e.GetComponents<Component>();
        var noms = new System.Text.StringBuilder();
        foreach (var c in comps) { if (!(c is Transform)) { noms.Append(c.GetType().Name + " "); } }
        Vector3 local = rt.InverseTransformPoint(e.position);
        sb.AppendLine("  " + e.name.PadRight(24) + " local (" + local.x.ToString("F3") + ", "
            + local.y.ToString("F3") + ", " + local.z.ToString("F3") + ")  actif " + e.gameObject.activeSelf
            + "  [" + noms.ToString().Trim() + "]  enfants " + e.childCount);
    }
}

sb.AppendLine();

// --- 6. repères pour poser les slingshots ------------------------------------------------
sb.AppendLine("=== repères pour poser les slingshots ===");

foreach (var nom in new[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    var f = Trouver(rt, nom);
    if (f == null) { continue; }
    Vector3 local = rt.InverseTransformPoint(f.position);
    sb.AppendLine("  " + f.name + " local (" + local.x.ToString("F4") + ", " + local.y.ToString("F4")
        + ", " + local.z.ToString("F4") + ")");
}

var table = rt.Find("Table");
if (table != null)
{
    var pt = table.Find("Pinball_Table");
    if (pt != null)
    {
        var rends = pt.GetComponentsInChildren<MeshRenderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) { b.Encapsulate(rends[i].bounds); }

            Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 coin = b.center + new Vector3(
                    (i & 1) == 0 ? -b.extents.x : b.extents.x,
                    (i & 2) == 0 ? -b.extents.y : b.extents.y,
                    (i & 4) == 0 ? -b.extents.z : b.extents.z);
                Vector3 l = rt.InverseTransformPoint(coin);
                mn = Vector3.Min(mn, l); mx = Vector3.Max(mx, l);
            }

            sb.AppendLine("  Pinball_Table : x [" + mn.x.ToString("F3") + " → " + mx.x.ToString("F3")
                + "]  y [" + mn.y.ToString("F3") + " → " + mx.y.ToString("F3")
                + "]  z [" + mn.z.ToString("F3") + " → " + mx.z.ToString("F3") + "]");
        }
    }
}

return sb.ToString();
