// SONDE (lecture seule) — de quoi a l'air le plastique de slingshot, et quels matériaux opaques
// le projet a sous la main ?
//
// Le plastique de slingshot a été mappé sur `PlayfieldGlass_Mat`, qui est le VERRE du plateau
// (alpha 0,06). Résultat : le slingshot est transparent. Il faut un matériau opaque — et le
// choix doit se faire sur des mesures, pas sur un nom.
//
// On relève donc, pour chaque matériau du projet : le shader, le mode de surface, l'alpha et la
// couleur. « Opaque » se lit sur `_Surface` (0 = opaque, 1 = transparent) ET sur l'alpha.

var sb = new System.Text.StringBuilder();

string[] chemins = System.IO.Directory.GetFiles(
    System.IO.Path.Combine(Application.dataPath, "Materials"), "*.mat");

sb.AppendLine("=== matériaux du projet ===");
sb.AppendLine("  _Surface : 0 = opaque, 1 = transparent");
sb.AppendLine();

foreach (var fichier in chemins)
{
    var relatif = "Assets" + fichier.Replace('\\', '/').Substring(Application.dataPath.Length);
    var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(relatif);

    if (mat == null) { continue; }

    float surface = mat.HasProperty("_Surface") ? mat.GetFloat("_Surface") : -1f;
    var couleur = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.magenta;
    float lisse = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : -1f;
    float metal = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : -1f;

    bool opaque = surface == 0f && couleur.a >= 0.99f;

    sb.AppendLine("  " + mat.name.PadRight(24)
                  + (opaque ? "OPAQUE    " : "transparent")
                  + "  surface " + surface.ToString("F0")
                  + "   couleur (" + couleur.r.ToString("F2") + " ; " + couleur.g.ToString("F2")
                  + " ; " + couleur.b.ToString("F2") + " ; a " + couleur.a.ToString("F2") + ")"
                  + "   lisse " + lisse.ToString("F2")
                  + "   métal " + metal.ToString("F2"));
}

// --- l'état actuel du plastique sur les slingshots -------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== ce que porte le plastique des slingshots ===");

var racine = GameObject.Find("PinballTable");
var rt = racine != null ? racine.transform : null;
var gameplay = rt != null ? rt.Find("Gameplay") : null;

if (gameplay != null)
{
    foreach (var nom in new string[] { "Slingshot_Left", "Slingshot_Right" })
    {
        var hote = gameplay.Find(nom);
        var enfant = hote != null ? hote.Find("Plastique_Decal") : null;

        sb.AppendLine();
        sb.AppendLine("  " + nom + " : " + (enfant != null ? "plastique présent" : "PAS DE PLASTIQUE"));

        if (enfant == null) { continue; }

        foreach (var rendu in enfant.GetComponentsInChildren<MeshRenderer>())
        {
            for (int i = 0; i < rendu.sharedMaterials.Length; i++)
            {
                var m = rendu.sharedMaterials[i];

                if (m == null) { sb.AppendLine("      [" + i + "] null"); continue; }

                float surface = m.HasProperty("_Surface") ? m.GetFloat("_Surface") : -1f;
                var couleur = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.magenta;

                // Les textures portées par le matériau : c'est le décal qu'on ne veut pas perdre.
                var travail = new UnityEditor.SerializedObject(m);
                var iter = travail.GetIterator();
                var textures = new System.Collections.Generic.List<string>();

                if (iter.NextVisible(true))
                {
                    do
                    {
                        if (iter.propertyType == UnityEditor.SerializedPropertyType.ObjectReference
                            && iter.objectReferenceValue is Texture)
                        {
                            textures.Add(iter.name + "=" + iter.objectReferenceValue.name);
                        }
                    } while (iter.NextVisible(false));
                }

                sb.AppendLine("      [" + i + "] '" + m.name + "'   surface " + surface.ToString("F0")
                              + "   alpha " + couleur.a.ToString("F2")
                              + (couleur.a < 0.99f ? "   ⚠ TRANSPARENT" : "   ✓ opaque")
                              + (textures.Count > 0
                                  ? "   textures : " + string.Join(", ", textures) : "   (aucune texture)"));
            }
        }
    }
}

var sortie = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "materiaux.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(sortie));
System.IO.File.WriteAllText(sortie, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(sortie) + "  (" + sb.Length + " car.)";
