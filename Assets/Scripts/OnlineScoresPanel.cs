using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Top 100 et saisie d'un score après le Game Over.</summary>
public sealed class OnlineScoresPanel : MonoBehaviour
{
    private const int PageSize = 10;
    private RectTransform card;
    private GameObject overlay;
    private TMP_Text title, status, ranks, names, scores, pageLabel;
    private Button previous, next, retry, close;
    private GameObject returnFocus;
    private OnlineScores.Entry[] entries;
    private TMP_InputField initials;
    private Button submit;
    private int page, finalScore;
    private string submittedInitials;
    private Button replay, mainMenu;
    private GameManager finishedGame;
    private bool busy, waitingForTransition;

    internal bool IsOpen => overlay != null && overlay.activeSelf;
    internal bool HandlingFinalScore => waitingForTransition || IsOpen;

    public void ShowRanking(Quaternion rotation)
    {
        if (IsOpen) return;
        Build(rotation, new Vector2(596, 686), "TOP 100 MONDIAL");
        Label("RANG", new Vector2(-208, 233), new Vector2(110, 34), 22, UiTheme.Muted);
        Label("PSEUDO", new Vector2(-42, 233), new Vector2(130, 34), 22, UiTheme.Muted);
        Label("SCORE", new Vector2(160, 233), new Vector2(190, 34), 22, UiTheme.Muted);
        ranks = Column(-208, 110);
        names = Column(-42, 130);
        scores = Column(160, 190);
        pageLabel = Label("", new Vector2(0, -226), new Vector2(140, 42), 22, UiTheme.Muted);
        previous = MakeButton("Précédent", new Vector2(-164, -226), 150, () => { page--; RenderPage(); });
        next = MakeButton("Suivant", new Vector2(164, -226), 150, () => { page++; RenderPage(); });
        retry = MakeButton("Actualiser", new Vector2(-139, -288), 185, RefreshRanking);
        close = MakeButton("Retour", new Vector2(139, -288), 185, Close);
        Select(close.gameObject);
        RefreshRanking();
    }

    internal void QueueFinalScore(GameManager game, int score)
    {
        if (HandlingFinalScore) return;
        finishedGame = game;
        waitingForTransition = true;
        StartCoroutine(AfterProgressionCheck(game, score));
    }

    private IEnumerator AfterProgressionCheck(GameManager game, int score)
    {
        // Le passage de Vosges Mania à Industries se décide en LateUpdate.
        yield return null;
        // Une partie transférée avec zéro bille peut finir pendant l'arrivée sur Industries.
        while (SceneTransition.IsPlaying && game != null)
            yield return null;
        waitingForTransition = false;
        if (game != null && game.State == GameManager.GameState.GameOver)
            CheckFinalScore(score);
    }

    private void CheckFinalScore(int score)
    {
        if (IsOpen) return;
        finalScore = score;
        var camera = Camera.main;
        bool rotated = camera != null && camera.TryGetComponent<CabinetViewport>(out _) && camera.aspect >= 1f;
        Build(rotated ? Quaternion.Euler(0, 0, -90) : Quaternion.identity, new Vector2(560, 370), "SCORE FINAL");
        UiTheme.Apply(Label(score.ToString("N0") + " PTS", new Vector2(0, 52), new Vector2(490, 60), 56, UiTheme.Gold),
            UiTheme.NumberFont);
        status.rectTransform.anchoredPosition = new Vector2(0, -14);
        close = MakeButton("Continuer", new Vector2(137, -135), 195, ShowFinalActions);
        retry = MakeButton("Réessayer", new Vector2(-137, -135), 195, CheckQualification);
        replay = MakeButton("Rejouer", new Vector2(-137, -135), 195, () =>
        {
            Close();
            finishedGame.Replay();
        });
        mainMenu = MakeButton("Menu principal", new Vector2(137, -135), 195, () =>
        {
            if (MainMenuController.ReturnToMainMenu()) Close();
        });
        replay.gameObject.SetActive(false);
        mainMenu.gameObject.SetActive(false);
        if (score <= 0)
            ShowFinalActions();
        else if (OnlineScores.RunTicket == null)
        {
            status.text = "Classement indisponible pour cette partie.";
            ShowFinalActions();
        }
        else
            CheckQualification();
    }

    private void RefreshRanking()
    {
        if (busy) return;
        entries = null;
        page = 0;
        RenderPage();
        status.text = "Chargement des scores…";
        busy = true;
        retry.interactable = false;
        StartCoroutine(OnlineScores.Fetch(result =>
        {
            entries = result;
            status.text = result == null ? "Classement indisponible. Réessaie." :
                result.Length == 0 ? "Le classement est encore vide." : "";
            busy = false;
            retry.interactable = true;
            RenderPage();
        }));
    }

    private void RenderPage()
    {
        int count = entries?.Length ?? 0;
        int pages = (count + PageSize - 1) / PageSize;
        ranks.text = names.text = scores.text = "";
        pageLabel.text = pages > 0 ? $"{page + 1} / {pages}" : "";
        previous.interactable = page > 0;
        next.interactable = page + 1 < pages;
        for (int i = page * PageSize; i < Mathf.Min(count, (page + 1) * PageSize); i++)
        {
            string line = i % PageSize == 0 ? "" : "\n";
            ranks.text += line + (i + 1).ToString("00");
            names.text += line + entries[i].initials;
            scores.text += line + entries[i].score.ToString("N0");
        }
    }

    private void CheckQualification()
    {
        busy = true;
        status.text = "Vérification du Top 100…";
        retry.gameObject.SetActive(false);
        StartCoroutine(OnlineScores.Fetch(ranking =>
        {
            busy = false;
            if (ranking == null)
            {
                status.text = "Classement indisponible.";
                retry.gameObject.SetActive(true);
                Select(retry.gameObject);
            }
            else if (OnlineScores.Qualifies(finalScore, ranking))
            {
                ShowEntryForm();
            }
            else
            {
                ShowFinalActions();
            }
        }));
    }

    private void ShowEntryForm()
    {
        title.text = "TOP 100 MONDIAL";
        status.text = "Saisis trois lettres de A à Z.";
        var field = PauseMenu.Rect("Pseudo", card, new Vector2(0, -66), new Vector2(166, 52));
        UiTheme.Round(field.gameObject.AddComponent<Image>(), UiTheme.Surface, 12);
        var value = PauseMenu.Text(field, "", Vector2.zero, new Vector2(148, 48), 32, UiTheme.Cream, font: UiTheme.NumberFont);
        var placeholder = PauseMenu.Text(field, "ABC", Vector2.zero, new Vector2(148, 48), 32,
            new Color32(142, 160, 158, 255), font: UiTheme.NumberFont);
        initials = field.gameObject.AddComponent<TMP_InputField>();
        initials.textViewport = field;
        initials.textComponent = (TextMeshProUGUI)value;
        initials.placeholder = placeholder;
        initials.characterLimit = 3;
        initials.onValidateInput += (_, _, letter) =>
        {
            char upper = char.ToUpperInvariant(letter);
            return OnlineScores.Letter(upper) ? upper : '\0';
        };
        submit = MakeButton("Enregistrer", new Vector2(-137, -135), 195, Submit);
        submit.interactable = false;
        initials.onValueChanged.AddListener(text => submit.interactable = OnlineScores.ValidInitials(text));
        initials.onSubmit.AddListener(_ => Submit());
        Select(initials.gameObject);
        initials.ActivateInputField();
    }

    private void Submit()
    {
        if (busy || !OnlineScores.ValidInitials(initials.text)) return;
        // Garder la même ligne et les mêmes données en cas de réponse réseau perdue.
        submittedInitials ??= initials.text;
        initials.interactable = false;
        submit.interactable = false;
        close.interactable = false;
        busy = true;
        status.text = "Enregistrement…";
        StartCoroutine(OnlineScores.Submit(OnlineScores.RunTicket, submittedInitials, finalScore, result =>
        {
            busy = false;
            close.interactable = true;
            if (result == OnlineScores.SubmissionResult.Unavailable)
            {
                status.text = "Connexion interrompue. Réessaie l’envoi.";
                submit.interactable = true;
                Select(submit.gameObject);
                return;
            }
            status.text = result switch
            {
                OnlineScores.SubmissionResult.Accepted => "Score enregistré !",
                OnlineScores.SubmissionResult.OutsideTop => "Le Top 100 a changé. Ce score est désormais hors classement.",
                _ => "Ce score n’a pas passé les contrôles du classement."
            };
            ShowFinalActions();
        }));
    }

    private void Update()
    {
        if (!IsOpen || close == null || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (finishedGame == null)
            Close();
        else if (close.gameObject.activeInHierarchy && close.interactable)
            ShowFinalActions();
    }

    private void ShowFinalActions()
    {
        if (busy)
        {
            StopAllCoroutines();
            busy = false;
            status.text = "";
        }
        if (initials != null) initials.gameObject.SetActive(false);
        if (submit != null) submit.gameObject.SetActive(false);
        retry.gameObject.SetActive(false);
        close.gameObject.SetActive(false);
        replay.gameObject.SetActive(true);
        mainMenu.gameObject.SetActive(true);
        Select(replay.gameObject);
    }

    private void Close()
    {
        StopAllCoroutines();
        busy = false;
        overlay.SetActive(false);
        Destroy(overlay);
        overlay = null;
        submittedInitials = null;
        if (returnFocus != null && returnFocus.activeInHierarchy) Select(returnFocus);
    }

    private void Build(Quaternion rotation, Vector2 size, string heading)
    {
        returnFocus = EventSystem.current?.currentSelectedGameObject;
        overlay = new GameObject("ClassementEnLigne", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(overlay, gameObject.scene);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        var scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(820, 820);
        // Conserver le panneau dans l'écran, même si le menu est tourné sur la borne.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        if (EventSystem.current == null)
        {
            var input = new GameObject("NavigationClassement", typeof(EventSystem), typeof(StandaloneInputModule));
            input.transform.SetParent(overlay.transform, false);
        }
        var veil = PauseMenu.Rect("Voile", overlay.transform, Vector2.zero, Vector2.zero);
        veil.anchorMin = Vector2.zero;
        veil.anchorMax = Vector2.one;
        veil.gameObject.AddComponent<Image>().color = new Color(.01f, .025f, .032f, .86f);
        card = PauseMenu.Rect("Carte", veil, Vector2.zero, size);
        card.localRotation = rotation;
        UiTheme.Round(card.gameObject.AddComponent<Image>(), UiTheme.Ink, 28);
        UiTheme.Round(PauseMenu.Rect("Filet", card, new Vector2(0, size.y / 2 - 84), new Vector2(64, 5))
            .gameObject.AddComponent<Image>(), UiTheme.Gold, 2.5f);
        title = Label(heading, new Vector2(0, size.y / 2 - 48), new Vector2(size.x - 50, 62), 48, UiTheme.Cream);
        UiTheme.Apply(title, UiTheme.DisplayFont);
        status = Label("", new Vector2(0, size.y / 2 - 140), new Vector2(size.x - 50, 48), 21, UiTheme.Muted);
    }

    private TMP_Text Column(float x, float width)
    {
        var text = PauseMenu.Text(card, "", new Vector2(x, 16), new Vector2(width, 380), 25, UiTheme.Cream,
            TextAlignmentOptions.Top, UiTheme.StrongFont);
        text.lineSpacing = 9;
        text.richText = false;
        return text;
    }

    private TMP_Text Label(string value, Vector2 at, Vector2 size, float fontSize, Color color)
        => PauseMenu.Text(card, value, at, size, fontSize, color);

    private Button MakeButton(string caption, Vector2 at, float width, Action action)
        => PauseMenu.CreateButton(card, caption, caption, at, new Vector2(width, 46), action, out _);

    private static void Select(GameObject target) => EventSystem.current?.SetSelectedGameObject(target);

    private void OnDestroy()
    {
        if (overlay != null) Destroy(overlay);
    }
}
