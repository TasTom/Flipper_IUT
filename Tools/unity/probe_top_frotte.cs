// Qui frotte la bille sur la trajectoire haute ? LECTURE SEULE.
//
// La bille à 8 u/s rampe le long de z ≈ 17,40 (x 4,3 → −3,0 en 8 s) au lieu de rouler.
// On pose la bille (sans vitesse) sur cette trajectoire et on relève, par station,
// les maillages à moins d'un rayon (contact ou quasi-contact).

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

var corps = bille.GetComponent<Rigidbody>();

Vector3 pos0 = corps.position;
bool kin0 = corps.isKinematic;
bool actif0 = bille.gameObject.activeSelf;

bille.gameObject.SetActive(true);
corps.isKinematic = true;   // posée, pas de chute : on ne mesure que la proximité

float r = 0.226f;

sb.AppendLine("stations z=17,42 / y=0,24 (maillages à < 0,226 u du centre) :");

for (float x = 4.3f; x >= -3.01f; x -= 0.3f)
{
    corps.position = rt.TransformPoint(new Vector3(x, 0.24f, 17.42f));
    Physics.SyncTransforms();

    var hits = Physics.OverlapSphere(corps.position, r, ~0, QueryTriggerInteraction.Ignore);
    var ligne = new System.Text.StringBuilder();
    ligne.Append("x " + x.ToString("F2") + " : ");

    if (hits.Length == 0) { ligne.Append("libre"); }
    else
    {
        foreach (var h in hits)
        {
            var col = h as Collider;
            float d = col != null ? Vector3.Distance(corps.position, col.ClosestPoint(corps.position)) : -1f;
            ligne.Append(h.name + "@" + (d * 1000f / 16.66667f).ToString("F1") + "mm  ");
        }
    }

    sb.AppendLine(ligne.ToString());
}

corps.position = pos0;
corps.isKinematic = kin0;
bille.gameObject.SetActive(actif0);

return sb.ToString();
