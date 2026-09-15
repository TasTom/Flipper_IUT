// Ce que la caméra cadre VRAIMENT. LECTURE SEULE, aucune modification de scène.
//
// Écrit parce que `solve_camera2.cs` / `place_camera.cs` projettent à la main, avec deux hypothèses
// non vérifiées : le rapport d'écran (16/9 écrit en dur) et le volume à cadrer (une boîte relevée à
// la main). Le rendu de contrôle les a démenties toutes les deux — la table y occupe ~65 % de la
// hauteur là où la projection annonçait 92 %, et le caisson (`Pinball_Cabinet`, qui porte le @
// gravé) est visible alors qu'il est hors de la boîte. Refaire le calcul à la main de plus près ne
// dirait rien : il faut la mesure.
//
// D'où ce fichier : on interroge la vraie `Camera` et les vrais `Renderer.bounds`.
//   • `aspect`, `pixelRect`, `fieldOfView` — le rapport d'écran réel, celui de la fenêtre Game, et
//     non celui qu'on a supposé ;
//   • les bornes MONDE des deux modèles, lues sur leurs renderers ;
//   • les 8 coins de chacune projetés par `WorldToViewportPoint` — donc par Unity, pas par nous.
//
// Le repère de sortie est le viewport : (0,0) coin bas-gauche, (1,1) coin haut-droit. Hors de
// [0,1] = hors champ.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "'PinballTable' introuvable dans la scène."; }

var rt = racine.transform;

var camera = GameObject.Find("Main Camera");

if (camera == null) { return "'Main Camera' introuvable dans la scène."; }

var cam = camera.GetComponent<Camera>();

if (cam == null) { return "'Main Camera' ne porte pas de Camera."; }

sb.AppendLine("=== la caméra ===");
sb.AppendLine("  position   " + camera.transform.position.ToString("F4"));
sb.AppendLine("  rotation   " + camera.transform.eulerAngles.ToString("F3"));
sb.AppendLine("  fieldOfView " + cam.fieldOfView.ToString("F2") + "°"
              + "   (usePhysicalProperties " + cam.usePhysicalProperties + ")");
sb.AppendLine("  aspect     " + cam.aspect.ToString("F5")
              + "   (16/9 = " + (16f / 9f).ToString("F5") + ")"
              + (Mathf.Abs(cam.aspect - 16f / 9f) > 0.001f ? "   ⚠ ≠ 16/9" : "   ✓"));
sb.AppendLine("  pixelRect  " + cam.pixelRect.ToString());
sb.AppendLine("  near/far   " + cam.nearClipPlane.ToString("F2") + " / " + cam.farClipPlane.ToString("F1"));
sb.AppendLine("  pixelWidth/Height " + cam.pixelWidth + " x " + cam.pixelHeight);
sb.AppendLine();

// --- les bornes monde réelles, lues sur les renderers ---------------------------------------------
//
// On ne reprend PAS la boîte écrite à la main dans le solveur : elle a été relevée pour la table
// seule, et le caisson n'y est pas.

bool Bornes(string nom, out Bounds b)
{
    b = new Bounds();

    var t = racine.transform.Find("Table") != null ? racine.transform.Find("Table").Find(nom) : null;

    if (t == null) { t = racine.transform.Find(nom); }
    if (t == null) { return false; }

    var rendus = t.GetComponentsInChildren<Renderer>(true);

    if (rendus.Length == 0) { return false; }

    b = rendus[0].bounds;

    for (int i = 1; i < rendus.Length; i++) { b.Encapsulate(rendus[i].bounds); }

    return true;
}

sb.AppendLine("=== ce qu'il y a à cadrer (bornes MONDE, lues sur les Renderer) ===");

var objets = new string[] { "Pinball_Table", "Pinball_Cabinet" };
var boites = new Bounds[objets.Length];

for (int i = 0; i < objets.Length; i++)
{
    Bounds b;

    if (Bornes(objets[i], out b))
    {
        boites[i] = b;

        sb.AppendLine("  " + objets[i].PadRight(18) + " centre " + b.center.ToString("F3")
                      + "   taille " + b.size.ToString("F3")
                      + "   (racines de renderer : " + (racine.transform.Find("Table") != null
                            ? racine.transform.Find("Table").Find(objets[i]) != null : false) + ")");
    }
    else
    {
        sb.AppendLine("  " + objets[i].PadRight(18) + " introuvable");
    }
}

// Le volume utile : les deux modèles réunis, s'ils existent.
var union = new Bounds(boites[0].center, boites[0].size);

for (int i = 1; i < boites.Length; i++)
{
    if (boites[i].size != Vector3.zero) { union.Encapsulate(boites[i]); }
}

sb.AppendLine("  union              centre " + union.center.ToString("F3") + "   taille " + union.size.ToString("F3"));
sb.AppendLine();

// --- la projection, faite par Unity ---------------------------------------------------------------

void Cadre(string titre, Bounds b)
{
    float xmin = float.MaxValue, xmax = float.MinValue;
    float ymin = float.MaxValue, ymax = float.MinValue;
    float zmin = float.MaxValue;

    bool derriere = false;

    for (int i = 0; i < 8; i++)
    {
        var coin = new Vector3(
            (i & 1) == 0 ? b.min.x : b.max.x,
            (i & 2) == 0 ? b.min.y : b.max.y,
            (i & 4) == 0 ? b.min.z : b.max.z);

        var v = cam.WorldToViewportPoint(coin);

        if (v.z <= 0f) { derriere = true; }

        xmin = Mathf.Min(xmin, v.x); xmax = Mathf.Max(xmax, v.x);
        ymin = Mathf.Min(ymin, v.y); ymax = Mathf.Max(ymax, v.y);
        zmin = Mathf.Min(zmin, v.z);
    }

    sb.AppendLine("  " + titre.PadRight(18)
                  + "  x " + xmin.ToString("F3").PadLeft(7) + " → " + xmax.ToString("F3").PadLeft(7)
                  + "   y " + ymin.ToString("F3").PadLeft(7) + " → " + ymax.ToString("F3").PadLeft(7)
                  + "   largeur " + ((xmax - xmin) * 100f).ToString("F1").PadLeft(5) + " %"
                  + "   hauteur " + ((ymax - ymin) * 100f).ToString("F1").PadLeft(5) + " %"
                  + "   plus proche " + zmin.ToString("F2") + " u"
                  + (derriere ? "   ⚠ un coin DERRIÈRE la caméra" : string.Empty));

    // Hors champ ?
    bool deborde = xmin < 0f || xmax > 1f || ymin < 0f || ymax > 1f;

    sb.AppendLine("  " + "".PadRight(18) + (deborde
        ? "  ⚠ DÉBORDE : "
          + (xmin < 0f ? "gauche " : "") + (xmax > 1f ? "droite " : "")
          + (ymin < 0f ? "bas " : "") + (ymax > 1f ? "haut " : "")
        : "  ✓ entièrement dans le champ"));
}

sb.AppendLine("=== cadrage réel (viewport : 0 = bord, 1 = bord opposé) ===");

Cadre("Pinball_Table", boites[0]);

if (boites[1].size != Vector3.zero) { Cadre("Pinball_Cabinet", boites[1]); }

Cadre("union", union);

sb.AppendLine();
sb.AppendLine("=== repère de la table, pour recouper avec les autres sondes ===");
sb.AppendLine("  racine PinballTable à " + rt.position.ToString("F4") + "   rotation " + rt.eulerAngles.ToString("F3"));
sb.AppendLine("  caméra en repère du root " + rt.InverseTransformPoint(camera.transform.position).ToString("F4"));
sb.AppendLine("  centre de l'union en repère du root " + rt.InverseTransformPoint(union.center).ToString("F4"));
sb.AppendLine("  taille de l'union en repère du root "
              + rt.InverseTransformVector(union.size).ToString("F4"));

return sb.ToString();
