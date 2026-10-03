using UnityEngine;

/// <summary>GDD §Direction sonore : pause, fin de partie et récompenses. References cached once.</summary>
[DisallowMultipleComponent]
public class PinballGameAudio : MonoBehaviour
{
    [SerializeField] private GameManager game;
    [SerializeField] private ScoreManager score;
    [SerializeField] private AudioManager audioManager;
    private int multiplier;
    private bool bound;

    private void Awake()
    {
        if (game == null) game = FindAnyObjectByType<GameManager>();
        if (score == null) score = FindAnyObjectByType<ScoreManager>();
        if (audioManager == null) audioManager = GetComponent<AudioManager>();
        if (audioManager == null) audioManager = FindAnyObjectByType<AudioManager>();
    }
    private void Start() => Bind();
    private void OnEnable() { if (bound) Bind(); }
    private void Bind()
    {
        Unbind();
        if (game != null)
        {
            game.StateChanged += StateChanged;
            game.ExtraBallAwarded += ExtraBall;
        }
        if (score != null)
        {
            multiplier = score.Multiplier;
            score.MultiplierChanged += MultiplierChanged;
            score.HighScoreChanged += HighScoreChanged;
        }
        bound = true;
        if (audioManager != null) audioManager.SetPaused(game != null && game.State == GameManager.GameState.Paused);
    }
    private void OnDisable() => Unbind();
    private void Unbind()
    {
        if (game != null) { game.StateChanged -= StateChanged; game.ExtraBallAwarded -= ExtraBall; }
        if (score != null) { score.MultiplierChanged -= MultiplierChanged; score.HighScoreChanged -= HighScoreChanged; }
    }
    private void StateChanged(GameManager.GameState state)
    {
        if (audioManager == null) return;
        audioManager.SetPaused(state == GameManager.GameState.Paused);
        if (state == GameManager.GameState.GameOver) audioManager.Play(PinballSound.GameOver);
    }
    private void ExtraBall()
    {
        if (audioManager != null) audioManager.Play(PinballSound.ExtraBall);
    }
    private void MultiplierChanged(int value)
    {
        if (value > multiplier && audioManager != null) audioManager.Play(PinballSound.MultiplierRaised);
        multiplier = value;
    }
    private void HighScoreChanged(int value)
    {
        if (game != null && game.State == GameManager.GameState.GameOver && audioManager != null)
            audioManager.Play(PinballSound.HighScore);
    }
}
