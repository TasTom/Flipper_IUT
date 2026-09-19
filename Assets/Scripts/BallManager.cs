using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Les billes physiques : création, suivi, plafond de vitesse et anti-blocage
/// (GDD §Bille et lanceur).
///
/// Distinct de <see cref="GameManager"/> à dessein : ici on ne connaît que des Rigidbody,
/// pas les règles du jeu. <see cref="GameManager"/> décide combien de billes il reste ;
/// ce composant se contente de les faire exister correctement.
///
/// La bille peut être <b>posée dans la scène</b> plutôt qu'instanciée à l'exécution : elle est
/// alors réutilisée à chaque bille et n'est jamais détruite. Voir <see cref="sceneBall"/>.
/// </summary>
[DefaultExecutionOrder(-50)]
public class BallManager : MonoBehaviour
{
    public static BallManager Instance { get; private set; }

    [Header("Physique")]
    [Tooltip("Vitesse maximale d'une bille, en unités/s. Au-delà, la bille traverse les murs.")]
    [SerializeField] private float maxSpeed = 170f;   // 10 m/s réels — la vitesse terminale d'une bille à l'échelle de la table (1 u = 60 mm)

    [Tooltip("En dessous de cette vitesse, le chronomètre d'anti-blocage démarre.")]
    [SerializeField] private float restSpeedThreshold = 5f;   // échelle : 0,6 × 16,67

    [Header("Anti-blocage")]
    [Tooltip("Durée d'immobilité avant la première relance, en secondes.")]
    [SerializeField] private float stuckDelay = 4f;

    [Tooltip("Force de la relance, en impulsion.")]
    [SerializeField] private float nudgeImpulse = 20f;   // échelle : 1,2 × 16,67

    [Tooltip("Nombre de relances avant de replacer la bille au point de départ.")]
    [SerializeField] private int maxNudges = 3;

    [Header("Sécurité")]
    [Tooltip("Hauteur sous laquelle une bille est considérée perdue, même hors zone de drain.")]
    [SerializeField] private float killHeight = -5f;

    [Header("Bille de scène")]
    [Tooltip("Bille posée à la main dans la scène, au point d'apparition. Elle est réutilisée à " +
             "chaque bille au lieu d'en instancier une nouvelle, et n'est jamais détruite. " +
             "Laissée vide, elle est cherchée par tag — ce champ n'est là que pour une bille " +
             "désactivée dans la scène, que la recherche par tag ne voit pas.")]
    [SerializeField] private Rigidbody sceneBall;

    private readonly List<Rigidbody> liveBalls = new List<Rigidbody>();
    private readonly Dictionary<Rigidbody, float> stuckTimers = new Dictionary<Rigidbody, float>();
    private readonly Dictionary<Rigidbody, int> nudgeCounts = new Dictionary<Rigidbody, int>();

    /// <summary>
    /// La bille de scène, tant qu'elle est <b>hors jeu</b> — c'est-à-dire absente de
    /// <see cref="liveBalls"/>. Elle se reconnaît à cela seul : aucun état à tenir à jour,
    /// donc rien qui puisse se désynchroniser de la liste des billes vivantes.
    /// </summary>
    private Rigidbody ParkedBall
    {
        get { return sceneBall != null && !liveBalls.Contains(sceneBall) ? sceneBall : null; }
    }

    /// <summary>Émis après qu'une bille a été retirée du jeu.</summary>
    public event Action<Rigidbody> BallDrained;

    /// <summary>Émis après chaque création ou retrait de bille.</summary>
    public event Action BallCountChanged;

    /// <summary>Nombre de billes actuellement en jeu.</summary>
    public int LiveBallCount
    {
        get
        {
            PruneDestroyed();

            // Une bille peut être apparue sans passer par SpawnBall (multiball, prefab
            // instancié à la main dans l'Inspector). On ne rescanne que si la liste est
            // vide, pour ne pas payer un FindGameObjectsWithTag à chaque appel.
            if (liveBalls.Count == 0)
            {
                Rigidbody found = FindAnyBall();

                if (found != null)
                {
                    Register(found);
                }
            }

            return liveBalls.Count;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[BallManager] Un second gestionnaire existe sur '{name}' : il est ignoré.", this);
            enabled = false;
            return;
        }

        Instance = this;

        // Le champ n'est qu'un repli : la bille posée dans la scène se trouve par son tag.
        if (sceneBall == null)
        {
            sceneBall = FindSceneBall();
        }

        // Elle est mise hors jeu dès le démarrage : laissée en jeu, elle roulerait dans le
        // couloir pendant l'attract et la partie pourrait la compter perdue avant d'avoir
        // commencé.
        Park(sceneBall);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void FixedUpdate()
    {
        PruneDestroyed();

        for (int i = liveBalls.Count - 1; i >= 0; i--)
        {
            Rigidbody ball = liveBalls[i];

            if (ball == null)
            {
                continue;
            }

            ClampSpeed(ball);
            CheckStuck(ball);
            CheckFellThroughWorld(ball);
        }
    }

    /// <summary>
    /// Met une bille en jeu et la prend en charge. Unique voie de création : une bille instanciée
    /// ailleurs échapperait au plafond de vitesse et à l'anti-blocage.
    ///
    /// La bille posée à la main dans la scène est <b>réutilisée</b> si elle est hors jeu ;
    /// <paramref name="prefab"/> ne sert alors à rien et n'est lu qu'en son absence.
    /// </summary>
    public Rigidbody SpawnBall(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        Rigidbody parked = ParkedBall;

        if (parked != null)
        {
            Register(Wake(parked, position, rotation));
            return parked;
        }

        if (prefab == null)
        {
            Debug.LogError("[BallManager] Aucun prefab de bille : impossible de lancer la partie.", this);
            return null;
        }

        GameObject instance = Instantiate(prefab, position, rotation);
        Rigidbody body = instance.GetComponent<Rigidbody>();

        if (body == null)
        {
            Debug.LogError($"[BallManager] Le prefab '{prefab.name}' n'a pas de Rigidbody : " +
                           "la bille ne réagira pas à la physique.", this);
            return null;
        }

        instance.tag = "Ball";
        Register(body);
        return body;
    }

    /// <summary>
    /// Signale qu'une bille est sortie du jeu (zone de drain ou chute hors table).
    /// C'est <see cref="DrainZone"/> qui appelle cette méthode.
    /// </summary>
    public void NotifyDrained(Collider ballCollider)
    {
        if (ballCollider == null)
        {
            return;
        }

        Rigidbody body = ballCollider.attachedRigidbody != null
            ? ballCollider.attachedRigidbody
            : ballCollider.GetComponentInParent<Rigidbody>();

        Unregister(body);

        // La bille de scène est parquée au lieu d'être détruite : c'est celle que l'utilisateur
        // a posée dans la scène, et la bille suivante doit la retrouver.
        if (body != null && body == sceneBall)
        {
            Park(body);
        }
        else
        {
            DestroyBallObject(ballCollider);
        }

        BallDrained?.Invoke(body);
    }

    /// <summary>
    /// Détruit l'objet d'une bille en remontant jusqu'à la racine de son Rigidbody.
    /// Statique pour que <see cref="DrainZone"/> puisse s'en servir même sans gestionnaire.
    /// </summary>
    public static void DestroyBallObject(Collider ballCollider)
    {
        if (ballCollider == null)
        {
            return;
        }

        Rigidbody body = ballCollider.attachedRigidbody != null
            ? ballCollider.attachedRigidbody
            : ballCollider.GetComponentInParent<Rigidbody>();
        GameObject objet = body != null ? body.gameObject : ballCollider.gameObject;

        // Désactivé AVANT d'être détruit, et ce n'est pas cosmétique : `Destroy` ne prend effet
        // qu'à la fin de la frame, alors que `LiveBallCount` rescanne la scène dès que sa liste
        // est vide. Laissé actif, un objet en cours de destruction est retrouvé par
        // `FindAnyBall` et réenregistré comme bille vivante ; `HandleBallLoss` voit alors
        // toujours une bille en jeu et ne décompte jamais rien. C'est ce qui faisait qu'une
        // bille tombée dans la zone de sortie ne coûtait aucune bille.
        objet.SetActive(false);
        Destroy(objet);
    }

    /// <summary>Retire toutes les billes en jeu, sans émettre <see cref="BallDrained"/>.</summary>
    public void ClearAll()
    {
        for (int i = liveBalls.Count - 1; i >= 0; i--)
        {
            Rigidbody ball = liveBalls[i];

            if (ball == null)
            {
                continue;
            }

            // La bille de scène survit à un redémarrage de partie : c'est elle que
            // `SpawnBall` réutilise juste après. La détruire ici priverait la partie suivante
            // de sa bille, pour ne laisser qu'un prefab à instancier.
            if (ball == sceneBall)
            {
                Park(ball);
            }
            else
            {
                Destroy(ball.gameObject);
            }
        }

        liveBalls.Clear();
        stuckTimers.Clear();
        nudgeCounts.Clear();
        BallCountChanged?.Invoke();
    }

    /// <summary>
    /// Met la bille de scène hors jeu : immobile, invisible, et absente de
    /// <see cref="liveBalls"/>.
    ///
    /// Elle est <b>désactivée</b> plutôt que simplement gelée, et c'est un choix visible : entre
    /// deux billes, elle ne stationne pas dans le couloir pendant le délai de relance. Elle
    /// n'est jamais détruite — <see cref="Wake"/> la remet en jeu telle quelle.
    /// </summary>
    private static void Park(Rigidbody ball)
    {
        if (ball == null)
        {
            return;
        }

        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;

        // Kinématique en plus d'inactive : si l'objet était réactivé autrement que par
        // `Wake`, la bille resterait sur place au lieu de se remettre à tomber.
        ball.isKinematic = true;
        ball.gameObject.SetActive(false);
    }

    /// <summary>Remet la bille de scène en jeu, au point demandé.</summary>
    private static Rigidbody Wake(Rigidbody ball, Vector3 position, Quaternion rotation)
    {
        // Le repositionnement se fait pendant que l'objet est encore inactif : la bille
        // apparaît exactement au point d'apparition, sans qu'un pas de physique ait pu la
        // déplacer entre-temps.
        ball.transform.SetPositionAndRotation(position, rotation);

        ball.gameObject.SetActive(true);
        ball.isKinematic = false;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;

        return ball;
    }

    /// <summary>Prend en compte une bille créée hors de <see cref="SpawnBall"/> (multiball).</summary>
    public void Register(Rigidbody ball)
    {
        if (ball == null || liveBalls.Contains(ball))
        {
            return;
        }

        liveBalls.Add(ball);
        stuckTimers[ball] = 0f;
        nudgeCounts[ball] = 0;
        BallCountChanged?.Invoke();
    }

    public void Unregister(Rigidbody ball)
    {
        if (ball == null)
        {
            return;
        }

        if (liveBalls.Remove(ball))
        {
            stuckTimers.Remove(ball);
            nudgeCounts.Remove(ball);
            BallCountChanged?.Invoke();
        }
    }

    /// <summary>
    /// Borne la vitesse : sans plafond, une bille lancée par un slingshot traverse un mur
    /// dès que le pas physique est trop grand pour la taille de son collider.
    /// </summary>
    private void ClampSpeed(Rigidbody ball)
    {
        Vector3 velocity = ball.linearVelocity;

        if (velocity.sqrMagnitude <= maxSpeed * maxSpeed)
        {
            return;
        }

        ball.linearVelocity = velocity.normalized * maxSpeed;
    }

    private void CheckStuck(Rigidbody ball)
    {
        // Une bille posée dans le couloir, en attente de lancement, est immobile par nature :
        // c'est même son état normal. La relancer ferait dériver la partie avant même que le
        // joueur ait touché au lanceur. L'anti-blocage ne vise que les billes réellement en jeu.
        if (GameManager.Instance != null && GameManager.Instance.State == GameManager.GameState.ReadyToLaunch)
        {
            stuckTimers[ball] = 0f;
            return;
        }

        if (ball.linearVelocity.sqrMagnitude > restSpeedThreshold * restSpeedThreshold)
        {
            stuckTimers[ball] = 0f;
            return;
        }

        stuckTimers[ball] += Time.fixedDeltaTime;

        if (stuckTimers[ball] < stuckDelay)
        {
            return;
        }

        stuckTimers[ball] = 0f;
        nudgeCounts[ball] = nudgeCounts.TryGetValue(ball, out int count) ? count + 1 : 1;

        if (nudgeCounts[ball] > maxNudges)
        {
            Debug.LogWarning("[BallManager] Bille bloquée malgré plusieurs relances : " +
                             "elle est replacée au point de départ. Vérifie les colliders autour d'elle.", ball);
            Respawn(ball);
            return;
        }

        // Relance douce et non verticale : une impulsion vers le haut ferait sortir la bille
        // de la table, ce qui masquerait le vrai problème au lieu de le révéler.
        Vector3 nudge = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0.1f, UnityEngine.Random.Range(-1f, 1f)).normalized;
        ball.AddForce(nudge * nudgeImpulse, ForceMode.Impulse);
    }

    private void CheckFellThroughWorld(Rigidbody ball)
    {
        if (ball.position.y >= killHeight)
        {
            return;
        }

        Debug.LogWarning("[BallManager] Bille passée sous la table : elle est comptée comme perdue. " +
                         "Vérifie qu'aucun trou ne subsiste dans les murs.", ball);
        NotifyDrained(ball.GetComponent<Collider>());
    }

    /// <summary>
    /// Ramène une bille bloquée dans le couloir de lancement, sans la détruire.
    ///
    /// Le déplacement doit être suivi d'un retour d'état auprès de <see cref="GameManager"/> :
    /// dans le couloir, une bille immobile est dans son état normal, alors que la partie est
    /// encore en <see cref="GameManager.GameState.Playing"/>. Sans ce retour, l'anti-blocage
    /// relancerait la bille toutes les <c>stuckDelay</c> secondes, sans fin.
    /// </summary>
    private void Respawn(Rigidbody ball)
    {
        Transform spawn = GameManager.Instance != null ? GameManager.Instance.BallSpawnPoint : null;

        if (spawn != null)
        {
            ball.position = spawn.position;
            ball.rotation = spawn.rotation;
        }
        else
        {
            // Repli sans point de départ connu : on la remonte à la verticale.
            ball.position += Vector3.up * 1.5f;
        }

        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        nudgeCounts[ball] = 0;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyBallReturnedToLane();
        }
    }

    private void PruneDestroyed()
    {
        for (int i = liveBalls.Count - 1; i >= 0; i--)
        {
            if (liveBalls[i] == null)
            {
                liveBalls.RemoveAt(i);
                BallCountChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// Une bille présente dans la scène mais absente de <see cref="liveBalls"/>.
    ///
    /// La bille de scène est écartée : elle est dans la scène sans être en jeu, et la compter
    /// comme vivante ferait passer toute perte de bille pour un multiball — la bille ne serait
    /// alors jamais décomptée, et la partie ne s'arrêterait plus.
    ///
    /// Ne voit que les objets <b>actifs</b>, ce qui est voulu : une bille parquée ou en cours de
    /// destruction est désactivée, donc hors jeu. C'est la seule chose qui empêche cette
    /// recherche de ressusciter une bille que <see cref="NotifyDrained"/> vient de retirer.
    /// </summary>
    private Rigidbody FindAnyBall()
    {
        GameObject[] candidates = GameObject.FindGameObjectsWithTag("Ball");

        foreach (GameObject candidate in candidates)
        {
            Rigidbody body = candidate.GetComponent<Rigidbody>();

            if (body != null && body != sceneBall)
            {
                return body;
            }
        }

        return null;
    }

    /// <summary>
    /// La bille posée à la main dans la scène, <b>y compris désactivée</b>.
    ///
    /// Distincte de <see cref="FindAnyBall"/> à dessein : celle-ci cherche une bille EN JEU et
    /// doit donc ignorer les objets inactifs, tandis que celle-là cherche la bille PARQUÉE, qui
    /// est inactive par construction — <see cref="Park"/> la désactive, et elle l'est déjà si la
    /// scène a été enregistrée ainsi. Cherchée par tag seul, elle resterait introuvable,
    /// <see cref="sceneBall"/> resterait nul, et chaque bille serait instanciée depuis le prefab
    /// au lieu de réutiliser celle que l'utilisateur a posée.
    /// </summary>
    private static Rigidbody FindSceneBall()
    {
        foreach (Rigidbody body in FindObjectsByType<Rigidbody>(FindObjectsInactive.Include))
        {
            // Un Rigidbody de prefab n'appartient à aucune scène : il ne doit pas être retenu.
            if (body.CompareTag("Ball") && body.gameObject.scene.IsValid())
            {
                return body;
            }
        }

        return null;
    }
}

