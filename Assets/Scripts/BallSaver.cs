using UnityEngine;

/// <summary>
/// Ball saver : pendant quelques secondes après le lancement, une bille qui draine est renvoyée
/// au lanceur au lieu d'être comptée perdue (aide standard des flippers pro ; GDD §Difficultés,
/// aides à la frustration).
///
/// <para>Aucune dépendance de scène : <see cref="BallManager"/> crée ce composant lui-même et
/// l'interroge à chaque drain. Il ne sauve que la <b>dernière</b> bille en jeu — pendant un
/// multiball, une bille perdue reste perdue.</para>
/// </summary>
public class BallSaver : MonoBehaviour
{
    [Tooltip("Durée de protection après chaque lancement, en secondes. 0 désactive le ball saver.")]
    [SerializeField] private float duree = 8f;

    [Tooltip("Durée de protection au démarrage d'un multiball, en secondes (sauve toutes les billes).")]
    [SerializeField] private float dureeMultiball = 12f;

    [Tooltip("Impulsion de renvoi automatique quand une bille est sauvée pendant un multiball.")]
    [SerializeField] private float impulsionRenvoiMultiball = 55f;

    [Tooltip("Le ball saver ne sert qu'une fois par bille.")]
    [SerializeField] private bool uneSeuleFois = true;

    private float finProtection = float.NegativeInfinity;
    private float finProtectionMultiball = float.NegativeInfinity;
    private bool consomme;
    private GameManager abonne;

    public bool EstActif => (duree > 0f && !consomme && Time.time < finProtection) || EstActifMultiball;
    public bool EstActifMultiball => dureeMultiball > 0f && Time.time < finProtectionMultiball;

    /// <summary>Configure la durée du ball saver depuis <see cref="DifficultyConfig"/>.</summary>
    public void SetDuration(float seconds)
    {
        duree = Mathf.Max(0f, seconds);
    }

    /// <summary>Arme le ball saver de multiball : toutes les billes drainées sont renvoyées en jeu.</summary>
    public void ArmMultiball(float seconds = -1f)
    {
        float d = seconds >= 0f ? seconds : dureeMultiball;
        if (d <= 0f) return;
        finProtectionMultiball = Mathf.Max(finProtectionMultiball, Time.time + d);
    }

    private void Update()
    {
        // Abonnement tardif : GameManager peut naître après nous.
        if (abonne == null && GameManager.Instance != null)
        {
            abonne = GameManager.Instance;
            abonne.StateChanged += OnStateChanged;
        }
    }

    private void OnDestroy()
    {
        if (abonne != null)
        {
            abonne.StateChanged -= OnStateChanged;
        }
    }

    private void OnStateChanged(GameManager.GameState etat)
    {
        if (etat != GameManager.GameState.Playing)
        {
            return;
        }

        // Playing revient aussi après une pause : on ne réarme que si la protection est neuve.
        if (Time.time >= finProtection)
        {
            finProtection = Time.time + duree;
            consomme = false;
        }
    }

    /// <summary>
    /// Tente de sauver la bille qui draine. Renvoie vrai si elle a été replacée au lanceur ;
    /// l'appelant ne doit alors rien décompter.
    /// </summary>
    public bool TrySave(Rigidbody ball)
    {
        BallManager manager = BallManager.Instance;
        GameManager game = GameManager.Instance;

        if (ball == null || !EstActif || manager == null || game == null || game.BallSpawnPoint == null)
        {
            return false;
        }

        if (game.State != GameManager.GameState.Playing)
        {
            return false;
        }

        bool multiballSave = EstActifMultiball && manager.LiveBallCount > 1;
        if (!multiballSave && manager.LiveBallCount > 1)
        {
            return false;
        }

        ball.position = game.BallSpawnPoint.position;
        ball.rotation = game.BallSpawnPoint.rotation;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;

        if (multiballSave)
        {
            // En multiball, on renvoie la bille directement en jeu sans bloquer le joueur au lanceur.
            ball.AddForce(game.BallSpawnPoint.forward * impulsionRenvoiMultiball, ForceMode.Impulse);
            AudioManager.Instance?.Play(PinballSound.ExtraBall);
            game.ShowMessage("MULTIBALL — BILLE SAUVÉE !", 1.5f);
            return true;
        }

        if (uneSeuleFois)
        {
            consomme = true;
        }

        AudioManager.Instance?.Play(PinballSound.ExtraBall);
        game.NotifyBallReturnedToLane();
        game.ShowMessage("BILLE SAUVÉE !", 1.5f);
        return true;
    }
}
