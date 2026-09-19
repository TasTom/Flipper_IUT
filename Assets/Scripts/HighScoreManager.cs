using System;
using UnityEngine;

/// <summary>
/// Gestionnaire du meilleur score local avec PlayerPrefs.
/// </summary>
public sealed class HighScoreManager : MonoBehaviour
{
    private const string DefaultKey = "VosgesTilt_HighScore";

    [SerializeField] private string playerPrefsKey = DefaultKey;

    public static HighScoreManager Instance { get; private set; }

    public int HighScore { get; private set; }

    public event Action<int> HighScoreChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[HighScoreManager] Un second gestionnaire existe sur '{name}' : il est ignore.", this);
            enabled = false;
            return;
        }

        Instance = this;
        Load();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Load()
    {
        HighScore = PlayerPrefs.GetInt(GetKey(), 0);
        HighScoreChanged?.Invoke(HighScore);
    }

    public bool SaveIfHigher(int score)
    {
        if (score <= HighScore)
        {
            return false;
        }

        HighScore = score;
        PlayerPrefs.SetInt(GetKey(), HighScore);
        PlayerPrefs.Save();
        HighScoreChanged?.Invoke(HighScore);
        return true;
    }

    public void ResetHighScore()
    {
        HighScore = 0;
        PlayerPrefs.DeleteKey(GetKey());
        PlayerPrefs.Save();
        HighScoreChanged?.Invoke(HighScore);
    }

    private string GetKey()
    {
        return string.IsNullOrWhiteSpace(playerPrefsKey) ? DefaultKey : playerPrefsKey;
    }
}
