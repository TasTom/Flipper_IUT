using UnityEngine;

[CreateAssetMenu(menuName = "Vosges Mania/Industries", fileName = "IndustriesConfig")]
public class IndustriesConfig : ScriptableObject
{
    [Min(1)] public int startingBalls = 3;
    [Min(0)] public int bumperPoints = 100;
    [Min(0)] public int targetPoints = 500;
    [Min(0)] public int lanePoints = 250;
    [Min(0)] public int spinnerPoints = 50;
    [Min(0)] public int slingshotPoints = 50;
    [Min(0)] public int scoopPoints = 1000;
    [Min(0)] public int sixTargetsBonus = 5000;
    [Min(.1f)] public float scoopHoldSeconds = .65f;
    [Min(.1f)] public float targetResetSeconds = 1f;
    [Min(.1f)] public float serveDelay = .3f;
    public float scoopKickAngle = 165f;
    [Min(1)] public float scoopKickSpeed = 12f;
    [Header("Retours sonores")]
    public AudioClip flipperUpSound, flipperDownSound, launchSound, bumperSound, targetSound, scoopSound, bonusSound, drainSound;
    [Range(0f,1f)] public float soundVolume=.5f;
}
