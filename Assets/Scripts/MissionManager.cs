using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance;

    /// <summary>Progression des matières : (validées, total).
    ///
    /// <para>Émis à chaque validation et à chaque remise à zéro. Un affichage s'y abonne au lieu
    /// d'écrire dans <see cref="missionText"/> — sinon un seul écran pourrait montrer les missions,
    /// et ce script devrait connaître l'existence de chacun d'eux.</para>
    ///
    /// <para>Le champ <see cref="missionText"/> reste alimenté : il évite de casser le HUD existant,
    /// mais c'est désormais un abonné parmi d'autres.</para>
    /// </summary>
    public event Action<int, int> ProgressChanged;

    /// <summary>Message ponctuel à afficher (Nuit de l'Info…). Vide quand il n'y en a pas.</summary>
    public event Action<string> Announced;

    [SerializeField] private TMP_Text missionText;
    [SerializeField] private int missionBonusScore = 500;

    /// <summary>Nombre de matières validées.</summary>
    public int CompletedCount => completedSubjects.Count;

    /// <summary>Nombre de matières à valider (5 — voir la divergence GDD/code dans CLAUDE.md).</summary>
    public int SubjectCount => allSubjects.Length;

    /// <summary>Indique si le bonus Nuit de l'Info est actuellement actif.</summary>
    public bool IsNuitDeLInfoActive => nuitDeLInfoTriggered;

    private readonly HashSet<string> completedSubjects = new HashSet<string>();
    private readonly string[] allSubjects =
    {
        "Programmation",
        "Reseau",
        "Web",
        "BaseDeDonnees",
        "Projet"
    };

    private bool nuitDeLInfoTriggered;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (missionText == null)
        {
            GameObject missionObject = GameObject.Find("MissionText");
            if (missionObject != null)
            {
                missionText = missionObject.GetComponent<TMP_Text>();
            }
        }
    }

    private void Start()
    {
        UpdateMissionUI();
    }

    public void CompleteSubject(string subjectId)
    {
        if (completedSubjects.Contains(subjectId))
        {
            return;
        }

        completedSubjects.Add(subjectId);
        GameManager.Instance.AddScore(missionBonusScore);
        UpdateMissionUI();

        if (completedSubjects.Count >= allSubjects.Length && !nuitDeLInfoTriggered)
        {
            TriggerNuitDeLInfo();
        }
    }

    private void TriggerNuitDeLInfo()
    {
        nuitDeLInfoTriggered = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(2000);
        }

        // L'annonce passe par l'evenement, comme la progression : l'affichage qui la montre
        // choisit son texte, ce script ne connait aucun ecran.
        Announced?.Invoke("NUIT DE L'INFO");

        if (missionText != null)
        {
            missionText.text = "NUIT DE L'INFO DECLENCHEE !";
        }

        if (MultiballManager.Instance != null)
        {
            MultiballManager.Instance.TriggerMultiball();
        }
        else
        {
            Debug.LogWarning("[MissionManager] MultiballManager.Instance manquant.");
        }
    }

    private void UpdateMissionUI()
    {
        ProgressChanged?.Invoke(completedSubjects.Count, allSubjects.Length);

        if (missionText == null)
        {
            return;
        }

        missionText.text = $"MATIERES VALIDEES : {completedSubjects.Count} / {allSubjects.Length}";
    }

    public void ResetMissions()
    {
        completedSubjects.Clear();
        nuitDeLInfoTriggered = false;
        UpdateMissionUI();
    }
}