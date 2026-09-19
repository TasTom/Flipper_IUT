using UnityEngine;

/// <summary>Côté d'un flipper. <see cref="Auto"/> déduit le côté du nom de l'objet.</summary>
public enum FlipperSide
{
    Auto,
    Left,
    Right,
    Upper,
}

/// <summary>
/// Flipper à rotation PILOTÉE PAR SCRIPT (GDD §Flippers principaux).
///
/// <para><b>Pas de <c>HingeJoint</c>.</b> Le montage d'origine — un joint à ressort dont la cible
/// basculait entre deux angles — s'est révélé instable sur cette table : mesuré en jeu, le
/// flipper balayait <b>359°</b> pour des butées de ±30°, avec une vitesse angulaire de
/// <b>1,1 × 10¹⁰ °/s</b> et un déplacement de <b>85 mm</b> alors que sa position était gelée. Et
/// l'angle du joint passait à <c>NaN</c> dès le 3ᵉ pas physique.</para>
///
/// <para><b>La cause, trouvée après coup.</b> Le tenseur d'inertie du corps valait
/// <c>(0 ; 0 ; 0,159)</c> — les axes X et Y avaient une inertie <b>exactement nulle</b>, ce qui
/// rend le tenseur singulier et fait diverger n'importe quel solveur de contraintes. Elle venait
/// des contraintes <c>FreezeRotationX | FreezeRotationY</c> : <b>geler une rotation annule
/// l'inertie de cet axe.</b> Vérifié : en ne gardant que <c>FreezePosition</c>, l'inertie
/// redevient <c>(0,026 ; 0,164 ; 0,159)</c> et le flipper se pose proprement à son angle de
/// repos. Ce diagnostic est conservé ici parce qu'il vaut pour tout joint à ressort de ce projet.</para>
///
/// <para><b>Le choix retenu, néanmoins : l'angle est écrit directement.</b> Le <c>Rigidbody</c>
/// devient <b>cinématique</b> — il ne subit plus rien, il commande. C'est aussi le comportement
/// d'un vrai flipper : actionné par un solénoïde, donc <b>position-commandé</b>, et jamais poussé
/// par la bille.</para>
///
/// <para><b>Le piège évité : <c>MoveRotation</c>, pas <c>transform.rotation</c>.</b> Déplacer un
/// collider par son transform ne déclenche aucune détection de collision balayée : un flipper qui
/// monte en 27 ms traverserait la bille sans la toucher. <c>MoveRotation</c> sur un corps
/// cinématique passe par le moteur physique, qui balaie alors le volume entre les deux poses.</para>
///
/// <para><b>Aucun composant à retirer à la main.</b> Le joint est supprimé de la scène par
/// `retirer_joints_flippers.cs`, et `EnsureMvpStructure` n'en crée plus. Ce script ne fait donc
/// aucune référence à <c>HingeJoint</c> : il n'a rien à réparer.</para>
///
/// <para>Un seul composant sert les trois flippers : le côté détermine la touche (via
/// <see cref="InputRouter"/>) et le sens de rotation. Si aucun routeur n'est présent dans la
/// scène, le composant retombe sur <see cref="activationKey"/>, ce qui garde les scènes
/// anciennes fonctionnelles.</para>
/// </summary>
public class Flipper : MonoBehaviour
{
    [Header("Identité")]
    [Tooltip("Auto : le côté est déduit du nom de l'objet (…_Left_…, …_Right_…, …_Upper_…).")]
    [SerializeField] private FlipperSide side = FlipperSide.Auto;

    [Tooltip("Repli quand la scène n'a pas d'InputRouter. Ignoré sinon.")]
    [SerializeField] private KeyCode activationKey = KeyCode.A;

    [Header("Débattement")]
    [Tooltip("Angle au repos, en degrés, pour un flipper gauche.")]
    [SerializeField] private float restAngle = -30f;

    [Tooltip("Angle en butée haute, en degrés, pour un flipper gauche.")]
    [SerializeField] private float activeAngle = 30f;

    [Tooltip("Inverse les deux angles pour les flippers droit et secondaire.")]
    [SerializeField] private bool mirrorAngles = true;

    [Header("Vitesse de rotation")]
    [Tooltip("Vitesse de montée, en degrés par seconde. 2200 fait 60° en 27 ms — le temps d'un " +
             "solénoïde réel.")]
    [SerializeField] private float swingSpeed = 2200f;

    [Tooltip("Vitesse de retour au repos, en degrés par seconde. Plus lente que la montée, " +
             "comme un vrai flipper que le ressort de rappel ramène.")]
    [SerializeField] private float returnSpeed = 700f;

    private Rigidbody body;
    private FlipperSide resolvedSide = FlipperSide.Left;
    private bool pressed;
    private float angle;                      // angle courant, en degrés, signé pour ce côté
    private Quaternion poseZero;              // rotation du pivot à l'angle zéro
    private Transform batTransform;
    private Vector3 batBaseScale = Vector3.one;

    /// <summary>Vrai pendant que le flipper est en butée haute.</summary>
    public bool IsPressed => pressed;

    /// <summary>Côté effectif, une fois <see cref="FlipperSide.Auto"/> résolu.</summary>
    public FlipperSide Side => resolvedSide;

    /// <summary>Angle courant, en degrés — en lecture seule, pour les tests et le HUD.</summary>
    public float Angle => angle;

    private void Awake()
    {
        resolvedSide = ResolveSide(side, name);
        batTransform = FindBatTransform();
        if (batTransform != null)
        {
            batBaseScale = batTransform.localScale;
        }

        // --- le corps : cinématique, il commande sans jamais subir ---
        body = GetComponent<Rigidbody>();

        if (body == null)
        {
            // Le contrat de scène pose un Rigidbody ; s'il manque, on le crée plutôt que
            // d'échouer — le flipper reste fonctionnel dans tous les cas.
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = false;

        // AUCUNE contrainte de rotation. `FreezeRotationX | FreezeRotationY` annulerait l'inertie
        // de ces deux axes — c'est la cause de l'instabilité du joint à ressort qui équipait ce
        // flipper avant (voir le résumé de classe). Sur un corps cinématique les contraintes ne
        // servent de toute façon à rien : il ne subit aucune force.
        body.constraints = RigidbodyConstraints.None;

        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // --- le repère du flipper -----------------------------------------------------------
        // `Rigidbody.MoveRotation` prend une rotation MONDE. Écrire directement
        // `AngleAxis(angle, Vector3.forward)` fait donc tourner le flipper autour du Z du monde
        // au lieu de l'axe de son joint — et détruit au passage l'orientation que la table
        // inclinée à 7° et le cadre à 90° lui avaient donnée.
        //
        // Le repère se DÉDUIT du parent (+90° autour de son X, cf. `place_scene_flippers.cs` §1)
        // au lieu d'être lu sur la rotation courante : la pose enregistrée dans la scène peut
        // ainsi être celle du repos — ce que l'éditeur doit montrer — sans que l'angle soit
        // compté deux fois au démarrage.
        poseZero = transform.parent != null
            ? transform.parent.rotation * Quaternion.Euler(90f, 0f, 0f)
            : transform.rotation;

        // --- la pose de départ ---
        angle = restAngle * AngleSign(resolvedSide);
        Appliquer();
    }

    public void ApplyDifficulty(float batScale)
    {
        if (batTransform == null)
        {
            batTransform = FindBatTransform();
            if (batTransform != null)
            {
                batBaseScale = batTransform.localScale;
            }
        }

        if (batTransform != null)
        {
            batTransform.localScale = batBaseScale * Mathf.Max(0.1f, batScale);
        }
    }

    private Transform FindBatTransform()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child == transform)
            {
                continue;
            }

            if (child.name == "Bat_Mesh" || child.name == "Flipper_Bat")
            {
                return child;
            }
        }

        foreach (Transform child in children)
        {
            if (child != transform && child.name.IndexOf("Bat", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child;
            }
        }

        return null;
    }

    private void OnValidate()
    {
        // Les deux vitesses ne peuvent pas être négatives : un flipper immobile serait un bug
        // silencieux, l'objet aurait l'air posé là sans raison.
        swingSpeed = Mathf.Max(0f, swingSpeed);
        returnSpeed = Mathf.Max(0f, returnSpeed);
    }

    private void FixedUpdate()
    {
        pressed = ReadInput();

        float cible = (pressed ? activeAngle : restAngle) * AngleSign(resolvedSide);
        float vitesse = pressed ? swingSpeed : returnSpeed;

        // On avance vers la cible à vitesse bornée, sans jamais la dépasser : c'est ce qui
        // donne au flipper sa course franche, et ce qui rend son angle déterministe.
        float pas = Mathf.MoveTowards(angle, cible, vitesse * Time.fixedDeltaTime);

        if (!Mathf.Approximately(pas, angle))
        {
            angle = pas;
            Appliquer();
        }
    }

    /// <summary>Écrit l'angle courant sur le corps, par le moteur physique.
    ///
    /// <para><c>MoveRotation</c> et non <c>transform.rotation</c> : la rotation passe par PhysX,
    /// qui balaie alors le volume entre la pose précédente et la nouvelle. Sans ce balayage, un
    /// flipper qui monte en 27 ms traverserait la bille sans la toucher.</para>
    ///
    /// <para>L'angle se compose APRÈS <see cref="poseZero"/> : c'est ce qui fait tourner le
    /// flipper autour de l'axe de son joint, dans le repère de la table inclinée, et non autour
    /// du Z du monde.</para>
    /// </summary>
    private void Appliquer()
    {
        if (body == null) { return; }

        body.MoveRotation(poseZero * Quaternion.AngleAxis(angle, Vector3.forward));
    }

    /// <summary>
    /// Le routeur d'entrées fait autorité ; <see cref="activationKey"/> n'est qu'un repli
    /// pour les scènes qui n'en ont pas encore.
    /// </summary>
    private bool ReadInput()
    {
        if (InputRouter.Instance != null)
        {
            switch (resolvedSide)
            {
                case FlipperSide.Right:
                    return InputRouter.Instance.RightFlipperHeld;
                case FlipperSide.Upper:
                    return InputRouter.Instance.UpperFlipperHeld;
                default:
                    return InputRouter.Instance.LeftFlipperHeld;
            }
        }

        return activationKey != KeyCode.None && Input.GetKey(activationKey);
    }

    private float AngleSign(FlipperSide forSide)
    {
        if (!mirrorAngles)
        {
            return 1f;
        }

        return forSide == FlipperSide.Left ? 1f : -1f;
    }

    /// <summary>
    /// Déduit le côté du nom de l'objet, pour que le contrat de scène
    /// (<c>Flipper_Left_Pivot</c>, <c>Flipper_Right_Pivot</c>, <c>Flipper_Upper_Pivot</c>)
    /// suffise à configurer le composant sans réglage manuel.
    /// </summary>
    internal static FlipperSide ResolveSide(FlipperSide declared, string objectName)
    {
        if (declared != FlipperSide.Auto)
        {
            return declared;
        }

        string n = (objectName ?? string.Empty).ToLowerInvariant();

        if (n.Contains("right") || n.Contains("droit"))
        {
            return FlipperSide.Right;
        }

        if (n.Contains("upper") || n.Contains("secondaire") || n.Contains("haut"))
        {
            return FlipperSide.Upper;
        }

        return FlipperSide.Left;
    }
}
