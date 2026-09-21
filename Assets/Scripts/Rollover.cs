using UnityEngine;

/// <summary>
/// Un rollover : un interrupteur qu'une bille déclenche en <b>roulant dessus</b>, sans le percuter
/// (GDD §Spinners et rollovers ; Wikipédia, « Pinball » §Components : « Rollovers detect when the
/// ball passes over them »).
///
/// <para>C'est la différence avec une cible : un rollover est <b>à plat dans le plateau</b> et ne
/// renvoie pas la bille. Il ne se contente pas de marquer des points — c'est le composant d'un
/// <see cref="RolloverSet"/>, qui récompense le passage dans <i>toutes</i> les voies.</para>
///
/// <para>Sur une vraie table, les rollovers du haut forment les « top lanes » : la bille tombe de
/// l'orbit dans l'une des trois voies, et les allumer toutes les trois donne un bonus. C'est aussi
/// là que se joue le <i>skill shot</i> : une voie est allumée au lancement, et l'y faire passer
/// donne un bonus immédiat.</para>
/// </summary>
public class Rollover : MonoBehaviour
{
    [Header("Appartenance")]
    [Tooltip("Le groupe de voies auquel appartient ce rollover. Laissé vide, il est cherché sur " +
             "le parent — c'est la disposition normale : trois rollovers sous un même hôte.")]
    [SerializeField] private RolloverSet set;

    [Header("Score")]
    [Tooltip("Points versés à chaque passage. Un rollover est un interrupteur simple : le GDD " +
             "donne 500 points à une « cible fixe simple ».")]
    [SerializeField] private int points = 500;

    [Header("Retour visuel")]
    [Tooltip("Les maillages à teinter. Vide : tous les Renderer du sous-arbre.")]
    [SerializeField] private Renderer[] visuels;

    [SerializeField] private Color couleurEteinte = new Color(0.18f, 0.20f, 0.24f);
    [SerializeField] private Color couleurAllumee = new Color(1f, 0.72f, 0.20f);
    [SerializeField] private Color couleurValidee = new Color(0.32f, 0.92f, 0.38f);

    [Tooltip("Durée de l'éclair au passage, en secondes.")]
    [SerializeField] private float dureeEclair = 0.15f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly System.Collections.Generic.List<MaterialPropertyBlock> blocs
        = new System.Collections.Generic.List<MaterialPropertyBlock>();

    private bool allume;
    private bool valide;
    private float finEclair;

    /// <summary>La voie attend-elle d'être franchie ?</summary>
    public bool IsLit => allume;

    /// <summary>A-t-elle été franchie depuis le dernier remise à zéro ?</summary>
    public bool IsRolled => valide;

    /// <summary>Le groupe auquel appartient ce rollover.</summary>
    public RolloverSet Set => set;

    private void Awake()
    {
        if (set == null) { set = GetComponentInParent<RolloverSet>(); }

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

    // --- état, piloté par le groupe --------------------------------------------------------------

    /// <summary>Allume la voie : elle attend d'être franchie et comptera.</summary>
    public void Allumer()
    {
        allume = true;
        valide = false;
        finEclair = 0f;
        Rafraichir();
    }

    /// <summary>Éteint la voie et oublie son passage.</summary>
    public void Eteindre()
    {
        allume = false;
        valide = false;
        finEclair = 0f;
        Rafraichir();
    }

    // --- passage de la bille ----------------------------------------------------------------------

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball"))
        {
            return;
        }

        // Un rollover marque à CHAQUE passage, allumé ou non — comme une cible, il ne bloque pas
        // les autres systèmes. Seule la progression du groupe dépend de l'allumage.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(points);
        }

        if (allume && !valide)
        {
            valide = true;
        }

        Eclair();

        if (set != null)
        {
            set.NotifyRollover(this);
        }
    }

    // --- visuel ------------------------------------------------------------------------------------

    private void Eclair()
    {
        finEclair = Time.time + dureeEclair;
        Appliquer(Color.white * 1.6f);
    }

    private void Rafraichir()
    {
        if (valide) { Appliquer(couleurValidee); return; }
        if (allume) { Appliquer(couleurAllumee); return; }

        Appliquer(couleurEteinte);
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

            MaterialPropertyBlock bloc = blocs[i];
            r.GetPropertyBlock(bloc);

            bloc.SetColor(BaseColorId, couleur);
            bloc.SetColor(EmissionColorId, couleur * 2.5f);

            r.SetPropertyBlock(bloc);
        }
    }
}
