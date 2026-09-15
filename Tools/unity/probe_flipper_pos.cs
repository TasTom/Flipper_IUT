// Où sont les pivots après le windmilling ? LECTURE SEULE.
//
// La sonde du jour dit "libre partout" là où la pose disait "touche au repos" : le balayage
// simulé (rotations multiples + contacts violents) a pu faire DÉRIVER les Rigidbody, et la
// restauration du test ne couvrait que la rotation — pas la position (les mouvements physiques
// ne passent pas par la pile Undo). Référence : PlaceImportedTable pose les pivots en
// (±1,428 ; 0 ; 1,533) local Gameplay.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay == null) { return "'PinballTable/Gameplay' introuvable."; }

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    var pivot = gameplay.Find(nom);

    if (pivot == null) { sb.AppendLine(nom + " : ABSENT"); continue; }

    float sx = nom.Contains("Left") ? -1.428f : 1.428f;

    var attendu = new Vector3(sx, 0f, 1.533f);
    var ecart = pivot.localPosition - attendu;

    sb.AppendLine(nom);
    sb.AppendLine("  pos locale " + pivot.localPosition.ToString("F4")
                  + "   attendu " + attendu.ToString("F4")
                  + "   écart " + ecart.ToString("F4")
                  + (ecart.magnitude > 0.001f ? "   ⚠ DÉRIVE" : "   ✓"));
    sb.AppendLine("  rot locale " + pivot.localEulerAngles.ToString("F2"));

    var joint = pivot.GetComponent<HingeJoint>();

    if (joint != null) { sb.AppendLine("  hinge.angle = " + joint.angle.ToString("F1") + "°"); }

    var corps = pivot.GetComponent<Rigidbody>();

    if (corps != null)
    {
        sb.AppendLine("  Rigidbody : pos " + corps.position.ToString("F4")
                      + "   dort=" + corps.IsSleeping());
    }
}

return sb.ToString();
