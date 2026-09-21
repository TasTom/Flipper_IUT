using UnityEngine;

/// <summary>
/// Le tilt : secouer la table pour sauver une bille, au risque de tout perdre
/// (GDD §Contrôles : « Secouer à gauche / à droite » ; Wikipédia, « Pinball » §Playing techniques).
///
/// <para><b>Ce que fait une secousse.</b> Elle pousse la bille latéralement — c'est la seule
/// action du joueur qui agit sur une bille <i>déjà en mouvement</i>, et c'est ce qui en fait un
/// outil de rattrapage : une bille qui part vers l'outlane peut être ramenée de quelques
/// centimètres. Elle ne remplace pas un flipper, elle donne une seconde chance.</para>
///
/// <para><b>Pourquoi elle est limitée.</b> Une secousse gratuite et illimitée rendrait la perte de
/// bille impossible. Le GDD range le tilt dans les mécaniques à part entière, et les vraies tables
/// ont un <i>plumb bob</i> qui se ferme au-delà d'un seuil : « When any of these sensors is
/// activated, the game registers a "tilt" and the lights go out, solenoids for the flippers no
/// longer work. » D'où deux paliers : un <b>avertissement</b>, puis le <b>tilt</b>.</para>
///
/// <para><b>Le tilt ne rend pas la main.</b> Une fois tilté, les flippers sont morts et la bille
/// est perdue. C'est brutal, et c'est voulu : le GDD demande « une physique lisible et
/// suffisamment exigeante ». Un tilt qui ne coûterait rien ne serait qu'un bouton de plus.</para>
/// </summary>
[DefaultExecutionOrder(-40)]
public class TiltController : MonoBehaviour
{
    public static TiltController Instance { get; private set; }

    [Header("Secousse")]
    [Tooltip("Impulsion latérale appliquée à chaque bille en jeu, en unités/s.")]
    [SerializeField] private float forceSecousse = 14f;

    [Tooltip("Part de l'impulsion dirigée vers le haut de table. Une secousse purement latérale " +
             "ne décolle pas la bille du plateau et ne sauve rien.")]
    [Range(0f, 1f)]
    [SerializeField] private float partVersLeHaut = 0.25f;

    [Header("Tolérance")]
    [Tooltip("Nombre de secousses tolérées avant l'avertissement.")]
    [SerializeField] private int secoussesAvantAvertissement = 2;

    [Tooltip("Nombre de secousses tolérées avant le tilt.")]
    [SerializeField] private int secoussesAvantTilt = 4;

    [Tooltip("Durée de la fenêtre de comptage, en secondes. Passé ce délai sans secousse, le " +
             "compteur redescend — sans quoi une partie longue finirait tiltée d'office.")]
    [SerializeField] private float fenetre = 6f;

    [Tooltip("Délai minimum entre deux secousses, en secondes. Empêche de marteler la touche.")]
    [SerializeField] private float delaiEntreSecousses = 0.25f;

    [Header("Messages")]
    [SerializeField] private string messageAvertissement = "ATTENTION AU TILT";
    [SerializeField] private string messageTilt = "TILT !";

    private int secousses;
    private float derniereSecousse = float.NegativeInfinity;
    private float finFenetre;
    private bool avertissementDonne;

    /// <summary>La table est-elle tiltée ? Les flippers doivent alors refuser de fonctionner.</summary>
    public bool IsTilted { get; private set; }

    /// <summary>Nombre de secousses dans la fenêtre courante.</summary>
    public int Secousses => secousses;

    /// <summary>Le joueur a-t-il déjà été averti ?</summary>
    public bool AvertissementDonne => avertissementDonne;

    /// <summary>Émis au tilt. La partie doit retirer la bille en jeu.</summary>
    public event System.Action<TiltController> Tilted;

    /// <summary>Émis au premier avertissement.</summary>
    public event System.Action<TiltController> Averti;

    /// <summary>
    /// Fixe la tolérance aux secousses (GDD §Difficultés). Appelé par
    /// <see cref="DifficultyManager"/> ; les valeurs viennent d'un
    /// <see cref="DifficultyConfig"/>, jamais en dur.
    /// </summary>
    public void ConfigurerSecousses(int avantAvertissement, int avantTilt)
    {
        secoussesAvantAvertissement = Mathf.Max(1, avantAvertissement);
        secoussesAvantTilt = Mathf.Max(secoussesAvantAvertissement, avantTilt);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[TiltController] Un second contrôleur existe sur '{name}' : il est ignoré.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; }
    }

    private void Start()
    {
        // ⚠ On ne s'abonne PAS à `BallsChanged` : mesuré, ce compteur ne MONTE jamais en cours de
        // partie — il passe de 3 à 2 à la perte d'une bille et reste à 2 pendant la suivante. Le
        // prendre pour signal d'une nouvelle bille ferait qu'un tilt ne serait jamais absous, et
        // le joueur perdrait toutes ses billes d'affilée pour une seule secousse de trop.
        //
        // Le vrai signal est `GameManager.SpawnBall()`, qui appelle `ResetTilt()` directement.
    }

    private void Update()
    {
        if (IsTilted) { return; }

        // La fenêtre glisse : sans cela, le compteur ne redescendrait jamais et toute partie un
        // peu longue finirait par se tilte tout seule.
        if (secousses > 0 && Time.time > finFenetre)
        {
            secousses = 0;
            avertissementDonne = false;
        }

        bool gauche = InputRouter.Instance != null && InputRouter.Instance.TiltLeftPressed;
        bool droite = InputRouter.Instance != null && InputRouter.Instance.TiltRightPressed;

        if (!gauche && !droite) { return; }

        if (Time.time - derniereSecousse < delaiEntreSecousses) { return; }

        derniereSecousse = Time.time;

        // Deux secousses opposées dans la même frame s'annulent : on ne pousse qu'une fois, dans
        // le sens de la première touche lue.
        Secouer(gauche ? -1f : 1f);
    }

    /// <summary>Applique une secousse et tient le compte.</summary>
    public void Secouer(float sens)
    {
        if (IsTilted) { return; }

        if (secousses == 0) { finFenetre = Time.time + fenetre; }

        secousses++;

        PousserLesBilles(sens);

        if (secousses >= secoussesAvantTilt)
        {
            DeclencherTilt();
            return;
        }

        if (secousses >= secoussesAvantAvertissement && !avertissementDonne)
        {
            avertissementDonne = true;

            if (GameManager.Instance != null) { GameManager.Instance.ShowMessage(messageAvertissement, 1.5f); }

            Averti?.Invoke(this);
        }
    }

    /// <summary>
    /// Pousse toutes les billes en jeu. On passe par <see cref="BallManager"/> plutôt que par une
    /// recherche : c'est lui qui sait quelles billes sont vivantes, et une bille parquée ne doit
    /// pas bouger.
    /// </summary>
    private void PousserLesBilles(float sens)
    {
        var racine = transform.root;
        Vector3 lateral = racine != null ? racine.right : Vector3.right;
        Vector3 versLeHaut = racine != null ? racine.forward : Vector3.forward;

        Vector3 direction = (lateral * sens + versLeHaut * partVersLeHaut).normalized;

        foreach (var balle in FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude))
        {
            if (!balle.CompareTag("Ball")) { continue; }
            if (balle.isKinematic) { continue; }

            if (balle.IsSleeping()) { balle.WakeUp(); }

            balle.AddForce(direction * forceSecousse, ForceMode.Impulse);
        }
    }

    private void DeclencherTilt()
    {
        IsTilted = true;

        if (GameManager.Instance != null) { GameManager.Instance.ShowMessage(messageTilt, 2f); }

        Debug.Log("[TiltController] TILT : les flippers sont morts et la bille est perdue.", this);

        Tilted?.Invoke(this);

        // La bille est perdue : c'est le prix du tilt. On laisse un court instant au message
        // avant de la retirer, sinon le joueur ne comprend pas ce qui vient de se passer.
        Invoke(nameof(PerdreLaBille), 0.6f);
    }

    private void PerdreLaBille()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoseBall();
        }
        else
        {
            Debug.LogWarning("[TiltController] GameManager absent : la bille tiltée reste en jeu.", this);
        }
    }

    /// <summary>Remet le tilt à zéro pour une nouvelle bille.</summary>
    public void ResetTilt()
    {
        IsTilted = false;
        secousses = 0;
        avertissementDonne = false;
        derniereSecousse = float.NegativeInfinity;
    }
}
