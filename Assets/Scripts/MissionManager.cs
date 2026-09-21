using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>Ce qu'une mission demande au joueur (GDD §Missions et événements).</summary>
public enum MissionGoal
{
    /// <summary>Aucun but : la mission ne peut pas se terminer. Réservé au jalon qui attend une
    /// pièce (le boss).</summary>
    Aucun,

    /// <summary>Réussir une rampe donnée.</summary>
    Rampe,

    /// <summary>Valider toutes les cibles d'un groupe.</summary>
    Groupe,

    /// <summary>Réussir le loop @ un certain nombre de fois.</summary>
    LoopAt,

    /// <summary>Objectif du boss (GDD §Boss : « Projet Final / Bug Critique »).</summary>
    Boss
}

/// <summary>
/// Les missions (GDD §Missions et événements) : sept étapes qui suivent le thème d'un semestre à
/// l'IUT, une seule active et affichée à la fois.
///
/// <para><b>Une cible ne compte que si elle est allumée.</b> Le GDD décrit un cycle
/// « éteinte → active → validée » : les cibles d'un groupe s'allument quand la mission qui les
/// concerne devient active, et une cible éteinte ne fait pas avancer la mission. C'est ce qui
/// donne au joueur une direction lisible — il voit ce qu'il doit viser — et c'est aussi ce qui
/// évite qu'une mission se termine toute seule parce que le joueur avait touché ses cibles
/// d'avance, pendant une autre mission.</para>
///
/// <para><b>Les cibles marquent toujours des points</b>, allumées ou non : « les missions ne
/// doivent pas nécessairement bloquer les autres systèmes » (GDD). Seule la <i>progression</i>
/// est conditionnée.</para>
///
/// <para><b>Les missions sont séquentielles.</b> Le GDD demande qu'une seule mission principale
/// soit affichée « afin de préserver la lisibilité » ; l'ordre du tableau §Missions est donc
/// l'ordre de jeu, et la mission suivante s'active quand la précédente se termine.</para>
/// </summary>
public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance;

    /// <summary>Une étape du tableau §Missions du GDD.</summary>
    [Serializable]
    public class Mission
    {
        [Tooltip("Nom affiché au joueur.")]
        public string nom;

        [Tooltip("Objectif, tel qu'affiché au joueur.")]
        public string objectif;

        [Tooltip("Ce que la mission demande.")]
        public MissionGoal but = MissionGoal.Groupe;

        [Tooltip("Pour Goal.Groupe : l'identifiant du groupe de cibles. " +
                 "Pour Goal.Rampe : le nom de l'hôte de la rampe.")]
        public string cible;

        [Tooltip("Nombre demandé : cibles du groupe, ou tours de loop. " +
                 "0 = TOUTES les cibles du groupe, comptées dans la scène — c'est le réglage à " +
                 "préférer, il ne peut pas devenir impossible.")]
        public int quantite = 1;

        [Tooltip("Points versés à la réussite (GDD §Missions).")]
        public int recompense;

        [Tooltip("Multiplicateur posé à la réussite. 0 = aucun (GDD : la mission 2 donne x2).")]
        public int multiplicateur;

        [Tooltip("Réussir cette mission débloque le boss (GDD §Boss, étape « Initialisation » : " +
                 "« Débloquer les cibles Projet → Le boss devient visible et lumineux »).")]
        public bool debloqueLeBoss;
    }

    /// <summary>
    /// La mission active a changé. Sans argument : l'affichage interroge les propriétés, plutôt
    /// que de recevoir une copie de l'état — il y a cinq choses à lire, et un événement à cinq
    /// paramètres serait pénible à faire évoluer.
    /// </summary>
    public event Action MissionChanged;

    /// <summary>Avancement de la mission active : (fait, demandé).</summary>
    public event Action<int, int> ProgressChanged;

    /// <summary>Message ponctuel à afficher (« NUIT DE L'INFO »…).</summary>
    public event Action<string> Announced;

    [Header("Mission en cours")]
    [SerializeField] private TMP_Text missionText;

    [Tooltip("Les sept missions du GDD §Missions. Laissé vide, le tableau est rempli par le code " +
             "— c'est un repli pour une scène construite à la main, pas le chemin normal.")]
    [SerializeField] private Mission[] missions;

    [Tooltip("Score du déclenchement de la Nuit de l'Info.")]
    [SerializeField] private int nuitDeLInfoScore = 2000;

    private int activeIndex = -1;
    private int progress;
    private bool nuitDeLInfoTriggered;

    /// <summary>Index de la mission active, -1 si la partie n'a pas commencé.</summary>
    public int ActiveIndex => activeIndex;

    /// <summary>La mission active, ou <c>null</c>.</summary>
    public Mission Active => missions != null && activeIndex >= 0 && activeIndex < missions.Length
                             ? missions[activeIndex]
                             : null;

    /// <summary>Nom de la mission active. Vide s'il n'y en a pas.</summary>
    public string ActiveName => Active != null ? Active.nom : string.Empty;

    /// <summary>Objectif affiché de la mission active. Vide s'il n'y en a pas.</summary>
    public string ActiveObjective => Active != null ? Active.objectif : string.Empty;

    /// <summary>Avancement de la mission active.</summary>
    public int ActiveProgress => progress;

    /// <summary>Ce que la mission active demande.</summary>
    ///
    /// <para>⚠ Pour une mission de <b>groupe</b>, <c>quantite = 0</c> veut dire « toutes les
    /// cibles du groupe », et le compte est fait <b>dans la scène</b>. C'est délibéré : une
    /// quantité écrite en dur devient impossible dès qu'on retire une cible — ce qui est arrivé
    /// quand la table est passée de seize cibles à six, et la mission 4 n'avait plus aucune cible
    /// Java à désigner. Compter sur place rend le réglage insensible à la scène.</para>
    /// </summary>
    public int ActiveRequired
    {
        get
        {
            Mission m = Active;

            if (m == null)
            {
                return 1;
            }

            if (m.but == MissionGoal.Groupe && m.quantite <= 0)
            {
                return Mathf.Max(1, CompterCibles(m.cible));
            }

            return Mathf.Max(1, m.quantite);
        }
    }

    /// <summary>Nombre de missions du tableau.</summary>
    public int MissionCount => missions != null ? missions.Length : 0;

    /// <summary>Nombre de missions terminées.</summary>
    public int CompletedCount => Mathf.Clamp(activeIndex, 0, MissionCount);

    /// <summary>
    /// Nombre de missions. Conservé pour les affichages qui comptaient les matières : la
    /// propriété garde son nom et sa place, elle compte désormais les missions.
    /// </summary>
    public int SubjectCount => MissionCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (missions == null || missions.Length == 0)
        {
            missions = ValeursParDefaut();
        }

        if (missionText == null)
        {
            GameObject missionObject = GameObject.Find("MissionText");
            if (missionObject != null)
            {
                missionText = missionObject.GetComponent<TMP_Text>();
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        // Au `Start`, tous les `Awake` sont passés : les cibles existent et peuvent être
        // allumées. Le faire dans `Awake` allumerait un groupe dont les cibles ne sont pas
        // encore prêtes — même piège d'ordre que le fronton.
        ActivateMission(0);
    }

    /// <summary>
    /// Le tableau §Missions du GDD, en dur. Sert de repli quand la scène n'en porte pas — ce qui
    /// est le cas tant que les ScriptableObjects `*Config` ne sont pas écrits.
    /// </summary>
    private static Mission[] ValeursParDefaut()
    {
        return new[]
        {
            new Mission
            {
                nom = "DÉCOUVERTE DU CAMPUS",
                objectif = "RÉUSSIR LA RAMPE IUT",
                but = MissionGoal.Rampe,
                cible = "Ramp_IUT_In",
                quantite = 1,
                recompense = 10000
            },
            new Mission
            {
                nom = "VALIDER LES COURS",
                objectif = "TOUCHER LES CIBLES MATIÈRES",
                but = MissionGoal.Groupe,
                cible = "Matieres",
                quantite = 0,          // 0 = toutes les cibles du groupe, comptées dans la scène
                recompense = 25000,
                multiplicateur = 2
            },
            new Mission
            {
                nom = "PAUSE CAFÉ",
                objectif = "TOUCHER LES CIBLES CAFÉ",
                but = MissionGoal.Groupe,
                cible = "Cafe",
                quantite = 0,
                recompense = 15000
            },
            new Mission
            {
                nom = "JAVA MAÎTRISÉ",
                objectif = "TOUCHER LES CIBLES JAVA",
                but = MissionGoal.Groupe,
                cible = "Java",
                quantite = 0,
                recompense = 25000
            },
            new Mission
            {
                nom = "RÉSEAU CONNECTÉ",
                objectif = "RÉUSSIR LE LOOP @ 3 FOIS",
                but = MissionGoal.LoopAt,
                cible = "Loop_At_Entry",
                quantite = 3,
                recompense = 35000
            },
            new Mission
            {
                nom = "PROJET FINAL",
                objectif = "TOUCHER LES CIBLES PROJET",
                but = MissionGoal.Groupe,
                cible = "Projet",
                quantite = 0,
                recompense = 50000,
                debloqueLeBoss = true
            },
            new Mission
            {
                nom = "NUIT DE L'INFO",
                objectif = "RÉUSSIR L'OBJECTIF DU BOSS",
                but = MissionGoal.Boss,
                cible = "Boss_ProjetFinal",
                quantite = 1,
                recompense = 0
            }
        };
    }

    // --- activation --------------------------------------------------------------------------

    /// <summary>
    /// Active la mission d'index donné : allume les cibles qu'elle concerne et remet son compteur
    /// à zéro. La précédente est éteinte — deux groupes allumés à la fois rendraient la table
    /// illisible, ce que le GDD veut précisément éviter.
    /// </summary>
    private void ActivateMission(int index)
    {
        if (missions == null || index < 0 || index >= missions.Length)
        {
            activeIndex = missions != null ? missions.Length : 0;
            EteindreTout();
            RaiseChanged();
            return;
        }

        EteindreTout();

        activeIndex = index;
        progress = 0;

        Mission m = missions[index];

        switch (m.but)
        {
            case MissionGoal.Groupe:
                AllumerGroupe(m.cible);
                break;

            case MissionGoal.Aucun:
                // Rien à allumer : le but dépend d'une pièce absente. On le dit, sinon la mission
                // paraîtrait simplement bloquée sans raison.
                Debug.LogWarning($"[MissionManager] La mission « {m.nom} » n'a pas de but " +
                                 "réalisable : le jeu s'arrête à l'étape " + (index + 1) +
                                 ". C'est attendu tant que la pièce qui la déclenche n'existe pas.", this);
                break;
        }

        RaiseChanged();
    }

    private void RaiseChanged()
    {
        MissionChanged?.Invoke();
        ProgressChanged?.Invoke(progress, ActiveRequired);
        UpdateMissionUI();
    }

    /// <summary>Allume les cibles d'un groupe. Elles seules feront avancer la mission.</summary>
    private void AllumerGroupe(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return;
        }

        int allumees = 0;

        foreach (SubjectTarget t in TrouverCibles())
        {
            if (t.GroupId != groupId) { continue; }

            t.Activer();
            allumees++;
        }

        if (allumees == 0)
        {
            // Le cas typique d'une mission dont les cibles ne sont pas encore posées : le dire
            // évite de chercher pourquoi la mission ne progresse pas.
            Debug.LogWarning($"[MissionManager] Aucune cible du groupe '{groupId}' dans la scène : " +
                             "la mission ne pourra pas avancer.", this);
        }
        else if (allumees < ActiveRequired)
        {
            Debug.LogWarning($"[MissionManager] Le groupe '{groupId}' ne compte que {allumees} " +
                             $"cible(s) pour {ActiveRequired} demandée(s) : la mission ne pourra " +
                             "pas se terminer en l'état.", this);
        }
    }

    /// <summary>Éteint toutes les cibles de la table, validées comprises.</summary>
    private void EteindreTout()
    {
        foreach (SubjectTarget t in TrouverCibles())
        {
            t.Eteindre();
        }
    }

    /// <summary>
    /// Les cibles de la table. Cherchées à chaque activation plutôt que tenues dans une liste
    /// alimentée par les cibles elles-mêmes : l'ordre d'exécution d'Unity rend un enregistrement
    /// depuis un `Awake` de cible fragile, et l'activation n'a lieu que sept fois par partie.
    /// </summary>
    private static SubjectTarget[] TrouverCibles()
    {
        return FindObjectsByType<SubjectTarget>(FindObjectsInactive.Exclude);
    }

    // --- entrées des éléments de table -------------------------------------------------------

    /// <summary>
    /// Une cible vient d'être touchée. Appelé par <see cref="SubjectTarget"/> après avoir marqué
    /// ses points : la progression ne compte que si la cible était allumée.
    /// </summary>
    public void NotifyTargetHit(SubjectTarget cible)
    {
        if (cible == null || Active == null)
        {
            return;
        }

        if (Active.but != MissionGoal.Groupe || Active.cible != cible.GroupId)
        {
            return;
        }

        progress = CompterValidees(Active.cible);

        ProgressChanged?.Invoke(progress, ActiveRequired);
        UpdateMissionUI();

        if (progress >= ActiveRequired)
        {
            CompleteActiveMission();
        }
    }

    /// <summary>
    /// Une rampe vient d'être réussie. <paramref name="rampName"/> est le nom de son <b>entrée</b>
    /// — c'est l'entrée qui porte le nom de la rampe dans la scène.
    /// </summary>
    public void NotifyRampCompleted(string rampName)
    {
        if (Active == null || Active.but != MissionGoal.Rampe)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(Active.cible) && Active.cible != rampName)
        {
            return;
        }

        progress = 1;

        ProgressChanged?.Invoke(progress, ActiveRequired);
        UpdateMissionUI();

        CompleteActiveMission();
    }

    /// <summary>
    /// Un tour de loop @ vient d'être réussi. <paramref name="totalCompletions"/> est le total
    /// depuis le début de la partie.
    ///
    /// <para>⚠ Le compteur de la mission n'est pas ce total. Une mission qui démarre après deux
    /// tours doit repartir de zéro, sinon elle se terminerait sur le premier tour qui suit. Le
    /// total est reçu pour l'affichage, pas pour la progression.</para>
    /// </summary>
    public void NotifyLoopCompleted(int totalCompletions)
    {
        if (Active == null || Active.but != MissionGoal.LoopAt)
        {
            return;
        }

        progress = Mathf.Min(progress + 1, ActiveRequired);

        ProgressChanged?.Invoke(progress, ActiveRequired);
        UpdateMissionUI();

        if (progress >= ActiveRequired)
        {
            CompleteActiveMission();
        }
    }

    /// <summary>
    /// Le boss est vaincu. Point d'entrée prévu pour le futur <c>BossTarget</c> (GDD §Boss) : le
    /// boss n'existe pas encore, mais la mission 7 est déjà câblée dessus.
    /// </summary>
    public void NotifyBossDefeated()
    {
        if (Active == null || Active.but != MissionGoal.Boss)
        {
            return;
        }

        progress = 1;

        ProgressChanged?.Invoke(progress, ActiveRequired);
        UpdateMissionUI();

        CompleteActiveMission();
    }

    // --- complétion --------------------------------------------------------------------------

    private void CompleteActiveMission()
    {
        Mission m = Active;

        if (m == null)
        {
            return;
        }

        if (GameManager.Instance != null)
        {
            if (m.recompense > 0)
            {
                GameManager.Instance.AddScore(m.recompense);
            }

            GameManager.Instance.ShowMessage($"{m.nom} — RÉUSSIE", 2f);
        }

        // ⚠ Le multiplicateur est posé APRÈS le score de la mission. Dans l'autre ordre, la
        // récompense s'appliquerait à elle-même et la mission 2 verserait 50 000 au lieu de
        // 25 000 — le barème du GDD dit 25 000.
        if (m.multiplicateur > 1 && ScoreManager.Instance != null)
        {
            ScoreManager.Instance.SetMultiplier(m.multiplicateur);
        }

        // GDD §Boss : c'est la validation des cibles Projet qui débloque le boss.
        if (m.debloqueLeBoss && BossTarget.Instance != null)
        {
            BossTarget.Instance.Activer();
        }

        int suivante = activeIndex + 1;

        if (suivante >= MissionCount)
        {
            // Le tableau est fini : la Nuit de l'Info est la dernière étape du GDD, et elle
            // débouche sur le multiball.
            DeclencherNuitDeLInfo();
            return;
        }

        ActivateMission(suivante);
    }

    private void DeclencherNuitDeLInfo()
    {
        if (nuitDeLInfoTriggered)
        {
            return;
        }

        nuitDeLInfoTriggered = true;

        if (GameManager.Instance != null)
        {
            if (nuitDeLInfoScore > 0)
            {
                GameManager.Instance.AddScore(nuitDeLInfoScore);
            }

            GameManager.Instance.ShowMessage("NUIT DE L'INFO", 3f);
        }

        // L'annonce passe par l'événement, comme la progression : l'affichage qui la montre
        // choisit son texte, ce script ne connaît aucun écran.
        Announced?.Invoke("NUIT DE L'INFO");

        if (MultiballManager.Instance != null)
        {
            MultiballManager.Instance.TriggerMultiball();
        }
        else
        {
            Debug.LogWarning("[MissionManager] MultiballManager.Instance manquant : la Nuit de " +
                             "l'Info est déclenchée mais aucun multiball ne suivra.", this);
        }

        RaiseChanged();
    }

    /// <summary>Compte les cibles d'un groupe présentes dans la scène.</summary>
    private static int CompterCibles(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return 0;
        }

        int n = 0;

        foreach (SubjectTarget t in TrouverCibles())
        {
            if (t.GroupId == groupId)
            {
                n++;
            }
        }

        return n;
    }

    /// <summary>Compte les cibles validées d'un groupe.</summary>
    private static int CompterValidees(string groupId)
    {
        int n = 0;

        foreach (SubjectTarget t in TrouverCibles())
        {
            if (t.GroupId == groupId && t.IsValidated)
            {
                n++;
            }
        }

        return n;
    }

    private void UpdateMissionUI()
    {
        if (missionText == null)
        {
            return;
        }

        Mission m = Active;

        if (m == null)
        {
            missionText.text = nuitDeLInfoTriggered
                ? "NUIT DE L'INFO DÉCLENCHÉE !"
                : "MISSIONS TERMINÉES";
            return;
        }

        missionText.text = $"{m.nom}  {progress} / {ActiveRequired}";
    }

    /// <summary>
    /// Remet les missions à zéro pour une nouvelle partie : la première reprend, et toutes les
    /// cibles sont éteintes puis rallumées par son activation.
    /// </summary>
    public void ResetMissions()
    {
        nuitDeLInfoTriggered = false;
        ActivateMission(0);
    }
}