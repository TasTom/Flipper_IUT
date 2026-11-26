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

    private const string MainMenuScene = "MainMenu";
    private static readonly string[] Labels = { "Flipper gauche", "Flipper droit", "Flipper secondaire", "Lanceur", "Tilt gauche", "Tilt droit" };

    private GameObject overlay;
    private GameObject settingsCard, exitPrompt;
    private TMP_Text exitPromptTitle;
    private Button cancelExit;
    private Button exitSource;
    private Action confirmedExit;
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
    private bool standalone, optionsOpen;
    private Quaternion cardRotation = Quaternion.identity;
    private GameObject returnFocus;

    public void ShowOptions(Quaternion rotation)
    {
        if (optionsOpen) return;
        standalone = true;
        optionsOpen = true;
        cardRotation = rotation;
        returnFocus = EventSystem.current?.currentSelectedGameObject;
        if (overlay == null) Build();
        overlay.SetActive(true);
        Refresh();
        EventSystem.current?.SetSelectedGameObject(resume.gameObject);
    }

    private void Awake()
    {
        if (panel != null && panel != gameObject) panel.SetActive(false);
    }

    private void LateUpdate()
    {
        var game = GameManager.Instance;
        bool show = standalone ? optionsOpen :
            game != null && game.State == GameManager.GameState.Paused && !SceneTransition.IsPlaying;
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
                DismissExitPrompt();
                PlayerPrefs.Save();
            }
        }
        if (!show) return;
        if (standalone && !focusedAction.HasValue && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseOptions();
            return;
        }
        if (!focusedAction.HasValue || Time.frameCount == captureFrame || captureRouter == null) return;
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
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        if (EventSystem.current == null)
        {
            var input = new GameObject("NavigationPause", typeof(EventSystem), typeof(StandaloneInputModule));
            input.transform.SetParent(overlay.transform, false);
        }

        var veil = Rect("Voile", overlay.transform, Vector2.zero, Vector2.zero);
        veil.anchorMin = Vector2.zero;
        veil.anchorMax = Vector2.one;
        veil.gameObject.AddComponent<Image>().color = new Color(.01f, .025f, .032f, .80f);
        float height = standalone ? 664f : 802f;
        float offset = (height - 664f) / 2f;
        var card = Rect("Panneau", veil, Vector2.zero, new Vector2(650, height));
        settingsCard = card.gameObject;
        card.localRotation = cardRotation;
        UiTheme.Round(card.gameObject.AddComponent<Image>(), UiTheme.Ink, 28);

        var body = Rect("Réglages", card, new Vector2(0, offset), new Vector2(650, 664));
        BuildSettings(body);
        if (!standalone) BuildExitActions(veil, card);
    }

    private void BuildSettings(RectTransform card)
    {
        Text(card, standalone ? "OPTIONS" : "PAUSE", new Vector2(0, 278), new Vector2(540, 56), 54, UiTheme.Cream,
            font: UiTheme.DisplayFont);
        UiTheme.Round(Rect("Filet", card, new Vector2(0, 241), new Vector2(64, 5)).gameObject.AddComponent<Image>(),
            UiTheme.Gold, 2.5f);
        Text(card, "Musique", new Vector2(-185, 199), new Vector2(200, 32), 24, UiTheme.Cream, TextAlignmentOptions.MidlineLeft);
        volumeText = Text(card, "", new Vector2(250, 199), new Vector2(78, 32), 24, UiTheme.Gold,
            font: UiTheme.NumberFont);
        music = CreateSlider(card);
        music.onValueChanged.AddListener(value =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(value / 100f);
            else AudioManager.SaveMusicVolume(value / 100f);
            volumeText.text = Mathf.RoundToInt(value) + " %";
        });
        Text(card, "Touches", new Vector2(-185, 139), new Vector2(200, 36), 32, UiTheme.Gold, TextAlignmentOptions.MidlineLeft,
            UiTheme.NumberFont);
        for (int i = 0; i < Labels.Length; i++)
        {
            float y = 91 - i * 48;
            var action = (Binding)i;
            Text(card, Labels[i], new Vector2(-130, y), new Vector2(310, 40), 22, UiTheme.Cream, TextAlignmentOptions.MidlineLeft);
            rebindButtons[i] = CreateButton(card, action.ToString(), "", new Vector2(174, y), new Vector2(222, 40), () => BeginCapture(action), out keyLabels[i]);
        }
        feedback = Text(card, "", new Vector2(0, -200), new Vector2(575, 45), 19, UiTheme.Muted);
        reset = CreateButton(card, "TouchesParDefaut", "Touches par défaut", new Vector2(-151, -267), new Vector2(270, 54), () =>
        {
            EndCapture(false);
            InputRouter.Instance?.ResetBindings();
            Refresh();
        }, out _);
        resume = CreateButton(card, "Reprendre", standalone ? "Retour" : "Reprendre", new Vector2(152, -267), new Vector2(264, 54), () =>
        {
            if (standalone) CloseOptions();
            else
            {
                EndCapture(false);
                GameManager.Instance?.TogglePause();
            }
        }, out var resumeLabel);
        UiTheme.StyleButton(resume, true);
        UiTheme.Apply(resumeLabel, UiTheme.DisplayFont);
        resumeLabel.fontSize = 30;
        resumeLabel.color = UiTheme.Ink;
    }

    private void BuildExitActions(RectTransform veil, RectTransform card)
    {
        Button menu = null;
        menu = CreateButton(card, "RetourMenuPrincipal", "Menu principal", new Vector2(-151, -337),
            new Vector2(270, 54), () => AskToExit(menu, "Revenir au menu principal ?", () => MainMenuController.ReturnToMainMenu()), out _);
        menu.interactable = SceneTransition.CanLoad(MainMenuScene);

        Button quit = null;
        quit = CreateButton(card, "QuitterJeu", "Quitter le jeu", new Vector2(152, -337),
            new Vector2(264, 54), () => AskToExit(quit, "Quitter le jeu ?", MainMenuController.ExitApplication), out _);

        var prompt = Rect("ConfirmationSortie", veil, Vector2.zero, new Vector2(550, 246));
        prompt.localRotation = cardRotation;
        exitPrompt = prompt.gameObject;
        UiTheme.Round(exitPrompt.AddComponent<Image>(), UiTheme.Ink, 24);
        exitPromptTitle = Text(prompt, "", new Vector2(0, 72), new Vector2(490, 54), 34, UiTheme.Cream,
            font: UiTheme.NumberFont);
        Text(prompt, "La partie en cours sera abandonnée.", new Vector2(0, 12), new Vector2(490, 42), 22, UiTheme.Muted);
        cancelExit = CreateButton(prompt, "Annuler", "Annuler", new Vector2(-120, -67),
            new Vector2(196, 50), DismissExitPrompt, out _);
        CreateButton(prompt, "Confirmer", "Confirmer", new Vector2(120, -67),
            new Vector2(196, 50), ConfirmExit, out _);
        exitPrompt.SetActive(false);
    }

    private void AskToExit(Button source, string message, Action action)
    {
        EndCapture(false);
        exitSource = source;
        confirmedExit = action;
        exitPromptTitle.text = message;
        settingsCard.SetActive(false);
        exitPrompt.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(cancelExit.gameObject);
    }

    private void DismissExitPrompt()
    {
        if (exitPrompt == null || !exitPrompt.activeSelf) return;
        exitPrompt.SetActive(false);
        settingsCard.SetActive(true);
        if (overlay.activeSelf && exitSource != null)
            EventSystem.current?.SetSelectedGameObject(exitSource.gameObject);
        confirmedExit = null;
    }

    private void ConfirmExit()
    {
        var action = confirmedExit;
        DismissExitPrompt();
        action?.Invoke();
    }

    private void Refresh()
    {
        var audio = AudioManager.Instance;
        music.interactable = audio != null || standalone;
        var volume = Mathf.RoundToInt((audio != null ? audio.MusicVolume : AudioManager.GetSavedMusicVolume()) * 100f);
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
        DismissExitPrompt();
    }

    private void CloseOptions()
    {
        optionsOpen = false;
        EndCapture(false);
        overlay.SetActive(false);
        PlayerPrefs.Save();
        if (returnFocus != null && returnFocus.activeInHierarchy)
            EventSystem.current?.SetSelectedGameObject(returnFocus);
    }

    private void OnDestroy()
    {
        if (overlay != null) Destroy(overlay);
    }

    private static Slider CreateSlider(Transform card)
    {
        var root = Rect("VolumeMusique", card, new Vector2(65, 199), new Vector2(276, 38));
        root.gameObject.AddComponent<Image>().color = Color.clear;
        UiTheme.Round(Rect("Piste", root, Vector2.zero, new Vector2(276, 8)).gameObject.AddComponent<Image>(),
            UiTheme.Surface, 4);
        var fillArea = Rect("ZoneNiveau", root, Vector2.zero, new Vector2(276, 8));
        var fill = Rect("Niveau", fillArea, Vector2.zero, Vector2.zero);
        UiTheme.Round(fill.gameObject.AddComponent<Image>(), UiTheme.Gold, 4);
        var rail = Rect("Course", root, Vector2.zero, new Vector2(260, 38));
        var handle = Rect("Curseur", rail, Vector2.zero, new Vector2(17, -10)); // Le Slider l'étire en hauteur.
        var image = handle.gameObject.AddComponent<Image>();
        UiTheme.Round(image, UiTheme.Gold, 8);
        var slider = root.gameObject.AddComponent<Slider>();
        slider.targetGraphic = image;
        slider.handleRect = handle;
        slider.fillRect = fill;
        slider.minValue = 0;
        slider.maxValue = 100;
        slider.wholeNumbers = true;
        return slider;
    }

    internal static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size,
        Action onClick, out TMP_Text title)
    {
        var r = Rect(name, parent, position, size);
        var image = r.gameObject.AddComponent<Image>();
        var button = r.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        UiTheme.StyleButton(button, false);
        button.onClick.AddListener(() => onClick());
        title = Text(r, label, Vector2.zero, size - new Vector2(12, 4), 21, UiTheme.Cream, font: UiTheme.StrongFont);
        return button;
    }

    internal static TMP_Text Text(Transform parent, string label, Vector2 position, Vector2 size, float fontSize,
        Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center, string font = UiTheme.BodyFont)
    {
        var r = Rect("Texte", parent, position, size);
        var text = r.gameObject.AddComponent<TextMeshProUGUI>();
        UiTheme.Apply(text, font);
        text.text = label;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    internal static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
        r.sizeDelta = size;
        r.anchoredPosition = position;
        return r;
    }
}
