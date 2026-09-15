// SONDE (lecture seule) — pourquoi les flippers tremblent-ils ?
//
// Retour terrain 2026-09-15 : « en jeu ils bougent dans tous les sens et tremblent tout seuls »,
// après un remaniement manuel de la scène. Un flipper qui tremble est un `Rigidbody` que deux
// forces se disputent à chaque image. Les causes possibles sont peu nombreuses, et chacune se
// mesure :
//
//   A. un COLLIDER qui mord — la physique pousse pour sortir de la pénétration ;
//   B. l'ANCRAGE du `HingeJoint` — un point du monde ; faux, il tire le corps de travers ;
//   C. les CONTRAINTES du `Rigidbody` — perdues, le corps se met à translater librement ;
//   D. l'ÉCHELLE de la pièce — un `BoxCollider` dont la taille a été calculée en unités locales
//      devient énorme si l'échelle change, et recouvre alors tout ce qui l'entoure.
//
// On relève donc l'état complet, PUIS on interroge la physique : ce qui recouvre quoi, et de
// combien. C'est le recouvrement qui décide, pas l'apparence.

var sb = new System.Text.StringBuilder();

const float U = 16.6666667f;

string Mm(float u) { return (u * 1000f / U).ToString("F2") + " mm"; }

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
sb.AppendLine("=== scène '" + scene.name + "'   modifiée = " + scene.isDirty + " ===");

var racine = GameObject.Find("PinballTable");
if (racine == null) { return "PinballTable introuvable."; }

var rt = racine.transform;
var gameplay = rt.Find("Gameplay");

sb.AppendLine();
sb.AppendLine("### racine : pos " + rt.position.ToString("F4")
              + "   rot " + rt.eulerAngles.ToString("F2")
              + "   échelle " + rt.lossyScale.ToString("F6"));
sb.AppendLine("### Gameplay : " + (gameplay != null
    ? "échelle " + gameplay.lossyScale.ToString("F6") : "ABSENT"));

if (gameplay == null) { return sb.ToString(); }

// --- 1. état complet de chaque flipper ----------------------------------------------------------

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    sb.AppendLine();
    sb.AppendLine("############### " + nom);

    var pivot = gameplay.Find(nom);

    if (pivot == null) { sb.AppendLine("  ABSENT"); continue; }

    var table = rt.InverseTransformPoint(pivot.position);

    sb.AppendLine("  position table (" + table.x.ToString("F5") + " ; " + table.y.ToString("F5")
                  + " ; " + table.z.ToString("F5") + ")");
    sb.AppendLine("  rotation monde " + pivot.eulerAngles.ToString("F2")
                  + "   échelle locale " + pivot.localScale.ToString("F5"));

    // --- les composants présents : un doublon expliquerait bien des choses ---
    var comps = pivot.GetComponents<Component>();
    var noms = new string[comps.Length];

    for (int i = 0; i < comps.Length; i++)
    {
        noms[i] = comps[i] != null ? comps[i].GetType().Name : "null";
    }

    sb.AppendLine("  composants : " + string.Join(", ", noms));

    int combienFlipper = pivot.GetComponents<MonoBehaviour>().Length;
    sb.AppendLine("  MonoBehaviours : " + combienFlipper
                  + (combienFlipper > 1 ? "   ⚠ doublon possible" : ""));

    // --- le corps ---
    var corps = pivot.GetComponent<Rigidbody>();

    if (corps == null) { sb.AppendLine("  ⚠ AUCUN Rigidbody"); }
    else
    {
        sb.AppendLine("  Rigidbody : masse " + corps.mass.ToString("F3")
                      + "   drag " + corps.linearDamping.ToString("F3")
                      + "   angDrag " + corps.angularDamping.ToString("F3"));
        sb.AppendLine("    gravité " + corps.useGravity
                      + "   kinematic " + corps.isKinematic
                      + "   interpolation " + corps.interpolation
                      + "   détection " + corps.collisionDetectionMode);
        sb.AppendLine("    contraintes " + (int)corps.constraints + " = " + corps.constraints);

        // 126 = tout gelé ; 62 = position + rotations X et Y gelées (ce que le projet attend).
        if ((int)corps.constraints != 62)
        {
            sb.AppendLine("    ⚠ attendu 62 (FreezePosition | FreezeRotationX | FreezeRotationY)");
        }
        else { sb.AppendLine("    ✓ contraintes conformes"); }

        sb.AppendLine("    vitesse " + corps.linearVelocity.ToString("F5")
                      + "   ω " + corps.angularVelocity.ToString("F5"));
    }

    // --- le joint ---
    var joint = pivot.GetComponent<HingeJoint>();

    if (joint == null) { sb.AppendLine("  ⚠ AUCUN HingeJoint"); }
    else
    {
        var ancreTable = rt.InverseTransformPoint(joint.connectedAnchor);

        sb.AppendLine("  HingeJoint :");
        sb.AppendLine("    axis (local)        " + joint.axis.ToString("F4"));
        sb.AppendLine("    anchor (local)      " + joint.anchor.ToString("F4"));
        sb.AppendLine("    connectedAnchor     " + ancreTable.ToString("F5")
                      + "   autoConfigure " + joint.autoConfigureConnectedAnchor);
        sb.AppendLine("    connectedBody       " + (joint.connectedBody != null
                      ? joint.connectedBody.name : "null (=> ancrage en MONDE)"));

        float ecart = Vector3.Distance(ancreTable, table);

        sb.AppendLine("    écart ancrage ↔ pivot : " + ecart.ToString("F5") + " u = "
                      + Mm(ecart) + (ecart < 1e-4f ? "   ✓" : "   ⚠ L'ANCRAGE EST AILLEURS"));
        sb.AppendLine("    limites             " + (joint.useLimits
                      ? "[" + joint.limits.min.ToString("F1") + " ; "
                        + joint.limits.max.ToString("F1") + "]"
                      : "désactivées"));
        sb.AppendLine("    ressort             " + (joint.useSpring
                      ? joint.spring.spring.ToString("F0") + " / amorti "
                        + joint.spring.damper.ToString("F0")
                        + "   cible " + joint.spring.targetPosition.ToString("F1")
                      : "désactivé"));
    }

    // --- le bat ---
    var porteur = pivot.Find("Flipper_Bat");

    if (porteur == null) { sb.AppendLine("  ⚠ pas de 'Flipper_Bat'"); continue; }

    sb.AppendLine("  'Flipper_Bat' : local " + porteur.localPosition.ToString("F5")
                  + "   rot " + porteur.localEulerAngles.ToString("F2")
                  + "   échelle " + porteur.lossyScale.ToString("F6"));

    for (int i = 0; i < porteur.childCount; i++)
    {
        var e = porteur.GetChild(i);
        sb.AppendLine("    [" + i + "] '" + e.name + "'   local " + e.localPosition.ToString("F5")
                      + "   rot " + e.localEulerAngles.ToString("F2")
                      + "   échelle " + e.localScale.ToString("F5")
                      + "   cumulée " + e.lossyScale.ToString("F3"));
    }

    // Les colliders portés par le bat, avec leurs bornes MONDE.
    foreach (var c in pivot.GetComponentsInChildren<Collider>(true))
    {
        var col = c as BoxCollider;

        sb.AppendLine("    collider " + c.GetType().Name + " '" + c.name + "'"
                      + "   activé " + c.enabled
                      + "   trigger " + c.isTrigger
                      + (col != null ? "   centre " + col.center.ToString("F5")
                                       + "   taille " + col.size.ToString("F5")
                                     : "")
                      + "   porté par '"
                      + System.IO.Path.GetFileName(c.transform.parent != null
                          ? c.transform.parent.name : "?") + "'");
        sb.AppendLine("      bornes monde : centre " + c.bounds.center.ToString("F4")
                      + "   taille " + c.bounds.size.ToString("F4")
                      + "   soit " + Mm(c.bounds.size.x) + " × " + Mm(c.bounds.size.y)
                      + " × " + Mm(c.bounds.size.z));
    }
}

// --- 2. ce que la physique dit : qui recouvre qui ? -----------------------------------------------

sb.AppendLine();
sb.AppendLine("############### recouvrements (la mesure qui décide)");
sb.AppendLine("(une bille = 0,450 u = 27,00 mm ; au-delà du millimètre, la physique pousse pour de bon)");

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    var pivot = gameplay.Find(nom);

    if (pivot == null) { continue; }

    sb.AppendLine();
    sb.AppendLine("### " + nom);

    foreach (var boite in pivot.GetComponentsInChildren<BoxCollider>(true))
    {
        if (!boite.enabled) { continue; }

        var centre = boite.bounds.center;
        var demi = boite.bounds.extents;

        // `OverlapBox` avec les bornes monde : c'est un test AXE-ALIGNÉ, donc il grossit la boîte
        // d'un bat incliné. Il sert à TROUVER les candidats ; la profondeur, elle, se mesure
        // ensuite avec `ComputePenetration`, qui tient compte de l'orientation.
        var candidats = Physics.OverlapBox(centre, demi, Quaternion.identity, ~0,
                                          QueryTriggerInteraction.Ignore);

        var vus = new System.Collections.Generic.HashSet<Collider>();
        int serieux = 0;

        foreach (var autre in candidats)
        {
            if (autre == boite || autre.transform.IsChildOf(pivot)) { continue; }
            if (!vus.Add(autre)) { continue; }

            // La profondeur, dans les repères réels cette fois.
            Vector3 direction;
            float distance;

            bool touche = Physics.ComputePenetration(boite, boite.transform.position,
                                                     boite.transform.rotation,
                                                     autre, autre.transform.position,
                                                     autre.transform.rotation,
                                                     out direction, out distance);

            // `ComputePenetration` ne sait pas tester un MeshCollider non convexe : il rend
            // `false`. On le dit plutôt que de laisser croire à une absence de recouvrement.
            bool muet = autre is MeshCollider && !((MeshCollider)autre).convex;

            if (touche && distance > 0.002f)
            {
                serieux++;

                var d = rt.InverseTransformDirection(direction);

                sb.AppendLine("    RECOUVRE '" + autre.name + "' [" + autre.GetType().Name + "]"
                              + "  de " + Mm(distance)
                              + "   sortie (" + d.x.ToString("F2") + " ; " + d.y.ToString("F2")
                              + " ; " + d.z.ToString("F2") + ")");
            }
            else if (muet)
            {
                sb.AppendLine("    ?  '" + autre.name + "' [MeshCollider non convexe]"
                              + " — non testable par ComputePenetration, à vérifier autrement");
            }
        }

        if (serieux == 0) { sb.AppendLine("    aucun recouvrement sérieux ✓"); }
    }
}

// --- 3. les scripts Flipper, tels qu'ils sont réglés ------------------------------------------------

sb.AppendLine();
sb.AppendLine("############### réglages des scripts Flipper");

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    var pivot = gameplay.Find(nom);

    if (pivot == null) { continue; }

    var script = pivot.GetComponent("Flipper");

    if (script == null) { sb.AppendLine("  " + nom + " : pas de script Flipper"); continue; }

    var so = new UnityEditor.SerializedObject(script);
    var it = so.GetIterator();

    sb.AppendLine("  " + nom);

    if (it.NextVisible(true))
    {
        do
        {
            if (it.propertyType == UnityEditor.SerializedPropertyType.Generic) { continue; }

            string v;

            switch (it.propertyType)
            {
                case UnityEditor.SerializedPropertyType.Float: v = it.floatValue.ToString("F3"); break;
                case UnityEditor.SerializedPropertyType.Boolean: v = it.boolValue ? "true" : "false"; break;
                case UnityEditor.SerializedPropertyType.Enum:
                    v = it.enumValueIndex >= 0 && it.enumValueIndex < it.enumDisplayNames.Length
                        ? it.enumDisplayNames[it.enumValueIndex] : "?"; break;
                case UnityEditor.SerializedPropertyType.ObjectReference:
                    v = it.objectReferenceValue != null ? it.objectReferenceValue.name : "null"; break;
                default: v = "(" + it.propertyType + ")"; break;
            }

            sb.AppendLine("    " + it.name + " = " + v);
        } while (it.NextVisible(false));
    }
}

var sortie = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "flippers_tremblement.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(sortie));
System.IO.File.WriteAllText(sortie, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(sortie) + "  (" + sb.Length + " car.)";
