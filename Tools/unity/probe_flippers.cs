// État des flippers et du FBX `Flipper_Williams_3`. LECTURE SEULE.
//
// Questions : où sont les pivots, que portent-ils (Flipper ? HingeJoint ? Rigidbody ? réglages),
// que contient `Flipper_Bat`, et dans quel sens s'étend le bat du FBX (son origine est-elle au
// talon, comme un vrai bat tenu par son axe) ? Le FBX est lu dans l'asset, pas instancié.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null) { return "'PinballTable/Gameplay' introuvable."; }

sb.AppendLine("=== pivots ===");

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    var hote = gameplay.Find(nom);

    if (hote == null) { sb.AppendLine(nom + " : ABSENT"); continue; }

    sb.AppendLine(nom);
    sb.AppendLine("  pos locale " + hote.localPosition.ToString("F4")
                  + "   rot locale " + hote.localEulerAngles.ToString("F2")
                  + "   échelle " + hote.localScale.ToString("F4"));
    sb.AppendLine("  pos monde  " + hote.position.ToString("F4")
                  + "   rot monde " + hote.eulerAngles.ToString("F2"));

    var flip = hote.GetComponent<Flipper>();

    sb.AppendLine("  Flipper : " + (flip != null ? "oui, côté " + flip.Side : "NON"));

    var joint = hote.GetComponent<HingeJoint>();

    if (joint == null) { sb.AppendLine("  HingeJoint : NON"); }
    else
    {
        sb.AppendLine("  HingeJoint : axe " + joint.axis.ToString("F3")
                      + "   ancre " + joint.anchor.ToString("F4")
                      + "   corps lié " + (joint.connectedBody != null ? joint.connectedBody.name : "(aucun)")
                      + "   ressort " + joint.useSpring + " " + joint.spring.ToString()
                      + "   limites " + joint.useLimits + " " + joint.limits.ToString());
    }

    var corps = hote.GetComponent<Rigidbody>();

    if (corps == null) { sb.AppendLine("  Rigidbody : NON"); }
    else
    {
        sb.AppendLine("  Rigidbody : masse " + corps.mass + "   cinématique " + corps.isKinematic
                      + "   gravité " + corps.useGravity + "   contraintes " + corps.constraints
                      + "   collision " + corps.collisionDetectionMode
                      + "   interpolation " + corps.interpolation);
    }

    foreach (Transform enfant in hote)
    {
        sb.AppendLine("  enfant '" + enfant.name + "'  pos locale " + enfant.localPosition.ToString("F4")
                      + "   rot " + enfant.localEulerAngles.ToString("F2")
                      + "   échelle " + enfant.localScale.ToString("F4"));
        sb.AppendLine("    rendus " + enfant.GetComponentsInChildren<MeshRenderer>(true).Length
                      + "   filtres " + enfant.GetComponentsInChildren<MeshFilter>(true).Length
                      + "   colliders " + enfant.GetComponentsInChildren<Collider>(true).Length);

        foreach (var c in enfant.GetComponentsInChildren<Collider>(true))
        {
            sb.AppendLine("    collider " + c.GetType().Name + " sur '" + c.name
                          + "'   déclencheur " + c.isTrigger + "   boîte monde " + c.bounds.ToString("F4"));
        }
    }
}

// L'écart entre les deux pivots, dans les axes du root : c'est le vide à défendre.
var gauche = gameplay.Find("Flipper_Left_Pivot");
var droite = gameplay.Find("Flipper_Right_Pivot");

if (gauche != null && droite != null)
{
    var a = rt.InverseTransformPoint(gauche.position);
    var b = rt.InverseTransformPoint(droite.position);

    sb.AppendLine();
    sb.AppendLine("=== écart entre pivots (repère root) ===");
    sb.AppendLine("  gauche " + a.ToString("F4"));
    sb.AppendLine("  droite " + b.ToString("F4"));
    sb.AppendLine("  distance " + Vector3.Distance(a, b).ToString("F4") + " u"
                  + "   soit " + (Vector3.Distance(a, b) / 0.45f).ToString("F2") + " billes");
}

sb.AppendLine();
sb.AppendLine("=== FBX Flipper_Williams_3 (asset, non instancié) ===");

var modele = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
    "Assets/Models/Parts/Flipper/Flipper_Williams_3.fbx");

if (modele == null) { sb.AppendLine("FBX introuvable"); }
else
{
    // La chaîne de transforms de la racine : c'est elle qui porte l'échelle d'import et le quart
    // de tour éventuel.
    var t = modele.transform;
    int niveau = 0;

    while (t != null && niveau < 6)
    {
        sb.AppendLine("  [" + niveau + "] '" + t.name + "'  pos " + t.localPosition.ToString("F4")
                      + "   rot " + t.localEulerAngles.ToString("F2")
                      + "   échelle " + t.localScale.ToString("F4"));

        t = t.childCount > 0 ? t.GetChild(0) : null;
        niveau++;
    }

    foreach (var filtre in modele.GetComponentsInChildren<MeshFilter>(true))
    {
        if (filtre.sharedMesh == null) { continue; }

        var m = filtre.sharedMesh.bounds;

        sb.AppendLine("  maillage '" + filtre.name + "' : " + filtre.sharedMesh.vertexCount + " sommets"
                      + "   bornes locales centre " + m.center.ToString("F4") + "   taille " + m.size.ToString("F4"));

        // Où tombe la géométrie par rapport à l'origine du prefab : un bat dont le talon est à
        // l'origine se pose tel quel sur le pivot ; sinon il faut le décaler.
        var monde = new Bounds(filtre.transform.TransformPoint(m.center), Vector3.zero);

        for (int i = 0; i < 8; i++)
        {
            monde.Encapsulate(filtre.transform.TransformPoint(new Vector3(
                (i & 1) == 0 ? m.min.x : m.max.x,
                (i & 2) == 0 ? m.min.y : m.max.y,
                (i & 4) == 0 ? m.min.z : m.max.z)));
        }

        var dansRacine = new Bounds(
            modele.transform.InverseTransformPoint(monde.center), Vector3.zero);

        for (int i = 0; i < 8; i++)
        {
            dansRacine.Encapsulate(modele.transform.InverseTransformPoint(new Vector3(
                (i & 1) == 0 ? monde.min.x : monde.max.x,
                (i & 2) == 0 ? monde.min.y : monde.max.y,
                (i & 4) == 0 ? monde.min.z : monde.max.z)));
        }

        sb.AppendLine("    dans la racine du prefab : centre " + dansRacine.center.ToString("F4")
                      + "   taille " + dansRacine.size.ToString("F4")
                      + "   soit " + (dansRacine.size.x * 1000f / 16.66667f).ToString("F1")
                      + " x " + (dansRacine.size.y * 1000f / 16.66667f).ToString("F1")
                      + " x " + (dansRacine.size.z * 1000f / 16.66667f).ToString("F1") + " mm");

        foreach (var rendu in filtre.GetComponents<MeshRenderer>())
        {
            foreach (var mat in rendu.sharedMaterials)
            {
                sb.AppendLine("    matériau : " + (mat != null ? mat.name : "(nul)"));
            }
        }
    }
}

return sb.ToString();
