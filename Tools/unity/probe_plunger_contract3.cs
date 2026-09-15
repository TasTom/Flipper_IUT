// CONTRAT DE SCENE de Plunger.cs — sonde 3, LECTURE SEULE.
//
// Le couloir : qui sont LaneFloor / Divider / LaneOuter, ou s'arrete chacun, et y a-t-il quoi que
// ce soit en travers du couloir sous la hauteur de bille (ce qui pourrait retenir la bille a la
// place du lanceur, qui est vide).

var sb = new System.Text.StringBuilder();
System.Action<string> w = s => sb.AppendLine(s);

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

Transform point = null;
Transform plunger = null;

foreach (var root in scene.GetRootGameObjects())
{
    foreach (var t in root.GetComponentsInChildren<Transform>(true))
    {
        if (t.name == "BallSpawnPoint") { point = t; }
        if (t.name == "Plunger") { plunger = t; }
    }
}

UnityEngine.Physics.SyncTransforms();

System.Func<Transform, string> chemin = t =>
{
    string c = t.name;
    var p = t.parent;

    while (p != null) { c = p.name + "/" + c; p = p.parent; }

    return c;
};

// --- 1. les parois et le sol du couloir ---------------------------------------------------------

w("=== colliders du couloir (par nom) ===");

foreach (string nom in new[] { "LaneFloor", "Divider", "LaneOuter", "Surface" })
{
    bool trouve = false;

    foreach (var root in scene.GetRootGameObjects())
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != nom) { continue; }

            trouve = true;

            var col = t.GetComponent<Collider>();
            var rend = t.GetComponent<Renderer>();
            var b = col != null ? col.bounds : new Bounds();

            w("  " + chemin(t));
            w("      actif " + t.gameObject.activeInHierarchy
              + "   collider " + (col != null ? col.GetType().Name : "AUCUN")
              + "   renderer " + (rend != null ? "oui" : "non")
              + "   layer " + t.gameObject.layer);
            w("      bounds monde : centre " + b.center.ToString("F4")
              + "   taille " + b.size.ToString("F4")
              + "   z de " + b.min.z.ToString("F4") + " a " + b.max.z.ToString("F4")
              + "   y de " + b.min.y.ToString("F4") + " a " + b.max.y.ToString("F4"));
        }
    }

    if (!trouve) { w("  " + nom + " : ABSENT"); }
}

// --- 2. ou s'arretent les parois du couloir (bissection sur z) ----------------------------------

w("");
w("=== fin des parois du couloir (y = 0.45) ===");

if (point != null)
{
    foreach (float sens in new[] { -1f, 1f })
    {
        string cote = sens < 0 ? "gauche (Divider)" : "droite (LaneOuter)";

        System.Func<float, bool> mur = z =>
        {
            var o = new Vector3(point.position.x, 0.45f, z);
            return UnityEngine.Physics.Raycast(o, sens < 0 ? Vector3.left : Vector3.right,
                                                out var h, 2f, ~0, QueryTriggerInteraction.Ignore);
        };

        float bas = -1.0f, haut = 2.0f;

        for (int i = 0; i < 40; i++)
        {
            float milieu = (bas + haut) * 0.5f;

            if (mur(milieu)) { haut = milieu; } else { bas = milieu; }
        }

        w("  " + cote + " presente a partir de z = " + haut.ToString("F5")
          + "   (absente a z = " + bas.ToString("F5") + ")");
    }
}

// --- 3. quelque chose en travers du couloir sous la hauteur de bille ? --------------------------

w("");
w("=== travers du couloir a basse hauteur (vers +Z) ===");

if (point != null)
{
    foreach (float y in new[] { 0.45f, 0.30f, 0.15f, 0.05f, -0.10f })
    {
        var depart = new Vector3(point.position.x, y, plunger.position.z - 1.0f);
        var hit = UnityEngine.Physics.Raycast(depart, Vector3.forward, out var h, 2.5f,
                                              ~0, QueryTriggerInteraction.Ignore);

        w("  y=" + y.ToString("F4") + "  rayon +Z depuis z=" + depart.z.ToString("F4") + " : "
          + (hit ? "obstacle z=" + h.point.z.ToString("F4") + " ('" + h.collider.name + "')" : "AUCUN"));
    }
}

// --- 4. la bille est-elle enfoncee dans le sol, la ou elle est ? ---------------------------------

w("");
w("=== bille de scene, etat courant ===");

Transform balle = null;

foreach (var root in scene.GetRootGameObjects())
{
    foreach (var t in root.GetComponentsInChildren<Transform>(true))
    {
        if (t.name == "Ball" && t.GetComponent<Collider>() != null) { balle = t; }
    }
}

if (balle == null)
{
    w("  aucune bille.");
}
else
{
    var o = balle.position + Vector3.up * 0.5f;
    var sol = UnityEngine.Physics.Raycast(o, Vector3.down, out var h, 6f, ~0, QueryTriggerInteraction.Ignore);

    w("  position        : " + balle.position.ToString("F4"));
    w("  spawnPoint      : " + point.position.ToString("F4"));
    w("  ecart au spawn  : " + UnityEngine.Vector3.Distance(balle.position, point.position).ToString("F4") + " u");
    w("  sol dessous     : " + (sol ? h.point.y.ToString("F4") + " ('" + h.collider.name + "')" : "AUCUN"));
    w("  scene modifiee  : " + scene.isDirty);
}

return sb.ToString();
