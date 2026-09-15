// Largeur du couloir le long de son axe, et jusqu'où descend son sol. LECTURE SEULE.
//
// Deux cotes à relever avant de retoucher le bouchon :
//
//   1. **La largeur du couloir à la cote du bouchon.** Le bouchon fait 0,50 u de large et le
//      couloir 0,5667 u : il reste une encoche concave de ~0,033 u de chaque côté, et une bille
//      qui s'y engage prend un contact d'arête au lieu d'un contact de face. Élargir le bouchon
//      juste ce qu'il faut demande la largeur **à la cote du bouchon** (z local 0,078 → 0,178),
//      pas à celle de la bille — les parois ne sont pas forcément parallèles.
//
//   2. **Où s'arrête le sol du couloir.** La bille qui redescend la pente recule ; si le sol
//      s'arrête avant le logement du lanceur, elle tombe dans le trou. C'est ce qui borne le
//      recul maximal de la bille pendant une charge, donc la profondeur d'enfoncement possible
//      du bouchon dans la bille au relâchement.
//
// Tout est exprimé en repère de l'HÔTE `Plunger` : c'est le repère dans lequel le bouchon est
// coté (Tools/unity/place_scene_plunger.cs), donc celui dans lequel une largeur se compare.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "'PinballTable' introuvable dans la scène."; }

var rt = racine.transform;
var gameplay = rt.Find("Gameplay");
var hote = gameplay != null ? gameplay.Find("Plunger") : null;
var piece = hote != null ? hote.Find("Plunger_Rod") : null;
var bille = gameplay != null ? gameplay.Find("Ball") : null;

if (hote == null) { return "'PinballTable/Gameplay/Plunger' introuvable."; }

Physics.SyncTransforms();

// Premier objet touché, en ignorant un collider donné.
bool Rayon(Vector3 origine, Vector3 direction, float portee, Collider ignorer, out RaycastHit touche)
{
    touche = default(RaycastHit);

    float mieux = float.MaxValue;
    bool trouve = false;

    foreach (var h in Physics.RaycastAll(origine, direction, portee, ~0, QueryTriggerInteraction.Ignore))
    {
        if (ignorer != null && h.collider == ignorer) { continue; }
        if (h.distance >= mieux) { continue; }

        mieux = h.distance;
        touche = h;
        trouve = true;
    }

    return trouve;
}

var colBille = bille != null ? bille.GetComponent<Collider>() : null;

float yBalle = 0.2f;

sb.AppendLine("=== la bille, en repère de l'hôte ===");

if (colBille == null)
{
    sb.AppendLine("  aucune bille avec collider sous Gameplay : les hauteurs de visée sont estimées.");
}
else
{
    var local = hote.InverseTransformPoint(colBille.bounds.center);

    yBalle = local.y;

    sb.AppendLine("  centre " + local.ToString("F4")
                  + "   rayon " + colBille.bounds.extents.x.ToString("F4"));
}

// --- 1. largeur du couloir, cote par cote --------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== 1. largeur du couloir le long de l'axe (repère de l'hôte) ===");
sb.AppendLine("     z       gauche   (nom)                    droite   (nom)                    largeur");
sb.AppendLine("  --------   ------------------------------   ------------------------------   --------");

var cotes = new float[] { -0.40f, -0.20f, 0.00f, 0.078f, 0.13f, 0.178f, 0.25f, 0.40f, 0.60f, 0.90f, 1.30f };

foreach (var z in cotes)
{
    var origine = hote.TransformPoint(new Vector3(0f, yBalle, z));

    RaycastHit g, d;
    bool aGauche = Rayon(origine, -hote.right, 0.8f, colBille, out g);
    bool aDroite = Rayon(origine, hote.right, 0.8f, colBille, out d);

    if (!aGauche && !aDroite) { continue; }

    string nom = string.Empty;

    sb.Append("  " + z.ToString("F3").PadLeft(8) + "   ");

    if (aGauche) { nom = g.collider.name; sb.Append(g.distance.ToString("F4").PadLeft(8) + "  " + nom.PadRight(30)); }
    else { sb.Append("  (rien)  " + "".PadRight(30)); }

    sb.Append("   ");

    if (aDroite) { sb.Append(d.distance.ToString("F4").PadLeft(8) + "  " + d.collider.name.PadRight(30)); }
    else { sb.Append("  (rien)  " + "".PadRight(30)); }

    if (aGauche && aDroite)
    {
        sb.Append("   " + (g.distance + d.distance).ToString("F4"));
    }

    sb.AppendLine();
}

// --- 2. jusqu'où descend le sol du couloir -------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== 2. le sol du couloir, vers le bas de la table ===");
sb.AppendLine("  on descend un rayon depuis 0,3 u au-dessus de la bille, à chaque cote :");

foreach (var z in new float[] { 0.60f, 0.40f, 0.20f, 0.10f, 0.00f, -0.10f, -0.20f, -0.40f, -0.60f, -0.90f, -1.20f })
{
    var origine = hote.TransformPoint(new Vector3(0f, yBalle + 0.3f, z));

    RaycastHit sol;
    bool touche = Rayon(origine, -hote.up, 3f, colBille, out sol);

    sb.AppendLine("    z " + z.ToString("F3").PadLeft(7) + "   " + (touche
        ? "sol à " + (sol.distance - 0.3f).ToString("F4") + " sous la bille   " + sol.collider.name
        : "AUCUN SOL sur 3 u ⚠"));
}

// --- 3. le bouchon, pour mémoire -----------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== 3. le bouchon (rappel de sa cote, en axes de l'hôte) ===");

if (piece == null)
{
    sb.AppendLine("  'Plunger_Rod' absente sous 'Plunger'.");
}
else
{
    var boite = piece.GetComponent<BoxCollider>();

    if (boite == null)
    {
        sb.AppendLine("  'Plunger_Rod' n'a pas de BoxCollider.");
    }
    else
    {
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            var coin = boite.center + Vector3.Scale(boite.size * 0.5f, new Vector3(x, y, z));
            var local = hote.InverseTransformPoint(piece.TransformPoint(coin));

            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
        }

        sb.AppendLine("  min " + min.ToString("F4") + "   max " + max.ToString("F4")
                      + "   largeur " + (max.x - min.x).ToString("F4")
                      + "   hauteur " + (max.y - min.y).ToString("F4")
                      + "   épaisseur " + (max.z - min.z).ToString("F4"));
    }
}

return sb.ToString();
