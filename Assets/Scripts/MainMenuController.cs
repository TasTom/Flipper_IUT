using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("Scène de jeu")]
    [Tooltip("Nom exact de la scène du premier niveau.")]
    [SerializeField] private string firstLevelScene = "VosgesMania";

    [Header("Affichage Record")]
    [SerializeField] private TMP_Text highScoreText;

    [Header("Panneau d'aide / Contrôles")]
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private PauseMenu options;
    [SerializeField] private OnlineScoresPanel ranking;
    [SerializeField] private RectTransform menuLayout;
    [SerializeField] private RectTransform background;
    [SerializeField] private GameObject startButton;
    private Vector2 lastSize;

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
        FitMenu();
        if (startButton != null)
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(startButton);
    }

    private void LateUpdate() => FitMenu();

    private void FitMenu()
    {
        if (menuLayout == null || !(menuLayout.parent is RectTransform canvas)) return;
        Vector2 size = canvas.rect.size;
        if (size == lastSize) return;
        lastSize = size;
        bool landscape = size.x > size.y;
        menuLayout.localRotation = landscape ? Quaternion.Euler(0f, 0f, -90f) : Quaternion.identity;
        if (background == null) return;
        background.localRotation = menuLayout.localRotation;
        // L'illustration d'origine est en portrait et couvre tout l'écran.
        Vector2 rotatedSize = landscape ? new Vector2(size.y, size.x) : size;
        float aspect = 1536f / 2752f;
        float width = Mathf.Max(rotatedSize.x, rotatedSize.y * aspect);
        background.sizeDelta = new Vector2(width, width / aspect);
    }

    /// <summary>Lance la table principale.</summary>
    public void PlayGame()
    {
        if (SceneTransition.IsPlaying) return;
        Time.timeScale = 1f;
        TableSessionTransfer.Clear();

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

    /// <summary>Ouvre les mêmes réglages que le menu de pause.</summary>
    public void ToggleControlsPanel()
    {
        if (!SceneTransition.IsPlaying && options != null && (ranking == null || !ranking.IsOpen))
        {
            options.ShowOptions(MenuRotation);
        }
    }

    public void ShowScores()
    {
        if (!SceneTransition.IsPlaying) ranking?.ShowRanking(MenuRotation);
    }

    private Quaternion MenuRotation => menuLayout != null ? menuLayout.localRotation : Quaternion.identity;

    /// <summary>Quitte le jeu ou le mode Play dans l'éditeur.</summary>
    public void QuitGame() => ExitApplication();

    internal static void ExitApplication()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
