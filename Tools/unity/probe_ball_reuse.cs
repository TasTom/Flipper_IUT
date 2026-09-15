// La réutilisation de la bille de scène est-elle bien dans l'assembly chargé ?
//
// `recompile_status` répond « completed, failed: false » — mais il a déjà répondu
// `up_to_date` sur du code qui n'était pas chargé. On vérifie donc par réflexion sur le type
// réellement présent en mémoire, pas sur la parole du compilateur.

var sb = new System.Text.StringBuilder();

var type = typeof(BallManager);

sb.AppendLine("=== BallManager, tel qu'il est chargé ===");
sb.AppendLine("assembly : " + type.Assembly.GetName().Name
              + "   chemin : " + type.Assembly.Location);

// --- le champ sérialisé -------------------------------------------------------------------------

sb.AppendLine();

var champ = type.GetField("sceneBall",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

sb.AppendLine("champ 'sceneBall'          : " + (champ != null ? champ.FieldType.Name : "ABSENT"));

// --- la propriété qui décide de l'état hors jeu --------------------------------------------------

var propriete = type.GetProperty("ParkedBall",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

sb.AppendLine("propriété 'ParkedBall'     : "
              + (propriete != null ? propriete.PropertyType.Name : "ABSENT"));

// --- les deux bascules et la recherche ----------------------------------------------------------

string[] methodes = { "Park", "Wake", "FindAnyBall" };

foreach (string nom in methodes)
{
    var trouvee = type.GetMethod(nom,
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance
        | System.Reflection.BindingFlags.NonPublic);

    sb.AppendLine(("méthode '" + nom + "'").PadRight(27) + ": "
                  + (trouvee != null
                     ? "(" + string.Join(", ", System.Array.ConvertAll(
                           trouvee.GetParameters(), p => p.ParameterType.Name)) + ") -> "
                       + trouvee.ReturnType.Name
                     : "ABSENTE"));
}

// `FindAnyBall` rendait un `Component` avant : c'est ce type de détail qu'un simple
// « ça compile » ne montre pas.
sb.AppendLine();
sb.AppendLine("FindAnyBall rend un Rigidbody : "
              + (type.GetMethod("FindAnyBall",
                     System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                 ?.ReturnType == typeof(Rigidbody)));

// --- et l'état de la scène, qui ne doit pas encore avoir de bille --------------------------------

sb.AppendLine();
sb.AppendLine("=== scène ===");
sb.AppendLine("objets taggés 'Ball' : " + GameObject.FindGameObjectsWithTag("Ball").Length);

var manager = UnityEngine.Object.FindAnyObjectByType<BallManager>();

if (manager != null)
{
    var serialise = new SerializedObject(manager);
    var prop = serialise.FindProperty("sceneBall");

    sb.AppendLine("BallManager.sceneBall : "
                  + (prop != null && prop.objectReferenceValue != null
                     ? prop.objectReferenceValue.name
                     : "VIDE — la bille n'est pas encore posée"));
}

return sb.ToString();
