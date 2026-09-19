using UnityEngine;

/// <summary>
/// Inclinaison du plateau (GDD §Plateau).
///
/// La table est <b>réellement</b> inclinée dans la scène : le root <c>PinballTable</c> porte une
/// rotation de -7° autour de X, et la gravité reste verticale. C'est ce qui donne des surfaces
/// réellement en pente.
///
/// L'ancien réglage penchait la gravité en laissant la géométrie parfaitement plate. Les deux
/// donnent la même gravité <i>dans le repère de la table</i> — (0, -9.74, -1.20) — mais seule une
/// pente réelle fait glisser la bille le long d'un obstacle. Avec une gravité penchée sur un sol
/// plat, la bille s'arrête net contre le premier collider rencontré vers l'aval, et rien ne peut
/// plus la déloger : c'est ce qui bloquait les parties.
///
/// Ce composant ne penche donc plus rien. Il garantit une gravité verticale et vérifie que la table
/// a bien la pente attendue — l'inclinaison elle-même est posée par le menu
/// <c>Flipper &gt; Incliner la table à 7°</c>.
/// </summary>
public class TableGravity : MonoBehaviour
{
    [Tooltip("Intensité de la gravité, en unités par seconde au carré.")]
    [SerializeField] private float gravityStrength = 163.5f;

    [Tooltip("Inclinaison attendue de la table, en degrés.")]
    [SerializeField] private float tiltAngle = 7f;

    [Tooltip("Root incliné de la table. Vide : recherché par son nom au démarrage.")]
    [SerializeField] private Transform tiltedRoot;

    private const string TableRootName = "PinballTable";

    // ── L'échelle de la table, et pourquoi ces trois valeurs ────────────────────────────
    //
    // La table n'est pas à l'échelle 1:1 : elle fait 60 mm par unité (la bille mesure 0,45 u et
    // représente 27 mm réels, l'ancre du projet depuis le début). Or la gravité était réglée à
    // 9,81 — un chiffre qui vaut 9,81 m/s² seulement si 1 unité = 1 mètre.
    //
    // Appliquée telle quelle, elle donnait 0,59 m/s² en réel : une bille qui flotte, un jeu
    // 4,3 fois trop lent (mesuré : 1 u franchie en 0,48 s au lieu de 0,11). Toutes les vitesses
    // du projet en découlaient, d'où un plongeur qui pousse 1,1 m/s au lieu de 4 à 6.
    //
    // La référence : `VisualPinball.Engine` travaille en unités réelles et sa gravité vaut
    // 1,81751 pour 9,81 m/s². Voir `Assets/References/PHYSIQUE_REFERENCE_VPE.md`.
    //
    //   9,81 m/s² ÷ 0,06 m/u  =  163,5 u/s²
    //
    // Le pas physique suit : à 50 Hz, la bille parcourt 4,4 diamètres entre deux calculs — elle
    // traverse les murs et les collisions sont résolues après coup. 200 Hz ramène ça à 1,1
    // diamètre, et 12 itérations de solveur stabilisent les empilements de contacts qu'un pas
    // plus court fait apparaître.
    private const float PasPhysique = 1f / 200f;
    private const int IterationsSolveur = 12;

    /// <summary>Inclinaison attendue de la table, en degrés.</summary>
    public float TiltAngle => tiltAngle;

    private void Awake()
    {
        Transform root = ResolveRoot();
        Physics.gravity = CalculateGravity(root);

        // Le pas et le solveur vont avec la gravité : les trois forment un seul réglage.
        Time.fixedDeltaTime = PasPhysique;
        Physics.defaultSolverIterations = IterationsSolveur;

        if (root == null)
        {
            Debug.LogWarning($"[TableGravity] Aucun '{TableRootName}' dans la scène : impossible de " +
                             "vérifier la pente de la table.", this);
            return;
        }

        // eulerAngles ramène -7° à 353° ; DeltaAngle redonne bien -7.
        float actual = Mathf.DeltaAngle(0f, root.eulerAngles.x);
        float expected = -tiltAngle;

        if (Mathf.Abs(actual - expected) > 0.5f)
        {
            Debug.LogWarning(
                $"[TableGravity] '{root.name}' est incliné de {actual:F1}° au lieu de {expected:F1}°.\n" +
                "Avec une table plate, une bille s'arrête définitivement contre le premier obstacle " +
                "rencontré vers l'aval et la partie se bloque.\n" +
                "Menu Flipper > Incliner la table à 7° pour corriger.", this);
        }
    }

    private Vector3 CalculateGravity(Transform root)
    {
        if (root == null)
        {
            return Vector3.down * gravityStrength;
        }

        float actual = Mathf.DeltaAngle(0f, root.eulerAngles.x);
        float expected = -tiltAngle;

        if (Mathf.Abs(actual - expected) <= 0.5f)
        {
            return Vector3.down * gravityStrength;
        }

        // Compatibilité avec les scènes legacy dont le plateau est encore horizontal :
        // la composante -Z locale remplace la pente absente et empêche la bille de remonter.
        Vector3 tableDownhill = new Vector3(
            0f,
            -Mathf.Cos(tiltAngle * Mathf.Deg2Rad),
            -Mathf.Sin(tiltAngle * Mathf.Deg2Rad));
        return root.TransformDirection(tableDownhill.normalized) * gravityStrength;
    }

    private Transform ResolveRoot()
    {
        if (tiltedRoot != null)
        {
            return tiltedRoot;
        }

        GameObject found = GameObject.Find(TableRootName);
        return found != null ? found.transform : null;
    }
}

