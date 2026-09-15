// Limites sérialisées des joints + jour nécessaire au talon. LECTURE SEULE.
//
// Le balayage simulé part en windmilling (course 350°) : soit les limites sérialisées sont
// larges (résidus procéduraux — `Flipper.Update` ne les impose qu'en Play), soit le coin du
// talon laboure le pied du slingshot. Cette sonde lit les deux faits :
// 1. `useLimits`, `limits.min/max`, `useSpring` de chaque `HingeJoint` (état sérialisé réel) ;
// 2. le jour : la boîte du bat, décalée vers le centre de 0 à 0,12 u par pas de 0,03,
//    touche-t-elle encore le mur (neutre + repos + actif) ? Le premier offset libre borne
//    la profondeur du chevauchement.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null) { return "'PinballTable/Gameplay' introuvable."; }

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    bool aGauche = nom.Contains("Left");
    float sens = aGauche ? 1f : -1f;
    float repos = aGauche ? -30f : 30f;
    float actif = aGauche ? 30f : -30f;

    sb.AppendLine("=== " + nom + " ===");

    var pivot = gameplay.Find(nom);

    if (pivot == null) { sb.AppendLine("pivot absent"); continue; }

    var joint = pivot.GetComponent<HingeJoint>();

    if (joint == null) { sb.AppendLine("pas de HingeJoint"); continue; }

    sb.AppendLine("joint : useLimits=" + joint.useLimits
                  + " min=" + joint.limits.min.ToString("F1")
                  + " max=" + joint.limits.max.ToString("F1")
                  + "   useSpring=" + joint.useSpring
                  + " target=" + joint.spring.spring.ToString("F0"));

    var porteur = pivot.Find("Flipper_Bat");
    var bat = porteur != null ? porteur.Find("Bat_Mesh") : null;

    if (bat == null) { sb.AppendLine("pas de Bat_Mesh"); continue; }

    Bounds boite = default(Bounds);
    bool premier = true;

    foreach (var filtre in bat.GetComponentsInChildren<MeshFilter>())
    {
        if (filtre.sharedMesh == null) { continue; }

        var m = filtre.sharedMesh.bounds;

        for (int i = 0; i < 8; i++)
        {
            var p = pivot.InverseTransformPoint(filtre.transform.TransformPoint(new Vector3(
                (i & 1) == 0 ? m.min.x : m.max.x,
                (i & 2) == 0 ? m.min.y : m.max.y,
                (i & 4) == 0 ? m.min.z : m.max.z)));

            if (premier) { boite = new Bounds(p, Vector3.zero); premier = false; }
            else { boite.Encapsulate(p); }
        }
    }

    foreach (var offset in new float[] { 0f, 0.03f, 0.06f, 0.09f, 0.12f })
    {
        string ligne = "  offset +" + offset.ToString("F2") + " (" + (offset * 1000f / 16.66667f).ToString("F1") + " mm) :";

        foreach (var a in new float[] { 0f, repos, actif })
        {
            var q = Quaternion.AngleAxis(a, Vector3.forward);
            var centre = boite.center + new Vector3(sens * offset, 0f, 0f);
            var c = pivot.TransformPoint(q * centre);
            var hits = Physics.OverlapBox(c, boite.size * 0.5f, pivot.rotation * q, ~0,
                                          QueryTriggerInteraction.Ignore);

            int n = 0;

            foreach (var h in hits)
            {
                if (h.name == "Body_L" || h.name == "Body_R") { n++; }
            }

            ligne += "  " + a + "°:" + (n == 0 ? "libre" : "MUR");
        }

        sb.AppendLine(ligne);
    }
}

return sb.ToString();
