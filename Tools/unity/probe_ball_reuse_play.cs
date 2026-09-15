// La partie réutilise-t-elle bien la bille posée dans la scène ?
//
// Le code compile, les membres existent — mais « la bille est réutilisée » est un fait
// d'exécution, qui ne se lit que dans une partie lancée. On entre donc en Play mode et on
// regarde, à l'état `ReadyToLaunch` :
//
//   * combien de Rigidbody `BallManager` tient pour vivants — **un seul** attendu, sinon la
//     partie ne pourrait plus perdre de bille ;
//   * combien d'objets portent le tag `Ball` dans la scène — **un seul**, sinon c'est que
//     `SpawnBall` a instancié par-dessus la bille posée ;
//   * si c'est bien la bille de la scène qui est en jeu, à l'endroit attendu. Le champ
//     `sceneBall` porte la référence posée à la main : comparer son instance ID à celui de la
//     bille vivante démasque une bille instanciée, même si elle porte le même nom.

var sb = new System.Text.StringBuilder();

sb.AppendLine("=== Play mode : " + UnityEditor.EditorApplication.isPlaying + " ===");

var gestionnaire = UnityEngine.Object.FindAnyObjectByType<BallManager>();

if (gestionnaire == null)
{
    return sb.Append("BallManager introuvable").ToString();
}

var serialise = new SerializedObject(gestionnaire);

// --- ce que BallManager tient pour vivant -------------------------------------------------------

var vivantes = new System.Collections.Generic.List<Rigidbody>();
var champ = typeof(BallManager).GetField("liveBalls",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

if (champ != null && champ.GetValue(gestionnaire) is System.Collections.IEnumerable liste)
{
    foreach (var element in liste)
    {
        if (element is Rigidbody corps) { vivantes.Add(corps); }
    }
}

sb.AppendLine();
sb.AppendLine("billes vivantes (liveBalls) : " + vivantes.Count);

foreach (var corps in vivantes)
{
    sb.AppendLine("  " + corps.name
                  + "   actif " + corps.gameObject.activeSelf
                  + "   cinématique " + corps.isKinematic
                  + "   position " + corps.position.ToString("F4"));
}

sb.AppendLine("LiveBallCount (propriété)   : " + gestionnaire.LiveBallCount);

// --- ce qu'il y a réellement dans la scène ------------------------------------------------------

var parTag = GameObject.FindGameObjectsWithTag("Ball");

sb.AppendLine();
sb.AppendLine("objets taggés 'Ball' dans la scène : " + parTag.Length);

foreach (var objet in parTag)
{
    var corps = objet.GetComponent<Rigidbody>();

    sb.AppendLine("  " + objet.name
                  + "   actif " + objet.activeSelf
                  + (corps != null ? "   cinématique " + corps.isKinematic : "   SANS RIGIDBODY"));
}

// --- est-ce bien l'instance posée à la main ? ---------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== la bille en jeu est-elle celle de la scène ? ===");

var reference = serialise.FindProperty("sceneBall");
var attendu = reference != null ? reference.objectReferenceValue : null;

sb.AppendLine("sceneBall                   : "
              + (attendu != null ? attendu.name + "   actif " + ((Component)attendu).gameObject.activeSelf
                                 : "VIDE — pas de bille de scène, instanciation depuis le prefab"));

if (attendu != null && vivantes.Count == 1)
{
    // Comparaison de références : deux objets distincts peuvent porter le même nom, un seul
    // peut être la bille posée à la main.
    sb.AppendLine("la bille vivante est la bille de scène : " + (vivantes[0] == (Rigidbody)attendu));
    sb.AppendLine("la bille de scène est-elle active     : "
                  + ((Rigidbody)attendu).gameObject.activeSelf
                  + "   (elle doit l'être : c'est elle qui est en jeu)");
}

// --- l'état de la partie ------------------------------------------------------------------------

var partie = UnityEngine.Object.FindAnyObjectByType<GameManager>();

sb.AppendLine();
sb.AppendLine("état de la partie  : " + (partie != null ? partie.State.ToString() : "GameManager absent"));
sb.AppendLine("billes restantes   : " + (partie != null ? partie.BallsRemaining.ToString() : "—"));

var point = GameObject.Find("BallSpawnPoint");

if (point != null && vivantes.Count > 0)
{
    sb.AppendLine("écart au point d'apparition : "
                  + Vector3.Distance(vivantes[0].position, point.transform.position).ToString("F4") + " u");
}

return sb.ToString();
