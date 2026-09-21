using TMPro;
using UnityEngine;

/// <summary>
/// Écran de fin de partie (GDD §UI) : score final, record, invitation à rejouer.
///
/// <para>S'abonne à <see cref="GameManager.StateChanged"/> et n'apparaît qu'en
/// <see cref="GameManager.GameState.GameOver"/>. Le panneau est un objet séparé du HUD
/// permanent : masquer un overlay est plus simple que de vider cinq textes.</para>
///
/// <para>Le prompt de rejeu est déjà diffusé par <see cref="GameManager"/> sur le
/// <c>MessageText</c> du HUD (« ENTRÉE POUR REJOUER ») ; le texte du prompt ici est donc
/// facultatif, laissé vide pour éviter un doublon à l'écran.</para>
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    [Header("Panneau")]
    [Tooltip("Panneau à montrer en fin de partie. Désactivé partout ailleurs.")]
    [SerializeField] private GameObject panel;

    [Header("Textes (TMP)")]
    [Tooltip("Score final. Vide = pas d'affichage.")]
    [SerializeField] private TMP_Text scoreText;

    [Tooltip("Record battu ou à battre. Vide = pas d'affichage.")]
    [SerializeField] private TMP_Text highScoreText;

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StateChanged += OnStateChanged;
        }

        // L'écran ne doit pas traîner au démarrage.
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StateChanged -= OnStateChanged;
        }
    }

    private void OnStateChanged(GameManager.GameState state)
    {
        if (panel == null)
        {
            return;
        }

        bool fin = state == GameManager.GameState.GameOver;
        panel.SetActive(fin);

        if (fin)
        {
            Rafraichir();
        }
    }

    private void Rafraichir()
    {
        if (ScoreManager.Instance == null)
        {
            return;
        }

        if (scoreText != null)
        {
            scoreText.text = $"SCORE : {ScoreManager.Instance.Score:N0}";
        }

        if (highScoreText != null)
        {
            highScoreText.text = $"RECORD : {ScoreManager.Instance.HighScore:N0}";
        }
    }
}
