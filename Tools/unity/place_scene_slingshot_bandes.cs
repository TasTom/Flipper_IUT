// Pose la BANDE visible des slingshots — un maillage ProBuilder, sans collider. ÉCRITURE.
//
// ── Pourquoi une bande séparée ────────────────────────────────────────────────────────
// L'hôte porte déjà le `BoxCollider` réactif (voir `place_scene_slingshots.cs`) : c'est lui
// que la bille touche et qui déclenche `Slingshot.cs`. Mais un collider ne se voit pas, et
// `Slingshot.Flash()` n'a rien à illuminer : `flashRenderers` se remplit dans `Awake` avec
// `GetComponentsInChildren<Renderer>(true)`, et l'hôte n'a pour l'instant aucun enfant.
// Mesuré : « rendus de flash : 0 ».
//
// ── Decor : sans collider ─────────────────────────────────────────────────────────────
// Le décor de ce projet est **sans collider** — c'est la règle de CLAUDE.md, et ici elle a
// une raison précise : la bande est posée À L'INTÉRIEUR de la face du collider réactif. Lui
// donner un collider mettrait deux volumes dans la même zone et doublerait le contact.
// `ShapeGenerator.GenerateCube` produit un `MeshFilter` + `MeshRenderer`, jamais de collider.
//
// ── La bande est alignée sur le collider, pas l'inverse ───────────────────────────────
// Face avant de la bande à z local = 0,12 : exactement la face avant du `BoxCollider`. La
// bande affleure donc le collider au lieu de le dépasser — sinon la bille paraîtrait
// s'enfoncer dans le caoutchouc avant de rebondir. Le reste (z de −0,08 à 0) est noyé dans
// le corps du slingshot, ce qui la fait tenir dessus au lieu de flotter devant.

var sb = new System.Text.StringBuilder();

const float U = 16.6666667f;

// mêmes cotes de face que `place_scene_slingshots.cs` — le pied a suivi le pivot des flippers
// le 2026-09-15 (drain gap porté à 1,75 bille). Voir ce script pour la dérivation du 0,091915.
const float TOP_X = 0.1925f * U, TOP_Z = 0.330f * U;
const float BOT_X = 0.091915f * U, BOT_Z = 0.085f * U;

const float BANDE_EPAIS = 0.12f;      // face avant de la bande — identique au BoxCollider
const float BANDE_NOYEE = 0.08f;      // partie prise dans le corps
const float BANDE_HAUTEUR = 0.26f;
const float BANDE_Y = 0.03f;          // centre vertical, en local de l'hôte
const float BANDE_MARGE = 0.06f;      // retrait à chaque bout

var racine = GameObject.Find("PinballTable");

if (racine == null) { return "PinballTable introuvable."; }

var rt = racine.transform;

Transform Chercher(Transform r, string nom)
{
    if (r.name == nom) { return r; }
    for (int i = 0; i < r.childCount; i++)
    {
        var t = Chercher(r.GetChild(i), nom);
        if (t != null) { return t; }
    }
    return null;
}

var caoutchouc = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BumperRed_Mat.mat");

if (caoutchouc == null) { return "BumperRed_Mat introuvable dans Assets/Materials."; }

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

foreach (var cote in new[] { new { nom = "Slingshot_Right", s = 1 }, new { nom = "Slingshot_Left", s = -1 } })
{
    var hote = Chercher(rt, cote.nom);

    sb.AppendLine("=== " + cote.nom + " ===");

    if (hote == null) { sb.AppendLine("  ABSENT — on ne le crée pas."); continue; }

    // Non destructif : un enfant du bon nom est laissé tel quel (règle du projet)
    var existante = hote.Find("Slingshot_Band");

    if (existante != null)
    {
        sb.AppendLine("  'Slingshot_Band' existe déjà — laissée intacte. (Retirer l'objet pour rejouer la pose.)");
        continue;
    }

    Vector3 a = new Vector3(cote.s * TOP_X, 0f, TOP_Z);
    Vector3 b = new Vector3(cote.s * BOT_X, 0f, BOT_Z);
    float L = Vector3.Distance(a, b);

    Vector3 taille = new Vector3(L - 2f * BANDE_MARGE, BANDE_HAUTEUR, BANDE_EPAIS + BANDE_NOYEE);

    // centre : la face avant tombe sur BANDE_EPAIS, le reste est noyé dans le corps
    Vector3 centre = new Vector3(0f, BANDE_Y, (BANDE_EPAIS - BANDE_NOYEE) * 0.5f);

    var mesh = UnityEngine.ProBuilder.ShapeGenerator.GenerateCube(
        UnityEngine.ProBuilder.PivotLocation.Center, taille);

    var go = mesh.gameObject;
    go.name = "Slingshot_Band";

    UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Poser la bande " + cote.nom);
    UnityEditor.Undo.SetTransformParent(go.transform, hote, "Poser la bande " + cote.nom);

    go.transform.localPosition = centre;
    go.transform.localRotation = Quaternion.identity;
    go.transform.localScale = Vector3.one;

    var rendu = go.GetComponent<MeshRenderer>();

    if (rendu != null)
    {
        UnityEditor.Undo.RecordObject(rendu, "Poser la bande " + cote.nom);
        rendu.sharedMaterial = caoutchouc;
    }

    // Le décor ne porte AUCUN collider. On le constate plutôt que de le supposer.
    var colliders = go.GetComponents<Collider>();

    sb.AppendLine("  ProBuilder : " + mesh.vertexCount + " sommets, " + mesh.faceCount + " faces");
    sb.AppendLine("  taille " + taille.ToString("F4") + "   centre local " + centre.ToString("F4"));
    sb.AppendLine("  face avant à z local " + (centre.z + taille.z * 0.5f).ToString("F4")
        + "  (collider réactif : " + BANDE_EPAIS.ToString("F4") + ")");
    sb.AppendLine("  matériau : " + (rendu != null && rendu.sharedMaterial != null
        ? rendu.sharedMaterial.name : "AUCUN"));
    sb.AppendLine("  colliders sur la bande : " + colliders.Length
        + (colliders.Length == 0 ? "   ✓ décor, aucun" : "   ⚠ IL EN FAUT AUCUN"));

    // Ce que le flash illuminera
    var sl = hote.GetComponent("Slingshot");
    sb.AppendLine("  rendus visibles depuis l'hôte : " + hote.GetComponentsInChildren<Renderer>(true).Length
        + "   (c'est ce que `Slingshot.Awake` prendra pour `flashRenderers`)");
    sb.AppendLine("  script Slingshot sur l'hôte : " + (sl != null ? "présent" : "⚠ ABSENT"));
    sb.AppendLine();
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
sb.AppendLine("scène modifiée (Ctrl+Z annule, Ctrl+S conserve).");

return sb.ToString();
