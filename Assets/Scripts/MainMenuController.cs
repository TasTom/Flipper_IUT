using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("Scène de jeu")]
    [Tooltip("Nom exact de la scène du premier niveau.")]
    [SerializeField] private string firstLevelScene = "Neutral";

    [Header("Affichage Record")]
    [SerializeField] private TMP_Text highScoreText;

    [Header("Panneau d'aide / Contrôles")]
    [SerializeField] private GameObject controlsPanel;

    private const string HighScoreKey = "VosgesTilt_HighScore";

    private void Start()
    {
        // Rétablit le temps normal au cas où une partie précédente aurait quitté en pause
        Time.timeScale = 1f;

        // Affiche le meilleur score enregistré sur la machine
        int highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        if (highScoreText != null)
        {
            highScoreText.text = $"MEILLEUR SCORE : {highScore:N0} PTS";
        }

        if (controlsPanel != null)
        {
            controlsPanel.SetActive(false);
        }
    }

    /// <summary>Lance la table principale.</summary>
    public void PlayGame()
    {
        Time.timeScale = 1f;

        // Utilise la transition animée du projet si disponible, sinon LoadScene classique
        if (SceneTransition.CanLoad(firstLevelScene))
        {
            SceneTransition.Play(firstLevelScene, "VOSGES MANIA", "Ligne 1 — Campus & Forêt");
        }
        else
        {
            SceneManager.LoadScene(firstLevelScene);
        }
    }

    /// <summary>Bascule l'affichage du volet d'aide/contrôles.</summary>
    public void ToggleControlsPanel()
    {
        if (controlsPanel != null)
        {
            controlsPanel.SetActive(!controlsPanel.activeSelf);
        }
    }

    /// <summary>Quitte le jeu ou le mode Play dans l'éditeur.</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}