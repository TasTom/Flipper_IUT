using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Habillage « Crépuscule » du fronton des tables : mêmes cartes arrondies et mêmes polices,
/// avec un accent or pour Vosges Mania et cuivre pour Industries. Ne déplace aucun texte.
/// Annulable par Ctrl+Z, n'enregistre pas la scène.
/// </summary>
public static class StyleBackglass
{
    [MenuItem("Flipper/UI/Habiller le fronton de la table")]
    public static void Apply()
    {
        var display = Object.FindAnyObjectByType<BackglassDisplay>(FindObjectsInactive.Include);
        if (display == null)
        {
            Debug.LogWarning("[StyleBackglass] Aucun fronton dans la scène active.");
            return;
        }
        StyleMainMenu.BuildAssets();
        var root = (RectTransform)display.transform;
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Habiller le fronton");

        bool industries = display.gameObject.scene.name == "Industries";
        Color accent = industries ? new Color32(242, 140, 72, 255) : (Color32)UiTheme.Gold;
        Color ink = industries ? new Color32(21, 25, 29, 255) : (Color32)UiTheme.Ink;
        Color card = industries ? new Color32(37, 43, 49, 255) : (Color32)UiTheme.Surface;

        var back = root.Find("Fond");
        if (back != null && back.TryGetComponent<Image>(out var backImage)) backImage.color = ink;

        // Les cartes passent juste devant le fond, donc derrière tous les textes.
        int index = back != null ? back.GetSiblingIndex() + 1 : 0;
        Card(root, "CarteRecord", 0, 200, 1470, 84, card, 20, index);
        Card(root, "CarteBilles", -435, -220, 600, 68, card, 20, index);
        Card(root, "CarteMission", 330, -220, 810, 68, card, 20, index);
        Card(root, "FiletAccent", 0, 148, 96, 6, accent, 3, index);

        // L'ancien « 8 888 888 » imitait un afficheur à segments : sans objet avec la nouvelle police.
        var ghost = root.Find("ScoreGhost");
        if (ghost != null && ghost.TryGetComponent<TMP_Text>(out var ghostText)) ghostText.enabled = false;

        Set(root, "ScoreValue", UiTheme.DisplayFont, accent);
        Set(root, "HighScoreLabel", UiTheme.StrongFont, UiTheme.Muted, 6);
        Set(root, "HighScoreValue", UiTheme.NumberFont, UiTheme.Cream);
        Set(root, "MultiplierValue", UiTheme.StrongFont, UiTheme.Muted, 6);
        Set(root, "ScoreFeedbackValue", UiTheme.NumberFont, accent, 2);
        Set(root, "MessageValue", UiTheme.StrongFont, UiTheme.Cream, 2);
        Set(root, "BallsLabel", UiTheme.StrongFont, UiTheme.Muted, 6);
        Set(root, "BallsValue", UiTheme.NumberFont, accent, 12);
        Set(root, "MissionsLabel", UiTheme.StrongFont, UiTheme.Muted, 2);
        Set(root, "MissionsValue", UiTheme.NumberFont, UiTheme.Cream);
        // Industries y écrit deux lignes : la police condensée permet un corps plus lisible.
        if (industries && root.Find("MissionsValue") is Transform missions &&
            missions.TryGetComponent<TMP_Text>(out var value)) value.fontSize = 25;

        EditorSceneManager.MarkSceneDirty(display.gameObject.scene);
    }

    private static void Card(RectTransform root, string name, float x, float y, float width, float height,
        Color color, float radius, int index)
    {
        var rect = StyleMainMenu.Child(root, name);
        StyleMainMenu.Place(rect, x, y, width, height);
        rect.SetSiblingIndex(index);
        var image = StyleMainMenu.Ensure<Image>(rect);
        UiTheme.Round(image, color, radius);
        image.raycastTarget = false;
    }

    private static void Set(RectTransform root, string name, string font, Color color, float spacing = 0)
    {
        var child = root.Find(name);
        if (child == null || !child.TryGetComponent<TMP_Text>(out var text))
        {
            Debug.LogWarning($"[StyleBackglass] Texte '{name}' absent du fronton.");
            return;
        }
        UiTheme.Apply(text, font);
        text.color = color;
        text.characterSpacing = spacing;
        text.fontStyle = FontStyles.Normal;
    }
}
