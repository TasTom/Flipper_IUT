// Pourquoi la bille traverse-t-elle la table ?
//
// Le test de chute donne une chute **libre parfaite** : x et z constants au millième, 2,578 u
// parcourus en 0,70 s, soit exactement ½·g·t². La bille n'a donc rencontré aucun collider.
//
// Trois causes possibles, et elles ne se distinguent pas à l'œil :
//
// 1. **Les `MeshCollider` n'ont pas de maillage.** Un `MeshCollider` dont `sharedMesh` est nul
//    ne collisionne avec rien — et il compte pourtant dans « 41 colliders ». C'est la cause la
//    plus probable vu que le compte est bon mais l'effet nul.
// 2. **Ils sont désactivés** (`enabled = false`), ou sur un GameObject inactif.
// 3. **La matrice de collision** les sépare de la bille — peu probable (tout se croise par
//    défaut, et les layers viennent d'être ajoutés), mais ça se lit en une ligne.
//
// Et une quatrième, qui ne serait pas un défaut de la table mais du **test** : en mode Édition,
// la scène physique peut être en retard sur les transforms. D'où la sonde `SyncTransforms` :
// on lance un rayon avant et après, sur la même scène, et on compare.
//
// La sonde ne répare rien, elle constate — et elle le fait en mode Édition, sans rien instancier
// tant que la lecture de la table n'a pas parlé.

var sb = new System.Text.StringBuilder();

// --- 1. la table elle-même --------------------------------------------------------------------

var root = GameObject.Find("PinballTable");
var table = root != null ? root.transform.Find("Table/Pinball_Table") : null;

sb.AppendLine("=== structure ===");
sb.AppendLine("PinballTable              : "
              + (root != null ? root.transform.eulerAngles.ToString("F2") + "   échelle " + root.transform.lossyScale.ToString("F4")
                              : "ABSENT"));

if (table == null)
{
    sb.AppendLine("PinballTable/Table/Pinball_Table : ABSENT");
    return sb.ToString();
}

sb.AppendLine("Table/Pinball_Table       : actif " + table.gameObject.activeInHierarchy
              + "   échelle monde " + table.lossyScale.ToString("F4")
              + "   échelle locale " + table.localScale.ToString("F4"));

// --- 2. les colliders, un par un --------------------------------------------------------------

var colliders = table.GetComponentsInChildren<Collider>(true);

int sansMaillage = 0;
int desactives = 0;
int triggers = 0;
int horsHierarchie = 0;
int maillagesVides = 0;

var parType = new System.Collections.Generic.SortedDictionary<string, int>();
var boite = new Bounds();
bool premiere = true;
var exemples = new System.Text.StringBuilder();

foreach (var collider in colliders)
{
    string type = collider.GetType().Name;

    if (!parType.ContainsKey(type)) { parType[type] = 0; }
    parType[type]++;

    if (!collider.enabled) { desactives++; }
    if (collider.isTrigger) { triggers++; }
    if (!collider.gameObject.activeInHierarchy) { horsHierarchie++; }

    if (collider is MeshCollider meshCollider)
    {
        if (meshCollider.sharedMesh == null) { sansMaillage++; }
        else if (meshCollider.sharedMesh.vertexCount == 0) { maillagesVides++; }
    }

    if (premiere) { boite = collider.bounds; premiere = false; }
    else { boite.Encapsulate(collider.bounds); }

    // Les cinq premiers suffisent à voir la forme du problème.
    if (exemples.Length < 5)
    {
        exemples.AppendLine("    " + collider.gameObject.name.PadRight(26)
                            + type.PadRight(14)
                            + " actif " + collider.enabled
                            + "   boîte " + collider.bounds.size.ToString("F4")
                            + "   centre " + collider.bounds.center.ToString("F4"));

        if (collider is MeshCollider mc)
        {
            exemples.AppendLine("        maillage : "
                                + (mc.sharedMesh != null
                                   ? mc.sharedMesh.name + " (" + mc.sharedMesh.vertexCount + " sommets)"
                                   : "AUCUN")
                                + "   convex " + mc.convex);
        }
    }
}

sb.AppendLine();
sb.AppendLine("=== colliders ===");
sb.AppendLine("total                     : " + colliders.Length);

foreach (var entry in parType)
{
    sb.AppendLine("  " + entry.Key.PadRight(22) + entry.Value);
}

sb.AppendLine("désactivés                : " + desactives);
sb.AppendLine("en déclencheur            : " + triggers);
sb.AppendLine("hors hiérarchie active    : " + horsHierarchie);
sb.AppendLine("MeshCollider sans maillage: " + sansMaillage);
sb.AppendLine("MeshCollider maillage vide: " + maillagesVides);

if (colliders.Length > 0)
{
    sb.AppendLine("boîte englobante (monde)  : " + boite.size.ToString("F4")
                  + "   centre " + boite.center.ToString("F4"));
}

sb.AppendLine("exemples :");
sb.Append(exemples.ToString());

// --- 3. la matrice de collision ----------------------------------------------------------------

int ballLayer = LayerMask.NameToLayer("Ball");

sb.AppendLine();
sb.AppendLine("=== matrice de collision (layer " + ballLayer + " = Ball) ===");

if (ballLayer < 0)
{
    sb.AppendLine("  layer 'Ball' introuvable");
}
else
{
    for (int i = 0; i < 32; i++)
    {
        string nom = LayerMask.LayerToName(i);

        if (string.IsNullOrEmpty(nom)) { continue; }

        bool ignore = Physics.GetIgnoreLayerCollision(ballLayer, i);

        if (ignore) { sb.AppendLine("  IGNORÉ avec " + i + " (" + nom + ")"); }
    }

    sb.AppendLine("  (seules les paires ignorées sont listées — aucune ligne = tout se croise)");
}

// --- 4. des rayons verticaux, avant et après synchronisation ------------------------------------

var spawn = GameObject.Find("BallSpawnPoint");
Vector3 from = spawn != null ? spawn.transform.position : new Vector3(4.67f, 0.51f, 0.94f);

sb.AppendLine();
sb.AppendLine("=== rayons vers le bas ===");

Vector3[] sondes =
{
    new Vector3(from.x, from.y + 0.2f, from.z),   // sous le point d'apparition
    new Vector3(4.67f, 3f, 0.94f),                // le couloir, de haut
    new Vector3(0f, 3f, 0f),                      // le centre du plateau
    new Vector3(0f, 3f, -3f),                     // vers les flippers
    new Vector3(-4f, 3f, 5f),                     // coin gauche
};

for (int passe = 0; passe < 2; passe++)
{
    if (passe == 1) { Physics.SyncTransforms(); }

    sb.AppendLine(passe == 0 ? "  — sans SyncTransforms —" : "  — après SyncTransforms —");

    foreach (var origine in sondes)
    {
        RaycastHit hit;

        bool touche = Physics.Raycast(origine, Vector3.down, out hit, 20f, ~0, QueryTriggerInteraction.Ignore);

        sb.AppendLine("    depuis " + origine.ToString("F2").PadRight(24)
                      + (touche
                         ? "TOUCHE " + hit.collider.gameObject.name + " à y " + hit.point.y.ToString("F4")
                           + "   (" + hit.collider.GetType().Name + ")"
                         : "rien"));
    }
}

// --- 5. le maillage du plateau, vu par Unity ---------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== maillages de la table ===");

var filters = table.GetComponentsInChildren<MeshFilter>(true);
int avecMaillage = 0;

foreach (var filter in filters) { if (filter.sharedMesh != null) { avecMaillage++; } }

sb.AppendLine("MeshFilter                : " + filters.Length + "   dont avec maillage : " + avecMaillage);

var renderers = table.GetComponentsInChildren<MeshRenderer>(true);
sb.AppendLine("MeshRenderer              : " + renderers.Length);

int renderersActifs = 0;

foreach (var renderer in renderers) { if (renderer.enabled) { renderersActifs++; } }

sb.AppendLine("  dont actifs             : " + renderersActifs);
sb.AppendLine("SkinnedMeshRenderer       : " + table.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length);

return sb.ToString();
