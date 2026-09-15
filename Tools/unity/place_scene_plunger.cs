// Pose le lanceur dans `Neutral.unity`, sous l'hôte `PinballTable/Gameplay/Plunger`.
//
// L'hôte existe depuis le menu 1 mais il est **vide** : 0 collider, 0 maillage. Conséquence
// mesurée : le sol du couloir s'arrête à `z = 0,3187` et il n'y a **rien** en dessous — la bille
// part du point d'apparition, roule vers -Z et quitte la table par le bas du couloir. C'est ce
// trou que le lanceur ferme.
//
// ## Où poser le bouchon
//
// La cote n'est pas esthétique, elle est contrainte des deux côtés :
//
//   * **Le bouchon doit arrêter la bille sur le sol.** Le sol du couloir s'arrête à
//     `z 0,3187` : le centre de la bille doit rester au-delà, sinon elle bascule dans le vide.
//   * **Le bouchon doit arrêter la bille dans la zone de recherche du script**, sinon un
//     relâchement ne pousse rien. `Plunger.PushBalls` (Plunger.cs:93-97) balaie une boîte qui
//     va de `maxPull` (0,8 u) **derrière** la position de repos à `catchOffset` (0,6 u)
//     **devant**. Au point d'apparition la bille est à 0,8330 u devant — hors zone. C'est
//     pourquoi le lancement ne fait rien aujourd'hui, même en supposant le couloir fermé.
//
// La fenêtre utile est donc `z ∈ [0,32 ; 0,72]` pour le centre de la bille. On vise **0,525**,
// soit un bouchon à `z 0,30` : la bille s'arrête à 0,40 u devant le repos du lanceur, en plein
// milieu de la zone, et à 0,21 u au-delà de l'arête du sol — elle est franchement posée dessus.
//
// La cote est écrite ici en **local de l'hôte**, pas en monde : l'hôte est incliné avec la table
// (-7°), et une cote en monde deviendrait fausse au premier changement d'inclinaison.
//   `local z 0,17828` = le point situé à `z 0,30` en monde sur l'axe du couloir,
//   `local y -0,041436` = la hauteur où l'axe du couloir passe à `y 0,390` en monde, c'est-à-dire
//   le centre d'une bille posée sur le sol à cet endroit.
//
// ## Ce que le script ne fait pas
//
//   * Il **ne déplace pas l'hôte.** `PlaceImportedTable` réécrit sa position sans garde
//     (PlaceImportedTable.cs:262-263) : la bouger ici serait défait au prochain passage du menu 4.
//   * Il **ne touche pas à `localScale`.** C'est l'erreur qui a donné une bille de 60 mm ; une
//     échelle n'est pas une coordonnée à recopier (voir `place_scene_ball.cs`). La taille de la
//     pièce vient de son FBX, mesuré à 165,1 mm contre 165,31 annoncés par le dépôt.
//   * Il n'enregistre pas la scène (Ctrl+S).
//
// ## Le collider va sur la pièce, pas sur l'hôte
//
// `NeutralScene.Strip` (NeutralScene.cs:127-145) retire les composants **directs** de chaque
// hôte : un `BoxCollider` posé sur l'hôte serait effacé au prochain passage du menu 3. Un
// collider sur l'enfant survit — et c'est déjà le motif du projet, où c'est `Flipper_Bat` qui
// porte le collider et non son pivot.
//
// Les axes locaux de la pièce sont tournés par rapport à ceux de l'hôte (la racine du FBX porte
// un quart de tour) : la taille du collider est donc **remise dans les axes de la pièce** au lieu
// d'être écrite à la main dans un ordre qui ne vaudrait que pour cette orientation.

var sb = new System.Text.StringBuilder();

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

// --- ce que la scène doit contenir ------------------------------------------------------------

var racine = GameObject.Find("PinballTable");
var gameplay = racine != null ? racine.transform.Find("Gameplay") : null;
var hote = gameplay != null ? gameplay.Find("Plunger") : null;

if (hote == null)
{
    return "PinballTable/Gameplay/Plunger introuvable — la scène n'est pas celle du menu 4";
}

var modele = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
    "Assets/Models/Parts/Miscellaneous/Plunger.fbx");

if (modele == null)
{
    return "Assets/Models/Parts/Miscellaneous/Plunger.fbx introuvable";
}

// --- helpers ------------------------------------------------------------------------------------

// Boîte englobante d'**un** maillage, exprimée dans le repère de `repere`.
//
// Les **huit coins**, et non le centre et la taille : la racine d'un FBX Blender porte un quart de
// tour, et une taille mise à l'échelle sans être tournée échange `y` et `z`. C'est l'erreur qui
// avait donné une table de 14,72 u au lieu de 17,46.
Bounds BoiteFiltre(MeshFilter filtre, Transform repere)
{
    var local = filtre.sharedMesh.bounds;
    Bounds boite = default(Bounds);
    bool premier = true;

    for (int i = 0; i < 8; i++)
    {
        var coin = new Vector3(
            (i & 1) == 0 ? local.min.x : local.max.x,
            (i & 2) == 0 ? local.min.y : local.max.y,
            (i & 4) == 0 ? local.min.z : local.max.z);

        var p = repere.InverseTransformPoint(filtre.transform.TransformPoint(coin));

        if (premier) { boite = new Bounds(p, Vector3.zero); premier = false; }
        else { boite.Encapsulate(p); }
    }

    return boite;
}

// Boîte englobante de tous les maillages sous `objet`.
Bounds BoiteLocale(Transform objet, Transform repere)
{
    Bounds boite = default(Bounds);
    bool premier = true;

    foreach (var filtre in objet.GetComponentsInChildren<MeshFilter>())
    {
        if (filtre.sharedMesh == null) { continue; }

        var b = BoiteFiltre(filtre, repere);

        if (premier) { boite = b; premier = false; }
        else { boite.Encapsulate(b); }
    }

    return boite;
}

// Boîte d'un `BoxCollider`, ramenée dans le repère de `repere`.
//
// C'est le contrôle de la cote **réellement obtenue**. La boîte monde ne le dirait pas : c'est une
// englobante alignée sur le monde, donc dès que la pièce est inclinée de 7° elle étale chaque cote
// sur deux axes et toute cote fausse s'y noie. Mesuré : une épaisseur de 0,1 sortie à 0,1666,
// invisible dans `Extents (0,25 ; 0,2871 ; 0,1165)`.
Bounds BoiteCollider(BoxCollider c, Transform repere)
{
    Vector3 demi = c.size * 0.5f;
    Bounds boite = default(Bounds);
    bool premier = true;

    for (int i = 0; i < 8; i++)
    {
        var coin = c.center + new Vector3(
            (i & 1) == 0 ? -demi.x : demi.x,
            (i & 2) == 0 ? -demi.y : demi.y,
            (i & 4) == 0 ? -demi.z : demi.z);

        var p = repere.InverseTransformPoint(c.transform.TransformPoint(coin));

        if (premier) { boite = new Bounds(p, Vector3.zero); premier = false; }
        else { boite.Encapsulate(p); }
    }

    return boite;
}

// Le bouchon est le sous-maillage au matériau `Plunger` — c'est le plastique blanc
// (`CustomWhiteTip.png`) qui touche la bille. On l'identifie par son matériau plutôt que par sa
// position : c'est la seule chose qui ne dépende pas de l'orientation de la pièce.
//
// On rend le **filtre** et non le transform : `Spring` est enfant de `Rod`, donc une boîte prise
// sur les descendants du bouchon ramènerait le ressort avec lui et le test d'orientation ne
// distinguerait plus les deux bouts.
MeshFilter Bouchon(Transform piece)
{
    foreach (var rendu in piece.GetComponentsInChildren<MeshRenderer>())
    {
        foreach (var mat in rendu.sharedMaterials)
        {
            if (mat != null && mat.name == "Plunger")
            {
                var filtre = rendu.GetComponent<MeshFilter>();

                if (filtre != null && filtre.sharedMesh != null) { return filtre; }
            }
        }
    }

    return null;
}

// Une taille écrite dans les axes de l'hôte, reportée dans ceux de la pièce.
//
// Les axes sont ceux de **l'hôte**, pas ceux du monde : l'hôte est incliné de 7° avec la table,
// donc `Vector3.up` / `Vector3.forward` ne sont pas ses `y` et `z`. Les confondre répartit une
// cote sur deux axes à la fois — mesuré, une épaisseur de 0,1 sortie à 0,1666.
//
// La somme pondérée par les valeurs absolues est le calcul d'une boîte englobante : elle reste
// juste même si l'orientation de la pièce n'est pas un quart de tour.
Vector3 TailleDansAxesPiece(Transform piece, Transform hote, Vector3 enAxesHote)
{
    Vector3 droite = piece.InverseTransformDirection(hote.right);
    Vector3 haut = piece.InverseTransformDirection(hote.up);
    Vector3 avant = piece.InverseTransformDirection(hote.forward);

    return new Vector3(
        Mathf.Abs(droite.x) * enAxesHote.x + Mathf.Abs(haut.x) * enAxesHote.y + Mathf.Abs(avant.x) * enAxesHote.z,
        Mathf.Abs(droite.y) * enAxesHote.x + Mathf.Abs(haut.y) * enAxesHote.y + Mathf.Abs(avant.y) * enAxesHote.z,
        Mathf.Abs(droite.z) * enAxesHote.x + Mathf.Abs(haut.z) * enAxesHote.y + Mathf.Abs(avant.z) * enAxesHote.z);
}

// --- la cible -------------------------------------------------------------------------------------

// Face avant du bouchon, dans le repère de l'hôte. Voir l'en-tête pour le calcul.
const float bouchonLocalZ = 0.17828f;
const float bouchonLocalY = -0.041436f;

// Épaisseur et section du collider. La section reste sous la largeur du couloir (0,5667) : un
// collider plus large que le couloir s'enfoncerait dans les parois.
const float epaisseur = 0.10f;
const float largeur = 0.50f;
const float hauteur = 0.55f;

sb.AppendLine("=== scène '" + scene.name + "' ===");
sb.AppendLine("hôte     : " + hote.name
              + "   pos locale " + hote.localPosition.ToString("F4")
              + "   pos monde " + hote.position.ToString("F4"));
sb.AppendLine("forward  : " + hote.forward.ToString("F4") + "   (= +Z local, vers le haut du couloir)");
sb.AppendLine("modèle   : " + modele.name);

// --- idempotence ----------------------------------------------------------------------------------

var deja = hote.Find("Plunger_Rod");

if (deja != null)
{
    var colliders = deja.GetComponents<Collider>();

    sb.AppendLine();
    sb.AppendLine("RIEN FAIT : 'Plunger_Rod' est déjà sous l'hôte — pas de doublon.");
    sb.AppendLine("  pos locale " + deja.localPosition.ToString("F4"));
    sb.AppendLine("  rotation   " + deja.localEulerAngles.ToString("F2"));
    sb.AppendLine("  colliders  " + colliders.Length);

    foreach (var c in colliders)
    {
        sb.AppendLine("    " + c.GetType().Name
                      + "   déclencheur " + c.isTrigger
                      + "   boîte monde " + c.bounds.ToString("F4"));
    }

    return sb.ToString();
}

// --- la pose ----------------------------------------------------------------------------------------

var piece = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(modele, hote);

piece.name = "Plunger_Rod";

// Une instance de prefab, pas un `Instantiate` : le lien avec le FBX est conservé, donc une
// retouche du modèle se répercute sur la scène.
UnityEditor.Undo.RegisterCreatedObjectUndo(piece, "Poser le lanceur dans la scène");

var pieceT = piece.transform;

var bouchon = Bouchon(pieceT);

sb.AppendLine();
sb.AppendLine("=== orientation ===");
sb.AppendLine("bouchon (matériau 'Plunger') : " + (bouchon != null ? bouchon.name : "INTROUVABLE"));

if (bouchon == null)
{
    return sb.ToString() + "\nLe sous-maillage du bouchon est introuvable : orientation indécidable.";
}

// Où tombe le bouchon dans l'emprise de la pièce. C'est le test qui décide du retournement, et il
// porte sur les **extrémités**, pas sur le centre : un bouchon qui couvrirait presque toute la
// pièce — le cas d'un `Rod` englobant — aurait son centre au milieu et passerait un test de
// centre quel que soit le côté où il pointe.
var boite0 = BoiteLocale(pieceT, hote);
var boiteBouchon = BoiteFiltre(bouchon, hote);

float distanceAuBoutHaut = boite0.max.z - boiteBouchon.max.z;   // 0 = le bouchon atteint le bout +Z
float distanceAuBoutBas = boiteBouchon.min.z - boite0.min.z;    // 0 = le bouchon atteint le bout -Z

sb.AppendLine("  emprise de la pièce    : z " + boite0.min.z.ToString("F4") + " → " + boite0.max.z.ToString("F4"));
sb.AppendLine("  emprise du bouchon     : z " + boiteBouchon.min.z.ToString("F4") + " → " + boiteBouchon.max.z.ToString("F4"));
sb.AppendLine("  retrait au bout +Z     : " + distanceAuBoutHaut.ToString("F4") + " u");
sb.AppendLine("  retrait au bout -Z     : " + distanceAuBoutBas.ToString("F4") + " u");

// La bille vient du +Z (elle naît en haut du couloir et descend) : le bouchon doit occuper le bout
// +Z, sinon c'est le ressort qui la recevrait et le bouchon regarderait le vide.
bool bouchonEnHaut = distanceAuBoutHaut <= distanceAuBoutBas;

// Le retournement se fait **sur** la rotation existante et non à sa place : la racine du FBX porte
// un quart de tour dont dépend l'orientation de la géométrie.
if (!bouchonEnHaut)
{
    pieceT.localRotation = Quaternion.Euler(0f, 180f, 0f) * pieceT.localRotation;
    sb.AppendLine("  → le bouchon est au bout -Z : pièce retournée de 180° autour de Y");
}
else
{
    sb.AppendLine("  → le bouchon est au bout +Z, face à la bille : rien à retourner");
}

// La boîte est relue **après** le retournement : c'est elle qui donne la face avant.
var boite = BoiteLocale(pieceT, hote);

sb.AppendLine("  boîte, local hôte : centre " + boite.center.ToString("F4")
              + "   taille " + boite.size.ToString("F4")
              + "   soit " + (boite.size.z / 16.66667f * 1000f).ToString("F1") + " mm de long");

var face = new Vector3(boite.center.x, boite.center.y, boite.max.z);

pieceT.localPosition += new Vector3(0f - face.x, bouchonLocalY - face.y, bouchonLocalZ - face.z);

// --- contrôles --------------------------------------------------------------------------------------

Physics.SyncTransforms();

var boite2 = BoiteLocale(pieceT, hote);
var face2 = new Vector3(boite2.center.x, boite2.center.y, boite2.max.z);
var faceMonde = hote.TransformPoint(face2);

sb.AppendLine();
sb.AppendLine("=== pose ===");
sb.AppendLine("pos locale : " + pieceT.localPosition.ToString("F4"));
sb.AppendLine("rotation   : " + pieceT.localEulerAngles.ToString("F2"));
sb.AppendLine("échelle    : " + pieceT.localScale.ToString("F4") + "   (celle du FBX, non touchée)");
sb.AppendLine("face avant : locale " + face2.ToString("F4") + "   monde " + faceMonde.ToString("F4"));
sb.AppendLine("  écart à la cible locale z " + bouchonLocalZ.ToString("F5") + " : "
              + Mathf.Abs(face2.z - bouchonLocalZ).ToString("F5") + " u");
sb.AppendLine("  écart à la cible locale y " + bouchonLocalY.ToString("F5") + " : "
              + Mathf.Abs(face2.y - bouchonLocalY).ToString("F5") + " u");

// Où la bille doit s'arrêter : son centre à un rayon devant la face.
float reposZ = faceMonde.z + 0.225f;
float solY = 0.2168f + (reposZ - 0.9448f) * 0.12282f;

sb.AppendLine();
sb.AppendLine("bille au repos attendue : centre à z " + reposZ.ToString("F4")
              + "   sol à y " + solY.ToString("F4") + "   centre à y " + (solY + 0.225f).ToString("F4"));

// Les deux bornes de la zone de recherche (Plunger.cs:93-97), recalculées ici : c'est le contrôle
// qui dit si un relâchement poussera la bille ou ne fera rien.
//
// C'est la **projection sur l'axe du lanceur** qu'il faut comparer à `catchOffset`, pas la
// distance : la boîte de `PushBalls` est orientée par `transform.rotation` et s'étend le long de
// `forward`. Une distance droite mélangerait l'avance et la dénivellation, et surestimerait
// l'éloignement d'autant — ici 0,007 u, assez pour franchir une borne.
var reposMonde = hote.position;
var centreRepos = new Vector3(reposMonde.x, solY + 0.225f, reposZ);

float devant = Vector3.Dot(centreRepos - reposMonde, hote.forward);
float ecartLateral = Vector3.Dot(centreRepos - reposMonde, hote.right);
float ecartVertical = Vector3.Dot(centreRepos - reposMonde, hote.up);

sb.AppendLine("  écarts dans le repère du lanceur : avant " + devant.ToString("F4")
              + "   latéral " + ecartLateral.ToString("F4") + "   vertical " + ecartVertical.ToString("F4"));
sb.AppendLine("  demi-extents de la boîte : latéral " + 0.5f + "   vertical " + 0.5f);

bool dansLaBoite = devant <= 0.6f && devant >= -0.8f
                   && Mathf.Abs(ecartLateral) <= 0.5f && Mathf.Abs(ecartVertical) <= 0.5f;

sb.AppendLine("→ "
              + (dansLaBoite
                 ? "DANS la boîte de PushBalls : un relâchement poussera la bille ✓"
                 : "HORS de la boîte de PushBalls — le lancement ne fera rien ⚠"));

if (reposZ < 0.3187f)
{
    sb.AppendLine("  ⚠ le centre de la bille serait sous l'arête du sol (z 0,3187) : elle basculerait");
}

// --- le collider, sur la pièce -----------------------------------------------------------------------

var centreHote = new Vector3(face2.x, face2.y, face2.z - epaisseur * 0.5f);
var centrePiece = pieceT.InverseTransformPoint(hote.TransformPoint(centreHote));

var collider = UnityEditor.Undo.AddComponent<BoxCollider>(piece);

collider.center = centrePiece;

// `BoxCollider.size` s'exprime dans le repère **local du collider**, donc échelle de la pièce
// comprise — et la racine du FBX porte 0,0167. Une taille écrite en unités de scène y devient
// 60 fois plus petite : mesuré au premier essai, `Extents (0,0042, 0,0048, 0,0019)`, soit un
// collider de 8 mm au lieu de 500. C'est la même famille de piège que la bille à 60 mm — une
// dimension n'est pas une coordonnée, elle se convertit par l'échelle.
var tailleScene = TailleDansAxesPiece(pieceT, hote, new Vector3(largeur, hauteur, epaisseur));
var echelle = pieceT.lossyScale;

collider.size = new Vector3(
    tailleScene.x / Mathf.Max(Mathf.Abs(echelle.x), 1e-6f),
    tailleScene.y / Mathf.Max(Mathf.Abs(echelle.y), 1e-6f),
    tailleScene.z / Mathf.Max(Mathf.Abs(echelle.z), 1e-6f));

collider.isTrigger = false;

UnityEditor.EditorUtility.SetDirty(collider);

sb.AppendLine();
sb.AppendLine("=== collider (sur la pièce, pas sur l'hôte) ===");
sb.AppendLine("BoxCollider  centre local pièce " + collider.center.ToString("F4"));
sb.AppendLine("             taille locale pièce " + collider.size.ToString("F4")
              + "   (largeur " + largeur + " x hauteur " + hauteur + " x épaisseur " + epaisseur
              + " dans les axes de l'hôte)");
sb.AppendLine("             déclencheur " + collider.isTrigger);

Physics.SyncTransforms();

sb.AppendLine("             boîte monde " + collider.bounds.ToString("F4")
              + "   (englobante : mélange les axes, indicative seulement)");

// Le contrôle qui compte : la cote dans les axes de l'hôte, à comparer au voulu.
var boiteCollider = BoiteCollider(collider, hote);
var voulu = new Vector3(largeur, hauteur, epaisseur);

sb.AppendLine("             en axes hôte : centre " + boiteCollider.center.ToString("F4")
              + "   taille " + boiteCollider.size.ToString("F4"));
sb.AppendLine("             voulu        : centre "
              + new Vector3(face2.x, face2.y, face2.z - epaisseur * 0.5f).ToString("F4")
              + "   taille " + voulu.ToString("F4"));
sb.AppendLine("             écart de taille : "
              + (boiteCollider.size - voulu).ToString("F5") + " u");

// La face avant du collider doit coïncider avec la face avant du bouchon : c'est elle que la
// bille touche, et un collider en retrait la laisserait s'enfoncer dans le décor.
float faceCollider = boiteCollider.max.z;
float facePiece = boite2.max.z;

sb.AppendLine("             face avant du collider " + faceCollider.ToString("F4")
              + "   face avant du bouchon " + facePiece.ToString("F4")
              + "   écart " + Mathf.Abs(faceCollider - facePiece).ToString("F5") + " u");

// Le couloir fait 0,5667 entre ses faces internes : un collider plus large s'y enfoncerait.
if (collider.bounds.size.x > 0.5667f)
{
    sb.AppendLine("  ⚠ le collider (" + collider.bounds.size.x.ToString("F4")
                  + " u) est plus large que le couloir (0,5667 u) : il s'enfonce dans les parois");
}

// --- la bille de la scène ------------------------------------------------------------------------------

var point = gameplay.Find("BallSpawnPoint");

// Par son chemin d'abord : `BallManager` parque la bille hors jeu en la **désactivant**, et
// `FindGameObjectWithTag` ne voit pas les objets inactifs. Le tag reste le repli, pour le cas où
// la bille aurait été posée ailleurs.
var billeT = gameplay.Find("Ball");

if (billeT == null)
{
    var parTag = GameObject.FindGameObjectWithTag("Ball");
    billeT = parTag != null ? parTag.transform : null;
}

sb.AppendLine();
sb.AppendLine("=== bille de la scène ===");

if (billeT == null || point == null)
{
    sb.AppendLine("bille ou point d'apparition introuvable — rien déplacé");
}
else
{
    float ecart = Vector3.Distance(billeT.position, point.position);

    sb.AppendLine("position actuelle : " + billeT.position.ToString("F4"));
    sb.AppendLine("point d'apparition : " + point.position.ToString("F4")
                  + "   écart " + ecart.ToString("F4") + " u");
    sb.AppendLine("active : " + billeT.gameObject.activeSelf
                  + "   (parquée hors jeu par BallManager)");

    if (ecart > 0.001f)
    {
        UnityEditor.Undo.RecordObject(billeT, "Remettre la bille au point d'apparition");

        billeT.SetPositionAndRotation(point.position, point.rotation);

        UnityEditor.EditorUtility.SetDirty(billeT);

        sb.AppendLine("→ bille remise au point d'apparition (elle était tombée hors de la table)");
    }
    else
    {
        sb.AppendLine("→ déjà au point d'apparition, rien à faire");
    }
}

// --- fin ----------------------------------------------------------------------------------------------

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

sb.AppendLine();
sb.AppendLine("scène modifiée : " + scene.isDirty + "   (Ctrl+Z annule, Ctrl+S conserve)");

return sb.ToString();
