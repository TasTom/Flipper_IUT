using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance;

    [SerializeField] private TMP_Text missionText;
    [SerializeField] private int missionBonusScore = 500;

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