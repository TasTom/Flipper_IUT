// Borne la profondeur du contact talon ↔ slingshot. LECTURE SEULE.
//
// La pose v2 rapporte une garde de 0,0 mm aux coins du talon : un coin de la boîte touche le
// mur. Mais un coin de boîte englobante dépasse d'un talon arrondi d'environ (√2−1)×r ≈ 5 mm :
// il faut distinguer le baiser de la boîte (bénin, le mesh passe) de la pénétration du mesh.
//
// Trois mesures, par côté :
// 1. distance CENTRE du talon → mur : si elle dépasse le rayon du talon (~0,2165 u), le mesh
//    ne touche pas et seul le coin sharp de la boîte frotte ;
// 2. OverlapBox au repos avec la boîte RÉDUITE de 0,01 u (0,6 mm) puis 0,03 u (1,8 mm) : si ça
//    touche encore à −0,03, la pénétration dépasse 1,8 mm et le mesh est suspect ;
// 3. le point du mur le plus proche du centre (pour situer : face interne ? arête ?).

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

    sb.AppendLine("=== " + nom + " (repos " + repos + "°) ===");

    var pivot = gameplay.Find(nom);

    if (pivot == null) { sb.AppendLine("pivot absent"); continue; }

    var porteur = pivot.Find("Flipper_Bat");

    if (porteur == null) { sb.AppendLine("pas de Flipper_Bat"); continue; }

    var bat = porteur.Find("Bat_Mesh");

    if (bat == null) { sb.AppendLine("pas de Bat_Mesh"); continue; }

    // Boîte du bat en repère pivot (8 coins, comme la pose).
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

    var corps = GameObject.Find("PinballTable/Table/Pinball_Table/Slingshots/"
                                + (aGauche ? "Body_L" : "Body_R"));

    if (corps == null) { sb.AppendLine("mur introuvable"); continue; }

    var colMur = corps.GetComponent<Collider>();

    if (colMur == null) { sb.AppendLine("mur sans collider"); continue; }

    // 1. Centre du talon → mur.
    float hx = sens > 0f ? boite.min.x : boite.max.x;
    var centreTalon = new Vector3(hx, boite.center.y, boite.center.z);
    var centreMonde = pivot.TransformPoint(centreTalon);
    var proche = colMur.ClosestPoint(centreMonde);
    float dCentre = Vector3.Distance(centreMonde, proche);

    sb.AppendLine("centre du talon → mur : " + (dCentre * 1000f / 16.66667f).ToString("F1")
                  + " mm   (rayon du talon ≈ " + (boite.size.y * 0.5f * 1000f / 16.66667f).ToString("F1")
                  + " mm)");
    sb.AppendLine("  point du mur : " + proche.ToString("F4") + " monde");

    var q = Quaternion.AngleAxis(repos, Vector3.forward);
    var rotMonde = pivot.rotation * q;

    // 2. Boîte réduite au repos : ça touche encore ?
    foreach (var inset in new float[] { 0f, 0.01f, 0.03f })
    {
        var demi = boite.size * 0.5f - new Vector3(inset, inset, inset);

        if (demi.x <= 0f || demi.y <= 0f || demi.z <= 0f)
        {
            sb.AppendLine("  inset " + inset.ToString("F2") + " : boîte vide — ignoré");
            continue;
        }

        var c = pivot.TransformPoint(q * boite.center);
        var hits = Physics.OverlapBox(c, demi, rotMonde, ~0, QueryTriggerInteraction.Ignore);

        string qui = "";

        foreach (var h in hits) { qui += h.name + " "; }

        sb.AppendLine("  inset " + inset.ToString("F2") + " (" + (inset * 1000f / 16.66667f).ToString("F1")
                      + " mm) : " + (hits.Length == 0 ? "LIBRE ✓" : "touche " + qui));
    }
}

return sb.ToString();
