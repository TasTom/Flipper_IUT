using UnityEngine;

public enum ScoreElement
{
    Slingshot, Bumper, SimpleTarget, Matieres, Java, Cafe, Loop, VosgesRamp, IutRamp
}

/// <summary>Données d'équilibrage du GDD §Score, combos, multiplicateur et bonus de bille.</summary>
[CreateAssetMenu(menuName = "Vosges Mania/Score", fileName = "ScoreConfig")]
public class ScoreConfig : ScriptableObject
{
    [Header("Barème de base")]
    [Min(0)] public int slingshot = 100;
    [Min(0)] public int bumper = 1000;
    [Min(0)] public int simpleTarget = 500;
    [Min(0)] public int matieres = 2500;
    [Min(0)] public int java = 2000;
    [Min(0)] public int cafe = 1500;
    [Min(0)] public int loop = 5000;
    [Min(0)] public int vosgesRamp = 7500;
    [Min(0)] public int iutRamp = 10000;

    [Header("Enchaînements — 2, 3, 4 et 5 actions")]
    [Min(.1f)] public float comboWindow = 4f;
    [Min(0)] public int comboOne = 2500;
    [Min(0)] public int comboTwo = 5000;
    [Min(0)] public int comboThree = 10000;
    [Min(0)] public int superCombo = 25000;
    public AudioClip comboSound;
    [Range(0f, 1f)] public float comboVolume = .85f;
    [Range(.25f, 3f)] public float comboPitch = 1f;
    [Range(0f, .5f)] public float comboPitchStep = .08f;

    [Header("Multiplicateur")]
    [Min(1)] public int maximumMultiplier = 5;
    [Min(1)] public int seriesMultiplier = 2;
    [Min(1)] public int missionsForMultiplier = 2;
    [Min(1)] public int missionMultiplier = 3;
    [Min(1)] public int bossMultiplier = 4;
    [Min(1)] public int multiballMultiplier = 5;
    [Min(0)] public int multiballActivationBonus = 75000;
    [Min(1)] public int rampsForIncrease = 3;
    [Range(2, 5)] public int comboActionsForIncrease = 4;
    [Min(1)] public int decayFromMultiplier = 3;

    [Header("Bonus de bille — ajouté directement")]
    [Min(0)] public int validatedCourseBonus = 5000;
    [Min(0)] public int successfulRampBonus = 2000;
    [Min(0)] public int maximumComboBonus = 1000;
    public bool forfeitBonusOnTilt = true;

    public int Points(ScoreElement element)
    {
        int value = element switch
        {
            ScoreElement.Slingshot => slingshot, ScoreElement.Bumper => bumper,
            ScoreElement.Matieres => matieres, ScoreElement.Java => java,
            ScoreElement.Cafe => cafe, ScoreElement.Loop => loop,
            ScoreElement.VosgesRamp => vosgesRamp, ScoreElement.IutRamp => iutRamp,
            _ => simpleTarget
        };
        return Mathf.Max(0, value);
    }

    public int ComboBonus(int level) => Mathf.Max(0, level switch
    {
        1 => comboOne, 2 => comboTwo, 3 => comboThree, 4 => superCombo, _ => 0
    });
}
