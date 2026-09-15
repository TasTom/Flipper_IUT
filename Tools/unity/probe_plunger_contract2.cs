// CONTRAT DE SCENE de Plunger.cs — sonde 2, LECTURE SEULE.
//
// La sonde 1 a mesure l'hote et la bille. Celle-ci repond aux questions restees ouvertes :
//   - la zone d'OverlapBox atteint-elle le POINT D'APPARITION (et pas la bille, qui est tombee) ?
//   - ou s'arrete exactement le sol du couloir (fin precise, par bissection) ?
//   - quelle est la largeur du couloir a hauteur du lanceur, mesuree DU CENTRE vers chaque paroi ?
//   - la couche Ball est-elle bien vue par OverlapBox et collide-t-elle avec le decor ?
//   - quel est le nom exact de la touche serialisee (enumValueIndex n'est pas la valeur) ?
//   - qu'est-ce qui, aujourd'hui, ferme le bas du couloir ? (rien, ou un collider)

var sb = new System.Text.StringBuilder();
System.Action<string> w = s => sb.AppendLine(s);

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

Transform plunger = null;
Transform point = null;
Transform balle = null;

foreach (var root in scene.GetRootGameObjects())
{
    foreach (var t in root.GetComponentsInChildren<Transform>(true))
    {
        if (t.name == "Plunger") { plunger = t; }
        if (t.name == "BallSpawnPoint") { point = t; }
        if (t.name == "Ball" && t.GetComponent<Collider>() != null) { balle = t; }
    }
}

int coucheBalle = balle != null ? balle.gameObject.layer : 8;

UnityEngine.Physics.SyncTransforms();

// --- 1. nom exact de la touche ------------------------------------------------------------------

w("=== touche de repli serialisee ===");

var plungerComp = UnityEngine.Object.FindAnyObjectByType<Plunger>();

if (plungerComp != null)
{
    var so = new UnityEditor.SerializedObject(plungerComp);
    var prop = so.FindProperty("plungerKey");

    w("  enumValueIndex = " + prop.enumValueIndex
      + "   intValue = " + prop.intValue
      + "   nom = " + prop.enumDisplayNames[prop.enumValueIndex]
      + "   valeur enum = " + ((KeyCode)prop.intValue));
}

var routeur = UnityEngine.Object.FindAnyObjectByType<InputRouter>();

if (routeur != null)
{
    var so = new UnityEditor.SerializedObject(routeur);
    var prop = so.FindProperty("plungerKey");

    w("  InputRouter.plungerKey : intValue = " + prop.intValue
      + "   nom = " + prop.enumDisplayNames[prop.enumValueIndex]
      + "   valeur enum = " + ((KeyCode)prop.intValue));
}

// --- 2. la zone d'OverlapBox atteint-elle le point d'apparition ? --------------------------------

w("");
w("=== zone de recherche vs POINT D'APPARITION ===");

if (plunger != null && point != null && plungerComp != null)
{
    var so = new UnityEditor.SerializedObject(plungerComp);

    float maxPull = so.FindProperty("maxPull").floatValue;
    float catchOffset = so.FindProperty("catchOffset").floatValue;
    float catchHalfWidth = so.FindProperty("catchHalfWidth").floatValue;

    Vector3 repos = plunger.position;
    Vector3 fwd = plunger.forward;
    Quaternion rot = plunger.rotation;

    Vector3 front = repos + fwd * catchOffset;
    Vector3 back = repos - fwd * maxPull;
    Vector3 centre = (front + back) * 0.5f;
    Vector3 demi = new Vector3(catchHalfWidth, 0.5f, (front - back).magnitude * 0.5f);

    // Etendue reelle : les 8 coins, car la boite est inclinee de -7 degres.
    var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
    var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

    for (int sx = -1; sx <= 1; sx += 2)
    for (int sy = -1; sy <= 1; sy += 2)
    for (int sz = -1; sz <= 1; sz += 2)
    {
        Vector3 coin = centre + rot * new Vector3(sx * demi.x, sy * demi.y, sz * demi.z);

        min = Vector3.Min(min, coin);
        max = Vector3.Max(max, coin);
    }

    w("  boite monde     : min " + min.ToString("F4") + "   max " + max.ToString("F4"));
    w("  (les 8 coins, inclinaison -7 prise en compte)");

    Vector3 delta = point.position - repos;

    w("  point d'apparition : " + point.position.ToString("F4"));
    w("  projection sur forward (code attend < " + catchOffset.ToString("F4") + ") : "
      + Vector3.Dot(delta, fwd).ToString("F4"));
    w("  distance laterale (code attend < " + catchHalfWidth.ToString("F4") + ") : "
      + (delta - Vector3.Project(delta, fwd)).magnitude.ToString("F4"));

    Vector3 local = Quaternion.Inverse(rot) * (point.position - centre);

    w("  local dans la boite : " + local.ToString("F4") + "   (demi " + demi.ToString("F4") + ")");
    w("  DANS la boite : " + (System.Math.Abs(local.x) <= demi.x
                            && System.Math.Abs(local.y) <= demi.y
                            && System.Math.Abs(local.z) <= demi.z));

    var hits = UnityEngine.Physics.OverlapBox(centre, demi, rot);
    w("  OverlapBox au repos -> " + hits.Length + " collider(s), dont bille : "
      + System.Array.Exists(hits, h => h.CompareTag("Ball")));

    // La couche Ball est-elle vue par OverlapBox ? On interroge la bille la ou elle est.
    if (balle != null)
    {
        var surBalle = UnityEngine.Physics.OverlapBox(balle.position, Vector3.one * 0.05f, Quaternion.identity);

        w("  OverlapBox sur la bille (la ou elle est) -> " + surBalle.Length
          + " collider(s), bille vue : " + System.Array.Exists(surBalle, h => h.CompareTag("Ball")));
    }
}

// --- 3. ou s'arrete le sol du couloir ? (bissection) --------------------------------------------

w("");
w("=== fin du sol du couloir (bissection) ===");

if (point != null)
{
    System.Func<float, bool> solPresent = z =>
    {
        var o = new Vector3(point.position.x, point.position.y + 0.5f, z);
        return UnityEngine.Physics.Raycast(o, Vector3.down, out var h, 4f, ~0, QueryTriggerInteraction.Ignore);
    };

    float bas = -1.0f, haut = 1.0f;

    for (int i = 0; i < 40; i++)
    {
        float milieu = (bas + haut) * 0.5f;

        if (solPresent(milieu)) { haut = milieu; } else { bas = milieu; }
    }

    w("  sol present jusqu'a z = " + haut.ToString("F5") + "   (rien a z = " + bas.ToString("F5") + ")");
    w("  soit " + (haut - plunger.position.z).ToString("F4") + " u devant le lanceur (z lanceur "
      + plunger.position.z.ToString("F4") + ")");

    // Hauteur du sol en ce point.
    var o2 = new Vector3(point.position.x, point.position.y + 0.5f, haut - 0.02f);

    if (UnityEngine.Physics.Raycast(o2, Vector3.down, out var h2, 4f, ~0, QueryTriggerInteraction.Ignore))
    {
        w("  sol a y = " + h2.point.y.ToString("F4") + " sur '" + h2.collider.name + "'");
        w("  centre de bille posee : y = " + (h2.point.y + 0.225f).ToString("F4"));
    }
}

// --- 4. largeur du couloir, DU CENTRE vers chaque paroi -----------------------------------------

w("");
w("=== largeur du couloir (depuis le centre vers chaque paroi) ===");

if (point != null)
{
    foreach (float z in new[] { point.position.z, plunger.position.z, 0.6f, 0.3f })
    {
        float y = 0.45f;

        var gauche = UnityEngine.Physics.Raycast(new Vector3(point.position.x, y, z), Vector3.left, out var hg, 2f, ~0, QueryTriggerInteraction.Ignore);
        var droite = UnityEngine.Physics.Raycast(new Vector3(point.position.x, y, z), Vector3.right, out var hd, 2f, ~0, QueryTriggerInteraction.Ignore);

        w("  z=" + z.ToString("F4") + "  y=" + y
          + "   gauche " + (gauche ? hg.distance.ToString("F4") + " (" + hg.collider.name + ")" : "AUCUNE")
          + "   droite " + (droite ? hd.distance.ToString("F4") + " (" + hd.collider.name + ")" : "AUCUNE")
          + "   largeur " + (gauche && droite ? (hg.distance + hd.distance).ToString("F4") : "-"));
    }
}

// --- 5. qu'est-ce qui ferme le bas du couloir aujourd'hui ? -------------------------------------

w("");
w("=== ce qui ferme le bas du couloir ===");

if (point != null)
{
    // Vers le bas du couloir (-Z) depuis la hauteur de bille, en partant de derriere le lanceur.
    var depart = new Vector3(point.position.x, 0.45f, plunger.position.z - 1.5f);
    var versHaut = UnityEngine.Physics.Raycast(depart, Vector3.forward, out var hz, 3f, ~0, QueryTriggerInteraction.Ignore);

    w("  rayon +Z depuis z=" + depart.z.ToString("F4") + " : "
      + (versHaut ? "obstacle a z=" + hz.point.z.ToString("F4") + " ('" + hz.collider.name + "')" : "AUCUN obstacle"));

    var versBas = UnityEngine.Physics.Raycast(new Vector3(point.position.x, 0.45f, point.position.z + 0.5f),
                                             Vector3.back, out var hb, 3f, ~0, QueryTriggerInteraction.Ignore);

    w("  rayon -Z depuis z=" + (point.position.z + 0.5f).ToString("F4") + " : "
      + (versBas ? "obstacle a z=" + hb.point.z.ToString("F4") + " ('" + hb.collider.name + "')" : "AUCUN obstacle"));
}

// --- 6. matrice de collision des couches ---------------------------------------------------------

w("");
w("=== couches ===");
w("  couche de la bille : " + coucheBalle + " (" + UnityEngine.LayerMask.LayerToName(coucheBalle) + ")");
w("  couche du lanceur  : " + plunger.gameObject.layer + " (" + UnityEngine.LayerMask.LayerToName(plunger.gameObject.layer) + ")");
w("  Ball ignore Default : " + UnityEngine.Physics.GetIgnoreLayerCollision(coucheBalle, 0));
w("  Ball ignore Table(9) : " + UnityEngine.Physics.GetIgnoreLayerCollision(coucheBalle, 9));
w("  DefaultRaycastLayers = " + UnityEngine.Physics.DefaultRaycastLayers
  + "  -> couche Ball incluse : " + ((UnityEngine.Physics.DefaultRaycastLayers & (1 << coucheBalle)) != 0));

return sb.ToString();
