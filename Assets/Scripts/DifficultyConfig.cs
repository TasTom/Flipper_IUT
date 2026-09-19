using UnityEngine;

public enum DifficultyMode
{
    Easy,
    Normal,
    Hard
}

[CreateAssetMenu(fileName = "DifficultyConfig", menuName = "Flipper/Difficulty Config")]
public sealed class DifficultyConfig : ScriptableObject
{
    [Header("Mode")]
    public DifficultyMode mode = DifficultyMode.Normal;

    [Header("Bille")]
    [Min(0f)] public float ballMaxSpeed = 170f;

    [Header("Bumpers")]
    [Min(0f)] public float bumperBounceForce = 125f;

    [Header("Flippers")]
    [Min(0.1f)] public float flipperBatScale = 1f;

    [Header("Drain et sauvetage")]
    [Min(0.1f)] public float drainWidthMultiplier = 1f;
    public bool lateralRescueEnabled;
    [Min(0f)] public float lateralRescueForce = 35f;

    [Header("Tilt")]
    [Range(0f, 1f)] public float tiltTolerance = 0.5f;

    public static DifficultyConfig CreateDefault(DifficultyMode difficultyMode)
    {
        DifficultyConfig config = CreateInstance<DifficultyConfig>();
        config.mode = difficultyMode;

        switch (difficultyMode)
        {
            case DifficultyMode.Easy:
                config.ballMaxSpeed = 135f;
                config.bumperBounceForce = 90f;
                config.flipperBatScale = 1f;
                config.drainWidthMultiplier = 0.7f;
                config.lateralRescueEnabled = true;
                config.lateralRescueForce = 45f;
                config.tiltTolerance = 0.9f;
                break;
            case DifficultyMode.Hard:
                config.ballMaxSpeed = 210f;
                config.bumperBounceForce = 165f;
                config.flipperBatScale = 0.75f;
                config.drainWidthMultiplier = 1.35f;
                config.lateralRescueEnabled = false;
                config.lateralRescueForce = 0f;
                config.tiltTolerance = 0.15f;
                break;
            default:
                config.ballMaxSpeed = 170f;
                config.bumperBounceForce = 125f;
                config.flipperBatScale = 1f;
                config.drainWidthMultiplier = 1f;
                config.lateralRescueEnabled = false;
                config.lateralRescueForce = 35f;
                config.tiltTolerance = 0.5f;
                break;
        }

        return config;
    }
}
