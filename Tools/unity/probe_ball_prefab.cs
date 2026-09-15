// Deux questions avant de toucher à la bille.
//
// 1. **Le prefab `Assets/Prefabs/Ball.prefab` existe déjà.** D'où vient-il ? S'il date de la
//    table procédurale abandonnée, il porte peut-être un maillage ou une échelle qui n'ont plus
//    cours. On le décrit avant de décider de le garder, de le refaire ou de le supprimer —
//    écraser sans regarder est exactement ce qu'il ne faut pas faire.
//
// 2. **Le pivot du FBX n'est pas au centre de la bille.** La sonde d'import a mesuré le centre
//    des bornes à `-0,2667` en x, alors que le rayon n'est que de `0,225` : le pivot tombe donc
//    *hors* de la sphère. Un `SphereCollider` posé sur ce pivot serait décalé du maillage, et un
//    `Rigidbody` ferait tourner la bille autour d'un point qui n'est pas son centre — elle
//    orbiterait au lieu de rouler. Il faut donc connaître le décalage exact à compenser.

var sb = new System.Text.StringBuilder();

// --- 1. le prefab existant ------------------------------------------------------------------

sb.AppendLine("=== Assets/Prefabs/Ball.prefab ===");

var existing = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ball.prefab");

if (existing == null)
{
    sb.AppendLine("absent");
}
else
{
    sb.AppendLine("racine : " + existing.name
                  + "   tag " + existing.tag
                  + "   layer " + existing.layer + " (" + LayerMask.LayerToName(existing.layer) + ")"
                  + "   actif " + existing.activeSelf);

    foreach (var component in existing.GetComponents<Component>())
    {
        sb.AppendLine("  composant : " + component.GetType().FullName);

        if (component is SphereCollider sphere)
        {
            sb.AppendLine("      rayon " + sphere.radius.ToString("F4")
                          + "   centre " + sphere.center.ToString("F4"));
        }

        if (component is Rigidbody body)
        {
            sb.AppendLine("      masse " + body.mass.ToString("F4")
                          + "   drag " + body.linearDamping.ToString("F2")
                          + "   angularDrag " + body.angularDamping.ToString("F2")
                          + "   détection " + body.collisionDetectionMode
                          + "   interpolation " + body.interpolation);
        }
    }

    foreach (var filter in existing.GetComponentsInChildren<MeshFilter>(true))
    {
        var mesh = filter.sharedMesh;

        sb.AppendLine("  maillage : " + filter.name
                      + "   mesh " + (mesh != null ? mesh.name + " (" + mesh.vertexCount + " sommets)" : "AUCUN")
                      + "   pos locale " + filter.transform.localPosition
                      + "   échelle " + filter.transform.localScale);

        if (mesh != null)
        {
            sb.AppendLine("      chemin du mesh : "
                          + AssetDatabase.GetAssetPath(mesh));
        }

        var renderer = filter.GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            foreach (var m in renderer.sharedMaterials)
            {
                sb.AppendLine("      matériau : "
                              + (m != null ? m.name + "  (" + (m.shader != null ? m.shader.name : "?") + ")" : "AUCUN"));
            }
        }
    }

    // Les enfants éventuels, avec leur transform complet : c'est là que se lit une échelle
    // héritée d'un import FBX.
    foreach (var t in existing.GetComponentsInChildren<Transform>(true))
    {
        if (t == existing.transform) { continue; }

        sb.AppendLine("  enfant : " + t.name
                      + "   pos " + t.localPosition
                      + "   échelle " + t.localScale);
    }
}

// --- 2. le décalage du pivot dans le FBX ----------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== Ball.fbx : où est le centre par rapport au pivot ===");

var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Parts/Miscellaneous/Ball.fbx");

if (source == null)
{
    sb.AppendLine("Ball.fbx introuvable");
    return sb.ToString();
}

var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var instance = PrefabUtility.InstantiatePrefab(source, preview) as GameObject;

foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
{
    var mesh = filter.sharedMesh;

    if (mesh == null) { continue; }

    // Le centre du maillage exprimé dans le repère de la **racine** du prefab : c'est ce
    // décalage qu'il faudra annuler pour que le pivot retombe au centre de la bille.
    Vector3 centreDansRacine = instance.transform.InverseTransformPoint(filter.transform.TransformPoint(mesh.bounds.center));

    sb.AppendLine("  objet " + filter.name);
    sb.AppendLine("    position monde        : " + filter.transform.position.ToString("F4"));
    sb.AppendLine("    échelle monde         : " + filter.transform.lossyScale.ToString("F4"));
    sb.AppendLine("    centre du mesh (monde): " + filter.transform.TransformPoint(mesh.bounds.center).ToString("F4"));
    sb.AppendLine("    centre dans la racine : " + centreDansRacine.ToString("F4")
                  + "   → longueur " + centreDansRacine.magnitude.ToString("F4") + " u");
    sb.AppendLine("    rayon du mesh (local) : " + mesh.bounds.extents.x.ToString("F6")
                  + "   × échelle = " + (mesh.bounds.extents.x * filter.transform.lossyScale.x).ToString("F4") + " u");
}

// Le maillage est-il centré sur son propre objet, ou décalé dans le maillage lui-même ?
foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
{
    var mesh = filter.sharedMesh;

    if (mesh == null) { continue; }

    var verts = mesh.vertices;
    var sum = Vector3.zero;

    for (int i = 0; i < verts.Length; i++) { sum += verts[i]; }

    sb.AppendLine("  centre des sommets (repère du mesh) : " + (sum / verts.Length).ToString("F6"));
    sb.AppendLine("  bounds.center (repère du mesh)      : " + mesh.bounds.center.ToString("F6"));
}

UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);

return sb.ToString();
