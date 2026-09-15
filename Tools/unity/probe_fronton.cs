// Le fronton (Screen du caisson) est-il dans le cadre de Main Camera ? LECTURE SEULE.
// C'est la question qui décide si le HUD peut être un Canvas monde sur le panneau.

var sb = new System.Text.StringBuilder();

var scr = GameObject.Find("PinballTable/Table/Pinball_Cabinet/Cabinet/Screen");
if (scr == null) { return "PAS de Screen sous Table/Pinball_Cabinet/Cabinet."; }
var r = scr.GetComponent<Renderer>();
var cam = Camera.main;

sb.AppendLine("Screen transform monde : pos " + scr.transform.position.ToString("F4")
    + " euler " + scr.transform.eulerAngles.ToString("F2")
    + " lossyScale " + scr.transform.lossyScale.ToString("F2"));
sb.AppendLine("Screen right " + scr.transform.right.ToString("F3")
    + " up " + scr.transform.up.ToString("F3")
    + " forward " + scr.transform.forward.ToString("F3"));
var b = r.bounds;
sb.AppendLine("Screen bounds monde : centre " + b.center.ToString("F4") + " taille " + b.size.ToString("F4"));

var rt = GameObject.Find("PinballTable").transform;
sb.AppendLine("Screen centre en repère TABLE : " + rt.InverseTransformPoint(b.center).ToString("F4"));
sb.AppendLine("Screen forward en repère TABLE : " + rt.InverseTransformDirection(scr.transform.forward).ToString("F3"));

if (cam == null) { return sb.ToString() + "\nPAS de Main Camera."; }
sb.AppendLine();
sb.AppendLine("Main Camera pos " + cam.transform.position.ToString("F4") + " euler " + cam.transform.eulerAngles.ToString("F2") + " fov " + cam.fieldOfView + " (vertical)");
var vc = cam.WorldToViewportPoint(b.center);
sb.AppendLine("centre du Screen -> viewport " + vc.ToString("F3") + "   (0..1 = dans le cadre, z > 0 = devant)");

sb.AppendLine("4 coins de la face avant (viewport) :");
for (int i = 0; i < 4; i++)
{
    var p = b.center
        + scr.transform.right * ((i & 1) == 0 ? -1f : 1f) * b.extents.x * 0.92f
        + scr.transform.up * ((i & 2) == 0 ? -1f : 1f) * b.extents.y * 0.92f
        - scr.transform.forward * b.extents.z;
    var v = cam.WorldToViewportPoint(p);
    sb.AppendLine("   coin " + i + " monde " + p.ToString("F3") + " -> viewport " + v.ToString("F3")
        + (v.z > 0 && v.x > 0 && v.x < 1 && v.y > 0 && v.y < 1 ? "  DANS" : "  HORS"));
}

sb.AppendLine();
sb.AppendLine("Repère table : z=0 au drain, mur du fond à " + rt.Find("Table/Pinball_Table") + "" );
var top = rt.Find("Table/Pinball_Table/Walls/Top");
if (top != null)
{
    var tb = top.GetComponent<Renderer>().bounds;
    sb.AppendLine("Walls/Top bounds monde centre " + tb.center.ToString("F4") + " taille " + tb.size.ToString("F4"));
    sb.AppendLine("Walls/Top centre en repère TABLE " + rt.InverseTransformPoint(tb.center).ToString("F4"));
}
var orb = rt.Find("Table/Pinball_Table/Orbit/Rail");
if (orb != null)
{
    var ob = orb.GetComponent<Renderer>().bounds;
    sb.AppendLine("Orbit/Rail bounds monde centre " + ob.center.ToString("F4") + " taille " + ob.size.ToString("F4"));
    sb.AppendLine("Orbit/Rail centre en repère TABLE " + rt.InverseTransformPoint(ob.center).ToString("F4"));
}
var sl = rt.Find("Table/Pinball_Table/Slingshots/Body_L");
if (sl != null)
{
    var sbounds = sl.GetComponent<Renderer>().bounds;
    sb.AppendLine("Body_L centre en repère TABLE " + rt.InverseTransformPoint(sbounds.center).ToString("F4") + " taille monde " + sbounds.size.ToString("F4"));
}
var sr = rt.Find("Table/Pinball_Table/Slingshots/Body_R");
if (sr != null)
{
    var sbounds = sr.GetComponent<Renderer>().bounds;
    sb.AppendLine("Body_R centre en repère TABLE " + rt.InverseTransformPoint(sbounds.center).ToString("F4") + " taille monde " + sbounds.size.ToString("F4"));
}
var surf = rt.Find("Table/Pinball_Table/Playfield/Surface");
if (surf != null)
{
    var sb2 = surf.GetComponent<Renderer>().bounds;
    sb.AppendLine("Playfield/Surface centre en repère TABLE " + rt.InverseTransformPoint(sb2.center).ToString("F4") + " taille monde " + sb2.size.ToString("F4"));
}
var lf = rt.Find("Table/Pinball_Table/Playfield/LaneFloor");
if (lf != null)
{
    var lb = lf.GetComponent<Renderer>().bounds;
    sb.AppendLine("LaneFloor centre en repère TABLE " + rt.InverseTransformPoint(lb.center).ToString("F4") + " taille monde " + lb.size.ToString("F4"));
}

return sb.ToString();
