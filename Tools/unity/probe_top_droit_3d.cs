// Dégagement 3D de la transition chenal→orbite. LECTURE SEULE.
//
// La carte au niveau bille montre tout bouché en haut à droite : reste à savoir si
// c'est un mur franc (toutes hauteurs) ou un muret bas / un rail haut (passage à une
// autre hauteur). Grille x 3,0…4,8 / z 16,0…17,7, sphère bille r 0,225, hauteurs
// y 0,10…1,00 par pas de 0,10. Coordonnées en LOCAL table.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;

if (rt == null) { return "PinballTable introuvable."; }

float r = 0.225f;

for (float y = 0.10f; y <= 1.001f; y += 0.10f)
{
    sb.AppendLine("--- y = " + y.ToString("F2") + " ---");

    for (float z = 17.7f; z >= 16.0f; z -= 0.15f)
    {
        var ligne = new System.Text.StringBuilder();
        ligne.Append("z " + z.ToString("F2") + " ");

        for (float x = 3.0f; x <= 4.81f; x += 0.15f)
        {
            Vector3 p = rt.TransformPoint(new Vector3(x, y, z));
            bool bloque = Physics.OverlapSphere(p, r, ~0, QueryTriggerInteraction.Ignore).Length > 0;
            ligne.Append(bloque ? '#' : '.');
        }

        sb.AppendLine(ligne.ToString());
    }
}

sb.AppendLine("x : 3,00 à gauche → 4,80 à droite (le chenal est vers 4,3-4,7)");

// Qui bouche ? Un sondage par maillage au point critique (sphère qui touche).
sb.AppendLine("coupables à (4,20 / y 0,24 / 17,05) :");

{
    Vector3 p = rt.TransformPoint(new Vector3(4.20f, 0.24f, 17.05f));
    var hits = Physics.OverlapSphere(p, r, ~0, QueryTriggerInteraction.Ignore);

    foreach (var h in hits)
    {
        var col = h as Collider;
        Vector3 proche = col != null ? col.ClosestPoint(p) : p;
        sb.AppendLine("  " + h.name + " (surface à " + Vector3.Distance(p, proche).ToString("F3") + " u)");
    }

    if (hits.Length == 0) { sb.AppendLine("  (rien — ce point est libre)"); }
}

return sb.ToString();
