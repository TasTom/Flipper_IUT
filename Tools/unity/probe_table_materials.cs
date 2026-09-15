// Relève les matériaux réellement portés par les maillages de `Pinball_Table`.
//
// La capture de la scène sort uniformément grise. Deux causes possibles, et elles n'ont pas le
// même remède : soit les pièces du dépôt ont perdu leurs matériaux Blender à l'import (elles
// tombent alors sur le matériau par défaut, gris), soit elles les ont gardés et c'est
// l'éclairage de la vue Scène qui écrase les couleurs. Ce script tranche en lisant les
// `sharedMaterials` des `MeshRenderer`, et en relevant au passage le shader de chacun — un
// shader resté sur `Standard` (Built-in) rend magenta sous URP, ce qui est le piège documenté.

var report = new System.Text.StringBuilder();
var group = GameObject.Find("PinballTable");

if (group == null) { return "PinballTable introuvable"; }

var table = group.transform.Find("Table/Pinball_Table");

if (table == null) { return "Table/Pinball_Table introuvable"; }

var usage = new System.Collections.Generic.Dictionary<string, int>();
var shaders = new System.Collections.Generic.Dictionary<string, int>();
int renderers = 0;
int missing = 0;

foreach (var renderer in table.GetComponentsInChildren<MeshRenderer>(true))
{
    renderers++;

    var materials = renderer.sharedMaterials;

    if (materials == null || materials.Length == 0)
    {
        missing++;
        continue;
    }

    foreach (var material in materials)
    {
        if (material == null)
        {
            missing++;
            continue;
        }

        string name = material.name;

        if (name.EndsWith(" (Instance)")) { name = name.Substring(0, name.Length - 11); }

        usage[name] = usage.ContainsKey(name) ? usage[name] + 1 : 1;

        string shader = material.shader != null ? material.shader.name : "<aucun>";
        shaders[shader] = shaders.ContainsKey(shader) ? shaders[shader] + 1 : 1;
    }
}

report.AppendLine(renderers + " MeshRenderer, " + missing + " emplacement(s) sans matériau");
report.AppendLine();

report.AppendLine("shaders :");

foreach (var entry in shaders)
{
    report.AppendLine("  " + entry.Value.ToString().PadLeft(3) + "  " + entry.Key);
}

report.AppendLine();
report.AppendLine("matériaux :");

foreach (var entry in usage)
{
    report.AppendLine("  " + entry.Value.ToString().PadLeft(3) + "  " + entry.Key);
}

// Les matériaux du projet, pour comparaison : ce sont eux qu'il faudrait réassigner si les
// matériaux Blender ne conviennent pas.
report.AppendLine();
report.AppendLine("matériaux disponibles dans le projet :");

foreach (var guid in AssetDatabase.FindAssets("t:Material"))
{
    var path = AssetDatabase.GUIDToAssetPath(guid);

    if (path.Contains("/Materials/"))
    {
        report.AppendLine("  " + path);
    }
}

return report.ToString();
