using UnityEngine;

/// <summary>
/// Le kickback : un solénoïde placé au fond d'un outlane qui <b>renvoie la bille en jeu</b> au lieu
/// de la laisser tomber (Wikipédia, « Pinball » §Features : « When a ball goes into one of the
/// outlane instead of draining goes into a kicker that will launch the ball back into play. Their
/// use is limited and has to be earned to be used. »).
///
/// <para><b>Il s'épuise.</b> Le GDD le range dans les aides à la frustration : « Une petite zone de
/// sauvetage latérale sur la difficulté facile ». Un kickback permanent supprimerait la perte par
/// l'outlane, qui est justement ce qui fait payer un tir raté. Il est donc <b>armé</b>, consommé à
/// usage, et se réarme par le jeu — ici en complétant la série de rollovers du haut, ce qui donne
/// une raison de plus d'aller les chercher.</para>
///
/// <para><b>Il ne compte pas la bille perdue.</b> La bille repart : elle n'est pas perdue. Le
/// déclencheur de perte de l'outlane est donc neutralisé pendant le renvoi, sinon la partie
/// décompterait une bille que le joueur vient de sauver.</para>
/// </summary>
public class Kickback : MonoBehaviour
{
    [Header("Réglage")]
    [Tooltip("Le kickback est-il armé au début de la partie ?")]
    [SerializeField] private bool armeAuDepart;

    [Tooltip("Force du renvoi, en unités/s. Doit suffire à remonter le couloir : mesurer plutôt " +
             "que supposer — un renvoi trop faible laisse la bille retomber aussitôt.")]
    [SerializeField] private float force = 45f;

    [Tooltip("Inclinaison du renvoi, dans les axes de la table. Un peu vers l'aire de jeu, sinon " +
             "la bille retombe exactement dans l'outlane qu'elle vient de quitter.")]
    [SerializeField] private float inclinaison = 0.35f;

    [Header("Retour au joueur")]
    [Tooltip("Message affiché quand le kickback sauve la bille. Vide = aucun.")]
    [SerializeField] private string message = "SAUVÉ !";

    [Tooltip("Durée du message, en secondes.")]
    [SerializeField] private float dureeMessage = 1.5f;

    [Header("Anti-rebond")]
    [Tooltip("Délai minimum entre deux renvois, en secondes. Sans lui, une bille qui vibre dans le " +
             "déclencheur serait renvoyée plusieurs fois.")]
    [SerializeField] private float delai = 0.8f;

    private bool arme;
    private float dernierRenvoi = float.NegativeInfinity;

    /// <summary>Le kickback est-il prêt à sauver une bille ?</summary>
    public bool IsArmed => arme;

    /// <summary>Émis quand le kickback sauve une bille.</summary>
    public event System.Action<Kickback, Rigidbody> Saved;

    private void Start()
    {
        arme = armeAuDepart;
    }

    /// <summary>Arme le kickback — appelé par la série de rollovers complétée, ou par un bonus.</summary>
    public void Armer()
    {
        arme = true;
    }

    /// <summary>Le désarme sans l'utiliser.</summary>
    public void Desarmer()
    {
        arme = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!arme || !other.CompareTag("Ball"))
        {
            return;
        }

        if (Time.time - dernierRenvoi < delai)
        {
            return;
        }

        Rigidbody balle = other.attachedRigidbody;

        if (balle == null)
        {
            return;
        }

        dernierRenvoi = Time.time;
        arme = false;

        // La direction du renvoi : vers le haut de table, dans les axes de la table inclinée, plus
        // une inclinaison vers l'aire de jeu. On ne suppose rien du sens de `transform.forward` —
        // un kickback se pose dans un couloir vertical, mais rien ne le garantit.
        var racine = transform.root;
        Vector3 versLeHaut = racine != null ? racine.forward : Vector3.forward;
        Vector3 versLeCentre = -Mathf.Sign(transform.position.x) * (racine != null ? racine.right : Vector3.right);

        Vector3 direction = (versLeHaut + versLeCentre * inclinaison).normalized;

        balle.linearVelocity = Vector3.zero;
        balle.angularVelocity = Vector3.zero;
        balle.AddForce(direction * force, ForceMode.Impulse);

        if (GameManager.Instance != null && !string.IsNullOrEmpty(message))
        {
            GameManager.Instance.ShowMessage(message, dureeMessage);
        }

        Saved?.Invoke(this, balle);
    }
}
