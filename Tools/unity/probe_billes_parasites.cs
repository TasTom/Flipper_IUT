// Y a-t-il des billes parasites dans la scène ? LECTURE SEULE.

var sb = new System.Text.StringBuilder();
var racine = GameObject.Find("PinballTable");
var rt = racine.transform;

sb.AppendLine("objets taggés 'Ball' :");

foreach (var g in GameObject.FindGameObjectsWithTag("Ball"))
{
    Vector3 local = rt.InverseTransformPoint(g.transform.position);
    var corps = g.GetComponent<Rigidbody>();
    sb.AppendLine("  '" + g.name + "' parent '" + (g.transform.parent != null ? g.transform.parent.name : "(racine)")
        + "' local (" + local.x.ToString("F3") + ", " + local.y.ToString("F3") + ", " + local.z.ToString("F3") + ")"
        + " actif=" + g.activeSelf
        + (corps != null ? " v=" + corps.linearVelocity.magnitude.ToString("F2") : " (sans Rigidbody)"));
}

sb.AppendLine("objets NOMMÉS '*all*' (tous parents) :");

foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>())
{
    if (t.name.ToLower().Contains("ball"))
    {
        Vector3 local = rt.InverseTransformPoint(t.position);
        sb.AppendLine("  '" + t.name + "' parent '" + (t.parent != null ? t.parent.name : "(racine)")
            + "' local (" + local.x.ToString("F3") + ", " + local.y.ToString("F3") + ", " + local.z.ToString("F3") + ")"
            + " actif=" + t.gameObject.activeSelf);
    }
}

return sb.ToString();
