using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Côté d'un flipper. <see cref="Auto"/> déduit le côté du nom de l'objet.</summary>
public enum FlipperSide
{
    Auto,
    Left,
    Right,
    Upper,
}

/// <summary>
/// Action de la borne lue par un flipper.
///
/// <para><b>Pourquoi ce n'est pas déduit du côté.</b> Le numéro de bouton d'un encodeur de
/// borne ne dit pas où le fil est branché : sur le xin-mo, le bornier expose
/// <c>/trigger</c> puis <c>/button2</c>…<c>/button13</c> — le connecteur HID n°1 s'appelle
/// <c>trigger</c> — et c'est le câblage de la borne qui décide quel connecteur reçoit le
/// bouton de droite. L'asset, lui, ne fait que <i>nommer</i> des actions. Rien dans le
/// logiciel ne peut donc savoir que « trigger » est le bouton de droite : cette
/// correspondance se constate sur la machine, pas se déduit.</para>
///
/// <para><b><see cref="Auto"/> n'est pas un choix par défaut arbitraire</b> : il reproduit la
/// correspondance du contrat de scène (le côté gauche lit <c>LeftFlipper</c>), donc les scènes
/// existantes ne changent pas de comportement. Les autres valeurs existent pour corriger un
/// câblage inversé <i>sans toucher au fichier généré</i> <c>PinballControls.cs</c>, qui est
/// untracked et qu'une régénération écraserait.</para>
///
/// <para><b>⚠️ La valeur numérique est une simple ÉTIQUETTE, pas un numéro de bouton.</b> Le
/// bouton réellement écouté est celui de la <i>liaison</i> de l'asset
/// (<c>PinballControls.inputactions</c> / <c>PinballControls.cs</c>). Écrire
/// <c>LeftFlipper = 5</c> ne fait <b>pas</b> lire <c>/button5</c> : cela lit l'action
/// <c>LeftFlipper</c>, quelle que soit sa liaison. C'est le code qui associe un membre à une
/// action (voir <c>Flipper.CabinetHeld</c>) ; le nombre n'entre pas dans cette décision.
/// Les valeurs reprennent ici les numéros relevés sur la borne, à titre de mémo.</para>
///
/// <para>⚠️ <b>Ces valeurs sont sérialisées dans la scène.</b> Renuméroter un membre laisse les
/// objets existants sur l'ancien entier : la valeur devient orpheline et <c>CabinetHeld()</c>
/// tombe dans son cas <c>default</c> — le flipper cesse de répondre <i>sans lever d'erreur</i>.
/// Ajouter un membre uniquement en fin de liste, avec une valeur libre.</para>
/// </summary>
public enum CabinetAction
{
    /// <summary>Suit le côté du flipper : gauche -> <c>LeftFlipper</c>, droite -> <c>RightFlipper</c>.</summary>
    Auto = 0,

    /// <summary>Aucune action de la borne (le flipper secondaire, absent du bornier).</summary>
    None = 1,

    LeftFlipper = 5,   // mémo : bouton /button5 de la borne
    RightFlipper = 6,  // mémo : bouton /button6 de la borne

    /// <summary>Utile au diagnostic : révèle un bouton câblé sur la mauvaise action.</summary>
    LaunchBall = 10,   // mémo : bouton START /button10

    Quit = 7,          // mémo : bouton /button7
    // Non câblé faute d'action : SELECT = /button9.
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
/// <para><b>Trois sources d'entrée, cumulatives.</b> Le côté détermine la touche et le sens de
/// rotation, et la lecture s'arrête à la première source qui presse le flipper :</para>
///
/// <list type="number">
/// <item>la <b>borne physique</b> — les actions <c>GamePlayPF/*</c> de <see cref="PinballControls"/>,
/// quand <c>useCabinetController</c> est coché. <b>Quelle action lit quel pivot se règle par
/// <c>cabinetAction</c></b> : voir <see cref="CabinetAction"/>, le câblage d'une borne ne se
/// déduit pas du numéro de bouton ;</item>
/// <item>le <b>clavier</b>, via <see cref="InputRouter"/> ;</item>
/// <item><c>activationKey</c>, pour les scènes qui n'ont pas encore de routeur.</item>
/// </list>
///
/// <para>Les deux premières se <b>cumulent</b> au lieu de s'exclure : la borne et le clavier
/// pilotent la même table, ce qui permet de mettre au point au clavier sur une machine où la
/// borne n'est pas branchée. C'est le comportement voulu — la borne n'a aucune liaison clavier
/// dans son asset, s'en remettre à elle seule rendrait le jeu injouable sans matériel.</para>
///
/// <para><b>Le flipper secondaire n'existe pas sur la borne</b> : l'asset ne déclare pas
/// d'action <c>UpperFlipper</c>. <see cref="FlipperSide.Upper"/> reste donc purement clavier,
/// et ne retombe PAS sur une action voisine — un appui sur la borne ne doit pas faire sauter un
/// flipper que le joueur n'a pas demandé.</para>
/// </summary>
public class Flipper : MonoBehaviour
{
    [Header("Identité")]
    [Tooltip("Auto : le côté est déduit du nom de l'objet (…_Left_…, …_Right_…, …_Upper_…).")]
    [SerializeField] private FlipperSide side = FlipperSide.Auto;

    [Tooltip("Repli quand la scène n'a pas d'InputRouter. Ignoré sinon.")]
    [SerializeField] private KeyCode activationKey = KeyCode.A;

    [Header("Borne physique (Input System)")]
    [Tooltip("Lit les actions GamePlayPF/* du contrôleur (bornier xin-mo). " +
             "Le clavier reste actif en parallèle : les deux se cumulent.")]
    [SerializeField] private bool useCabinetController = true;

    [Tooltip("Action de la borne que ce pivot écoute.\n" +
             "Auto = selon le côté (LeftFlipper à gauche, RightFlipper à droite).\n" +
             "Si les deux flippers sont inversés sur votre borne, mettez LeftFlipper sur le " +
             "pivot droit et RightFlipper sur le pivot gauche — le câblage d'un bornier ne se " +
             "déduit pas du nom des actions.")]
    [SerializeField] private CabinetAction cabinetAction = CabinetAction.Auto;

    [Header("Diagnostic")]
    [Tooltip("Journalise chaque appui et chaque relâchement, avec la source réelle (borne ou " +
             "clavier) et le contrôle physique. À décocher une fois le câblage vérifié : un " +
             "log par appui reste du bruit en partie.")]
    [SerializeField] private bool logInput = true;

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
    private PinballControls cabinet;          // actions de la borne, null si désactivée
    private CabinetAction resolvedCabinet;    // action réellement écoutée, Auto résolu
    private string activeSource = "aucune";   // qui presse en ce moment
    private string lastSource = "aucune";     // qui a pressé en dernier, pour le log de relâchement

    /// <summary>Vrai pendant que le flipper est en butée haute.</summary>
    public bool IsPressed => pressed;

    /// <summary>Côté effectif, une fois <see cref="FlipperSide.Auto"/> résolu.</summary>
    public FlipperSide Side => resolvedSide;

    /// <summary>Angle courant, en degrés — en lecture seule, pour les tests et le HUD.</summary>
    public float Angle => angle;

    private void Awake()
    {
        resolvedSide = ResolveSide(side, name);

        // --- la borne physique ---------------------------------------------------------------
        // Chaque flipper construit son propre exemplaire plutôt que de partager un statique :
        // `PinballControls` n'est qu'un emballage autour d'un JSON, et un statique survivrait
        // d'une session de Play à l'autre dans l'éditeur en gardant une action map périmée.
        if (useCabinetController)
        {
            cabinet = new PinballControls();

            // Résolution de l'action, faite UNE fois : relire le champ à chaque pas physique
            // serait du travail par frame pour une valeur qui ne change pas en cours de partie.
            resolvedCabinet = cabinetAction != CabinetAction.Auto
                ? cabinetAction
                : (resolvedSide == FlipperSide.Right ? CabinetAction.RightFlipper
                   : resolvedSide == FlipperSide.Left ? CabinetAction.LeftFlipper
                   : CabinetAction.None);

            // Le câblage d'une borne ne se déduit pas du nom des actions : on l'annonce, pour
            // qu'un essai de bouton se lise dans la console au lieu de se chercher à l'aveugle.
            Debug.Log($"[Flipper] {name} : côté={resolvedSide}, borne='{resolvedCabinet}' " +
                      $"({CheminCabinet()}), clavier=" +
                      (InputRouter.Instance != null ? "InputRouter" : activationKey.ToString()), this);
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

    /// <summary>
    /// Active l'action map de la borne. Sans cet appel les actions ne reçoivent rien du tout :
    /// le nouvel Input System n'écoute pas une action laissée désactivée, et
    /// <c>IsPressed()</c> renverrait <c>false</c> en silence — la panne la plus difficile à
    /// diagnostiquer, puisqu'aucune erreur n'est levée.
    /// </summary>
    private void OnEnable()
    {
        if (cabinet != null)
        {
            cabinet.GamePlayPF.Enable();
        }
    }

    private void OnDisable()
    {
        if (cabinet != null)
        {
            cabinet.GamePlayPF.Disable();
        }
    }

    private void OnDestroy()
    {
        if (cabinet == null)
        {
            return;
        }

        // Désactiver AVANT de détruire : le finaliseur généré par Unity affirme que l'action map
        // est éteinte, et détruire un asset encore actif laisserait l'assertion tirer.
        cabinet.GamePlayPF.Disable();

        // `DestroyImmediate` dans TOUS les cas, et non `Dispose()`.
        //
        // Mesuré : `Dispose()` — qui appelle `Object.Destroy` — produit
        // « PinballControls: Destroy may not be called from edit mode! » quand la session de
        // Play se termine. Le garde `Application.isPlaying` ne suffit pas : au moment où
        // `OnDestroy` s'exécute, l'indicateur peut encore valoir `true` alors qu'Unity refuse
        // déjà les destructions différées. `DestroyImmediate` est accepté en édition comme en
        // Play, ce qui supprime la question au lieu d'essayer de la trancher.
        DestroyImmediate(cabinet.asset);

        cabinet = null;
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
        bool avant = pressed;
        pressed = ReadInput();

        // --- journal des appuis ---------------------------------------------------------------
        // On journalise la TRANSITION, jamais l'état : à 50 pas physiques par seconde, un log
        // par pas noierait la console. Et on nomme le contrôle qui a répondu — c'est la seule
        // façon de distinguer « le bouton fait bouger le mauvais flipper » de « le clavier
        // répond aussi », deux situations qui se ressemblent à l'écran.
        if (logInput && pressed != avant)
        {
            if (pressed)
            {
                lastSource = activeSource;
                Debug.Log($"[Flipper] {name} (côté {resolvedSide})  APPUI    <- {activeSource}", this);
            }
            else
            {
                Debug.Log($"[Flipper] {name} (côté {resolvedSide})  relâché  (source : {lastSource})", this);
            }
        }

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
    /// Les trois sources sont <b>cumulatives</b> : la première qui presse le flipper l'emporte,
    /// l'ordre n'a donc aucune incidence sur le résultat. La borne est interrogée en premier
    /// parce que c'est l'interface réelle de la table ; le routeur puis
    /// <see cref="activationKey"/> prennent le relais quand elle n'est pas là.
    ///
    /// <para>Au passage, la source est mémorisée dans <see cref="activeSource"/> : c'est ce qui
    /// permet au journal de dire <i>quel</i> bouton a répondu, et pas seulement qu'un flipper a
    /// bougé. Sans cela, un câblage inversé ne se distingue pas d'un clavier qui traîne.</para>
    /// </summary>
    private bool ReadInput()
    {
        if (CabinetHeld())
        {
            activeSource = "BORNE " + CheminCabinet();
            return true;
        }

        if (InputRouter.Instance != null)
        {
            bool clavier;
            string quoi;

            switch (resolvedSide)
            {
                case FlipperSide.Right:
                    clavier = InputRouter.Instance.RightFlipperHeld;
                    quoi = "flipper droit";
                    break;
                case FlipperSide.Upper:
                    clavier = InputRouter.Instance.UpperFlipperHeld;
                    quoi = "flipper secondaire";
                    break;
                default:
                    clavier = InputRouter.Instance.LeftFlipperHeld;
                    quoi = "flipper gauche";
                    break;
            }

            if (clavier)
            {
                activeSource = "CLAVIER (InputRouter, " + quoi + ")";
                return true;
            }

            activeSource = "aucune";
            return false;
        }

        if (activationKey != KeyCode.None && Input.GetKey(activationKey))
        {
            activeSource = "CLAVIER (" + activationKey + ")";
            return true;
        }

        activeSource = "aucune";
        return false;
    }

    /// <summary>
    /// Le bornier xin-mo, lu par le nouvel Input System.
    ///
    /// <para><b><c>IsPressed()</c>, et non un abonnement à <c>performed</c>.</b> La lecture a
    /// lieu au pas physique : un rappel d'événement manqué pendant un pic de charge laisserait le
    /// flipper baissé alors que le joueur appuie encore. L'état est donc <i>interrogé</i> à
    /// chaque pas, jamais subi.</para>
    ///
    /// <para>C'est <see cref="resolvedCabinet"/> qui décide de l'action, et non le côté : voir
    /// <see cref="CabinetAction"/> pour la raison. Un pivot resté en
    /// <see cref="CabinetAction.None"/> — le flipper secondaire, absent du bornier — ne retombe
    /// volontairement sur <b>aucune</b> action voisine : un appui sur la borne ne doit pas faire
    /// sauter un flipper que le joueur n'a pas demandé.</para>
    /// </summary>
    private bool CabinetHeld()
    {
        if (cabinet == null)
        {
            return false;
        }

        switch (resolvedCabinet)
        {
            case CabinetAction.LeftFlipper:  return cabinet.GamePlayPF.LeftFlipper.IsPressed();
            case CabinetAction.RightFlipper: return cabinet.GamePlayPF.RightFlipper.IsPressed();
            case CabinetAction.LaunchBall:   return cabinet.GamePlayPF.LaunchBall.IsPressed();
            case CabinetAction.Quit:         return cabinet.GamePlayPF.Quit.IsPressed();
            default:                         return false;
        }
    }

    /// <summary>
    /// Chemin du contrôle physique réellement écouté — par exemple
    /// <c>&lt;HID::xin-mo.com Xinmotek Controller&gt;/trigger</c>.
    ///
    /// <para>C'est ce qui rend le diagnostic possible : le nom de l'action ne dit pas quel bouton
    /// le joueur doit presser, le chemin si. Affiché au démarrage à côté du nom du pivot, il
    /// transforme « le flipper de droite répond à gauche » en une correspondance lisible.</para>
    /// </summary>
    private string CheminCabinet()
    {
        if (cabinet == null)
        {
            return "aucune borne";
        }

        UnityEngine.InputSystem.InputAction action;
        switch (resolvedCabinet)
        {
            case CabinetAction.LeftFlipper:  action = cabinet.GamePlayPF.LeftFlipper;  break;
            case CabinetAction.RightFlipper: action = cabinet.GamePlayPF.RightFlipper; break;
            case CabinetAction.LaunchBall:   action = cabinet.GamePlayPF.LaunchBall;   break;
            case CabinetAction.Quit:         action = cabinet.GamePlayPF.Quit;         break;
            default:                         return "aucune action";
        }

        if (action == null || action.bindings.Count == 0)
        {
            return "action non liée";
        }

        return action.bindings[0].path;
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
