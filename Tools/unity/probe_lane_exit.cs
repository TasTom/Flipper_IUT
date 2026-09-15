// Que percute la bille en haut du couloir de lancement ? LECTURE SEULE.
//
// Mesuré par `test_plunger_fix.cs`, dans le repère du bouchon au repos :
//
//     t+0,88   avance 16,514   hauteur -0,067   latéral  0,057   |v| 14,475
//     t+1,10   avance 17,350   hauteur -0,025   latéral -1,221   |v|  5,150
//     t+1,32   avance 17,372   hauteur -0,167   latéral -1,972   |v|  2,981
//     t+2,84   avance 17,238   hauteur -0,167   latéral -5,946   |v|  2,446
//
// La bille monte droit (latéral constant à 0,057), puis entre t+0,88 et t+1,10 elle perd les deux
// tiers de sa vitesse en n'avançant que de 0,85 u — un choc frontal. Ensuite `avance` et `hauteur`
// se figent et `latéral` défile à 2,5 u/s : la bille glisse le long d'une paroi sur toute la
// largeur de la table sans jamais ralentir.
//
// Trois questions, dans l'ordre :
//   1. Où s'arrête le couloir ? Y a-t-il une sortie vers l'aire de jeu, ou un mur en travers ?
//   2. Qu'est-ce qui est percuté, et à quelle cote ?
//   3. Après le choc, sur quoi la bille glisse-t-elle ?
//
// Tout est exprimé dans le repère du bouchon au repos — le même que le test, pour que les cotes se
// comparent directement.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "'PinballTable' introuvable dans la scène."; }

var rt = racine.transform;
var gameplay = rt.Find("Gameplay");
var hote = gameplay != null ? gameplay.Find("Plunger") : null;

if (hote == null) { return "'PinballTable/Gameplay/Plunger' introuvable."; }

Physics.SyncTransforms();

var origine = hote.position;
var avant = hote.forward;
var haut = hote.up;
var droite = hote.right;

float Avance(Vector3 p) { return Vector3.Dot(p - origine, avant); }
float Hauteur(Vector3 p) { return Vector3.Dot(p - origine, haut); }
float Lateral(Vector3 p) { return Vector3.Dot(p - origine, droite); }
Vector3 Point(float a, float h, float l) { return origine + avant * a + haut * h + droite * l; }

var bille = gameplay.Find("Ball");
var colBille = bille != null ? bille.GetComponent<Collider>() : null;

// La bille au repos, en repère du bouchon : c'est d'où part le lancement.
float latBille = 0.0566f, hautBille = -0.0670f;

if (colBille != null)
{
    latBille = Lateral(colBille.bounds.center);
    hautBille = Hauteur(colBille.bounds.center);
}

sb.AppendLine("=== repère du bouchon au repos ===");
sb.AppendLine("  origine " + origine.ToString("F4"));
sb.AppendLine("  bille au repos : avance " + (colBille != null ? Avance(colBille.bounds.center).ToString("F4") : "?")
              + "   hauteur " + hautBille.ToString("F4")
              + "   latéral " + latBille.ToString("F4"));
sb.AppendLine("  rayon " + (colBille != null ? colBille.bounds.extents.x.ToString("F4") : "?"));
sb.AppendLine();

// Premier objet touché, en ignorant la bille elle-même.
bool Tir(Vector3 o, Vector3 dir, float portee, out RaycastHit touche, out float distance)
{
    touche = default(RaycastHit);
    distance = float.MaxValue;

    foreach (var h in Physics.RaycastAll(o, dir, portee, ~0, QueryTriggerInteraction.Ignore))
    {
        if (h.collider == colBille) { continue; }
        if (h.distance >= distance) { continue; }

        distance = h.distance;
        touche = h;
    }

    return distance < float.MaxValue;
}

// --- 1. jusqu'où va le couloir, et que trouve-t-on devant ? ---------------------------------------
//
// On tire depuis l'intérieur du couloir, à hauteur de bille, droit devant. Le premier obstacle est
// le fond du couloir — s'il n'y en a pas sur 6 u, le couloir débouche.

sb.AppendLine("=== 1. tir droit devant, à hauteur de bille ===");
sb.AppendLine("     depuis    premier obstacle             à l'avance   hauteur   latéral");

foreach (var a in new float[] { 12.0f, 14.0f, 15.5f, 16.0f, 16.5f, 17.0f, 17.3f })
{
    var o = Point(a, hautBille, latBille);

    RaycastHit t;
    float d;

    if (Tir(o, avant, 6f, out t, out d))
    {
        var p = o + avant * d;

        sb.AppendLine("    " + a.ToString("F1").PadLeft(7) + "    " + t.collider.name.PadRight(30)
                      + "   " + Avance(p).ToString("F7").PadLeft(8)
                      + "   " + Hauteur(p).ToString("F4").PadLeft(8)
                      + "   " + Lateral(p).ToString("F4").PadLeft(8));
    }
    else
    {
        sb.AppendLine("    " + a.ToString("F1").PadLeft(7) + "    RIEN sur 6 u → le couloir débouche");
    }
}

// --- 2. la largeur du couloir, vers le haut ------------------------------------------------------
//
// Le fond du couloir est à ~17,2. Au-delà, est-ce que les parois continuent (couloir qui se
// poursuit) ou s'arrêtent-elles (couloir ouvert en haut, la bille part dans l'aire de jeu) ?

sb.AppendLine();
sb.AppendLine("=== 2. les parois, vers le haut du couloir ===");
sb.AppendLine("     avance    gauche   (nom)                      droite   (nom)");

foreach (var a in new float[] { 15.0f, 16.0f, 16.5f, 17.0f, 17.3f, 17.6f, 18.0f, 18.5f, 19.0f })
{
    var o = Point(a, hautBille, 0f);

    RaycastHit tg, td;
    float dg, dd;

    bool g = Tir(o, -droite, 1.2f, out tg, out dg);
    bool d = Tir(o, droite, 1.2f, out td, out dd);

    sb.Append("    " + a.ToString("F1").PadLeft(7) + "   ");

    sb.Append((g ? dg.ToString("F4").PadLeft(8) + "  " + tg.collider.name : "  (rien)").PadRight(31));

    sb.Append("   ");

    sb.AppendLine(d ? dd.ToString("F4").PadLeft(8) + "  " + td.collider.name : "  (rien)");
}

// --- 3. le sol, vers le haut ---------------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== 3. le sol, vers le haut (rayon vers le bas depuis 0,10 au-dessus de la bille) ===");

foreach (var a in new float[] { 15.0f, 16.0f, 16.8f, 17.2f, 17.5f, 18.0f, 19.0f, 20.0f })
{
    var o = Point(a, hautBille + 0.35f, latBille);

    RaycastHit t;
    float d;

    bool trouve = Tir(o, -haut, 4f, out t, out d);

    sb.AppendLine("    " + a.ToString("F1").PadLeft(7) + "   "
                  + (trouve ? (t.collider.name + " à l'hauteur " + Hauteur(o - haut * d).ToString("F4"))
                            : "AUCUN SOL sur 4 u ⚠"));
}

// --- 4. le plafond au-dessus du couloir ----------------------------------------------------------
//
// Ce sur quoi la bille glisse après le choc est plus HAUT que le sol du couloir (hauteur -0,167
// contre -0,2920). Un plafond au-dessus du couloir, ou un rail ?

sb.AppendLine();
sb.AppendLine("=== 4. ce qu'il y a au-dessus du couloir ===");
sb.AppendLine("     avance   au-dessus de la bille          au-dessus du couloir (latéral 0)");

foreach (var a in new float[] { 16.0f, 16.5f, 17.0f, 17.3f, 17.6f, 18.0f })
{
    RaycastHit tb, tc;
    float db, dc;

    bool b = Tir(Point(a, hautBille, latBille), haut, 3f, out tb, out db);
    bool c = Tir(Point(a, hautBille, 0f), haut, 3f, out tc, out dc);

    sb.AppendLine("    " + a.ToString("F1").PadLeft(6) + "   "
                  + (b ? tb.collider.name + " à l'hauteur " + Hauteur(Point(a, hautBille, latBille) + haut * db).ToString("F4")
                       : "rien sur 3 u").PadRight(45)
                  + "   "
                  + (c ? tc.collider.name + " à " + Hauteur(Point(a, hautBille, 0f) + haut * dc).ToString("F4")
                       : "rien sur 3 u"));
}

// --- 5. la glissade : ce que la bille longe après le choc -----------------------------------------
//
// Relevé du test : hauteur -0,167 et latéral qui défile. On tâte les quatre directions à la cote
// exacte où la bille s'est figée en avance.

sb.AppendLine();
sb.AppendLine("=== 5. autour du point d'arrêt de la bille (avance 17,238, hauteur -0,167) ===");

{
    var o = Point(17.238f, -0.167f, 0.057f);

    RaycastHit t;
    float d;

    sb.AppendLine("    latéral −  " + (Tir(o, -droite, 2f, out t, out d)
        ? t.collider.name + " à " + d.ToString("F4") + "  (latéral " + Lateral(o - droite * d).ToString("F4") + ")"
        : "rien sur 2 u"));

    sb.AppendLine("    latéral +  " + (Tir(o, droite, 2f, out t, out d)
        ? t.collider.name + " à " + d.ToString("F4") + "  (latéral " + Lateral(o + droite * d).ToString("F4") + ")"
        : "rien sur 2 u"));

    sb.AppendLine("    hauteur −  " + (Tir(o, -haut, 1f, out t, out d)
        ? t.collider.name + " à " + d.ToString("F4") + "  (hauteur " + Hauteur(o - haut * d).ToString("F4") + ")"
        : "rien sur 1 u"));

    sb.AppendLine("    hauteur +  " + (Tir(o, haut, 1f, out t, out d)
        ? t.collider.name + " à " + d.ToString("F4") + "  (hauteur " + Hauteur(o + haut * d).ToString("F4") + ")"
        : "rien sur 1 u"));

    sb.AppendLine("    avance +   " + (Tir(o, avant, 2f, out t, out d)
        ? t.collider.name + " à " + d.ToString("F4") + "  (avance " + Avance(o + avant * d).ToString("F4") + ")"
        : "rien sur 2 u"));
}

return sb.ToString();
