// CONTRAT DE SCENE de Plunger.cs — sonde LECTURE SEULE.
//
// Repond, par mesure et non par lecture du code seul :
//   - ou est l'hote Plunger, quelle orientation, quels composants, quels enfants ;
//   - la geometrie exacte de la zone de recherche d'OverlapBox telle que le script la calcule
//     (front = repos + forward*catchOffset, back = repos - forward*maxPull) ;
//   - si la bille posee dans la scene tombe DANS cette zone au repos ;
//   - ce que renvoie un vrai Physics.OverlapBox a cette place ;
//   - la valeur serialisee des champs du script (donc ce qui est reellement dans la scene,
//     pas le defaut du code).
//
// Aucune ecriture : que des lectures. Pas de GetInstanceID (obsolete en 6000.6).

var sb = new System.Text.StringBuilder();

System.Action<string> w = s => sb.AppendLine(s);

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
w("=== scene '" + scene.name + "' ===");

// --- 1. l'hote Plunger --------------------------------------------------------------------------

Transform plunger = null;

foreach (var root in scene.GetRootGameObjects())
{
    foreach (var t in root.GetComponentsInChildren<Transform>(true))
    {
        if (t.name == "Plunger") { plunger = t; break; }
    }

    if (plunger != null) { break; }
}

w("");
w("=== hote 'Plunger' ===");

if (plunger == null)
{
    w("  ABSENT de la scene.");
}
else
{
    string chemin = plunger.name;
    var p = plunger.parent;

    while (p != null) { chemin = p.name + "/" + chemin; p = p.parent; }

    w("  chemin          : " + chemin);
    w("  actif (hierar.) : " + plunger.gameObject.activeInHierarchy);
    w("  tag / layer     : " + plunger.tag + " / " + plunger.gameObject.layer
      + " (" + UnityEngine.LayerMask.LayerToName(plunger.gameObject.layer) + ")");
    w("  position monde  : " + plunger.position.ToString("F4"));
    w("  position locale : " + plunger.localPosition.ToString("F4"));
    w("  rotation monde  : " + plunger.rotation.eulerAngles.ToString("F3"));
    w("  rotation locale : " + plunger.localRotation.eulerAngles.ToString("F3"));
    w("  perte d'echelle : " + plunger.lossyScale.ToString("F6"));
    w("  forward monde   : " + plunger.forward.ToString("F6"));
    w("  forward local   : " + plunger.localRotation * Vector3.forward);
    w("  up monde        : " + plunger.up.ToString("F6"));

    // Composants portes par l'hote lui-meme.
    var composants = plunger.GetComponents<Component>();
    w("  composants (" + composants.Length + ") :");

    foreach (var c in composants)
    {
        w("      " + (c == null ? "<MANQUANT>" : c.GetType().FullName));
    }

    // Colliders : soi-meme puis enfants.
    var collidersSelf = plunger.GetComponents<Collider>();
    var collidersTout = plunger.GetComponentsInChildren<Collider>(true);
    var rbSelf = plunger.GetComponent<Rigidbody>();
    var rbTout = plunger.GetComponentsInChildren<Rigidbody>(true);

    w("  colliders sur l'hote : " + collidersSelf.Length
      + "   (dont triggers " + System.Array.FindAll(collidersSelf, c => c.isTrigger).Length + ")");
    w("  colliders dans la sous-hierarchie : " + collidersTout.Length);
    w("  Rigidbody sur l'hote : " + (rbSelf != null) + "   dans la sous-hierarchie : " + rbTout.Length);

    foreach (var c in collidersSelf)
    {
        w("      " + c.GetType().Name + "  trigger=" + c.isTrigger
          + "  centre local=" + (c is BoxCollider bc ? bc.center.ToString("F4") : "-")
          + "  taille=" + (c is BoxCollider b2 ? b2.size.ToString("F4") : "-")
          + "  bounds monde=" + c.bounds.center.ToString("F4") + " extents=" + c.bounds.extents.ToString("F4"));
    }

    // Enfants.
    w("  enfants (" + plunger.childCount + ") :");

    foreach (var e in plunger.GetComponentsInChildren<Transform>(true))
    {
        if (e == plunger) { continue; }

        w("      " + e.name
          + "  actif=" + e.gameObject.activeInHierarchy
          + "  localPos=" + e.localPosition.ToString("F4")
          + "  colliders=" + e.GetComponents<Collider>().Length
          + "  renderers=" + e.GetComponents<Renderer>().Length
          + "  composants=[" + string.Join(",", System.Array.ConvertAll(e.GetComponents<Component>(),
                c => c == null ? "<MANQUANT>" : c.GetType().Name)) + "]");
    }
}

// --- 2. champs serialises de Plunger (ce qui est REELLEMENT dans la scene) -----------------------

w("");
w("=== champs serialises de Plunger (scene) ===");

var plungerComp = UnityEngine.Object.FindAnyObjectByType<Plunger>();

if (plungerComp == null)
{
    w("  aucun composant Plunger dans la scene.");
}
else
{
    var so = new UnityEditor.SerializedObject(plungerComp);

    foreach (string nom in new[] { "pullSpeed", "maxPull", "launchForce", "catchHalfWidth",
                                   "catchOffset", "relaunchDelay", "plungerKey" })
    {
        var prop = so.FindProperty(nom);

        if (prop == null) { w("  " + nom.PadRight(16) + " : <champ introuvable>"); continue; }

        string val = prop.propertyType == UnityEditor.SerializedPropertyType.Enum
            ? ((System.Enum)System.Enum.ToObject(typeof(KeyCode), prop.enumValueIndex)).ToString()
            : prop.propertyType == UnityEditor.SerializedPropertyType.Float
                ? prop.floatValue.ToString("F4")
                : prop.ToString();

        w("  " + nom.PadRight(16) + " : " + val);
    }
}

// --- 3. la bille posee dans la scene ------------------------------------------------------------

w("");
w("=== bille posee dans la scene ===");

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
    w("  aucune bille (objet 'Ball' avec collider) trouvee.");
}
else
{
    string chemin = balle.name;
    var p = balle.parent;

    while (p != null) { chemin = p.name + "/" + chemin; p = p.parent; }

    var colBalle = balle.GetComponent<Collider>();

    w("  chemin          : " + chemin);
    w("  actif (hierar.) : " + balle.gameObject.activeInHierarchy);
    w("  tag / layer     : " + balle.tag + " / " + balle.gameObject.layer
      + " (" + UnityEngine.LayerMask.LayerToName(balle.gameObject.layer) + ")");
    w("  position monde  : " + balle.position.ToString("F4"));
    w("  position locale : " + balle.localPosition.ToString("F4"));
    w("  perte d'echelle : " + balle.lossyScale.ToString("F6"));
    w("  collider        : " + colBalle.GetType().Name + " trigger=" + colBalle.isTrigger);
    w("  bounds monde    : centre " + colBalle.bounds.center.ToString("F4")
      + " extents " + colBalle.bounds.extents.ToString("F4")
      + "  (rayon " + colBalle.bounds.extents.x.ToString("F4") + ")");
    w("  Rigidbody       : " + (balle.GetComponent<Rigidbody>() != null));
}

// --- 4. geometrie de la zone de recherche, calculee EXACTEMENT comme le script -------------------

w("");
w("=== zone d'OverlapBox telle que PushBalls la calcule ===");

if (plunger == null || plungerComp == null)
{
    w("  non calculable : hote ou composant absent.");
}
else
{
    var so2 = new UnityEditor.SerializedObject(plungerComp);

    float maxPull = so2.FindProperty("maxPull").floatValue;
    float catchOffset = so2.FindProperty("catchOffset").floatValue;
    float catchHalfWidth = so2.FindProperty("catchHalfWidth").floatValue;
    float launchForce = so2.FindProperty("launchForce").floatValue;

    Vector3 repos = plunger.position;   // == restWorldPosition capture en Awake
    Vector3 fwd = plunger.forward;

    Vector3 front = repos + fwd * catchOffset;
    Vector3 back = repos - fwd * maxPull;
    Vector3 centre = (front + back) * 0.5f;
    Vector3 demi = new Vector3(catchHalfWidth, 0.5f, (front - back).magnitude * 0.5f);

    w("  repos (monde)   : " + repos.ToString("F4"));
    w("  front           : " + front.ToString("F4"));
    w("  back            : " + back.ToString("F4"));
    w("  centre          : " + centre.ToString("F4"));
    w("  demi-extents    : " + demi.ToString("F4"));
    w("  rotation boite  : " + plunger.rotation.eulerAngles.ToString("F3"));
    w("  -> couvre z de  : " + System.Math.Min(front.z, back.z).ToString("F4")
      + " a " + System.Math.Max(front.z, back.z).ToString("F4"));
    w("  -> couvre y de  : " + System.Math.Min(front.y, back.y).ToString("F4")
      + " a " + System.Math.Max(front.y, back.y).ToString("F4"));
    w("  impulsion pleine charge : " + (launchForce).ToString("F4") + " u/s a masse 1");

    if (balle != null)
    {
        Vector3 local = Quaternion.Inverse(plunger.rotation) * (balle.position - centre);

        bool dedans = System.Math.Abs(local.x) <= demi.x
                   && System.Math.Abs(local.y) <= demi.y
                   && System.Math.Abs(local.z) <= demi.z;

        Vector3 delta = balle.position - repos;

        w("  bille dans la boite (calcul) : " + dedans);
        w("      bille en local de la boite : " + local.ToString("F4") + "  (demi " + demi.ToString("F4") + ")");
        w("      bille - repos              : " + delta.ToString("F4"));
        w("      projection sur forward     : " + Vector3.Dot(delta, fwd).ToString("F4")
          + "   (doit etre < " + catchOffset.ToString("F4") + " et > " + (-maxPull).ToString("F4") + ")");
        w("      distance laterale          : " + (delta - Vector3.Project(delta, fwd)).magnitude.ToString("F4")
          + "   (doit etre < " + demi.x.ToString("F4") + ")");
    }

    // Requete reelle — lecture seule, aucun effet de bord.
    UnityEngine.Physics.SyncTransforms();

    var hits = UnityEngine.Physics.OverlapBox(centre, demi, plunger.rotation);

    w("  Physics.OverlapBox -> " + hits.Length + " collider(s) :");

    foreach (var h in hits)
    {
        w("      " + h.name + "  tag=" + h.tag + "  layer=" + h.gameObject.layer
          + "  trigger=" + h.isTrigger
          + "  rigidbody=" + (h.attachedRigidbody != null ? h.attachedRigidbody.name : "AUCUN"));
    }
}

// --- 5. InputRouter et GameManager : qui appelle quoi --------------------------------------------

w("");
w("=== routeur et gestionnaire ===");

var routeur = UnityEngine.Object.FindAnyObjectByType<InputRouter>();

if (routeur == null)
{
    w("  InputRouter ABSENT -> Plunger retombe sur son propre plungerKey.");
}
else
{
    var so3 = new UnityEditor.SerializedObject(routeur);
    var prop = so3.FindProperty("plungerKey");

    w("  InputRouter present, sur '" + routeur.name + "'");
    w("  plungerKey = " + ((KeyCode)prop.enumValueIndex));
}

var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();

if (gm == null)
{
    w("  GameManager ABSENT -> NotifyBallLaunched() jamais appele.");
}
else
{
    var so4 = new UnityEditor.SerializedObject(gm);

    w("  GameManager present, sur '" + gm.name + "'");
    w("  autoStartOnPlay = " + so4.FindProperty("autoStartOnPlay").boolValue);
    w("  startingBalls   = " + so4.FindProperty("startingBalls").intValue);
    w("  ballSpawnPoint  = " + (so4.FindProperty("ballSpawnPoint").objectReferenceValue != null
        ? so4.FindProperty("ballSpawnPoint").objectReferenceValue.name : "VIDE"));

    var point = so4.FindProperty("ballSpawnPoint").objectReferenceValue as Transform;

    if (point != null) { w("  spawn (monde)   = " + point.position.ToString("F4")); }
}

// --- 6. ou tombe le sol autour du lanceur ? (profil vertical, depuis le spawn vers le bas) -------

w("");
w("=== sol du couloir : raycast vertical descendant depuis le spawn ===");

if (gm != null)
{
    var so5 = new UnityEditor.SerializedObject(gm);
    var point = so5.FindProperty("ballSpawnPoint").objectReferenceValue as Transform;

    if (point != null)
    {
        UnityEngine.Physics.SyncTransforms();

        for (int i = -3; i <= 6; i++)
        {
            float z = point.position.z + i * 0.25f;
            var origine = new Vector3(point.position.x, point.position.y + 0.5f, z);
            var sol = UnityEngine.Physics.Raycast(origine, Vector3.down, out var hit, 5f,
                                                  ~0, QueryTriggerInteraction.Ignore);

            w("  z=" + z.ToString("F4") + "  sol=" + (sol ? hit.point.y.ToString("F4") : "AUCUN")
              + "  (" + (sol ? hit.collider.name : "-") + ")");
        }
    }
}

return sb.ToString();
