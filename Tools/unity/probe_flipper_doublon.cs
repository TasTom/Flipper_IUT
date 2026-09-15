// Compte les maillages sous chaque Flipper_Bat (chasse au doublon). LECTURE SEULE.
var sb = new System.Text.StringBuilder();
var g = GameObject.Find("PinballTable/Gameplay");
foreach (var n in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    var bat = g != null ? g.transform.Find(n + "/Flipper_Bat") : null;
    if (bat == null) { sb.AppendLine(n + " : BAT ABSENT"); continue; }
    sb.AppendLine(n + " : " + bat.childCount + " enfant(s)");
    foreach (Transform e in bat)
    {
        sb.AppendLine("  - " + e.name + " : "
            + e.GetComponentsInChildren<MeshFilter>().Length + " MeshFilter, "
            + e.GetComponentsInChildren<MeshRenderer>().Length + " Renderer, pos "
            + e.localPosition.ToString("F4"));
    }
}
return sb.ToString();
