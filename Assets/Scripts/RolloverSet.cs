using UnityEngine;

/// <summary>
/// Un groupe de rollovers : les « top lanes » du haut de table, ou une rangée de voies.
///
/// <para>Le principe est celui d'une vraie table : franchir <b>toutes</b> les voies du groupe
/// donne un bonus, et le groupe se réarme alors pour recommencer. Une voie franchie reste allumée
/// tant que le groupe n'est pas complet — c'est ce que le joueur lit pour savoir où il en est.</para>
///
/// <para><b>Le skill shot.</b> Au lancement, une seule voie est allumée au hasard ; l'y faire
/// passer donne un bonus immédiat, sans attendre les trois. C'est le « tir d'adresse » du début
/// de bille, celui qui récompense un lancement dosé plutôt qu'un lancement à fond.</para>
/// </summary>
public class RolloverSet : MonoBehaviour
{
    [Header("Voies")]
    [Tooltip("Les rollovers du groupe. Laissé vide, ils sont cherchés dans le sous-arbre — c'est " +
             "la disposition normale : les trois voies sont enfants de cet hôte.")]
    [SerializeField] private Rollover[] rollovers;

    [Header("Récompense")]
    [Tooltip("Bonus quand toutes les voies ont été franchies (GDD §Barème : une série complétée " +
             "vaut 15 000).")]
    [SerializeField] private int bonusSerie = 15000;

    [Tooltip("Bonus du skill shot, quand la bille passe dans la voie allumée au lancement.")]
    [SerializeField] private int bonusSkillShot = 25000;

    [Tooltip("Message affiché quand la série est complétée. Vide = aucun.")]
    [SerializeField] private string nom = "VOIES DU HAUT";

    [Header("Récompense de table")]
    [Tooltip("Le kickback à armer quand la série est complétée. C'est ce qui rend le sauvetage " +
             "d'outlane MÉRITÉ : le joueur doit aller chercher les voies du haut pour le gagner.")]
    [SerializeField] private Kickback recompense;

    [Header("Skill shot")]
    [Tooltip("Allumer une voie au hasard au début de chaque bille. C'est le skill shot.")]
    [SerializeField] private bool skillShotActif = true;

    private bool skillShotEnCours;
    private Rollover voieDuSkillShot;
    private int dernieresBilles = -1;

    /// <summary>Nombre de voies franchies dans la série en cours.</summary>
    public int RolledCount
    {
        get
        {
            if (rollovers == null) { return 0; }

            int n = 0;

            foreach (Rollover r in rollovers)
            {
                if (r != null && r.IsRolled) { n++; }
            }

            return n;
        }
    }

    /// <summary>Nombre de voies du groupe.</summary>
    public int Count => rollovers != null ? rollovers.Length : 0;

    private void Awake()
    {
        if (rollovers == null || rollovers.Length == 0)
        {
            rollovers = GetComponentsInChildren<Rollover>(true);
        }

        if (rollovers.Length == 0)
        {
            Debug.LogWarning($"[RolloverSet] '{name}' n'a aucune voie : il ne pourra rien " +
                             "récompenser.", this);
        }
    }

    private void Start()
    {
        // Au `Start` et non dans `Awake` : les rollovers se sont enregistrés avant.
        ArmerSerie();

        if (GameManager.Instance != null)
        {
            dernieresBilles = GameManager.Instance.BallsRemaining;
            GameManager.Instance.BallsChanged += OnBallsChanged;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.BallsChanged -= OnBallsChanged;
        }
    }

    /// <summary>
    /// Une nouvelle bille relance la série — et donc le skill shot.
    ///
    /// <para>⚠ On réagit à une <b>hausse</b> du compteur, pas à tout changement : une bille perdue
    /// le fait baisser, et réarmer à ce moment-là allumerait le skill shot pour une bille qui
    /// n'existe plus.</para>
    ///
    /// <para>Le compteur est aussi le signal d'un redémarrage de partie : il repasse de 0 à
    /// `startingBalls`, ce qui est bien une hausse.</para>
    /// </summary>
    private void OnBallsChanged(int billes)
    {
        bool nouvelleBille = dernieresBilles < 0 || billes > dernieresBilles;

        dernieresBilles = billes;

        if (nouvelleBille) { ArmerSerie(); }
    }

    /// <summary>
    /// Remet la série à zéro et allume la voie du skill shot. Appelé au début d'une bille — c'est
    /// ce qui fait qu'un lancement réussi se paie tout de suite.
    /// </summary>
    public void ArmerSerie()
    {
        if (rollovers == null || rollovers.Length == 0) { return; }

        foreach (Rollover r in rollovers)
        {
            if (r != null) { r.Eteindre(); }
        }

        skillShotEnCours = skillShotActif;
        voieDuSkillShot = null;

        if (skillShotEnCours)
        {
            // Au hasard, et pas toujours la même : sinon le skill shot se joue par cœur et ne
            // récompense plus rien.
            voieDuSkillShot = rollovers[Random.Range(0, rollovers.Length)];

            if (voieDuSkillShot != null) { voieDuSkillShot.Allumer(); }
        }
        else
        {
            // Sans skill shot, toutes les voies sont allumées : le joueur doit les franchir
            // toutes, ce qui est le jeu normal d'une rangée de voies.
            foreach (Rollover r in rollovers)
            {
                if (r != null) { r.Allumer(); }
            }
        }
    }

    /// <summary>Appelé par un rollover quand la bille le franchit.</summary>
    public void NotifyRollover(Rollover voie)
    {
        if (voie == null) { return; }

        // --- le skill shot d'abord -----------------------------------------------------------------
        if (skillShotEnCours && voie == voieDuSkillShot)
        {
            skillShotEnCours = false;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(bonusSkillShot);
                GameManager.Instance.ShowMessage($"SKILL SHOT  +{bonusSkillShot:N0}", 2f);
            }

            // Le reste de la rangée s'allume : le joueur peut enchaîner sur la série complète.
            foreach (Rollover r in rollovers)
            {
                if (r != null && !r.IsRolled) { r.Allumer(); }
            }

            return;
        }

        // --- la série complète ---------------------------------------------------------------------
        if (RolledCount < Count) { return; }

        if (GameManager.Instance != null)
        {
            if (bonusSerie > 0) { GameManager.Instance.AddScore(bonusSerie); }

            if (!string.IsNullOrEmpty(nom))
            {
                GameManager.Instance.ShowMessage($"{nom}  COMPLET  +{bonusSerie:N0}", 2f);
            }
        }

        // La récompense de table : le kickback s'arme. C'est ce qui donne une raison d'aller
        // chercher les voies du haut autrement que pour les points.
        if (recompense != null) { recompense.Armer(); }

        // On réarme : sur une vraie table, une rangée complétée se rallume et peut l'être à
        // nouveau. Sans cela, les voies seraient mortes pour le reste de la partie.
        ArmerSerie();
    }

    /// <summary>Appelé quand une bille est perdue : la série repart à zéro.</summary>
    public void ResetSerie()
    {
        ArmerSerie();
    }
}
