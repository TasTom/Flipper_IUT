using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bumper actif (GDD §Bumpers) : repousse la bille dans l'axe du centre vers le point de
/// contact, marque des points et rend un retour visuel.
///
/// Remplace <see cref="ScoreTarget"/> sur les bumpers : ce dernier ne gère qu'un score et une
/// poussée, sans temporisation ni cadence. S'il est encore présent sur le même objet, il est
/// désactivé au démarrage plutôt que de marquer les points en double.
/// </summary>
/// <remarks>
/// Pas de <c>[RequireComponent(typeof(Collider))]</c> : la scène neutre doit pouvoir exister
/// avant les assets, avec le script lié mais sans collider. Le composant n'utilise le sien que
/// par <see cref="OnCollisionEnter"/>, il ne le déréférence jamais — sans collider il est
/// simplement muet, ce qui est l'état attendu d'un hôte vide.
/// </remarks>
public class Bumper : MonoBehaviour
{
    [Header("Effet")]
    [Tooltip("Impulsion de répulsion, en unités/s.")]
    [SerializeField] private float bounceForce = 125f;   // echelle : 7,5 x 16,67

    [Tooltip("Part de la poussée dirigée vers le haut de la table (GDD : direction.y 0.2 à 0.25).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float upwardBias = 0.05f;

    [Tooltip("Délai minimum entre deux répulsions, en secondes.")]
    [SerializeField] private float cooldown = 0.1f;

    [Header("Score")]
    [SerializeField] private int scoreValue = 150;

    [Tooltip("Nombre de touches avant que le bumper ne s'éteigne (0 : illimité). " +
             "Sert aux objectifs de type « atteindre les trois bumpers » du GDD §Multiball.")]
    [SerializeField] private int hitsBeforeExtinguish;

    [Header("Choc — le chapeau")]
    [Tooltip("Le chapeau qui s'enfonce puis remonte. Vide = recherche automatique de " +
             "l'enfant dont le nom contient « cap ».")]
    [SerializeField] private Transform cap;

    [Tooltip("Gonflement du chapeau au moment du choc, en fraction de sa taille. " +
             "0,14 = +14 % sur un coup franc. Purement visuel : le collider reste " +
             "sur l'hôte, donc la bille ne change pas de trajet.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float scalePunch = 0.14f;

    [Tooltip("Descente du chapeau pour un simple effleurement, en unités (1 u = 60 mm).")]
    [SerializeField] private float minTravel = 0.04f;

    [Tooltip("Descente du chapeau pour un coup franc. Au-delà de la course du chapeau, il " +
             "traverserait l'anneau : 0,10 u = 6 mm, sa course réelle.")]
    [SerializeField] private float maxTravel = 0.10f;

    [Tooltip("Vitesse d'impact (u/s) qui ne descend que de minTravel.")]
    [SerializeField] private float travelSpeedMin = 18f;

    [Tooltip("Vitesse d'impact (u/s) qui descend de maxTravel.")]
    [SerializeField] private float travelSpeedMax = 55f;

    [Tooltip("Durée de la descente, en secondes. Mesuré : à 0,01 s le chapeau avait déjà " +
             "repasse de 4,2 mm et filait vers le haut AVANT d'avoir atteint son creux de " +
             "6 mm — le pic était manqué, donc jamais vu. 0,02 s donne un creux franc.")]
    [SerializeField] private float travelTime = 0.02f;

    [Header("Choc — le ressort de retour")]
    [Tooltip("Fréquence propre de remontée, en Hz. 14 Hz = extinction en ~100 ms.")]
    [SerializeField] private float springFrequency = 14f;

    [Tooltip("Amortissement du ressort de retour. Un chapeau de bumper est un champignon " +
             "sur tige : il doit dépasser un peu en remontant, sinon le mouvement se lit " +
             "mécanique ; trop, et il se lit en gelée tremblante. Le dépassement vaut " +
             "exp(-zeta·pi/sqrt(1-zeta²)) : 0,50 → 16 %, 0,32 → 35 %.")]
    [Range(0.15f, 0.9f)]
    [SerializeField] private float springDamping = 0.5f;

    [Header("Retour visuel")]
    [SerializeField] private Renderer[] flashRenderers;
    [SerializeField] private Color flashColor = new Color(1f, 0.4f, 0.2f);

    [Tooltip("Force de la lueur au moment du choc, en multiplicateur de la couleur de base.")]
    [SerializeField] [Range(1f, 6f)] private float flashIntensity = 2.2f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly List<MaterialPropertyBlock> blocks = new List<MaterialPropertyBlock>();
    private readonly List<Color> baseColors = new List<Color>();
    private float lastBounceTime = float.NegativeInfinity;
    private int hitCount;

    // Course du chapeau en cours.
    private Vector3 capRestPosition;

    /// <summary>
    /// Échelle de repos du chapeau, dans le repère de l'hôte.
    /// </summary>
    /// <remarks>
    /// Elle est <b>non uniforme</b> — mesurée à (0,86 ; 1,00 ; 0,86) — parce que la pièce
    /// du dépôt arrive écrasée sur X et Z. Le gonflement doit donc MULTIPLIER cette
    /// échelle par un facteur unique, jamais la remplacer par <c>Vector3.one * k</c> :
    /// cela élargirait le chapeau de 16 % rien qu'en remettant son échelle au repos, et le
    /// retour à zéro se verrait comme un clignotement.
    /// </remarks>
    private Vector3 capRestScale = Vector3.one;

    /// <summary>
    /// Vrai tant que le chapeau défile. Explicite, et non une sentinelle de temps.
    /// </summary>
    /// <remarks>
    /// La version précédente utilisait <c>bounceStartedAt = -Infinity</c> comme marqueur
    /// d'inactivité. Or <c>Time.time - (-Infinity)</c> vaut <b>+Infinity</b> : la garde
    /// <c>if (t &lt; 0f) return;</c> ne l'arrêtait pas. Le ressort était alors évalué en
    /// +Infinity, <c>Mathf.Cos(damped * Infinity)</c> rendait NaN, et
    /// <c>Mathf.Abs(NaN) &lt; 0.0002f</c> étant lui aussi faux, la branche de repos ne le
    /// rattrapait pas — le NaN partait dans <c>localPosition</c>. Un booléen n'a pas
    /// d'arithmétique à ce genre de piège.
    /// </remarks>
    private bool bouncing;

    /// <summary>Temps du choc, valide seulement quand <see cref="bouncing"/> est vrai.</summary>
    private float bounceStartedAt;

    /// <summary>Course du chapeau pour le choc en cours. Nulle tant qu'il n'y a pas de choc.</summary>
    private float bounceTravel;

    /// <summary>Position d'où part la descente : le chapeau peut être encore en vol.</summary>
    private float bounceFrom;

    /// <summary>Nombre de fois où ce bumper a été touché depuis le début de la partie.</summary>
    public int HitCount => hitCount;

    /// <summary>
    /// Déclenche l'animation de course du chapeau sans passer par une collision.
    /// Réservé aux tests : à 0 la course est nulle, donc immobile.
    /// </summary>
    /// <param name="impactSpeed">Vitesse d'impact simulée, en u/s.</param>
    public void PlayBounce(float impactSpeed)
    {
        StartBounce(impactSpeed);
    }

    /// <summary>
    /// Position Y du chapeau, en unités, relativement au repos. 0 = au repos,
    /// négatif = enfoncé. Permet de mesurer la course réelle sans passer par une image.
    /// </summary>
    public float CapOffsetY
    {
        get
        {
            if (cap == null)
            {
                return 0f;
            }

            float t = Time.time - bounceStartedAt;
            return t < 0f ? 0f : BounceOffset(t);
        }
    }

    /// <summary>Chapeau animé trouvé, ou null si le bumper n'en a pas.</summary>
    public Transform Cap => cap;

    /// <summary>Vrai quand le bumper a atteint son quota de touches.</summary>
    public bool IsExtinguished => hitsBeforeExtinguish > 0 && hitCount >= hitsBeforeExtinguish;

    private void Awake()
    {
        DisableLegacyScoreTarget();

        if (flashRenderers == null || flashRenderers.Length == 0)
        {
            flashRenderers = GetComponentsInChildren<Renderer>(true);
        }

        CacheColors();
        ResolveCap();
        SetShade(0f);
    }

    private void Update()
    {
        if (cap == null || !bouncing)
        {
            return;
        }

        float t = Time.time - bounceStartedAt;
        float offset = BounceOffset(t);

        // Course terminée : on écrit une dernière fois la pose de repos, puis on rend la
        // main. Le drapeau est effacé AVANT de sortir, sinon le même NaN reviendrait.
        if (t > travelTime && Mathf.Abs(offset) < 0.0002f)
        {
            bouncing = false;
            bounceTravel = 0f;
            WriteCapPose(0f, 0f);
            SetShade(0f);
            return;
        }

        WriteCapPose(offset, Mathf.Clamp01(Mathf.Abs(offset) / Mathf.Max(0.0001f, bounceTravel)));
    }

    /// <summary>
    /// Écrit la pose du chapeau : enfoncement et gonflement, d'une seule fois.
    /// </summary>
    /// <param name="offset">Déplacement vertical local. Négatif = enfoncé.</param>
    /// <param name="energie">Énergie du ressort, de 0 (repos) à 1 (impact).</param>
    /// <remarks>
    /// Les deux effets se lisent sur <paramref name="energie"/> et non sur une horloge
    /// séparée : c'est ce qui fait lire « le bumper a été touché » comme un seul geste et
    /// non deux animations qui se suivent.
    /// </remarks>
    private void WriteCapPose(float offset, float energie)
    {
        // Position Y dans le repère de l'HÔTE, pas du monde : la table est inclinée de 7°,
        // et Vector3.down du monde enverrait le chapeau de travers.
        cap.localPosition = capRestPosition + Vector3.up * offset;

        // Facteur UNIQUE appliqué à l'échelle de repos, qui est non uniforme.
        cap.localScale = capRestScale * (1f + scalePunch * energie);
    }

    /// <summary>
    /// Position du chapeau en fonction du temps écoulé depuis le choc.
    /// </summary>
    /// <remarks>
    /// <para>La remontée est la <b>réponse libre d'un ressort amorti sous sa forme close</b>,
    /// pas une intégration d'Euler. Un ressort à 14 Hz demande ω·dt &lt; 2 pour rester stable
    /// en semi-implicite, soit une fréquence de trame de plus de 44 Hz — l'éditeur étant
    /// cadencé bien en dessous par moments, l'intégration divergerait par intermittence. La
    /// forme close donne le même mouvement à n'importe quel framerate, y compris nul.</para>
    /// <para>La fonction est <b>totale</b> : elle rend 0 pour un temps négatif ou infini au
    /// lieu de NaN. `Time.time` finit toujours par ∞ si l'horloge s'arrête, et un chapeau
    /// à NaN ne se répare plus tout seul — `localPosition` reste empoisonné jusqu'au
    /// rechargement de la scène.</para>
    /// </remarks>
    private float BounceOffset(float t)
    {
        if (bounceTravel <= 0f || float.IsNaN(t) || float.IsInfinity(t) || t < 0f)
        {
            return 0f;
        }

        if (t < travelTime)
        {
            return Mathf.Lerp(bounceFrom, -bounceTravel, t / Mathf.Max(1e-6f, travelTime));
        }

        float zeta = Mathf.Clamp(springDamping, 0.05f, 0.99f);
        float omega = 2f * Mathf.PI * Mathf.Max(0.5f, springFrequency);
        float damped = omega * Mathf.Sqrt(1f - zeta * zeta);

        // Conditions initiales x(0) = -course, v(0) = 0. La réponse vaut -course fois ce terme.
        float u = t - travelTime;
        float release = Mathf.Exp(-zeta * omega * u)
                        * (Mathf.Cos(damped * u)
                           + (zeta * omega / damped) * Mathf.Sin(damped * u));

        return -bounceTravel * release;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Ball"))
        {
            return;
        }

        Rigidbody ball = collision.rigidbody != null ? collision.rigidbody : collision.collider.attachedRigidbody;

        if (ball == null || IsExtinguished || Time.time - lastBounceTime < cooldown)
        {
            return;
        }

        lastBounceTime = Time.time;
        hitCount++;

        // Répulsion radiale : la bille repart à l'opposé du centre du bumper, quelle que soit
        // la face touchée. Utiliser la normale du contact seule ferait « coller » la bille sur
        // les flancs du chapeau.
        Vector3 direction = ball.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
        }

        direction = direction.normalized;
        direction.y = upwardBias;

        ball.AddForce(direction.normalized * bounceForce, ForceMode.Impulse);

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.Add(scoreValue);
        }

        StartBounce(collision.relativeVelocity.magnitude);
    }

    /// <summary>
    /// Déclenche la course du chapeau, d'autant plus profonde que le choc est violent.
    /// </summary>
    private void StartBounce(float impactSpeed)
    {
        // Un bumper éteint reste en place mais cesse de s'animer : il n'a plus de course,
        // il ne fait plus qu'absorber. Un bumper éteint qui rebondirait ferait le jeu.
        if (IsExtinguished)
        {
            bouncing = false;
            bounceTravel = 0f;

            if (cap != null)
            {
                cap.localPosition = capRestPosition;
                cap.localScale = capRestScale;
            }

            SetShade(0f);
            return;
        }

        float t = Mathf.InverseLerp(travelSpeedMin, Mathf.Max(travelSpeedMin + 0.01f, travelSpeedMax), impactSpeed);

        // On part de la position COURANTE, pas du repos : la bille peut revenir avant que
        // le chapeau soit revenu. Reprendre à 0 ferait sauter le chapeau en l'air.
        bounceFrom = bouncing ? BounceOffset(Time.time - bounceStartedAt) : 0f;
        bounceTravel = Mathf.Lerp(minTravel, Mathf.Max(minTravel, maxTravel), t);
        bounceStartedAt = Time.time;
        bouncing = true;

        // Le gonflement part TOUT DE SUITE, à l'image du contact. L'écrire seulement dans
        // le Update suivant retarderait le « pop » d'une frame, et sur un impact de 10 ms
        // cela le ferait manquer entièrement : c'est la différence entre voir le bumper
        // réagir et ne voir que sa course.
        if (cap != null)
        {
            WriteCapPose(bounceFrom, 1f);
        }

        SetShade(1f);
    }

    /// <summary>
    /// Retrouve le chapeau si le champ est vide. Un bumper sans chapeau animé n'est pas
    /// cassé — il perd juste son effet — mais il faut le signaler : le champ reste vide et
    /// personne ne saura jamais pourquoi la table semble terne.
    /// </summary>
    private void ResolveCap()
    {
        if (cap == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child == this.transform)
                {
                    continue;
                }

                if (child.name.IndexOf("cap", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                cap = child;
                break;
            }
        }

        if (cap == null)
        {
            Debug.LogWarning($"[Bumper] '{name}' : aucun chapeau trouvé, le bumper ne bougera pas. " +
                             "Renseigner le champ 'cap' ou nommer l'enfant avec « Cap ».", this);
            return;
        }

        capRestPosition = cap.localPosition;
        capRestScale = cap.localScale;
    }

    /// <summary>Remet le compteur de touches à zéro pour une nouvelle partie.</summary>
    public void ResetHits()
    {
        hitCount = 0;
        SetShade(0f);

        // Le chapeau revient aussi au repos physique : un bumper éteint qu'on rallume ne
        // doit pas rester figé dans la pose du dernier coup.
        if (cap != null)
        {
            cap.localPosition = capRestPosition;
            cap.localScale = capRestScale;
        }
    }

    /// <summary>
    /// <see cref="ScoreTarget"/> assurait le rôle de bumper avant ce composant. Le laisser actif
    /// ferait marquer deux fois les mêmes points et appliquerait deux impulsions.
    /// </summary>
    private void DisableLegacyScoreTarget()
    {
        ScoreTarget legacy = GetComponent<ScoreTarget>();

        if (legacy == null)
        {
            return;
        }

        legacy.enabled = false;
        Debug.Log($"[Bumper] '{name}' : ScoreTarget désactivé, Bumper prend le relais " +
                  "(sinon les points seraient comptés deux fois).", this);
    }

    private void CacheColors()
    {
        blocks.Clear();
        baseColors.Clear();

        foreach (Renderer target in flashRenderers)
        {
            if (target == null)
            {
                // Keep the same indices as the serialized array, including missing slots.
                blocks.Add(null);
                baseColors.Add(Color.white);
                continue;
            }

            blocks.Add(new MaterialPropertyBlock());
            baseColors.Add(ReadBaseColor(target));
        }
    }

    private static Color ReadBaseColor(Renderer target)
    {
        Material material = target.sharedMaterial;

        if (material == null)
        {
            return Color.white;
        }

        if (material.HasProperty(BaseColorId))
        {
            return material.GetColor(BaseColorId);
        }

        return material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
    }

    /// <summary>
    /// Teinte les matteriels selon l'énergie du choc, de 0 (repos) à 1 (impact).
    /// </summary>
    /// <remarks>
    /// Le mélange part de la couleur de base de chaque matière plutôt que d'une couleur
    /// fixe : sur un bumper à trois matières (socle acier, anneau, chapeau ivoire) un
    /// aplat orange uniforme donnait un ballon orange à la place d'un bumper qui s'allume.
    /// Un bumper éteint est assombri, jamais caché — masquer le GameObject couperait
    /// aussi son collider et la bille le traverserait.
    /// </remarks>
    private void SetShade(float amount)
    {
        amount = Mathf.Clamp01(amount);
        float dim = IsExtinguished ? 0.35f : 1f;

        // Les deux listes vont de pair : `blocks` est construit par `CacheColors` dans
        // `Awake`, alors que `flashRenderers` est sérialisé dans la scène. Hors Play — ou
        // si un Renderer est ajouté après l'Awake — elles divergent, et indexer `blocks`
        // sur la longueur de `flashRenderers` sort du tableau. Le composant perd son effet
        // au lieu de lever, comme le veut la règle « échouer proprement, jamais de
        // NullReferenceException ».
        if (blocks.Count != flashRenderers.Length)
        {
            CacheColors();
        }

        if (blocks.Count != flashRenderers.Length)
        {
            return;
        }

        for (int i = 0; i < flashRenderers.Length; i++)
        {
            Renderer target = flashRenderers[i];

            if (target == null)
            {
                continue;
            }

            MaterialPropertyBlock block = blocks[i];
            target.GetPropertyBlock(block);

            Color base_ = baseColors[i] * dim;

            // Color * float touche l'alpha : on le restaure, sinon le bumper deviendrait
            // translucide en plus d'être assombri.
            Color lit = base_ + flashColor * (amount * flashIntensity * 0.35f);
            lit.a = base_.a;
            block.SetColor(BaseColorId, lit);

            if (target.sharedMaterial != null && target.sharedMaterial.HasProperty(EmissionColorId))
            {
                block.SetColor(EmissionColorId, flashColor * (amount * flashIntensity));
            }

            target.SetPropertyBlock(block);
        }
    }
}

