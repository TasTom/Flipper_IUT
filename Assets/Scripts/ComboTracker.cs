/// <summary>Chaîne chronométrée : un impact répété sur place ne prolonge pas le combo.</summary>
public sealed class ComboTracker
{
    private object lastSource;
    private float lastTime;
    public int Actions { get; private set; }

    public bool Register(object source, bool allowRepeat, float now, float window)
    {
        Expire(now, window);
        if (source == null || (!allowRepeat && ReferenceEquals(source, lastSource))) return false;
        lastSource = source;
        lastTime = now;
        // Un Super Combo ne paie qu'une fois par chaîne, même si elle continue.
        if (Actions >= 5) return false;
        Actions++;
        return true;
    }

    public bool Expire(float now, float window)
    {
        if (Actions == 0 || now - lastTime <= window) return false;
        Clear();
        return true;
    }

    public void Clear()
    {
        Actions = 0;
        lastSource = null;
        lastTime = 0f;
    }
}
