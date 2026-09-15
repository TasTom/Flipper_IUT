// SONDE (lecture seule) — la pénétration EXACTE entre le collider du flipper et les colliders
// du slingshot, par `Physics.ComputePenetration`.
//
// Pourquoi cette sonde : `Physics.OverlapBox` dit « ces deux boîtes se recouvrent », il ne dit
// ni de combien ni dans quel sens. Or c'est la profondeur qui décide de tout — Unity résout une
// pénétration en poussant le corps dynamique, et c'est très probablement ce qui a arraché les
// deux `Rigidbody` de flipper de leur ancre (relevé du 2026-09-15 : le pivot droit s'était
// retrouvé au CENTRE de la table).
//
// `Physics.ComputePenetration` rend la séparation exacte entre deux colliders à une pose donnée :
//   direction + distance = le vecteur qu'il faudrait appliquer à A pour le sortir de B.
// Distance nulle = pas de recouvrement. C'est la seule mesure qui tranche.
//
// Pour chaque côté, on teste :
//   bat ↔ bande réactive   (`Slingshot_*`, le BoxCollider posé par `place_scene_slingshots.cs`)
//   bat ↔ corps de table   (`Body_L` / `Body_R`, le MeshCollider venu du FBX)
// et cela aux trois positions du flipper (neutre, repos, actif).

var sb = new System.Text.StringBuilder();

const float U = 16.6666667f;

var racine = GameObject.Find("PinballTable");
if (racine == null) { return "PinballTable introuvable."; }

var rt = racine.transform;
var gameplay = rt.Find("Gameplay");
if (gameplay == null) { return "Gameplay introuvable."; }

string Mm(float u) { return (u * 1000f / U).ToString("F2") + " mm"; }

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    var pivot = gameplay.Find(nom);

    sb.AppendLine();
    sb.AppendLine("### " + nom);

    if (pivot == null) { sb.AppendLine("  ABSENT"); continue; }

    bool aGauche = nom.Contains("Left");

    var porteur = pivot.Find("Flipper_Bat");
    var piece = porteur != null ? porteur.GetComponentInChildren<MeshFilter>() : null;

    if (piece == null) { sb.AppendLine("  pas de bat"); continue; }

    var boite = piece.GetComponent<BoxCollider>();

    if (boite == null) { sb.AppendLine("  pas de BoxCollider sur le bat"); continue; }

    var cibles = new System.Collections.Generic.List<Collider>();

    var bande = GameObject.Find("PinballTable/Gameplay/Slingshot_" + (aGauche ? "Left" : "Right"));

    if (bande != null)
    {
        var c = bande.GetComponent<Collider>();
        if (c != null) { cibles.Add(c); }
    }

    var corps = GameObject.Find("PinballTable/Table/Pinball_Table/Slingshots/Body_"
                                + (aGauche ? "L" : "R"));

    if (corps != null)
    {
        foreach (var c in corps.GetComponents<Collider>()) { cibles.Add(c); }
    }

    sb.AppendLine("  pivot table (" + rt.InverseTransformPoint(pivot.position).x.ToString("F4")
                  + " ; " + rt.InverseTransformPoint(pivot.position).z.ToString("F4") + ")");
    sb.AppendLine("  cibles : " + cibles.Count + " collider(s)");

    foreach (var cas in new[] { new { nom = "neutre", a = 0f },
                               new { nom = "repos", a = aGauche ? -30f : 30f },
                               new { nom = "actif", a = aGauche ? 30f : -30f } })
    {
        // La rotation du balayage se prend autour de l'axe du joint, qui est le Z local du pivot.
        var q = pivot.rotation * Quaternion.AngleAxis(cas.a, Vector3.forward);
        var pos = pivot.position;
        var rot = q;

        sb.AppendLine("  '" + cas.nom + "' (" + cas.a.ToString("F0") + "°) :");

        foreach (var c in cibles)
        {
            Vector3 dir;
            float dist;

            bool touche = Physics.ComputePenetration(
                boite, pos, rot, c, c.transform.position, c.transform.rotation, out dir, out dist);

            if (!touche || dist <= 0f)
            {
                sb.AppendLine("      " + c.name.PadRight(18) + " [" + c.GetType().Name + "]"
                              + "   pas de recouvrement ✓");
                continue;
            }

            // Sens de la sortie, exprimé dans le repère de table : vers le centre (jeu) ou vers
            // le mur ? C'est ce qui dit si le flipper est repoussé dans l'aire de jeu ou dans
            // le décor.
            var dirTable = rt.InverseTransformDirection(dir);

            sb.AppendLine("      " + c.name.PadRight(18) + " [" + c.GetType().Name + "]"
                          + "   RECOUVREMENT " + Mm(dist)
                          + "   sortie table (" + dirTable.x.ToString("F2") + " ; "
                          + dirTable.y.ToString("F2") + " ; " + dirTable.z.ToString("F2") + ")");
        }
    }
}

// Le seuil à ne pas franchir : une bille mesure 0,45 u. Une pénétration de quelques dixièmes de
// millimètre est un frôlement d'arête ; au-delà du millimètre, la physique pousse pour de bon.
sb.AppendLine();
sb.AppendLine("(rappel : une bille = 0,45 u = 27,00 mm)");

var chemin = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "flippers_penetration.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(chemin));
System.IO.File.WriteAllText(chemin, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(chemin) + "  (" + sb.Length + " car.)";
