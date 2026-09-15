// Pose `Plastic_with_decal` sur les deux slingshots. ÉCRITURE (annulable, non enregistrée).
//
// ── Ce que ce script remplace ─────────────────────────────────────────────────────────
// Les slingshots étaient engendrés par `build_table.py` (section `Slingshots`), faute
// d'équivalent dans le dépôt. `assemble_parts.py` ne les engendre plus depuis le 2026-09-15 :
// `Plastic with decal` (`Miscellaneous`) prend leur place, posé ici, sous les hôtes
// `Slingshot_Left` / `Slingshot_Right` — comme les flippers, c'est une pièce à part, pas de la
// géométrie de table.
//
// ── L'orientation : les axes sont MESURÉS dans la pièce, pas supposés ─────────────────
// Première tentative, mesurée et fausse : poser la rotation directement sur le GameObject a
// couché la plaque EN TRAVERS de la face. La raison est dans le FBX — un maillage importé vit
// sous des nœuds qui portent leur propre quart de tour (l'axe vertical de Blender n'est pas celui
// d'Unity), si bien que les axes du GameObject ne sont pas ceux du maillage. Supposer que le
// grand axe local est Z revient à supposer une convention que l'importeur a déjà défaite.
//
// Ce script relève donc la boîte de la pièce **dans son propre repère** et en déduit ses trois
// axes : le plus long est le grand axe, le plus court l'épaisseur, le reste la largeur. Puis il
// construit la rotation qui les amène là où ils doivent aller :
//
//     grand axe  →  la diagonale de la face   (la plaque la longe)
//     largeur    →  la verticale de la table  (elle se dresse)
//     épaisseur  →  la normale de la face     (elle s'applique contre le slingshot)
//
// La rotation est composée avec `R = M(cible) · M(source)⁻¹` sur des bases orthonormées : aucune
// convention d'axe n'est postulée, seulement les trois longueurs mesurées.
//
// ── Le placement : la boîte est relue, pas déduite ────────────────────────────────────
// Le pivot de la pièce n'est pas à son centre (bornes locales : de −97,86 à +19,46 mm sur le
// grand axe). On relève donc la boîte réelle aux 8 coins après rotation, puis on translate pour
// que le bas de la plaque repose à l'élévation voulue et que son centre tombe au milieu de la
// face. Déduire la translation d'un centre supposé donnerait un décalage de 39 mm.
//
// ── Idempotence ───────────────────────────────────────────────────────────────────────
// Un enfant `Plastique_Decal` déjà posé est **reposé** (détruit puis recréé) : ce script est la
// source de vérité de cette pose, et une pièce assise sur d'anciennes cotes ne se voit pas.

var sb = new System.Text.StringBuilder();

const float U = 16.6666667f;   // unités Unity par mètre

// Les cotes de la face — identiques à `place_scene_slingshots.cs`.
const float TOP_X = 0.1925f * U;      // 3,20833
const float TOP_Z = 0.330f * U;       // 5,50000
const float BOT_X = 0.091915f * U;    // 1,53192
const float BOT_Z = 0.085f * U;       // 1,41667

// Réglages de la pose.
const float ELEVATION = 0.00f;        // hauteur du bas de la plaque au-dessus du plateau, en u
const float RECUL = 0.00f;            // décalage le long de la face (0 = centrée dessus)
const float ECART_FACE = 0.005f;      // jeu entre la plaque et le plan de la face (0,3 mm)

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;

if (rt == null) { return "PinballTable introuvable."; }

var gameplay = rt.Find("Gameplay");

if (gameplay == null) { return "'PinballTable/Gameplay' introuvable."; }

const string cheminPiece = "Assets/Models/Parts/Miscellaneous/Plastic_with_decal.fbx";

var modele = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(cheminPiece);

if (modele == null) { return cheminPiece + " introuvable."; }

// Un plastique de slingshot est OPAQUE. Le mapper sur `PlayfieldGlass_Mat` (alpha 0,06) a rendu
// les slingshots transparents — une erreur de ma part : le nom de la pièce (« Plastic - Clear »)
// a été pris pour une consigne de transparence. Sur un flipper, cette plaque est le corps du
// slingshot, pas une vitre.
//
// `SlingshotPlastic_Mat` est le matériau du projet qui porte cette couleur, créé par
// `rendre_plastique_opaque.cs` s'il manque. Le DÉCAL, lui, est laissé tel quel : c'est le
// sous-maillage imprimé, il a sa propre couleur.
var matPlastique = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
    "Assets/Materials/SlingshotPlastic_Mat.mat");

if (matPlastique == null)
{
    sb.AppendLine("⚠ SlingshotPlastic_Mat introuvable — matériaux laissés tels quels");
}

sb.AppendLine("=== scène '" + scene.name + "' ===");
sb.AppendLine("modèle : " + modele.name);
sb.AppendLine("élévation " + ELEVATION.ToString("F4") + " u   recul " + RECUL.ToString("F4")
              + " u   jeu " + ECART_FACE.ToString("F4") + " u");

foreach (var cote in new[] { new { nom = "Slingshot_Right", s = 1f },
                             new { nom = "Slingshot_Left", s = -1f } })
{
    sb.AppendLine();
    sb.AppendLine("### " + cote.nom);

    var hote = gameplay.Find(cote.nom);

    if (hote == null) { sb.AppendLine("  ABSENT"); continue; }

    // Repose : on ne se fie pas à une pose antérieure.
    var ancienne = hote.Find("Plastique_Decal");

    if (ancienne != null)
    {
        UnityEditor.Undo.DestroyObjectImmediate(ancienne.gameObject);
        sb.AppendLine("  pose antérieure retirée");
    }

    // --- la face, en repère de table ---------------------------------------------------------
    var pied = new Vector3(cote.s * BOT_X, 0f, BOT_Z);
    var sommet = new Vector3(cote.s * TOP_X, 0f, TOP_Z);

    var direction = (sommet - pied).normalized;                   // le long de la face, vers le mur
    float longueurFace = Vector3.Distance(pied, sommet);

    // Perpendiculaire à la face, dans le plan du plateau. Son SIGNE dépend du côté (cf. plus bas) :
    // il ne sert qu'à placer la plaque derrière la face, et c'est `ECART_FACE` qui décide du côté.
    var travers = Vector3.Cross(direction, Vector3.up).normalized;

    var normaleJeu = travers * (cote.s > 0f ? -1f : 1f);   // vers le centre de l'aire de jeu

    sb.AppendLine("  face : pied (" + pied.x.ToString("F4") + " ; " + pied.z.ToString("F4")
                  + ") → sommet (" + sommet.x.ToString("F4") + " ; " + sommet.z.ToString("F4")
                  + ")   longueur " + longueurFace.ToString("F4") + " u = "
                  + (longueurFace * 1000f / U).ToString("F1") + " mm");

    // --- la pièce, posée sans rotation pour la mesurer ----------------------------------------
    var piece = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(modele, hote);
    piece.name = "Plastique_Decal";

    UnityEditor.Undo.RegisterCreatedObjectUndo(piece, "Poser le plastique de slingshot");

    piece.transform.localRotation = Quaternion.identity;

    // --- ses axes, DANS SON PROPRE REPÈRE -------------------------------------------------------
    // On ramène les 8 coins de chaque maillage dans le repère de la pièce : c'est la seule mesure
    // qui vaille, les nœuds internes du FBX portant leurs propres rotations.
    var locale = new Bounds();
    bool premier = true;

    foreach (var f in piece.GetComponentsInChildren<MeshFilter>())
    {
        if (f.sharedMesh == null) { continue; }

        var m = f.sharedMesh.bounds;

        for (int i = 0; i < 8; i++)
        {
            var p = piece.transform.InverseTransformPoint(f.transform.TransformPoint(new Vector3(
                (i & 1) == 0 ? m.min.x : m.max.x,
                (i & 2) == 0 ? m.min.y : m.max.y,
                (i & 4) == 0 ? m.min.z : m.max.z)));

            if (premier) { locale = new Bounds(p, Vector3.zero); premier = false; }
            else { locale.Encapsulate(p); }
        }
    }

    if (premier) { sb.AppendLine("  ⚠ aucun maillage — rien à poser"); continue; }

    sb.AppendLine("  boîte dans le repère de la PIÈCE : taille "
                  + (locale.size.x * 1000f / U).ToString("F1") + " × "
                  + (locale.size.y * 1000f / U).ToString("F1") + " × "
                  + (locale.size.z * 1000f / U).ToString("F1") + " mm");

    // Le plus long = grand axe, le plus court = épaisseur, le reste = largeur.
    int axeLong = locale.size.x >= locale.size.y && locale.size.x >= locale.size.z ? 0
                : (locale.size.y >= locale.size.z ? 1 : 2);
    int axeFin = locale.size.x <= locale.size.y && locale.size.x <= locale.size.z ? 0
               : (locale.size.y <= locale.size.z ? 1 : 2);
    int axeLarg = 3 - axeLong - axeFin;

    Vector3 Unite(int axe)
    {
        return axe == 0 ? Vector3.right : (axe == 1 ? Vector3.up : Vector3.forward);
    }

    var eLong = Unite(axeLong);
    var eLarg = Unite(axeLarg);
    var eFin = Unite(axeFin);

    sb.AppendLine("  axes mesurés : grand " + "XYZ"[axeLong]
                  + "   largeur " + "XYZ"[axeLarg]
                  + "   épaisseur " + "XYZ"[axeFin]);

    // --- la rotation : deux bases orthonormées, l'une ramenée sur l'autre -----------------------
    // `R = M(cible) · M(source)⁻¹` garantit que eLong → direction sans rien supposer des
    // conventions d'axes. Les deux bases sont construites par produits vectoriels, donc de même
    // sens : la pièce n'est pas retournée en miroir.
    var cibleLong = direction;
    var cibleLarg = Vector3.up;
    var cibleFin = Vector3.Cross(cibleLong, cibleLarg).normalized;

    var sourceFin = Vector3.Cross(eLong, eLarg).normalized;

    var mSource = new Matrix4x4();
    mSource.SetColumn(0, eLong);
    mSource.SetColumn(1, eLarg);
    mSource.SetColumn(2, sourceFin);
    mSource.SetColumn(3, new Vector4(0f, 0f, 0f, 1f));

    var mCible = new Matrix4x4();
    mCible.SetColumn(0, cibleLong);
    mCible.SetColumn(1, cibleLarg);
    mCible.SetColumn(2, cibleFin);
    mCible.SetColumn(3, new Vector4(0f, 0f, 0f, 1f));

    var rotationTable = mCible.rotation * Quaternion.Inverse(mSource.rotation);

    piece.transform.rotation = rt.rotation * rotationTable;

    // --- la boîte en repère de TABLE ------------------------------------------------------------
    Bounds BoiteTable()
    {
        var boite = new Bounds();
        bool prem = true;

        foreach (var f in piece.GetComponentsInChildren<MeshFilter>())
        {
            if (f.sharedMesh == null) { continue; }

            var m = f.sharedMesh.bounds;

            for (int i = 0; i < 8; i++)
            {
                var p = rt.InverseTransformPoint(f.transform.TransformPoint(new Vector3(
                    (i & 1) == 0 ? m.min.x : m.max.x,
                    (i & 2) == 0 ? m.min.y : m.max.y,
                    (i & 4) == 0 ? m.min.z : m.max.z)));

                if (prem) { boite = new Bounds(p, Vector3.zero); prem = false; }
                else { boite.Encapsulate(p); }
            }
        }

        return boite;
    }

    var avant = BoiteTable();

    sb.AppendLine("  orientée, boîte table : " + (avant.size.x * 1000f / U).ToString("F1") + " × "
                  + (avant.size.y * 1000f / U).ToString("F1") + " × "
                  + (avant.size.z * 1000f / U).ToString("F1") + " mm"
                  + "   → hauteur " + (avant.size.y * 1000f / U).ToString("F1") + " mm");

    // --- la translation, en repère de table -----------------------------------------------------
    float tPlaque = Vector3.Dot(new Vector3(avant.center.x, 0f, avant.center.z) - pied, direction);
    float tVoulu = longueurFace * 0.5f + RECUL;

    float ecartBas = avant.min.y - ELEVATION;

    // Distance du centre de la plaque au plan de la face, positive vers le JEU. On la veut à
    // −ECART_FACE : un peu vers le mur, donc derrière la face et hors du chemin de la bille.
    float d = Vector3.Dot(new Vector3(avant.center.x, 0f, avant.center.z)
                          - (pied + sommet) * 0.5f, normaleJeu);

    var correction = direction * (tVoulu - tPlaque)
                     + new Vector3(0f, -ecartBas, 0f)
                     + normaleJeu * (-ECART_FACE - d);

    piece.transform.position = rt.TransformPoint(rt.InverseTransformPoint(piece.transform.position)
                                                 + correction);

    var apres = BoiteTable();

    sb.AppendLine("  après translation :");
    sb.AppendLine("    centre table (" + apres.center.x.ToString("F4") + " ; "
                  + apres.center.y.ToString("F4") + " ; " + apres.center.z.ToString("F4") + ")");
    sb.AppendLine("    bas   y = " + (apres.min.y * 1000f / U).ToString("F1") + " mm"
                  + "   haut y = " + (apres.max.y * 1000f / U).ToString("F1") + " mm");
    sb.AppendLine("    le long de la face : centre à "
                  + (Vector3.Dot(new Vector3(apres.center.x, 0f, apres.center.z) - pied, direction))
                    .ToString("F4") + " u sur " + longueurFace.ToString("F4"));

    // Le grand axe de la pièce, ramené en unités de scène : `locale` est dans le repère LOCAL, où
    // le facteur d'échelle du FBX s'interpose — d'où la multiplication par `lossyScale`. Lire ces
    // cotes telles quelles annoncerait « 0 mm » sur une pièce de 117.
    var echelle = piece.transform.lossyScale;
    float longueurPiece = locale.size[axeLong] * Mathf.Abs(echelle[axeLong]);

    sb.AppendLine("    la plaque couvre " + (longueurPiece * 1000f / U).ToString("F0")
                  + " mm des " + (longueurFace * 1000f / U).ToString("F0") + " mm de la face"
                  + "   →  " + (100f * longueurPiece / longueurFace).ToString("F0") + " %"
                  + (longueurPiece > longueurFace ? "   ⚠ elle dépasse" : ""));

    float ecart = Vector3.Dot(new Vector3(apres.center.x, 0f, apres.center.z)
                              - (pied + sommet) * 0.5f, normaleJeu);

    sb.AppendLine("    distance au plan de la face : " + (ecart * 1000f / U).ToString("F1")
                  + " mm vers " + (ecart < 0f ? "le mur (hors du jeu) ✓" : "le jeu ⚠"));

    // --- les matériaux ---------------------------------------------------------------------------
    foreach (var rendu in piece.GetComponentsInChildren<MeshRenderer>())
    {
        var mats = rendu.sharedMaterials;
        bool change = false;

        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null || matPlastique == null) { continue; }

            // Tout ce qui est TRANSPARENT devient le plastique opaque. Le test porte sur la
            // surface ET sur l'alpha : un matériau peut se déclarer transparent avec un alpha
            // à 1, auquel cas il n'y a rien à corriger. Le décal, opaque, est donc laissé.
            bool transparent = (mats[i].HasProperty("_Surface")
                                && mats[i].GetFloat("_Surface") > 0.5f)
                               || (mats[i].HasProperty("_BaseColor")
                                   && mats[i].GetColor("_BaseColor").a < 0.99f);

            if (transparent && mats[i] != matPlastique)
            {
                sb.AppendLine("  matériau '" + mats[i].name + "' → " + matPlastique.name);
                mats[i] = matPlastique;
                change = true;
            }
        }

        if (change)
        {
            UnityEditor.Undo.RecordObject(rendu, "Réassigner les matériaux du plastique");
            rendu.sharedMaterials = mats;
        }

        sb.AppendLine("  matériaux : " + string.Join(" | ",
            System.Array.ConvertAll(rendu.sharedMaterials, m => m != null ? m.name : "null")));
    }

    var filtre = piece.GetComponentInChildren<MeshFilter>();
    sb.AppendLine("  maillage : " + (filtre != null && filtre.sharedMesh != null
        ? filtre.sharedMesh.name + "  " + filtre.sharedMesh.vertexCount + " sommets"
        : "AUCUN"));
    sb.AppendLine("  colliders : " + piece.GetComponentsInChildren<Collider>().Length
                  + "  (décor — doit rester 0)");
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

sb.AppendLine();
sb.AppendLine("scène modifiée : " + scene.isDirty + "   (Ctrl+Z annule, Ctrl+S conserve)");

var sortie = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "plastiques.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(sortie));
System.IO.File.WriteAllText(sortie, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(sortie) + "  (" + sb.Length + " car.)";
