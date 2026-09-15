// Pose le DÉCLENCHEUR de la zone de perte sur l'hôte `DrainZone`. ÉCRITURE.
//
// ── Ce que la mesure a donné ──────────────────────────────────────────────────────────
// `probe_fond_table.cs` puis `probe_drain_pose.cs` ont relevé, sans rien supposer :
//   • le sol du plateau s'arrête à z = 0,330, au même endroit sur toute la largeur
//     (l'aire de jeu va de x −4,286 à +4,286) ;
//   • SOUS cette ouverture, il n'y a RIEN sur 15 u — ni sol, ni cuve. Une bille qui passe
//     le bord tombe indéfiniment : elle ne sera jamais rattrapée par un plancher, c'est
//     donc au déclencheur de la compter, et il doit être placé sous le bord, pas au fond ;
//   • l'hôte `DrainZone` est à (0 ; −0,333 ; 0), rotation identité, échelle 1, et ne porte
//     AUCUN collider (le composant le signale au démarrage).
//
// ── Le collider va SUR l'hôte, et c'est forcé ─────────────────────────────────────────
// `DrainZone.cs` écoute `OnTriggerEnter` ; Unity ne remonte PAS ce message dans la
// hiérarchie. Un déclencheur posé sur un enfant ne réveillerait jamais le script du parent.
// (Le menu 3 « Rendre la scène neutre » retire de toute façon les colliders des hôtes ET de
// leurs enfants — `Strip` fait `GetComponents<Collider>()` sur les deux — le placer sur un
// enfant ne le protégerait donc pas.)
//
// ── La cote qui compte : le dessus du déclencheur ─────────────────────────────────────
// Une bille POSÉE sur le plateau a son centre à z ≥ 0,330 (le bord du sol) : au point de
// bascule, elle est encore soutenue. Son point le plus bas descend alors à 0,330 − 0,225
// = 0,105. Le dessus du déclencheur est donc mis à z = 0,05 — SOUS 0,105 — pour qu'une
// bille encore posée sur le plateau ne puisse jamais le toucher, même au bord exact.
// Sans cette marge, une bille rebondissant près du drain serait comptée perdue alors qu'elle
// est encore visible sur le plateau — et `BallManager` la ferait disparaître sous les yeux
// du joueur.
//
// ── Le couloir de lancement est EXCLU ─────────────────────────────────────────────────
// La face interne du séparateur est à x = 4,286 ; le couloir est derrière. Le bord droit du
// déclencheur est mis à x = 4,25 : la bille parquée au lanceur est à x 4,670, donc sa
// surface ne descend pas sous 4,445 — elle ne peut pas l'atteindre. Compter comme perdue une
// bille posée sur le lanceur serait un bug vicieux, car elle est légitimement immobile.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "PinballTable introuvable."; }

var rt = racine.transform;

Transform hote = null;

foreach (var t in racine.GetComponentsInChildren<Transform>(true))
{
    if (t.name == "DrainZone") { hote = t; break; }
}

sb.AppendLine("=== hôte DrainZone ===");

if (hote == null) { return sb.AppendLine("  ABSENT — rien à habiller, on ne le crée pas.").ToString(); }

sb.AppendLine("  position locale (repère table) : " + rt.InverseTransformPoint(hote.position).ToString("F4"));
sb.AppendLine("  échelle monde (lossyScale)     : " + hote.lossyScale.ToString("F4"));
sb.AppendLine("  script DrainZone               : " + (hote.GetComponent("DrainZone") != null ? "présent" : "⚠ ABSENT"));
sb.AppendLine("  colliders avant                : " + hote.GetComponents<Collider>().Length);
sb.AppendLine();

// Le volume voulu, exprimé dans le repère de la TABLE — celui dans lequel tout a été mesuré.
Vector3 cCentre = new Vector3(-0.175f, -0.300f, -0.200f);
Vector3 cTaille = new Vector3(8.850f, 1.800f, 0.500f);

sb.AppendLine("=== volume voulu (repère table) ===");
sb.AppendLine("  x [" + (cCentre.x - cTaille.x * 0.5f).ToString("F3") + " → " + (cCentre.x + cTaille.x * 0.5f).ToString("F3")
    + "]   y [" + (cCentre.y - cTaille.y * 0.5f).ToString("F3") + " → " + (cCentre.y + cTaille.y * 0.5f).ToString("F3")
    + "]   z [" + (cCentre.z - cTaille.z * 0.5f).ToString("F3") + " → " + (cCentre.z + cTaille.z * 0.5f).ToString("F3") + "]");
sb.AppendLine("  sol du plateau à z = 0,330   bord bas de bille posée = 0,105   dessus du trigger = 0,050");
sb.AppendLine();

// --- non destructif : un collider déjà posé est laissé tel quel ------------------------
var deja = hote.GetComponents<Collider>();

if (deja.Length > 0)
{
    sb.AppendLine("Un collider est DÉJÀ sur l'hôte — il est laissé intact (règle du projet).");
    sb.AppendLine("Pour rejouer la pose : retirer le collider, puis relancer ce script.");
    foreach (var c in deja)
    {
        sb.AppendLine("  " + c.GetType().Name + "  isTrigger = " + c.isTrigger
            + "  bounds monde " + c.bounds.center.ToString("F3") + " / " + c.bounds.size.ToString("F3"));
    }
    return sb.ToString();
}

// --- contrôle avant écriture : le volume est-il libre ? -------------------------------
var mondeCentre = rt.TransformPoint(cCentre);
var demi = Vector3.Scale(cTaille * 0.5f, rt.lossyScale);
var occupants = Physics.OverlapBox(mondeCentre, demi, rt.rotation, ~0, QueryTriggerInteraction.Collide);

sb.AppendLine("=== contrôle avant écriture ===");
sb.AppendLine("  OverlapBox sur le volume voulu → " + occupants.Length + " collider(s)");

foreach (var c in occupants)
{
    sb.AppendLine("    '" + c.name + "' (" + c.GetType().Name + ")");
}

if (occupants.Length > 0)
{
    sb.AppendLine("  ⚠ le volume n'est pas libre — on n'écrit RIEN. Un déclencheur posé par-dessus");
    sb.AppendLine("    un collider existant compterait des billes qui roulaient encore.");
    return sb.ToString();
}

sb.AppendLine("  ✓ volume libre");
sb.AppendLine();

// --- écriture --------------------------------------------------------------------------
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

var col = UnityEditor.Undo.AddComponent<BoxCollider>(hote.gameObject);
UnityEditor.Undo.RecordObject(col, "Poser le déclencheur de drain");

// `col.center` et `col.size` s'expriment dans les axes PROPRES de l'hôte, échelle de
// l'hôte comprise. On part donc du volume mesuré dans le repère de la table et on le
// convertit, au lieu de recopier des cotes — la leçon du lanceur, dont un `size` écrit en
// unités de scène était sorti 60 fois trop petit sous la racine du FBX.
col.center = hote.InverseTransformPoint(mondeCentre);

Vector3 ls = hote.lossyScale;
col.size = new Vector3(cTaille.x / ls.x, cTaille.y / ls.y, cTaille.z / ls.z);
col.isTrigger = true;

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

// --- relecture -------------------------------------------------------------------------
sb.AppendLine("=== déclencheur posé ===");
sb.AppendLine("  BoxCollider  center " + col.center.ToString("F4") + "   size " + col.size.ToString("F4"));
sb.AppendLine("  isTrigger = " + col.isTrigger + (col.isTrigger ? "   ✓" : "   ⚠ IL EN FAUT UN"));
sb.AppendLine("  bounds monde  centre " + col.bounds.center.ToString("F3") + "   taille " + col.bounds.size.ToString("F3"));

// Contrôle aller-retour : le collider relu, ramené dans le repère de la table, doit rendre
// les cotes demandées. C'est le seul contrôle qui vaille — une boîte monde est alignée sur
// le monde et noie une cote fausse dans son englobante.
Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
Vector3 mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);

for (int i = 0; i < 8; i++)
{
    Vector3 coin = col.bounds.center + new Vector3(
        (i & 1) == 0 ? -col.bounds.extents.x : col.bounds.extents.x,
        (i & 2) == 0 ? -col.bounds.extents.y : col.bounds.extents.y,
        (i & 4) == 0 ? -col.bounds.extents.z : col.bounds.extents.z);
    Vector3 l = rt.InverseTransformPoint(coin);
    mn = Vector3.Min(mn, l); mx = Vector3.Max(mx, l);
}

sb.AppendLine("  relu en repère table : x [" + mn.x.ToString("F3") + " → " + mx.x.ToString("F3")
    + "]  y [" + mn.y.ToString("F3") + " → " + mx.y.ToString("F3")
    + "]  z [" + mn.z.ToString("F3") + " → " + mx.z.ToString("F3") + "]");
sb.AppendLine("  (l'englobante monde est plus large que le volume réel quand la table est inclinée)");
sb.AppendLine();

// --- vérification par la physique : qui touche, qui ne touche pas ----------------------
sb.AppendLine("=== ce que le déclencheur attrape (sphères de rayon 0,225, repère table) ===");
sb.AppendLine("  cas                                    centre                  attendu | mesuré");
sb.AppendLine("  ---------------------------------------+-------------------------+--------+-------");

var cas = new[]
{
    new { nom = "posée au bord du sol",          p = new Vector3(0f, 0.225f, 0.330f),     veut = false },
    new { nom = "posée sur le plateau",          p = new Vector3(0f, 0.225f, 0.500f),     veut = false },
    new { nom = "au mur droit, encore posée",    p = new Vector3(4.061f, 0.225f, 0.400f), veut = false },
    new { nom = "au mur gauche, encore posée",   p = new Vector3(-4.061f, 0.225f, 0.400f),veut = false },
    new { nom = "parquée au lanceur (couloir)",  p = new Vector3(4.670f, 0.392f, 1.000f), veut = false },
    new { nom = "tombée sous le bord, au centre",p = new Vector3(0f, 0.200f, 0.150f),     veut = true },
    new { nom = "tombée au mur droit",           p = new Vector3(4.061f, 0.200f, 0.150f), veut = true },
    new { nom = "tombée au mur gauche",          p = new Vector3(-4.061f, 0.200f, 0.150f),veut = true },
    new { nom = "au fond de l'ouverture",        p = new Vector3(0f, -0.150f, -0.200f),   veut = true },
};

int justes = 0;

foreach (var c in cas)
{
    var centre = rt.TransformPoint(c.p);
    var touche = Physics.OverlapSphere(centre, 0.225f, ~0, QueryTriggerInteraction.Collide);

    bool dedans = false;

    foreach (var t in touche) { if (t == col) { dedans = true; } }

    if (dedans == c.veut) { justes++; }

    sb.AppendLine("  " + c.nom.PadRight(38) + " | " + c.p.ToString("F3").PadRight(23) + " | "
        + (c.veut ? "OUI" : "non").PadRight(6) + " | " + (dedans ? "OUI" : "non")
        + (dedans == c.veut ? "  ✓" : "  ✗ ÉCART"));
}

sb.AppendLine();
sb.AppendLine("  " + justes + " / " + cas.Length + " cas conformes"
    + (justes == cas.Length ? "   ✓" : "   ⚠ à revoir"));
sb.AppendLine();
sb.AppendLine("scène modifiée (Ctrl+Z annule, Ctrl+S conserve). Aucun script n'enregistre.");

return sb.ToString();
