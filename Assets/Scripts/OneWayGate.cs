using UnityEngine;

/// <summary>
/// Porte à sens unique du couloir de lancement (GDD §Rampe IUT, Wikipédia « Pinball » §Components :
/// « This block will allow balls to come through one way but will block the ball if it is going the
/// other way. »).
///
/// <para><b>Pourquoi elle existe.</b> Le couloir du plongeur débouche en haut dans l'aire de jeu.
/// Sans porte, une bille qui n'a pas assez d'élan pour se dégager <b>redescend le couloir</b> et
/// revient au plongeur — mesuré : à 60 u/s elle revient, et la partie se bloque puisqu'elle n'est
/// ni perdue ni jouable. Une vraie table a exactement ce clapet.</para>
///
/// <para><b>Comment il fonctionne.</b> Le clapet est un <b>collider solide</b>, donc fermé par
/// défaut. Un déclencheur placé juste en dessous surveille la direction de la bille : si elle
/// <i>monte</i>, le solide est désactivé le temps qu'elle passe ; sinon il reste fermé et la bille
/// bute dessus. C'est le comportement mécanique réel — le clapet s'ouvre dans un sens et se
/// referme sous son propre poids.</para>
///
/// <para>⚠ Le sens de « monter » se lit sur <c>transform.root.forward</c>, pas sur un axe du monde :
/// la table est inclinée de 7°, et un clapet posé ailleurs que sur cette table devrait continuer à
/// fonctionner.</para>
/// </summary>
public class OneWayGate : MonoBehaviour
{
    [Header("Clapet")]
    [Tooltip("Le collider SOLIDE qui bloque la bille qui redescend. Laissé vide, il est cherché " +
             "sur un enfant — c'est la disposition normale, le clapet étant le maillage du portail.")]
    [SerializeField] private Collider clapet;

    [Tooltip("Déclencheur qui surveille la direction de la bille. Laissé vide, il est pris sur cet " +
             "objet : le script doit alors être posé sur le déclencheur.")]
    [SerializeField] private Collider surveillance;

    [Header("Détection")]
    [Tooltip("Vitesse minimale vers le haut, en u/s, pour considérer que la bille monte. En dessous, " +
             "elle est traitée comme descendante — une bille immobile ne doit pas ouvrir le clapet.")]
    [SerializeField] private float seuilMontee = 1f;

    [Tooltip("Durée d'ouverture après le passage d'une bille montante, en secondes. Un peu de marge " +
             "évite que le clapet se referme sur la bille elle-même.")]
    [SerializeField] private float dureeOuverture = 0.4f;

    private float refermeA;
    private bool ouvert;

    /// <summary>Le clapet est-il ouvert en ce moment ?</summary>
    public bool IsOpen => ouvert;

    private void Awake()
    {
        if (surveillance == null) { surveillance = GetComponent<Collider>(); }

        if (surveillance != null && !surveillance.isTrigger)
        {
            Debug.LogWarning($"[OneWayGate] '{name}' : le collider de surveillance n'est pas un " +
                             "déclencheur. Le clapet ne s'ouvrira jamais et la bille restera " +
                             "bloquée dans le couloir.", this);
        }

        if (clapet == null)
        {
            // Le premier collider SOLIDE du sous-arbre : c'est le clapet.
            foreach (var c in GetComponentsInChildren<Collider>(true))
            {
                if (!c.isTrigger && c != surveillance) { clapet = c; break; }
            }
        }

        if (clapet == null)
        {
            Debug.LogWarning($"[OneWayGate] '{name}' n'a aucun collider solide : le clapet ne " +
                             "bloque rien, et la bille pourra redescendre le couloir.", this);
        }

        Fermer();
    }

    private void Update()
    {
        if (ouvert && Time.time >= refermeA) { Fermer(); }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Ball")) { return; }

        Rigidbody balle = other.attachedRigidbody;

        if (balle == null) { return; }

        var racine = transform.root;
        Vector3 monte = racine != null ? racine.forward : Vector3.forward;

        // Positif = la bille monte vers le haut de table.
        float versLeHaut = Vector3.Dot(balle.linearVelocity, monte);

        if (versLeHaut > seuilMontee)
        {
            Ouvrir();
        }
        else if (ouvert)
        {
            // Elle redescend : on referme tout de suite, sans attendre la fin du délai. C'est le
            // clapet qui la retient — sinon elle passerait pendant la marge d'ouverture.
            Fermer();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Ball")) { return; }

        if (ouvert) { refermeA = Time.time + dureeOuverture; }
    }

    private void Ouvrir()
    {
        if (ouvert) { return; }

        ouvert = true;
        refermeA = Time.time + dureeOuverture;

        if (clapet != null) { clapet.enabled = false; }
    }

    private void Fermer()
    {
        ouvert = false;

        if (clapet != null) { clapet.enabled = true; }
    }
}
