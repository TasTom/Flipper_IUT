// Qui frotte la bille sur la trajectoire haute ? LECTURE SEULE.
// Version qui exclut la bille elle-même de la détection de contacts.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
var rt = racine.transform;
var gameplay = rt.Find("Gameplay");
var bille = gameplay.Find("Ball");

if (bille == null)
{
    var parTag = GameObject.FindGameObjectWithTag("Ball");
    bille = parTag != null ? parTag.transform : null;
}

if (bille == null) { return "AUCUNE BILLE ?"; }

var corps = bille.GetComponent<Rigidbody>();

Vector3 pos0 = corps.position;
bool kin0 = corps.isKinematic;
bool actif0 = bille.gameObject.activeSelf;

// On laisse la bille telle quelle (inactive, cinématique) : on ne l'active pas.
// On ne mesure que la proximité des autres colliders avec la position fixe de la bille.
// Mais on veut parcourir la trajectoire, donc on doit déplacer un point de test.
// On utilise une sphère témoin posée sur la trajectoire, mais on n'active pas la bille réelle.

float r = 0.226f;

sb.AppendLine("stations z=17,42 / y=0,24 (maillages à < 0,226 u du centre, bille exclue) :");

for (float x = 4.3f; x >= -3.01f; x -= 0.3f)
{
    // Position de test sur la trajectoire
    Vector3 posTest = rt.TransformPoint(new Vector3(x, 0.24f, 17.42f));
    Physics.SyncTransforms();

    var hits = Physics.OverlapSphere(posTest, r, ~0, QueryTriggerInteraction.Ignore);
    var ligne = new System.Text.StringBuilder();
    ligne.Append("x " + x.ToString("F2") + " : ");

    // Filtrer les hits provenant de la bille elle-même (au cas où elle serait active)
    var hitsFiltres = new System.Collections.Generic.List<Collider>();
    foreach (var h in hits)
    {
        if (h.gameObject == bille.gameObject) continue;
        hitsFiltres.Add(h);
    }

    if (hitsFiltres.Count == 0) { ligne.Append("libre"); }
    else
    {
        foreach (var h in hitsFiltres)
        {
            var col = h as Collider;
            float d = col != null ? Vector3.Distance(posTest, col.ClosestPoint(posTest)) : -1f;
            ligne.Append(h.name + "@" + (d * 1000f / 16.66667f).ToString("F1") + "mm  ");
        }
    }

    sb.AppendLine(ligne.ToString());
}

// Restaurer l'état original de la bille (au cas où)
corps.position = pos0;
corps.isKinematic = kin0;
bille.gameObject.SetActive(actif0);
Physics.SyncTransforms();

return sb.ToString();