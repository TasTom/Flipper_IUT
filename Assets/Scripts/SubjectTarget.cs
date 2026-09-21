using UnityEngine;

/// <summary>État d'une cible, dans le cycle décrit par le GDD §Cibles fixes.</summary>
public enum TargetState
{
    /// <summary>La cible n'est pas concernée par la mission en cours : elle marque, mais ne
    /// fait pas avancer la mission.</summary>
    Eteinte,

    /// <summary>La mission en cours attend cette cible : elle est allumée et compte.</summary>
    Active,

    /// <summary>Touchée pendant sa mission. Elle reste allumée, mais ne compte plus.</summary>
    Validee
}

/// <summary>
/// Une cible de mission (GDD §Cibles fixes) : « Une cible touchée doit changer d'état visuel :
/// éteinte → active → validée. Cela aide le joueur à savoir ce qu'il lui reste à accomplir. »
///
/// <para><b>Une cible marque toujours ses points</b>, allumée ou non — « les missions ne doivent
/// pas nécessairement bloquer les autres systèmes » (GDD §Missions). Seule la <i>progression</i>
/// de la mission est conditionnée à l'allumage, et c'est <see cref="MissionManager"/> qui décide
/// de l'état.</para>
///
/// <para><b>Une cible validée ne se revalide pas.</b> Le GDD compte « les 3 cibles Café », pas
/// « trois touches sur une cible Café » : sans ce verrou, un seul tir qui traîne validerait la
/// mission entière.</para>
/// </summary>
/// <remarks>
/// L'appartenance à un groupe est un <b>champ texte</b>, pas un enum : les groupes du GDD
/// (Matieres, Cafe, Java, Reseau, Projet) sont des données de conception, susceptibles de bouger,
/// et un enum figerait le code à chaque renommage.
/// </remarks>
public class SubjectTarget : MonoBehaviour
{
    [Header("Groupe")]
    [Tooltip("Groupe de la cible (GDD §Cibles fixes : Matieres, Cafe, Java, Reseau, Projet). " +
             "C'est ce nom que MissionManager cherche pour allumer la cible.")]
    [SerializeField] private string groupId = "Matieres";

    [Header("Score")]
    [Tooltip("Points versés à chaque touche. GDD §Barème : Cible Matière 2 500, Cible Java 2 000, " +
             "Cible Café 1 500, cible fixe simple 500.")]
    [SerializeField] private int points = 2500;

    [Tooltip("Impulsion de renvoi de la bille, en unités/s.")]
    [SerializeField] private float impulseForce = 100f;

    [Tooltip("Délai minimum entre deux touches, en secondes. Sans lui, une bille qui vibre contre " +
             "la cible marque plusieurs fois dans la même seconde.")]
    [SerializeField] private float cooldown = 0.1f;

    [Header("Retour visuel")]
    [Tooltip("Les maillages à teinter. Laissé vide, tous les Renderer du sous-arbre sont pris.")]
    [SerializeField] private Renderer[] visuels;

    [Tooltip("Teinte d'une cible éteinte. Le GDD la veut visible mais discrète : une lueur " +
             "faible plutôt qu'un noir complet, sinon la cible disparaît dans le décor.")]
    [SerializeField] private Color couleurEteinte = new Color(0.20f, 0.22f, 0.26f);

    [Tooltip("Teinte d'une cible allumée — elle attend d'être touchée. Ambre, comme les autres " +
             "éléments interactifs de la table (GDD §Direction artistique).")]
    [SerializeField] private Color couleurActive = new Color(1f, 0.72f, 0.20f);

    [Tooltip("Teinte d'une cible validée.")]
    [SerializeField] private Color couleurValidee = new Color(0.32f, 0.92f, 0.38f);

    [Tooltip("Durée de l'éclair à la touche, en secondes.")]
    [SerializeField] private float dureeEclair = 0.12f;

    [Tooltip("Intensité de l'émission, quand le matériau l'a activée. À 0, seule la teinte " +
             "change — ce qui reste lisible.")]
    [SerializeField] private float intensiteEmission = 2.5f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly System.Collections.Generic.List<MaterialPropertyBlock> blocs
        = new System.Collections.Generic.List<MaterialPropertyBlock>();

    private TargetState state = TargetState.Eteinte;
    private float dernierHit = float.NegativeInfinity;
    private float finEclair;

    /// <summary>Groupe de la cible, tel que déclaré.</summary>
    public string GroupId => groupId;

    /// <summary>État courant.</summary>
    public TargetState State => state;

    /// <summary>La cible a-t-elle été validée pendant la mission en cours ?</summary>
    public bool IsValidated => state == TargetState.Validee;

    /// <summary>La cible attend-elle d'être touchée ?</summary>
    public bool IsActive => state == TargetState.Active;

    private void Awake()
    {
        if (visuels == null || visuels.Length == 0)
        {
            visuels = GetComponentsInChildren<Renderer>(true);
        }

        Rafraichir();
    }

    private void Update()
    {
        if (finEclair > 0f && Time.time >= finEclair)
        {
            finEclair = 0f;
            Rafraichir();
        }
    }

    // --- état, piloté par MissionManager ---------------------------------------------------------

    /// <summary>Allume la cible : elle attend d'être touchée et comptera pour sa mission.</summary>
    public void Activer()
    {
        state = TargetState.Active;
        finEclair = 0f;
        Rafraichir();
    }

    /// <summary>Éteint la cible, validée comprise. Appelé quand sa mission n'est plus la mission
    /// en cours.</summary>
    public void Eteindre()
    {
        state = TargetState.Eteinte;
        finEclair = 0f;
        Rafraichir();
    }

    // --- collision -------------------------------------------------------------------------------

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Ball"))
        {
            return;
        }

        if (Time.time - dernierHit < cooldown)
        {
            return;
        }

        dernierHit = Time.time;

        // Les points sont versés dans tous les cas : une cible éteinte reste une cible.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(points);
        }

        bool aValide = false;

        if (state == TargetState.Active)
        {
            state = TargetState.Validee;
            aValide = true;
        }

        Eclair();

        if (aValide && MissionManager.Instance != null)
        {
            MissionManager.Instance.NotifyTargetHit(this);
        }

        RenvoyerLaBille(collision);
    }

    /// <summary>
    /// Renvoie la bille. La direction part du contact et non du centre de la cible : une cible
    /// plate et haute donne sinon un renvoi vers le haut qui envoie la bille dans le décor.
    /// </summary>
    private void RenvoyerLaBille(Collision collision)
    {
        Rigidbody balle = collision.rigidbody != null
            ? collision.rigidbody
            : collision.collider.attachedRigidbody;

        if (balle == null)
        {
            return;
        }

        Vector3 direction = balle.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 1e-6f)
        {
            direction = transform.forward;
        }

        direction = direction.normalized;

        // `direction.y` entre 0,2 et 0,25 comme partout ailleurs dans le projet : assez pour
        // décoller la bille de la cible, pas assez pour la faire sortir de l'aire de jeu.
        direction.y = 0.22f;

        balle.AddForce(direction.normalized * impulseForce, ForceMode.Impulse);
    }

    // --- visuel ----------------------------------------------------------------------------------

    private void Eclair()
    {
        finEclair = Time.time + dureeEclair;
        Appliquer(Color.white * 1.6f);
    }

    private void Rafraichir()
    {
        switch (state)
        {
            case TargetState.Active: Appliquer(couleurActive); break;
            case TargetState.Validee: Appliquer(couleurValidee); break;
            default: Appliquer(couleurEteinte); break;
        }
    }

    /// <summary>
    /// Pose la teinte sur les maillages, par blocs de propriétés.
    ///
    /// <para>Un <see cref="MaterialPropertyBlock"/> plutôt que <c>Renderer.material</c> : ce
    /// dernier instancie le matériau pour chaque cible, ce qui multiplierait les draw calls et
    /// ferait perdre le bénéfice du matériau partagé. Le bloc, lui, ne touche pas l'asset.</para>
    ///
    /// <para>L'émission n'apparaît que si le matériau a la case Emission cochée : un bloc de
    /// propriétés ne peut pas activer un mot-clé de shader. La teinte, elle, marche toujours —
    /// c'est donc elle qui porte l'état, et l'émission n'est qu'un bonus.</para>
    /// </summary>
    private void Appliquer(Color couleur)
    {
        if (visuels == null)
        {
            return;
        }

        if (blocs.Count != visuels.Length)
        {
            blocs.Clear();

            for (int i = 0; i < visuels.Length; i++)
            {
                blocs.Add(new MaterialPropertyBlock());
            }
        }

        for (int i = 0; i < visuels.Length; i++)
        {
            Renderer r = visuels[i];

            if (r == null)
            {
                continue;
            }

            MaterialPropertyBlock bloc = blocs[i];
            r.GetPropertyBlock(bloc);

            bloc.SetColor(BaseColorId, couleur);
            bloc.SetColor(EmissionColorId, couleur * intensiteEmission);

            r.SetPropertyBlock(bloc);
        }
    }
}

