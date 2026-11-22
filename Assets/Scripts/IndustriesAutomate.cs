public enum IndustriesAutomateHit { Ignored, Damage, Defeated }

/// <summary>Boss de la table Industries, inspiré du mode Boss en trois touches du GDD.</summary>
public sealed class IndustriesAutomate
{
    public const int RequiredTargets = 3;
    private readonly float cooldown;
    private float nextHit;
    private int qualifiedTargets;
    public int RequiredHits { get; }
    public int Hits { get; private set; }
    public int QualificationHits { get; private set; }
    public bool IsActive { get; private set; }

    public IndustriesAutomate(int requiredHits, float cooldown)
    {
        RequiredHits = System.Math.Max(1, requiredHits);
        this.cooldown = System.Math.Max(0f, cooldown);
    }

    public bool QualifyTarget(int index)
    {
        if (IsActive || index < 0 || index >= 6) return false;
        int target = 1 << index;
        if ((qualifiedTargets & target) != 0) return false;
        qualifiedTargets |= target;
        if (++QualificationHits < RequiredTargets) return false;
        Hits = 0;
        nextHit = float.NegativeInfinity;
        IsActive = true;
        return true;
    }

    public IndustriesAutomateHit Hit(float now)
    {
        if (!IsActive || now < nextHit) return IndustriesAutomateHit.Ignored;
        nextHit = now + cooldown;
        if (++Hits < RequiredHits) return IndustriesAutomateHit.Damage;
        IsActive = false;
        ClearQualification();
        return IndustriesAutomateHit.Defeated;
    }

    public void Reset()
    {
        Hits = 0;
        nextHit = float.NegativeInfinity;
        IsActive = false;
        ClearQualification();
    }

    private void ClearQualification()
    {
        qualifiedTargets = 0;
        QualificationHits = 0;
    }
}
