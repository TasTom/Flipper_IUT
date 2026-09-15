// État des lieux de la caméra de `Neutral.unity`, et de quoi décider où la placer.
//
// Ce script ne modifie RIEN : il lit et rapporte. Il répond à trois questions, dans cet ordre :
//
//   1. Quelles caméras existent, où sont-elles, que voient-elles ? (position, orientation, FOV,
//      projection, masque, et si elles portent le tag `MainCamera`.)
//   2. Y a-t-il une Cinemachine en jeu (cerveau + caméra virtuelle) ? Un `CinemachineBrain` prend
//      la main sur la caméra Unity : le déplacer à la main ne servirait à rien tant qu'il est là.
//   3. Où est la table, et **cadrée comment** ? Le test qui décide n'est pas la distance mais la
//      projection : on projette les huit coins de la table dans le champ de la caméra et on regarde
//      s'ils tombent dans [0,1]×[0,1]. Un coin hors de ces bornes = un coin à l'écran absent.
//
// La table est mesurée dans le repère de son propre root (`PinballTable`), qui est incliné de −7° :
// c'est le repère dans lequel le plateau est un rectangle plat, donc celui dans lequel un cadrage
// se raisonne. Une boîte englobante alignée sur le **monde** décrirait un pavé penché, dont aucune
// face n'est le plateau.

var sb = new System.Text.StringBuilder();

// --- utilitaires ---------------------------------------------------------------------------------

// Chemin hiérarchique d'un transform, pour dire d'où sort une caméra.
string Chemin(Transform t)
{
    var chemin = t.name;

    while (t.parent != null)
    {
        t = t.parent;
        chemin = t.name + "/" + chemin;
    }

    return chemin;
}

// Les huit coins d'une boîte monde. On ne met jamais à l'échelle un centre et une taille : les
// coins, eux, se transforment exactement.
Vector3[] Coins(Bounds b)
{
    var coins = new Vector3[8];
    int i = 0;

    for (int x = -1; x <= 1; x += 2)
    for (int y = -1; y <= 1; y += 2)
    for (int z = -1; z <= 1; z += 2)
    {
        coins[i++] = b.center + Vector3.Scale(b.extents, new Vector3(x, y, z));
    }

    return coins;
}

// --- 1. les caméras ------------------------------------------------------------------------------

sb.AppendLine("=== 1. caméras de la scène ===");

var cameras = UnityEngine.Object.FindObjectsByType<Camera>(
    FindObjectsInactive.Include, FindObjectsSortMode.None);

int vues = 0;

foreach (var cam in cameras)
{
    if (cam == null || !cam.gameObject.scene.IsValid())
    {
        continue;   // écarte les assets (prefab non instancié)
    }

    var t = cam.transform;
    vues++;

    sb.AppendLine();
    sb.AppendLine("[" + vues + "] " + Chemin(t));
    sb.AppendLine("    tag           " + cam.gameObject.tag
                  + "      actif " + cam.gameObject.activeInHierarchy
                  + "      profondeur " + cam.depth);
    sb.AppendLine("    position      locale " + t.localPosition.ToString("F4")
                  + "   monde " + t.position.ToString("F4"));
    sb.AppendLine("    rotation      locale " + t.localEulerAngles.ToString("F3")
                  + "   monde " + t.eulerAngles.ToString("F3"));
    sb.AppendLine("    forward       " + t.forward.ToString("F4"));
    sb.AppendLine("    up            " + t.up.ToString("F4"));
    sb.AppendLine("    FOV           " + cam.fieldOfView.ToString("F3")
                  + "      ortho " + cam.orthographic
                  + (cam.orthographic ? "   taille " + cam.orthographicSize.ToString("F3") : "")
                  + "      near " + cam.nearClipPlane.ToString("F4")
                  + "      far " + cam.farClipPlane.ToString("F1"));
    sb.AppendLine("    masque        " + cam.cullingMask);
    sb.AppendLine("    parent        " + (t.parent != null ? Chemin(t.parent) : "(racine)"));

    var donnees = cam.GetComponent("UniversalAdditionalCameraData");
    sb.AppendLine("    URP           " + (donnees != null ? donnees.GetType().Name : "absent"));
    sb.AppendLine("    AudioListener " + (cam.GetComponent<AudioListener>() != null ? "oui" : "non"));
}

if (vues == 0)
{
    sb.AppendLine("  AUCUNE caméra dans la scène.");
}

var principale = Camera.main;
sb.AppendLine();
sb.AppendLine("Camera.main : " + (principale != null ? Chemin(principale.transform) : "INTROUVABLE (aucun tag MainCamera)"));

// --- 2. Cinemachine ------------------------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== 2. Cinemachine ===");

var tous = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
    FindObjectsInactive.Include, FindObjectsSortMode.None);

int cm = 0;

foreach (var mb in tous)
{
    if (mb == null) { continue; }

    var nom = mb.GetType().FullName;

    if (nom == null || !nom.Contains("Cinemachine")) { continue; }
    if (!mb.gameObject.scene.IsValid()) { continue; }

    cm++;
    sb.AppendLine("  " + nom + "   sur " + Chemin(mb.transform));
}

if (cm == 0)
{
    sb.AppendLine("  aucun composant Cinemachine dans la scène.");
}

// --- 3. la table ---------------------------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== 3. la table ===");

var racine = GameObject.Find("PinballTable");

if (racine == null)
{
    return sb.ToString() + "\n'PinballTable' introuvable.";
}

var rt = racine.transform;

sb.AppendLine("root          " + Chemin(rt));
sb.AppendLine("  position    " + rt.position.ToString("F4") + "   locale " + rt.localPosition.ToString("F4"));
sb.AppendLine("  euler       " + rt.eulerAngles.ToString("F3"));
sb.AppendLine("  up (normale du plateau) " + rt.up.ToString("F4"));
sb.AppendLine("  forward     " + rt.forward.ToString("F4"));
sb.AppendLine("  echelle     " + rt.lossyScale.ToString("F4"));

// Boîte monde de tout ce qui est rendu sous PinballTable, et la même ramenée dans le repère du
// root : c'est cette seconde qui décrit le rectangle du plateau.
var rendus = racine.GetComponentsInChildren<MeshRenderer>(true);

if (rendus.Length == 0)
{
    return sb.ToString() + "\nAucun MeshRenderer sous 'PinballTable'.";
}

var monde = new Bounds(rendus[0].bounds.center, Vector3.zero);
bool premier = true;

var minLocal = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
var maxLocal = new Vector3(float.MinValue, float.MinValue, float.MinValue);

foreach (var r in rendus)
{
    if (premier) { monde = r.bounds; premier = false; }
    else { monde.Encapsulate(r.bounds); }

    foreach (var c in Coins(r.bounds))
    {
        var l = rt.InverseTransformPoint(c);
        minLocal = Vector3.Min(minLocal, l);
        maxLocal = Vector3.Max(maxLocal, l);
    }
}

sb.AppendLine();
sb.AppendLine("  " + rendus.Length + " maillages rendus");
sb.AppendLine("  boîte MONDE  centre " + monde.center.ToString("F4")
              + "   taille " + monde.size.ToString("F4"));
sb.AppendLine("  boîte ROOT   min " + minLocal.ToString("F4")
              + "   max " + maxLocal.ToString("F4")
              + "   taille " + (maxLocal - minLocal).ToString("F4"));

// Surface de jeu : le plan y = 0 du repère du root (c'est la cote du plateau, cf. CLAUDE.md).
sb.AppendLine("  surface de jeu : plan y = 0 du root");

// --- 4. cadrage actuel ---------------------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== 4. cadrage actuel ===");

if (principale == null)
{
    sb.AppendLine("  pas de Camera.main : cadrage non mesurable.");
    return sb.ToString();
}

float vxMin = float.MaxValue, vyMin = float.MaxValue;
float vxMax = float.MinValue, vyMax = float.MinValue;
bool derriere = false;

foreach (var c in Coins(monde))
{
    var v = principale.WorldToViewportPoint(c);

    if (v.z <= 0f) { derriere = true; }

    vxMin = Mathf.Min(vxMin, v.x);
    vyMin = Mathf.Min(vyMin, v.y);
    vxMax = Mathf.Max(vxMax, v.x);
    vyMax = Mathf.Max(vyMax, v.y);
}

sb.AppendLine("  viewport occupé par la boîte de la table :");
sb.AppendLine("    x  " + vxMin.ToString("F3") + "  à  " + vxMax.ToString("F3"));
sb.AppendLine("    y  " + vyMin.ToString("F3") + "  à  " + vyMax.ToString("F3"));
sb.AppendLine("    (0 = bord gauche/bas, 1 = bord droit/haut)");
sb.AppendLine("  coins derrière la caméra : " + (derriere ? "OUI ⚠" : "non"));

bool dedans = vxMin >= 0f && vxMax <= 1f && vyMin >= 0f && vyMax <= 1f;

sb.AppendLine("  table entièrement dans le cadre : " + (dedans ? "OUI ✓" : "NON ⚠"));

// Distance et angle : de quoi caractériser la vue actuelle.
var versTable = monde.center - principale.transform.position;

sb.AppendLine("  centre de la table à " + versTable.magnitude.ToString("F3") + " u de la caméra");
sb.AppendLine("  angle entre l'axe de visée et la direction du centre : "
              + Vector3.Angle(principale.transform.forward, versTable).ToString("F2") + "°");
sb.AppendLine("  angle entre la normale de la caméra et celle du plateau : "
              + Vector3.Angle(principale.transform.forward, rt.up).ToString("F2") + "°"
              + "   (90° = rasante, 0° = à la verticale)");

// --- 5. la bille et le lanceur, pour mémoire -----------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== 5. bille et lanceur (état courant de la scène ouverte) ===");

var gameplay = rt.Find("Gameplay");

if (gameplay != null)
{
    var bille = gameplay.Find("Ball");

    sb.AppendLine("  Ball          " + (bille != null
        ? bille.position.ToString("F4") + "   local " + bille.localPosition.ToString("F4")
        : "absente"));

    var hote = gameplay.Find("Plunger");

    if (hote != null)
    {
        sb.AppendLine("  Plunger       local " + hote.localPosition.ToString("F4")
                      + "   monde " + hote.position.ToString("F4"));

        var piece = hote.Find("Plunger_Rod");

        sb.AppendLine("  Plunger_Rod   " + (piece != null
            ? "présente   local " + piece.localPosition.ToString("F4")
              + "   colliders " + piece.GetComponents<Collider>().Length
            : "ABSENTE — la pose n'est pas dans la scène ouverte"));
    }
}

return sb.ToString();
