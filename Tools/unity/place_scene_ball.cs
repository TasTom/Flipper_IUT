// Pose la bille dans `Neutral.unity`, sous `PinballTable/Gameplay`, au point d'apparition.
//
// Pourquoi une bille dans la scène plutôt qu'une bille instanciée à l'exécution : elle se voit
// et se déplace dans l'éditeur, on peut la faire rouler à la main pour éprouver le couloir, et
// `BallManager` la réutilise au lieu d'en créer une seconde (voir `sceneBall` dans
// `BallManager.cs`). Sans cette pose, `sceneBall` reste vide et le comportement d'avant est
// conservé — instanciation depuis `Ball.prefab`.
//
// Deux précautions, celles des autres menus :
//
//   * **Idempotent** — une bille déjà posée dans la scène n'est pas dupliquée. Le contrôle se
//     fait sur le tag, comme le fait `BallManager` : deux billes en jeu, c'est une partie qui
//     ne peut plus perdre.
//   * **Annulable** — `Undo.RegisterCreatedObjectUndo` : Ctrl+Z retire la bille.
//
// La scène n'est **pas** enregistrée (Ctrl+S).

var sb = new System.Text.StringBuilder();

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

sb.AppendLine("=== scène '" + scene.name + "' ===");

// --- y a-t-il déjà une bille ? ------------------------------------------------------------------

var deja = GameObject.FindGameObjectsWithTag("Ball");

if (deja.Length > 0)
{
    sb.AppendLine("RIEN FAIT : " + deja.Length + " objet(s) déjà taggé(s) 'Ball' — pas de doublon.");

    foreach (var objet in deja)
    {
        sb.AppendLine("  " + objet.name + "   position " + objet.transform.position.ToString("F4"));
    }

    return sb.ToString();
}

// --- le prefab et le point de pose --------------------------------------------------------------

var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ball.prefab");

if (prefab == null)
{
    return "Assets/Prefabs/Ball.prefab introuvable";
}

var racine = GameObject.Find("PinballTable");
var gameplay = racine != null ? racine.transform.Find("Gameplay") : null;
var point = GameObject.Find("BallSpawnPoint");

if (gameplay == null || point == null)
{
    return "PinballTable/Gameplay ou BallSpawnPoint introuvable — la scène n'est pas celle du menu 4";
}

sb.AppendLine("prefab    : " + prefab.name);
sb.AppendLine("Gameplay  : " + gameplay.name + "   pos locale " + gameplay.localPosition.ToString("F4"));
sb.AppendLine("point     : " + point.name
              + "   pos locale " + point.transform.localPosition.ToString("F4")
              + "   pos monde " + point.transform.position.ToString("F4"));

// --- la pose ------------------------------------------------------------------------------------

var bille = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, gameplay);

// Une instance de prefab, pas un `Instantiate` : le lien avec `Ball.prefab` est conservé, donc
// une retouche du prefab se répercute sur la scène. Posée en **enfant de `Gameplay`** et non de
// `BallSpawnPoint` : le point d'apparition est un repère, la bille est un objet physique — un
// Rigidbody sous un parent incliné hériterait de son mouvement.
//
// Les coordonnées sont recopiées du point plutôt que saisies : deux valeurs écrites à la main
// divergeraient au premier déplacement du point.
//
// ⚠️ **L'échelle, elle, ne se recopie pas.** Elle n'est pas une coordonnée : c'est une propriété
// du prefab, et `Ball.prefab` porte 0,45 — 27 mm, l'ancre d'échelle du projet. Recopier celle
// du point d'apparition (`1, 1, 1`) donne une bille de 60 mm, plus large que le couloir de
// lancement (34 mm) : la physique l'éjecte, et le rapport largeur/bille tombe de 19,05 à 8,6.
// C'est l'erreur que ce script a commise, et que le contrôle ci-dessous attrape désormais.
bille.transform.localPosition = point.transform.localPosition;
bille.transform.localRotation = point.transform.localRotation;
bille.name = "Ball";
bille.transform.SetSiblingIndex(point.transform.GetSiblingIndex() + 1);

UnityEditor.Undo.RegisterCreatedObjectUndo(bille, "Poser la bille dans la scène");
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

sb.AppendLine();
sb.AppendLine("=== bille posée ===");
sb.AppendLine("nom       : " + bille.name
              + "   tag " + bille.tag
              + "   layer " + UnityEngine.LayerMask.LayerToName(bille.layer)
              + " (" + bille.layer + ")");
sb.AppendLine("hiérarchie: " + gameplay.name + " > " + bille.name
              + "   (enfant " + bille.transform.GetSiblingIndex() + ")");
sb.AppendLine("pos locale: " + bille.transform.localPosition.ToString("F4"));
sb.AppendLine("pos monde : " + bille.transform.position.ToString("F4"));
sb.AppendLine("échelle   : " + bille.transform.localScale.ToString("F4"));

// --- contrôles ----------------------------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== contrôles ===");

sb.AppendLine("écart avec le point d'apparition : "
              + Vector3.Distance(bille.transform.position, point.transform.position).ToString("F6")
              + " u");

var corps = bille.GetComponent<Rigidbody>();

sb.AppendLine("Rigidbody  : " + (corps != null
                                 ? "masse " + corps.mass
                                   + "   cinématique " + corps.isKinematic
                                   + "   détection " + corps.collisionDetectionMode
                                 : "AUCUN"));

var sphere = bille.GetComponent<SphereCollider>();

// Le rayon réel, échelle comprise : c'est lui qui décide de tout, et une bille deux fois trop
// grande ne se voit pas sur une capture d'écran — elle se voit sur ce chiffre.
float rayon = sphere != null ? sphere.radius * bille.transform.lossyScale.x : 0f;
float diametre = rayon * 2f;

sb.AppendLine("collider   : " + (sphere != null
                                 ? "SphereCollider rayon " + rayon.ToString("F4") + " u"
                                   + "   diamètre " + diametre.ToString("F4") + " u = "
                                   + (diametre / 16.66667f * 1000f).ToString("F2") + " mm"
                                   + "   déclencheur " + sphere.isTrigger
                                 : "AUCUN"));

if (Mathf.Abs(diametre - 0.45f) > 0.001f)
{
    sb.AppendLine("  ⚠ diamètre attendu 0,4500 u (27 mm) — vérifie l'échelle de la bille");
}

// Ce que la bille va trouver sous elle : c'est le sol du couloir, mesuré au point exact de pose.
Physics.SyncTransforms();

RaycastHit sol;

if (Physics.Raycast(bille.transform.position, Vector3.down, out sol, 30f, ~0, QueryTriggerInteraction.Ignore))
{
    float bas = bille.transform.position.y - rayon;

    sb.AppendLine("sol dessous : " + sol.collider.gameObject.name + " à y " + sol.point.y.ToString("F4")
                  + "   → bas de la bille à " + bas.ToString("F4")
                  + ", chute de " + (bas - sol.point.y).ToString("F4") + " u"
                  + (bas < sol.point.y ? "   ⚠ ENFONCÉE DANS LE SOL" : ""));
}
else
{
    sb.AppendLine("sol dessous : AUCUN — la bille tomberait dans le vide");
}

// Un chevauchement avec la géométrie au moment de la pose se verrait ici, et nulle part ailleurs.
var intrus = Physics.OverlapSphere(bille.transform.position, rayon, ~0, QueryTriggerInteraction.Ignore);

sb.AppendLine("objets à moins d'un rayon : " + intrus.Length);

foreach (var autre in intrus)
{
    sb.AppendLine("  " + autre.gameObject.name
                  + (autre.transform.IsChildOf(bille.transform) ? "   (la bille elle-même)" : "   ⚠ SUPERPOSITION"));
}

sb.AppendLine();
sb.AppendLine("scène modifiée : " + scene.isDirty + "   (Ctrl+Z annule, Ctrl+S conserve)");

return sb.ToString();
