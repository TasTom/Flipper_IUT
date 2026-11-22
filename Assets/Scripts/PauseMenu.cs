using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Binding = InputRouter.ActionBinding;

/// <summary>Réglages accessibles pendant la pause des tables.</summary>
public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    private static readonly Color Ink = new Color32(17, 32, 36, 255);
    private static readonly Color ButtonColor = new Color32(32, 54, 59, 255);
    private static readonly Color Gold = new Color32(238, 188, 111, 255);
    private static readonly Color Muted = new Color32(171, 195, 195, 255);
    private static readonly string[] Labels = { "Flipper gauche", "Flipper droit", "Flipper secondaire", "Lanceur", "Tilt gauche", "Tilt droit" };

    private GameObject overlay;
    private Slider music;
    private TMP_Text volumeText, feedback;
    private readonly TMP_Text[] keyLabels = new TMP_Text[Labels.Length];
    private readonly Button[] rebindButtons = new Button[Labels.Length];
    private Button resume, reset;
    private Binding? focusedAction;
    private int captureFrame;
    private InputRouter captureRouter;
    private EventSystem captureEvents;
    private bool navigationWasEnabled;

    private void Awake()
    {
        if (panel != null && panel != gameObject) panel.SetActive(false);
    }

    private void LateUpdate()
    {
        var game = GameManager.Instance;
        bool show = game != null && game.State == GameManager.GameState.Paused && !SceneTransition.IsPlaying;
        if (show && overlay == null) Build();
        if (overlay == null) return;
        if (overlay.activeSelf != show)
        {
            overlay.SetActive(show);
            if (show)
            {
                Refresh();
                EventSystem.current?.SetSelectedGameObject(resume.gameObject);
            }
            else
            {
                EndCapture(false);
                PlayerPrefs.Save();
            }
        }
        if (!show || !focusedAction.HasValue || Time.frameCount == captureFrame || captureRouter == null) return;
        if (!captureRouter.TryReadBindingKey(out var key)) return;
        if (key == KeyCode.Escape)
        {
            EndCapture(true);
            return;
        }
        if (!captureRouter.SetBinding(focusedAction.Value, key))
        {
            feedback.text = "Touche déjà utilisée ou réservée. Essaie une autre touche.";
            return;
        }
        EndCapture(true);
    }

    private void Build()
    {
        overlay = new GameObject("OptionsPause", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        overlay.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(overlay, gameObject.scene);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;
        var scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        if (EventSystem.current == null)
        {
            var input = new GameObject("NavigationPause", typeof(EventSystem), typeof(StandaloneInputModule));
            input.transform.SetParent(overlay.transform, false);
        }

        var veil = Rect("Voile", overlay.transform, Vector2.zero, Vector2.zero);
        veil.anchorMin = Vector2.zero;
        veil.anchorMax = Vector2.one;
        veil.gameObject.AddComponent<Image>().color = new Color(.01f, .025f, .032f, .80f);
        var card = Rect("Panneau", veil, Vector2.zero, new Vector2(650, 664));
        card.gameObject.AddComponent<Image>().color = Ink;
        Rect("Filet", card, new Vector2(0, 329), new Vector2(650, 6)).gameObject.AddComponent<Image>().color = Gold;

        Text(card, "Pause", new Vector2(0, 273), new Vector2(540, 53), 43, Gold);
        Text(card, "Musique", new Vector2(-185, 199), new Vector2(200, 32), 24, Color.white, TextAlignmentOptions.MidlineLeft);
        volumeText = Text(card, "", new Vector2(250, 199), new Vector2(78, 32), 22, Gold);
        music = CreateSlider(card);
        music.onValueChanged.AddListener(value =>
        {
            AudioManager.Instance?.SetMusicVolume(value / 100f);
            volumeText.text = Mathf.RoundToInt(value) + " %";
        });
        Text(card, "Touches", new Vector2(-185, 139), new Vector2(200, 36), 29, Gold, TextAlignmentOptions.MidlineLeft);
        for (int i = 0; i < Labels.Length; i++)
        {
            float y = 91 - i * 48;
            var action = (Binding)i;
            Text(card, Labels[i], new Vector2(-130, y), new Vector2(310, 40), 23, Color.white, TextAlignmentOptions.MidlineLeft);
            rebindButtons[i] = CreateButton(card, action.ToString(), "", new Vector2(174, y), new Vector2(222, 40), () => BeginCapture(action), out keyLabels[i]);
        }
        feedback = Text(card, "", new Vector2(0, -200), new Vector2(575, 45), 19, Muted);
        reset = CreateButton(card, "TouchesParDefaut", "Touches par défaut", new Vector2(-151, -267), new Vector2(270, 54), () =>
        {
            EndCapture(false);
            InputRouter.Instance?.ResetBindings();
            Refresh();
        }, out _);
        resume = CreateButton(card, "Reprendre", "Reprendre", new Vector2(152, -267), new Vector2(264, 54), () =>
        {
            EndCapture(false);
            GameManager.Instance?.TogglePause();
        }, out var resumeLabel);
        resume.GetComponent<Image>().color = Gold;
        resumeLabel.color = Ink;
    }

    private void Refresh()
    {
        var audio = AudioManager.Instance;
        music.interactable = audio != null;
        var volume = Mathf.RoundToInt((audio != null ? audio.MusicVolume : 0f) * 100f);
        music.SetValueWithoutNotify(volume);
        volumeText.text = volume + " %";
        var input = InputRouter.Instance;
        reset.interactable = input != null;
        for (int i = 0; i < keyLabels.Length; i++)
        {
            rebindButtons[i].interactable = input != null;
            if (input == null) { keyLabels[i].text = "—"; continue; }
            var action = (Binding)i;
            var key = input.GetKey(action);
            var alias = input.GetAlias(action);
            keyLabels[i].text = alias == KeyCode.None ? InputRouter.KeyLabel(key)
                : InputRouter.KeyLabel(key) + " / " + InputRouter.KeyLabel(alias);
        }
        feedback.text = input != null ? "Sélectionne une touche pour la modifier." : "Réglage clavier indisponible.";
    }

    private void BeginCapture(Binding action)
    {
        EndCapture(false);
        captureRouter = InputRouter.Instance;
        if (captureRouter == null) return;
        focusedAction = action;
        captureFrame = Time.frameCount;
        captureRouter.CaptureBinding(true);
        captureEvents = EventSystem.current;
        if (captureEvents != null)
        {
            navigationWasEnabled = captureEvents.sendNavigationEvents;
            captureEvents.sendNavigationEvents = false;
        }
        keyLabels[(int)action].text = "…";
        feedback.text = "Appuie sur une touche. Échap pour annuler.";
    }

    private void EndCapture(bool refresh)
    {
        if (!focusedAction.HasValue) return;
        if (captureRouter != null) captureRouter.CaptureBinding(false);
        if (captureEvents != null) captureEvents.sendNavigationEvents = navigationWasEnabled;
        focusedAction = null;
        captureRouter = null;
        captureEvents = null;
        if (refresh) Refresh();
    }

    private void OnDisable()
    {
        if (overlay != null) overlay.SetActive(false);
        EndCapture(false);
    }

    private void OnDestroy()
    {
        if (overlay != null) Destroy(overlay);
    }

    private static Slider CreateSlider(Transform card)
    {
        var root = Rect("VolumeMusique", card, new Vector2(65, 199), new Vector2(276, 38));
        root.gameObject.AddComponent<Image>().color = Color.clear;
        Rect("Piste", root, Vector2.zero, new Vector2(276, 8)).gameObject.AddComponent<Image>().color = ButtonColor;
        var fillArea = Rect("ZoneNiveau", root, Vector2.zero, new Vector2(276, 8));
        var fill = Rect("Niveau", fillArea, Vector2.zero, Vector2.zero);
        fill.gameObject.AddComponent<Image>().color = Gold;
        var rail = Rect("Course", root, Vector2.zero, new Vector2(260, 38));
        var handle = Rect("Curseur", rail, Vector2.zero, new Vector2(17, 30));
        var image = handle.gameObject.AddComponent<Image>();
        image.color = Gold;
        var slider = root.gameObject.AddComponent<Slider>();
        slider.targetGraphic = image;
        slider.handleRect = handle;
        slider.fillRect = fill;
        slider.minValue = 0;
        slider.maxValue = 100;
        slider.wholeNumbers = true;
        return slider;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size,
        Action onClick, out TMP_Text title)
    {
        var r = Rect(name, parent, position, size);
        var image = r.gameObject.AddComponent<Image>();
        image.color = ButtonColor;
        var button = r.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
        colors.selectedColor = new Color(1.25f, 1.25f, 1.25f);
        button.colors = colors;
        button.onClick.AddListener(() => onClick());
        title = Text(r, label, Vector2.zero, size - new Vector2(12, 4), 21, Color.white);
        return button;
    }

    private static TMP_Text Text(Transform parent, string label, Vector2 position, Vector2 size, float fontSize,
        Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var r = Rect("Texte", parent, position, size);
        var text = r.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
        r.sizeDelta = size;
        r.anchoredPosition = position;
        return r;
    }
}
