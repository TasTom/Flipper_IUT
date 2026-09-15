using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rend la scène active <b>neutre</b> : les hôtes de gameplay restent, à leur place et avec
/// leurs scripts, mais on leur retire toute la géométrie que <c>EnsureMvpStructure</c> avait
/// générée (primitives, colliders, matériaux).
///
/// Pourquoi : les hôtes doivent rester trouvables par les scripts — c'est le contrat de scène
/// (voir claude.md) — mais la table, elle, se construit désormais avec de vrais assets. Garder
/// une sphère de 0,62 à la place d'un bumper importé fausserait l'échelle et la physique.
///
/// Ce que fait le menu :
/// <list type="bullet">
/// <item>complète les gestionnaires que <c>EnsureMvpStructure</c> ne crée pas
/// (<c>GameManager</c>, <c>MissionManager</c>, <c>MultiballManager</c>) et remplit les
/// références de bille restées vides ;</item>
/// <item>crée <c>BallSpawnPoint</c> s'il manque ;</item>
/// <item>retire colliders, filtres et rendus de chaque hôte, et remet son échelle à 1 ;</item>
/// <item>vide <c>Flipper_Bat</c> mais le conserve : le contrat veut que la batte soit un enfant
/// du pivot, et c'est là que viendra l'asset.</item>
/// </list>
///
/// <para>
/// <b>Un hôte sans collider a demandé de retirer des attributs.</b> <c>Bumper</c>,
/// <c>Slingshot</c> et <c>DrainZone</c> portaient <c>[RequireComponent(typeof(Collider))]</c> :
/// Unity refuse alors de supprimer le collider tant que le script est là, et le rajoute dès
/// qu'on ajoute le script. Un hôte vide était donc impossible. L'attribut a été retiré des
/// trois — aucun ne déréférence son collider, sauf <c>DrainZone</c> qui a reçu des gardes.
/// Sans collider, ces composants sont simplement muets, ce qui est l'état voulu ici.
/// </para>
///
/// La scène n'est <b>pas</b> enregistrée : comme tous les générateurs du projet, ce menu laisse
/// la main (Ctrl+S).
/// </summary>
public static class NeutralScene
{
    private const string MenuNeutralize = "Flipper/Rendre la scène neutre (hôtes vides)";

    /// <summary>Hôtes dont la géométrie générée doit disparaître.</summary>
    private static readonly string[] Hosts =
    {
        "Flipper_Left_Pivot", "Flipper_Right_Pivot",
        "Slingshot_Left", "Slingshot_Right",
        "Bumper_01", "Bumper_02", "Bumper_03",
        "Plunger", "DrainZone",
    };

    /// <summary>Enfants d'hôte à vider de leur primitive mais à conserver comme Transform.</summary>
    private static readonly string[] KeptChildren = { "Flipper_Bat" };

    [MenuItem(MenuNeutralize)]
    public static void Neutralize()
    {
        List<string> log = new List<string>();

        EnsureManager<GameManager>("GameManager", log);
        EnsureManager<MissionManager>("MissionManager", log);
        EnsureManager<MultiballManager>("MultiballManager", log);
        EnsureBallSpawnPoint(log);

        // Le GameManager vient peut-être d'être créé : ses références de bille sont alors
        // vides, et sans elles aucune bille n'apparaîtrait dans la scène. Non destructif :
        // un champ déjà assigné est conservé tel quel.
        EnsureMvpStructure.FillEmptyGameManagerFields();
        log.Add("GameManager : références de bille complétées si elles étaient vides");

        int stripped = 0;

        foreach (string hostName in Hosts)
        {
            GameObject host = Find(hostName);

            if (host == null)
            {
                log.Add($"absent : {hostName}");
                continue;
            }

            stripped += Strip(host, log);

            // Une échelle héritée de la primitive se reporterait sur l'asset importé.
            if (host.transform.localScale != Vector3.one)
            {
                host.transform.localScale = Vector3.one;
            }

            foreach (string childName in KeptChildren)
            {
                Transform child = host.transform.Find(childName);

                if (child == null)
                {
                    continue;
                }

                stripped += Strip(child.gameObject, log);
                child.localScale = Vector3.one;
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log(
            $"[NeutralScene] Scène neutre : {stripped} composant(s) de géométrie retiré(s) sur " +
            $"{Hosts.Length} hôte(s).\n" +
            string.Join("\n", log) + "\n" +
            "Les hôtes gardent leur nom, leur position et leurs scripts : les assets viennent " +
            "se poser dedans.\n" +
            "Tout composant marqué ⚠ ci-dessus a résisté au retrait et demande une action.\n" +
            "Rien n'a été enregistré : Ctrl+S pour conserver.");
    }

    /// <summary>
    /// Retire d'un objet tout ce qui produit de la forme ou de la collision, et rend le nombre
    /// de composants <b>réellement</b> retirés. Le GameObject lui-même n'est jamais détruit :
    /// c'est le nom que les scripts cherchent.
    ///
    /// Le compte est mesuré, pas déclaratif. Unity refuse de supprimer un composant qu'un
    /// script exige (<c>[RequireComponent]</c>) : compter ce qu'on a demandé de retirer
    /// annoncerait un succès qui n'a pas eu lieu — c'est exactement l'erreur qui a masqué le
    /// premier passage de ce menu. On relit donc les composants après coup, et ce qui survit
    /// est signalé nommément.
    /// </summary>
    private static int Strip(GameObject target, List<string> log)
    {
        int before = CountGeometry(target);

        foreach (Collider collider in target.GetComponents<Collider>())
        {
            Object.DestroyImmediate(collider);
        }

        foreach (MeshRenderer renderer in target.GetComponents<MeshRenderer>())
        {
            Object.DestroyImmediate(renderer);
        }

        foreach (MeshFilter filter in target.GetComponents<MeshFilter>())
        {
            Object.DestroyImmediate(filter);
        }

        int left = CountGeometry(target);

        if (left > 0)
        {
            log.Add($"  ⚠ {target.name} : {left} composant(s) ont résisté au retrait " +
                    $"({ListGeometry(target)}) — un script les exige. Retirer son " +
                    "[RequireComponent], ou les désactiver à la main.");
        }

        return before - left;
    }

    private static int CountGeometry(GameObject target)
    {
        return target.GetComponents<Collider>().Length
             + target.GetComponents<MeshRenderer>().Length
             + target.GetComponents<MeshFilter>().Length;
    }

    private static string ListGeometry(GameObject target)
    {
        List<string> names = new List<string>();

        foreach (Collider collider in target.GetComponents<Collider>())
        {
            names.Add(collider.GetType().Name);
        }

        foreach (MeshRenderer renderer in target.GetComponents<MeshRenderer>())
        {
            names.Add(renderer.GetType().Name);
        }

        foreach (MeshFilter filter in target.GetComponents<MeshFilter>())
        {
            names.Add(filter.GetType().Name);
        }

        return string.Join(", ", names);
    }

    private static void EnsureManager<T>(string name, List<string> log) where T : Component
    {
        if (Object.FindFirstObjectByType<T>(FindObjectsInactive.Include) != null)
        {
            log.Add($"conservé : {name}");
            return;
        }

        var host = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(host, "Rendre la scène neutre");
        host.AddComponent<T>();
        log.Add($"créé : {name} (gestionnaire)");
    }

    private static void EnsureBallSpawnPoint(List<string> log)
    {
        if (Find("BallSpawnPoint") != null)
        {
            log.Add("conservé : BallSpawnPoint");
            return;
        }

        var spawn = new GameObject("BallSpawnPoint");
        Undo.RegisterCreatedObjectUndo(spawn, "Rendre la scène neutre");
        log.Add("créé : BallSpawnPoint (Transform seul)");
    }

    /// <summary>
    /// Cherche un objet par nom, actifs et inactifs compris. <c>GameObject.Find</c> ignore les
    /// objets désactivés, ce qui ferait créer un doublon au lieu de compléter l'existant.
    /// </summary>
    private static GameObject Find(string name)
    {
        foreach (GameObject candidate in Object.FindObjectsByType<GameObject>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.name == name)
            {
                return candidate;
            }
        }

        return null;
    }
}
