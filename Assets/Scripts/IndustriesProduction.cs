using System.Collections.Generic;

/// <summary>
/// Deux commandes indépendantes : trois cibles chargent une matière, un passage
/// entrée/sortie de sa rampe la transforme, le scoop livre les produits terminés.
/// Extension Industries des cibles, rampes, combos et score décrits dans le GDD.
/// Le temps est fourni par le moteur : les délais s'arrêtent pendant la pause.
/// </summary>
public sealed class IndustriesProduction
{
    public const int Wood = 0, Textile = 1;
    public const string WoodEntry = "ProductionWoodEntry", WoodExit = "ProductionWoodExit";
    public const string TextileEntry = "ProductionTextileEntry", TextileExit = "ProductionTextileExit";
    private readonly IndustriesConfig config;
    private readonly Dictionary<(int line, int ball), float> entries = new Dictionary<(int, int), float>();
    public int Targets { get; private set; }
    public int Loaded { get; private set; }
    public int Processed { get; private set; }

    public IndustriesProduction(IndustriesConfig config) => this.config = config;

    public bool HitTarget(int index)
    {
        if (index < 0 || index >= 6) return false;
        Targets |= 1 << index;
        int line = index / 3, flag = 1 << line, bank = 7 << (line * 3);
        if ((Targets & bank) != bank || (Loaded & flag) != 0) return false;
        Loaded |= flag;
        return true;
    }

    public void Enter(int line, int ball, float now)
    {
        int flag = 1 << line;
        if ((Loaded & flag) != 0 && (Processed & flag) == 0)
            entries[(line, ball)] = now;
    }

    public bool Exit(int line, int ball, float now)
    {
        var key = (line, ball);
        if (!entries.TryGetValue(key, out float start)) return false;
        entries.Remove(key);
        int flag = 1 << line;
        if (now <= start || now - start > config.productionRouteSeconds ||
            (Loaded & flag) == 0 || (Processed & flag) != 0) return false;
        Processed |= flag;
        return true;
    }

    public int Deliver()
    {
        int products = Processed;
        if (products == 0) return 0;
        int count = (products & 1) + ((products >> 1) & 1);
        int bonus = count * config.deliveryBonusPerProduct + (count == 2 ? config.combinedDeliveryBonus : 0);
        for (int line = 0; line < 2; line++)
            if ((products & (1 << line)) != 0) Targets &= ~(7 << (line * 3));
        Loaded &= ~products;
        Processed = 0;
        entries.Clear();
        return bonus;
    }

    public void ForgetBall(int ball)
    {
        entries.Remove((Wood, ball));
        entries.Remove((Textile, ball));
    }

    public void Reset()
    {
        Targets = Loaded = Processed = 0;
        entries.Clear();
    }

    public string Status(int line)
    {
        int flag = 1 << line;
        if ((Processed & flag) != 0) return "LIVRER";
        if ((Loaded & flag) != 0) return "RAMPE";
        int count = 0;
        for (int i = line * 3; i < line * 3 + 3; i++) if ((Targets & (1 << i)) != 0) count++;
        return count + "/3";
    }
}
