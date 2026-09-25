using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Explicit replacement of the old lower guides after the user requested new returns.
/// No runtime construction and no automatic scene save.</summary>
public static class FinishNeutralReturns
{
    [MenuItem("Flipper/Finition Neutral/Raccorder les retours aux flippers")]
    public static void Apply()
    {
        if (Application.isPlaying || EditorSceneManager.GetActiveScene().path != "Assets/Scenes/Neutral.unity")
            throw new InvalidOperationException("Ouvrir Neutral hors Play.");
        var table = GameObject.Find("PinballTable").transform;
        var ramps = table.Find("Gameplay/Ramps_V2");
        foreach (string side in new[] { "Left", "Right" })
            if (ramps == null || ramps.Find("InlaneInner_" + side)?.GetComponent<MeshCollider>() == null)
                throw new InvalidOperationException("Importer et rafraîchir les nouveaux rails avant de retirer les anciens.");
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Raccordement des retours de rampe");
        try
        {
            foreach (string side in new[] { "Left", "Right" })
            {
                Disable(table.Find("Table/Pinball_Table/guide_flipper_" + side.ToLowerInvariant()), true);
                Disable(table.Find("Gameplay/Outlane_Inner_" + side), true);
            }
            var presentation = table.Find("VosgesPresentation");
            if (presentation == null) return;
            Disable(presentation.Find("BumperBank"), false);
            Disable(presentation.Find("ProjetFinal"), false);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            foreach (int side in new[] { -1, 1 }) foreach (bool loss in new[] { false, true })
            {
                string name = (loss ? "OutlaneLabel_" : "ReturnLabel_") + (side < 0 ? "Left" : "Right");
                if (presentation.Find(name) != null) continue;
                var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Repérer les couloirs");
                go.transform.SetParent(presentation, false);
                go.transform.localPosition = new Vector3(side * (loss ? 5.33f : 4.45f), .04f, 4.2f);
                go.transform.localRotation = Quaternion.Euler(90, 0, 90);
                var text = Undo.AddComponent<TextMeshPro>(go);
                text.font = font; text.fontSize = 1.45f; text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.rectTransform.sizeDelta = new Vector2(1.6f, .3f);
                text.text = loss ? "SORTIE" : "RETOUR";
                text.color = loss ? new Color(1f, .69f, .3f) : new Color(.6f, 1f, .96f);
            }
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
        finally { Undo.CollapseUndoOperations(group); }
    }

    static void Disable(Transform host, bool physical)
    {
        if (host == null) return;
        foreach (var renderer in host.GetComponentsInChildren<Renderer>(true))
        { Undo.RecordObject(renderer, "Remplacer les anciens guides"); renderer.enabled = false; }
        if (physical) foreach (var collider in host.GetComponentsInChildren<Collider>(true))
        { Undo.RecordObject(collider, "Remplacer les anciens guides"); collider.enabled = false; }
    }
}
