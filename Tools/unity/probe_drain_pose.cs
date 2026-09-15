// Le déclencheur de drain : où, et de quelle taille ? LECTURE SEULE.
//
// `probe_fond_table.cs` a montré que le sol du plateau s'arrête entre z = 0,35 et z = 0,20,
// et que RIEN ne subsiste en dessous : ni sol, ni murs — `Left`, `Divider` et `LaneOuter`
// commencent tous les trois à z = 0,285. L'ouverture basse fait donc toute la largeur de
// l'aire de jeu.
//
// Reste à poser le déclencheur sur le bon volume. Quatre choses se mesurent, plutôt que de
// les supposer :
//   1. l'hôte `DrainZone` — pose, échelle, colliders, champs sérialisés ;
//   2. le bord exact du sol, et s'il est le même sur toute la largeur ;
//   3. ce qu'il y a SOUS l'ouverture — sans quoi on ne sait pas jusqu'où descendre ;
//   4. ce que le volume candidat rencontre déjà (`OverlapBox`), pour ne pas poser un
//      déclencheur par-dessus un collider existant.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "PinballTable introuvable."; }

var rt = racine.transform;
var bas = -rt.up;

sb.AppendLine("échelle du root  : " + rt.lossyScale.ToString("F4"));
sb.AppendLine("rotation du root : " + rt.rotation.eulerAngles.ToString("F3"));
sb.AppendLine();

// --- 1. l'hôte -----------------------------------------------------------------------
Transform drain = null;

foreach (var t in racine.GetComponentsInChildren<Transform>(true))
{
    if (t.name == "DrainZone") { drain = t; break; }
}

sb.AppendLine("=== hôte DrainZone ===");

if (drain == null) { sb.AppendLine("  ABSENT — il n'y a rien à habiller."); }
else
{
    sb.AppendLine("  position locale (repère table) : " + rt.InverseTransformPoint(drain.position).ToString("F4"));
    sb.AppendLine("  rotation locale                : "
        + (Quaternion.Inverse(rt.rotation) * drain.rotation).eulerAngles.ToString("F3"));
    sb.AppendLine("  échelle locale                 : " + drain.localScale.ToString("F4"));
    sb.AppendLine("  actif dans la hiérarchie       : " + drain.gameObject.activeInHierarchy);

    var noms = new System.Text.StringBuilder();

    foreach (var c in drain.GetComponents<Component>())
    {
        if (noms.Length > 0) { noms.Append(", "); }
        noms.Append(c == null ? "(null)" : c.GetType().Name);
    }

    sb.AppendLine("  composants                     : " + noms);

    var cols = drain.GetComponents<Collider>();

    sb.AppendLine("  colliders                      : " + cols.Length
        + (cols.Length == 0 ? "   ← aucun : c'est ce que le composant signale au démarrage" : ""));

    foreach (var c in cols)
    {
        sb.AppendLine("    " + c.GetType().Name + "   isTrigger = " + c.isTrigger);

        var bc = c as BoxCollider;

        if (bc != null)
        {
            sb.AppendLine("      center " + bc.center.ToString("F4") + "   size " + bc.size.ToString("F4"));
        }
    }

    var sc = drain.GetComponent("DrainZone");

    sb.AppendLine("  script DrainZone               : " + (sc != null ? "présent" : "⚠ ABSENT"));

    if (sc != null)
    {
        const System.Reflection.BindingFlags TOUT = System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        foreach (var f in sc.GetType().GetFields(TOUT))
        {
            object v;

            try { v = f.GetValue(sc); } catch { v = "(illisible)"; }

            sb.AppendLine("    " + f.Name.PadRight(16) + " = " + (v == null ? "null" : v.ToString()));
        }
    }
}

// --- 2. le bord du sol ---------------------------------------------------------------
sb.AppendLine();
sb.AppendLine("=== bord du sol, par pas de 0,005 (repère table) ===");
sb.AppendLine("  x       plus petit z AVEC sol | plus grand z SANS sol");
sb.AppendLine("  --------+----------------------+---------------------");

foreach (float x in new[] { -4.2f, -4.0f, -2.0f, 0f, 2.0f, 4.0f, 4.5f, 4.8f })
{
    float minAvec = float.NaN;
    float maxSans = float.NaN;

    for (float z = -0.10f; z <= 0.60f; z += 0.005f)
    {
        if (Sol(x, z) && float.IsNaN(minAvec)) { minAvec = z; }
    }

    for (float z = 0.60f; z >= -0.10f; z -= 0.005f)
    {
        if (!Sol(x, z) && float.IsNaN(maxSans)) { maxSans = z; }
    }

    sb.AppendLine("  " + x.ToString("F2").PadLeft(5) + "   | "
        + (float.IsNaN(minAvec) ? "aucun" : minAvec.ToString("F3")).PadLeft(20) + " | "
        + (float.IsNaN(maxSans) ? "aucun" : maxSans.ToString("F3")).PadLeft(20));
}

// --- 3. sous l'ouverture -------------------------------------------------------------
sb.AppendLine();
sb.AppendLine("=== ce qu'on trouve SOUS l'ouverture (rayon de 15 u vers le bas) ===");

foreach (float x in new[] { -4.0f, -2.0f, 0f, 2.0f, 4.0f })
{
    var o = rt.TransformPoint(new Vector3(x, 0.10f, 0f));

    if (Physics.Raycast(o, bas, out RaycastHit h, 15f, ~0, QueryTriggerInteraction.Ignore))
    {
        sb.AppendLine("  x " + x.ToString("F2").PadLeft(5) + " → '" + h.collider.name + "' à y local "
            + rt.InverseTransformPoint(h.point).y.ToString("F4")
            + "  (distance " + h.distance.ToString("F3") + ")");
    }
    else { sb.AppendLine("  x " + x.ToString("F2").PadLeft(5) + " → RIEN sur 15 u : la bille tomberait indéfiniment"); }
}

// --- 4. le volume candidat -----------------------------------------------------------
// Cotes choisies : toute la largeur ouvrante, du mur gauche à la face interne du séparateur
// (le couloir de lancement, derrière, n'est pas une perte) ; sous le bord du sol ; assez
// profond pour qu'une bille qui tombe ne traverse pas la boîte entre deux pas.
Vector3 cCentre = new Vector3(-0.207f, -0.450f, -0.1175f);
Vector3 cTaille = new Vector3(8.986f, 2.100f, 0.665f);

sb.AppendLine();
sb.AppendLine("=== volume candidat (repère table) ===");
sb.AppendLine("  centre " + cCentre.ToString("F4") + "   taille " + cTaille.ToString("F4"));

var mondeCentre = rt.TransformPoint(cCentre);
var demi = Vector3.Scale(cTaille * 0.5f, rt.lossyScale);

var touche = Physics.OverlapBox(mondeCentre, demi, rt.rotation, ~0, QueryTriggerInteraction.Collide);

sb.AppendLine("  OverlapBox → " + touche.Length + " collider(s)");

foreach (var c in touche)
{
    sb.AppendLine("    '" + c.name + "'  (" + c.GetType().Name + ")"
        + "  sur la table : " + (c.transform.IsChildOf(rt) ? "oui" : "NON")
        + "  y local " + rt.InverseTransformPoint(c.bounds.center).y.ToString("F3"));
}

// --- 5. repères ----------------------------------------------------------------------
sb.AppendLine();
sb.AppendLine("=== repères ===");

var bille = rt.Find("Gameplay/Ball");

if (bille != null)
{
    sb.AppendLine("  bille posée : " + rt.InverseTransformPoint(bille.position).ToString("F4"));
}

var bal = rt.Find("Gameplay/BallSpawnPoint");

if (bal != null)
{
    sb.AppendLine("  point d'apparition : " + rt.InverseTransformPoint(bal.position).ToString("F4"));
}

// Le tag de la zone : le GDD recommande « Drain », la CLAUDE.md décrit « tag Ball ».
// On constate ce qui existe, la décision revient à l'utilisateur.
var tags = UnityEditorInternal.InternalEditorUtility.tags;
var liste = new System.Text.StringBuilder();

foreach (var t in tags) { if (liste.Length > 0) { liste.Append(", "); } liste.Append(t); }

sb.AppendLine("  tags du projet : " + liste);
sb.AppendLine("  tag de DrainZone : " + (drain != null ? drain.tag : "—"));

return sb.ToString();

// --- helpers -------------------------------------------------------------------------
// « Y a-t-il du plateau à cet endroit ? » — un sol, c'est une surface à y ≈ 0 dans le
// repère de la table. En deçà de 0,15 c'est autre chose (une pièce, un mur) ou rien.
bool Sol(float x, float z)
{
    var o = rt.TransformPoint(new Vector3(x, 3f, z));

    if (!Physics.Raycast(o, bas, out RaycastHit h, 6f, ~0, QueryTriggerInteraction.Ignore)) { return false; }

    return Mathf.Abs(rt.InverseTransformPoint(h.point).y) < 0.15f;
}
