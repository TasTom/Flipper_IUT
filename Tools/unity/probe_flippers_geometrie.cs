// Où sont VRAIMENT les flippers ? LECTURE SEULE.
//
// `probe_etat_scene.cs` donne `Flipper_Left_Pivot` à local (-0,2021 / 0,1819 / 1,0313)
// alors que `build_table.py::report_hosts` annonce (-1,428 / 0 / 1,533) — et que le
// collider du bat, lui, est centré à (∓0,73 / 0,22 / 1,53), ce qui est cohérent avec
// un pivot à ∓1,428 et un bat de 1,36 u. Les deux ne peuvent pas être vrais.
// On relève donc la chaîne complète : pivot, enfant, HingeJoint, et le bat mesuré.

var sb = new System.Text.StringBuilder();

var racine = GameObject.Find("PinballTable");
var rt = racine.transform;

void Detail(string nom)
{
    var pivot = GameObject.Find(nom);

    if (pivot == null) { sb.AppendLine(nom + " : ABSENT"); return; }

    var pt = pivot.transform;
    Vector3 lp = rt.InverseTransformPoint(pt.position);

    sb.AppendLine("=== " + nom + " ===");
    sb.AppendLine("  pivot : local (" + lp.x.ToString("F4") + ", " + lp.y.ToString("F4") + ", " + lp.z.ToString("F4") + ")"
        + "  parent '" + (pt.parent != null ? pt.parent.name : "(racine)") + "'"
        + "  échelle " + pt.lossyScale.ToString("F5"));

    var corps = pivot.GetComponent<Rigidbody>();
    if (corps != null)
    {
        Vector3 lr = rt.InverseTransformPoint(corps.position);
        sb.AppendLine("  Rigidbody : local (" + lr.x.ToString("F4") + ", " + lr.y.ToString("F4") + ", " + lr.z.ToString("F4") + ")"
            + "  cinématique " + corps.isKinematic
            + "  masse " + corps.mass.ToString("F3")
            + "  interpolation " + corps.interpolation
            + "  détection " + corps.collisionDetectionMode);
    }

    var hj = pivot.GetComponent<HingeJoint>();
    if (hj != null)
    {
        sb.AppendLine("  HingeJoint : anchor " + hj.anchor.ToString("F4")
            + "  axis " + hj.axis.ToString("F3")
            + "  autoConfigureConnectedAnchor " + hj.autoConfigureConnectedAnchor
            + "  connectedBody " + (hj.connectedBody != null ? hj.connectedBody.name : "(aucun / monde)"));
        sb.AppendLine("      useSpring " + hj.useSpring
            + "  spring " + hj.spring.spring.ToString("F2")
            + "  damper " + hj.spring.damper.ToString("F2")
            + "  target " + hj.spring.targetPosition.ToString("F2")
            + "  useLimits " + hj.useLimits
            + (hj.useLimits ? "  limits " + hj.limits.min.ToString("F1") + " → " + hj.limits.max.ToString("F1") : ""));
    }

    var fl = pivot.GetComponent("Flipper");
    if (fl != null)
    {
        var type = fl.GetType();
        var champs = type.GetFields(System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance);

        foreach (var c in champs)
        {
            if (c.Name.Contains("k__BackingField")) { continue; }
            var attr = c.GetCustomAttributes(typeof(SerializeField), true);
            bool visible = c.IsPublic || (attr != null && attr.Length > 0);
            if (!visible) { continue; }

            object v = c.GetValue(fl);
            string s;
            if (v == null) { s = "null"; }
            else if (v is UnityEngine.Object o) { s = o.name + " (" + o.GetType().Name + ")"; }
            else if (v is System.Collections.IEnumerable e && !(v is string))
            {
                var b = new System.Text.StringBuilder("[");
                int k = 0;
                foreach (var x in e) { if (k++ > 0) { b.Append(", "); } b.Append(x); }
                s = b.Append("]").ToString();
            }
            else { s = v.ToString(); }

            sb.AppendLine("  Flipper." + c.Name + " = " + s);
        }
    }

    // l'enfant qui porte le bat
    for (int i = 0; i < pt.childCount; i++)
    {
        var enf = pt.GetChild(i);
        Vector3 le = rt.InverseTransformPoint(enf.position);
        sb.AppendLine("  enfant " + i + " : '" + enf.name + "' local (" + le.x.ToString("F4") + ", "
            + le.y.ToString("F4") + ", " + le.z.ToString("F4") + ")"
            + "  offset local au pivot " + enf.localPosition.ToString("F4")
            + "  rot locale " + enf.localRotation.eulerAngles.ToString("F2")
            + "  échelle " + enf.localScale.ToString("F4"));

        for (int j = 0; j < enf.childCount; j++)
        {
            var petit = enf.GetChild(j);
            Vector3 lpetit = rt.InverseTransformPoint(petit.position);
            sb.AppendLine("      petit-enfant '" + petit.name + "' local (" + lpetit.x.ToString("F4") + ", "
                + lpetit.y.ToString("F4") + ", " + lpetit.z.ToString("F4") + ")");

            var mf = petit.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var mb = mf.sharedMesh.bounds;
                sb.AppendLine("          maillage : " + mf.sharedMesh.vertexCount + " sommets"
                    + "  bornes locales min " + mb.min.ToString("F4") + " max " + mb.max.ToString("F4"));
            }

            var col = petit.GetComponent<Collider>();
            if (col != null)
            {
                Vector3 lc = rt.InverseTransformPoint(col.bounds.center);
                sb.AppendLine("          " + col.GetType().Name + " centre table (" + lc.x.ToString("F4") + ", "
                    + lc.y.ToString("F4") + ", " + lc.z.ToString("F4") + ")  taille " + col.bounds.size.ToString("F3"));
            }
        }
    }

    sb.AppendLine();
}

Detail("Flipper_Left_Pivot");
Detail("Flipper_Right_Pivot");

// Le bat existe-t-il aussi comme objet indépendant ? (`probe_etat_scene` a trouvé un
// `Flipper_Bat` à (-0,346 / -0,417 / 1,204), distinct des pivots.)
sb.AppendLine("=== tous les objets nommés 'Flipper_Bat' ou 'Bat_Mesh' ===");

foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
    FindObjectsInactive.Include, FindObjectsSortMode.None))
{
    if (t.name != "Flipper_Bat" && t.name != "Bat_Mesh") { continue; }

    string chemin = t.name;
    var cur = t;
    while (cur.parent != null) { cur = cur.parent; chemin = cur.name + "/" + chemin; }

    Vector3 l = rt.InverseTransformPoint(t.position);
    sb.AppendLine("  " + chemin + "  local (" + l.x.ToString("F4") + ", " + l.y.ToString("F4") + ", " + l.z.ToString("F4")
        + ")  actif " + t.gameObject.activeInHierarchy);
}

return sb.ToString();
