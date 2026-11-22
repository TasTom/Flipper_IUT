using System;
using UnityEngine;

/// <summary>
/// Source unique des entrées clavier (GDD §Contrôles).
///
/// Aucun autre script ne lit <see cref="Input"/> directement : ils interrogent le routeur.
/// Une seule table de correspondance touche -> action existe donc dans le projet, ce qui rend
/// le remappage vérifiable d'un seul endroit.
///
/// Le GDD signale que les flippers et le tilt se disputent les mêmes touches :
/// « Pour éviter un conflit entre les flippers et le tilt, il est préférable de retenir une
/// seconde configuration claire. » Deux dispositions sont donc fournies :
///
///   Principale (fidèle au GDD) : flippers Q / A / D, tilt sur les flèches.
///   Alternative                : flippers sur les flèches, tilt sur A / D.
///
/// Basculer <see cref="alternateLayout"/> échange les deux groupes sans rien recompiler.
/// </summary>
[DefaultExecutionOrder(-100)]
public class InputRouter : MonoBehaviour
{
    public enum ActionBinding { LeftFlipper, RightFlipper, UpperFlipper, Plunger, TiltLeft, TiltRight }

    public static InputRouter Instance { get; private set; }
    private const string BindingPrefix = "VosgesTilt_Key_";
    private static readonly KeyCode[] KeyboardKeys = Array.FindAll((KeyCode[])Enum.GetValues(typeof(KeyCode)),
        key => key >= KeyCode.Backspace && key < KeyCode.Mouse0);
    private readonly KeyCode[] bindings = new KeyCode[6];
    private bool capturingBinding;

    [Header("Disposition")]
    [Tooltip("Décoche : flippers Q/A/D et tilt sur les flèches (fidèle au GDD).\n" +
             "Coche : flippers sur les flèches et tilt sur A/D.")]
    [SerializeField] private bool alternateLayout;

    [Header("Flippers — disposition principale")]
    [SerializeField] private KeyCode leftFlipperKey = KeyCode.A;
    [SerializeField] private KeyCode leftFlipperSecondKey = KeyCode.Q;
    [SerializeField] private KeyCode rightFlipperKey = KeyCode.D;
    [SerializeField] private KeyCode upperFlipperKey = KeyCode.E;
    [SerializeField] private KeyCode upperFlipperSecondKey = KeyCode.R;

    [Header("Flippers — disposition alternative")]
    [SerializeField] private KeyCode altLeftFlipperKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode altRightFlipperKey = KeyCode.RightArrow;

    [Header("Tilt — disposition principale")]
    [SerializeField] private KeyCode tiltLeftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode tiltRightKey = KeyCode.RightArrow;

    [Header("Tilt — disposition alternative")]
    [SerializeField] private KeyCode altTiltLeftKey = KeyCode.A;
    [SerializeField] private KeyCode altTiltRightKey = KeyCode.D;

    [Header("Lanceur et menus")]
    [SerializeField] private KeyCode plungerKey = KeyCode.Space;
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    [SerializeField] private KeyCode validateKey = KeyCode.Return;

    /// <summary>Vrai tant que la touche de charge du lanceur est maintenue.</summary>
    public bool PlungerHeld { get; private set; }

    /// <summary>Vrai la frame où le lanceur est relâché (GDD : « Charge et relâche »).</summary>
    public bool PlungerReleased { get; private set; }

    public bool LeftFlipperHeld { get; private set; }
    public bool RightFlipperHeld { get; private set; }
    public bool UpperFlipperHeld { get; private set; }

    public bool TiltLeftPressed { get; private set; }
    public bool TiltRightPressed { get; private set; }
    public bool PausePressed { get; private set; }
    public bool ValidatePressed { get; private set; }

    private bool warnedAboutClash;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[InputRouter] Un second routeur existe sur '{name}' : il est ignoré. " +
                             "Un seul InputRouter par scène.", this);
            enabled = false;
            return;
        }

        Instance = this;
        for (int i = 0; i < bindings.Length; i++)
            bindings[i] = (KeyCode)PlayerPrefs.GetInt(BindingPrefix + (ActionBinding)i, 0);
        for (int i = 0; i < bindings.Length; i++)
            if (!CanBind((ActionBinding)i, bindings[i])) bindings[i] = KeyCode.None;
        WarnAboutKeyClash();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        LeftFlipperHeld = Held(GetKey(ActionBinding.LeftFlipper)) || Held(GetAlias(ActionBinding.LeftFlipper));
        RightFlipperHeld = Held(GetKey(ActionBinding.RightFlipper));
        UpperFlipperHeld = Held(GetKey(ActionBinding.UpperFlipper)) || Held(GetAlias(ActionBinding.UpperFlipper));

        TiltLeftPressed = Pressed(GetKey(ActionBinding.TiltLeft));
        TiltRightPressed = Pressed(GetKey(ActionBinding.TiltRight));

        bool plungerNow = Held(GetKey(ActionBinding.Plunger));
        PlungerReleased = PlungerHeld && !plungerNow;
        PlungerHeld = plungerNow;

        PausePressed = !capturingBinding && Pressed(pauseKey);
        ValidatePressed = !capturingBinding && Pressed(validateKey);
    }

    public KeyCode GetKey(ActionBinding action)
    {
        var chosen = bindings[(int)action];
        if (chosen != KeyCode.None) return chosen;
        return action switch
        {
            ActionBinding.LeftFlipper => alternateLayout ? altLeftFlipperKey : leftFlipperKey,
            ActionBinding.RightFlipper => alternateLayout ? altRightFlipperKey : rightFlipperKey,
            ActionBinding.UpperFlipper => upperFlipperKey,
            ActionBinding.Plunger => plungerKey,
            ActionBinding.TiltLeft => alternateLayout ? altTiltLeftKey : tiltLeftKey,
            ActionBinding.TiltRight => alternateLayout ? altTiltRightKey : tiltRightKey,
            _ => KeyCode.None
        };
    }

    public KeyCode GetAlias(ActionBinding action)
    {
        if (bindings[(int)action] != KeyCode.None) return KeyCode.None;
        return action switch
        {
            ActionBinding.LeftFlipper when !alternateLayout => leftFlipperSecondKey,
            ActionBinding.UpperFlipper => upperFlipperSecondKey,
            _ => KeyCode.None
        };
    }

    public static string KeyLabel(KeyCode key) => key switch
    {
        KeyCode.None => "—", KeyCode.Space => "Espace", KeyCode.LeftArrow => "←", KeyCode.RightArrow => "→",
        KeyCode.UpArrow => "↑", KeyCode.DownArrow => "↓", KeyCode.LeftShift => "Maj gauche", KeyCode.RightShift => "Maj droite",
        _ when key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9 => ((int)key - (int)KeyCode.Alpha0).ToString(),
        _ => key.ToString()
    };

    public bool SetBinding(ActionBinding action, KeyCode key)
    {
        if (!CanBind(action, key)) return false;
        bindings[(int)action] = key;
        PlayerPrefs.SetInt(BindingPrefix + action, (int)key);
        return true;
    }

    public void ResetBindings()
    {
        for (int i = 0; i < bindings.Length; i++)
        {
            bindings[i] = KeyCode.None;
            PlayerPrefs.DeleteKey(BindingPrefix + (ActionBinding)i);
        }
    }

    public void CaptureBinding(bool value) => capturingBinding = value;

    public bool TryReadBindingKey(out KeyCode key)
    {
        key = KeyCode.None;
        if (!capturingBinding || !Input.anyKeyDown) return false;
        foreach (var candidate in KeyboardKeys)
        {
            if (!Pressed(candidate)) continue;
            key = candidate;
            return true;
        }
        return false;
    }

    private bool CanBind(ActionBinding action, KeyCode key)
    {
        if (key == pauseKey || key == validateKey || key == KeyCode.Escape || key == KeyCode.Return ||
            key == KeyCode.KeypadEnter || !Array.Exists(KeyboardKeys, candidate => candidate == key)) return false;
        for (int i = 0; i < bindings.Length; i++)
        {
            var other = (ActionBinding)i;
            if (other != action && (GetKey(other) == key || GetAlias(other) == key)) return false;
        }
        return true;
    }

    /// <summary>
    /// Lit l'état d'une touche. Isolé ici pour que la bascule vers le nouvel Input System
    /// ne touche qu'un seul fichier si le projet change de <c>Active Input Handling</c>.
    /// </summary>
    private static bool Held(KeyCode key)
    {
        // KeyCode.None : touche volontairement désactivée (ex. flipper secondaire absent).
        return key != KeyCode.None && Input.GetKey(key);
    }

    private static bool Pressed(KeyCode key)
    {
        return key != KeyCode.None && Input.GetKeyDown(key);
    }

    /// <summary>
    /// Le GDD insiste sur l'absence de conflit flippers/tilt. On le vérifie à l'exécution
    /// plutôt que de faire confiance au réglage : un doublon passerait inaperçu sinon.
    /// </summary>
    private void WarnAboutKeyClash()
    {
        if (warnedAboutClash)
        {
            return;
        }

        warnedAboutClash = true;

        KeyCode leftFlipper = alternateLayout ? altLeftFlipperKey : leftFlipperKey;
        KeyCode rightFlipper = alternateLayout ? altRightFlipperKey : rightFlipperKey;
        KeyCode tiltLeft = alternateLayout ? altTiltLeftKey : tiltLeftKey;
        KeyCode tiltRight = alternateLayout ? altTiltRightKey : tiltRightKey;

        if (leftFlipper == tiltLeft || leftFlipper == tiltRight ||
            rightFlipper == tiltLeft || rightFlipper == tiltRight)
        {
            Debug.LogWarning(
                "[InputRouter] Conflit flippers/tilt : une même touche sert aux deux. " +
                "Le GDD §Contrôles demande une seconde configuration claire — " +
                "bascule 'alternateLayout' ou change les touches dans l'Inspector.", this);
        }
    }
}
