using UnityEngine;

/// <summary>Étapes du boss « Projet Final / Bug Critique » (GDD §Boss).</summary>
public enum BossState
{
    /// <summary>Cibles Projet non validées : le boss est éteint et ne compte pas.</summary>
    Verrouille,

    /// <summary>Le boss est visible et lumineux : il faut le toucher pour détecter le bug.</summary>
    Initialise,

    /// <summary>Le bug est détecté : il faut réussir une rampe IUT ou le loop @ pour le corriger.</summary>
    Correction,

    /// <summary>Le bug est corrigé : une dernière touche déclenche la compilation finale.</summary>
    Compilation,

    /// <summary>Boss terminé : la Nuit de l'Info peut se déclencher.</summary>
    Vaincu
}

/// <summary>
/// Le boss de fin de semestre (GDD §Boss : « Projet Final / Bug Critique »).
///
/// <para><b>Ce n'est pas un personnage mobile.</b> Le GDD est explicite : « Il s'agit d'un mode
/// spécial représenté par un écran, une borne, un gros bug visuel, un terminal ou une structure
/// Projet Final au sommet de la table. » C'est donc un <b>collider à états</b>, et le GDD le dit
/// aussi : « Le boss doit rester simple à développer : il peut être composé d'un collider,
/// d'états visuels, d'un compteur de touches et d'événements de score. »</para>
///
/// <para><b>Quatre étapes, dans l'ordre.</b> Le tableau §Boss du GDD impose la séquence : on ne
/// peut pas finir le boss en le martelant, il faut alterner les touches et les tirs de rampe. Le
/// joueur lit l'étape à la couleur et au message — sans quoi il ne saurait pas qu'il doit
/// <i>arrêter</i> de viser le boss et aller prendre une rampe.</para>
/// </summary>
public class BossTarget : MonoBehaviour
{
    /// <summary>
    /// Le boss de la table. Un singleton, comme les autres gestionnaires du projet : les rampes et
    /// le loop doivent pouvoir le corriger sans qu'on ait à les câbler un par un dans l'Inspector.
    /// </summary>
    public static BossTarget Instance { get; private set; }

    [Header("Étapes")]
    [Tooltip("Nombre de touches pour détecter le bug (GDD §Boss : 3).")]
    [SerializeField] private int touchesPourDetection = 3;

    [Header("Score")]
    [Tooltip("Points par touche pendant la détection du bug.")]
    [SerializeField] private int pointsParTouche = 5000;

    [Tooltip("Points à la compilation finale. GDD §Barème : boss terminé = 100 000.")]
    [SerializeField] private int bonusCompilation = 100000;

    [Tooltip("Points à l'activation du boss. GDD §Barème : activation = 50 000.")]
    [SerializeField] private int bonusActivation = 50000;

    [Header("Messages")]
    [Tooltip("Durée des messages, en secondes.")]
    [SerializeField] private float dureeMessage = 2f;

    [Header("Visuel")]
    [Tooltip("Les maillages à teinter selon l'étape.")]
    [SerializeField] private Renderer[] visuels;

    [Tooltip("L'écran du boss, s'il est distinct : c'est lui qui porte la teinte de glitch.")]
    [SerializeField] private Renderer ecran;

    [Tooltip("Intensité de l'émission. Ne s'affiche que si la case Emission du matériau est cochée.")]
    [SerializeField] private float intensiteEmission = 3f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly System.Collections.Generic.List<MaterialPropertyBlock> blocs
        = new System.Collections.Generic.List<MaterialPropertyBlock>();

    private BossState etat = BossState.Verrouille;
    private int touches;
    private MaterialPropertyBlock blocEcran;

    /// <summary>Étape courante du boss.</summary>
    public BossState State => etat;

    /// <summary>Le boss est-il visible et actif ?</summary>
    public bool IsActive => etat != BossState.Verrouille && etat != BossState.Vaincu;

    /// <summary>Nombre de touches restantes pour franchir l'étape courante.</summary>
    public int TouchesRestantes => Mathf.Max(0, touchesPourDetection - touches);

    /// <summary>Émis à chaque changement d'étape.</summary>
    public event System.Action<BossTarget, BossState> StateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[BossTarget] Un second boss existe sur '{name}' : il est ignoré.", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (visuels == null || visuels.Length == 0)
        {
            visuels = GetComponentsInChildren<Renderer>(true);
        }

        blocEcran = new MaterialPropertyBlock();

        Rafraichir();
    }

    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; }
    }

    // --- pilotage depuis les missions -------------------------------------------------------------

    /// <summary>
    /// Débloque le boss : il devient visible et lumineux (GDD §Boss, étape « Initialisation »).
    /// Appelé quand les cibles Projet sont validées.
    /// </summary>
    public void Activer()
    {
        if (etat != BossState.Verrouille) { return; }

        touches = 0;
        PasserA(BossState.Initialise);

        if (GameManager.Instance != null)
        {
            if (bonusActivation > 0) { GameManager.Instance.AddScore(bonusActivation); }

            GameManager.Instance.ShowMessage($"BUG CRITIQUE DÉTECTÉ  +{bonusActivation:N0}", dureeMessage);
        }
    }

    /// <summary>
    /// Une rampe IUT ou le loop @ a été réussi : la barre de bug diminue (GDD §Boss, étape
    /// « Correction »). Appelé par <see cref="RampGate"/> et <see cref="LoopGate"/>.
    /// </summary>
    public void NotifyCorrection()
    {
        if (etat != BossState.Correction) { return; }

        PasserA(BossState.Compilation);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ShowMessage("BUG CORRIGÉ — COMPILATION FINALE", dureeMessage);
        }
    }

    /// <summary>Remet le boss à zéro pour une nouvelle partie.</summary>
    public void ResetBoss()
    {
        touches = 0;
        PasserA(BossState.Verrouille);
    }

    private void PasserA(BossState suivant)
    {
        if (etat == suivant) { return; }

        etat = suivant;
        Rafraichir();
        StateChanged?.Invoke(this, etat);
    }

    // --- touches ------------------------------------------------------------------------------------

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Ball")) { return; }

        RenvoyerLaBille(collision);

        switch (etat)
        {
            case BossState.Verrouille:
                // Le boss n'est pas encore débloqué : il ne compte pas, mais il ne bloque pas non
                // plus — le GDD veut que les missions n'empêchent pas les autres systèmes.
                return;

            case BossState.Initialise:
                touches++;

                if (GameManager.Instance != null && pointsParTouche > 0)
                {
                    GameManager.Instance.AddScore(pointsParTouche);
                }

                if (touches >= touchesPourDetection)
                {
                    PasserA(BossState.Correction);

                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.ShowMessage("BUG DÉTECTÉ — RAMPE IUT OU LOOP @", dureeMessage);
                    }
                }
                else
                {
                    Rafraichir();

                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.ShowMessage(
                            $"BUG  {touches} / {touchesPourDetection}", 1f);
                    }
                }

                return;

            case BossState.Correction:
                // Il faut une rampe ou le loop : marteler le boss ne sert à rien. On le dit, sinon
                // le joueur croirait que la touche n'a pas été prise en compte.
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ShowMessage("PRENDRE LA RAMPE IUT OU LE LOOP @", 1.2f);
                }

                return;

            case BossState.Compilation:
                PasserA(BossState.Vaincu);

                if (GameManager.Instance != null)
                {
                    if (bonusCompilation > 0) { GameManager.Instance.AddScore(bonusCompilation); }

                    GameManager.Instance.ShowMessage($"PROJET FINAL COMPILÉ  +{bonusCompilation:N0}", 3f);
                }

                // C'est ce qui débloque la mission 7 et, derrière, la Nuit de l'Info.
                if (MissionManager.Instance != null)
                {
                    MissionManager.Instance.NotifyBossDefeated();
                }

                return;
        }
    }

    /// <summary>
    /// Renvoie la bille. Sans cela, la bille reste collée contre le boss et le joueur peut le
    /// marteler en une seconde — ce qui vide la séquence de son sens.
    /// </summary>
    private void RenvoyerLaBille(Collision collision)
    {
        Rigidbody balle = collision.rigidbody != null
            ? collision.rigidbody
            : collision.collider.attachedRigidbody;

        if (balle == null) { return; }

        Vector3 direction = balle.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 1e-6f) { direction = -transform.forward; }

        direction = direction.normalized;
        direction.y = 0.22f;   // la valeur du projet pour tous les rebonds

        balle.AddForce(direction.normalized * 110f, ForceMode.Impulse);
    }

    // --- visuel --------------------------------------------------------------------------------------

    private void Rafraichir()
    {
        Color c;

        switch (etat)
        {
            case BossState.Initialise: c = new Color(0.95f, 0.25f, 0.20f); break;   // rouge : bug
            case BossState.Correction: c = new Color(1f, 0.72f, 0.20f); break;      // ambre : agir
            case BossState.Compilation: c = new Color(0.35f, 0.75f, 1f); break;     // bleu : compiler
            case BossState.Vaincu: c = new Color(0.32f, 0.92f, 0.38f); break;       // vert : réussi
            default: c = new Color(0.14f, 0.15f, 0.18f); break;                     // éteint
        }

        Appliquer(c);
    }

    private void Appliquer(Color couleur)
    {
        if (visuels == null) { return; }

        if (blocs.Count != visuels.Length)
        {
            blocs.Clear();

            for (int i = 0; i < visuels.Length; i++) { blocs.Add(new MaterialPropertyBlock()); }
        }

        for (int i = 0; i < visuels.Length; i++)
        {
            Renderer r = visuels[i];

            if (r == null) { continue; }

            // L'écran garde sa propre teinte, plus vive : c'est lui qui porte le « glitch ».
            Color teinte = r == ecran ? couleur * 1.4f : couleur;

            MaterialPropertyBlock bloc = blocs[i];
            r.GetPropertyBlock(bloc);

            bloc.SetColor(BaseColorId, teinte);
            bloc.SetColor(EmissionColorId, teinte * intensiteEmission);

            r.SetPropertyBlock(bloc);
        }
    }
}
