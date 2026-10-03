using UnityEngine;

/// <summary>Un instantané consommé une fois par la table d'arrivée, sans conserver les objets PhysX.</summary>
public static class TableSessionTransfer
{
    public readonly struct Session
    {
        public readonly int Score, Balls, Multiplier;
        public Session(int score, int balls, int multiplier)
        { Score = score; Balls = balls; Multiplier = multiplier; }
    }
    private static string destination;
    private static Session pending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Clear();
    public static void Capture(string scene)
    {
        destination = scene;
        pending = new Session(ScoreManager.Instance != null ? ScoreManager.Instance.Score : 0,
            GameManager.Instance != null ? GameManager.Instance.BallsRemaining : 3,
            ScoreManager.Instance != null ? ScoreManager.Instance.Multiplier : 1);
    }
    public static bool TryConsume(string scene, out Session session)
    {
        session = pending;
        if (destination != scene) return false;
        Clear();
        return true;
    }
    public static void Clear() { destination = null; pending = default; }
}
