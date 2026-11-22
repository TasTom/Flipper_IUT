using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Unity;
using VpeBallManager = VisualPinball.Unity.BallManager;

/// <summary>Règles d'Industries : cycle de partie partagé, contacts et physique exécutés par VPE.</summary>
[DisallowMultipleComponent]
public class IndustriesVpeGame : MonoBehaviour, IGamelogicEngine
{
    [SerializeField] private IndustriesConfig config;
    [SerializeField] private GameManager game;
    [SerializeField] private ScoreManager score;
    [SerializeField] private InputRouter input;
    [SerializeField] private TMPro.TMP_Text objectivesText;
    [SerializeField] private TMPro.TMP_Text nextShotText;
    [SerializeField] private bool compactObjectivesLabel;
    [SerializeField] private BumperComponent automateBumper;
    [SerializeField] private GameObject automateModel;
    [Tooltip("Durée du Ball Saver après lancement sur Industries, en secondes.")]
    [SerializeField] private float ballSaverDuration = 8f;
    private float ballSaverEnd = float.NegativeInfinity;
    private bool ballSaverUsed;
    private Player player;
    private TableApi api;
    private FlipperApi left, right;
    private PlungerApi plunger;
    private TroughApi trough;
    private KickerApi ballRelease;
    private Action retryBallRelease;
    private KickerApi scoop;
    private PinballControls cabinet;
    private IndustriesAudio audio;
    private IApiHittable troughDrain;
    private bool initialized, lastLeft, lastRight, lastLaunch;
    private int targets;
    private IndustriesProduction production;
    private IndustriesAutomate automate;
    private IndustriesAutomateFx automateFx;
    private readonly HashSet<int> drains = new HashSet<int>();
    private readonly Dictionary<int, Transform> liveBalls = new Dictionary<int, Transform>();
    private bool startingMultiball;
    private readonly List<Action> unbind = new List<Action>();
    private readonly List<(DropTargetApi target, int bit)> dropTargets = new List<(DropTargetApi, int)>();
    private Coroutine serveRoutine;
    public bool IsInitialized => initialized;
    public IndustriesProduction Production => production;
    public bool AutomateActive => automate?.IsActive == true;
    public bool MultiballActive { get; private set; }
    public int AutomateHits => automate?.Hits ?? 0;
    public int TargetsLit { get { int count = 0; for (int i = 0; i < 6; i++) if ((targets & (1 << i)) != 0) count++; return count; } }
    public int LitTargetMask=>targets;
    public string Name => "Vosges Mania — Industries";

    public GamelogicEngineSwitch[] RequestedSwitches { get; } = {
        new GamelogicEngineSwitch("s_left_flipper"), new GamelogicEngineSwitch("s_right_flipper"),
        new GamelogicEngineSwitch("s_trough_drain"), new GamelogicEngineSwitch("s_trough1"),
        new GamelogicEngineSwitch("s_trough2"), new GamelogicEngineSwitch("s_trough3"),
        new GamelogicEngineSwitch("s_trough4"), new GamelogicEngineSwitch("s_red_bumper")
    };
    public GamelogicEngineCoil[] RequestedCoils { get; } = {
        new GamelogicEngineCoil("c_flipper_left_main"), new GamelogicEngineCoil("c_flipper_left_hold"),
        new GamelogicEngineCoil("c_flipper_right_main"), new GamelogicEngineCoil("c_flipper_right_hold"),
        new GamelogicEngineCoil("c_trough_eject"), new GamelogicEngineCoil("c_trough_entry")
    };
    public GamelogicEngineLamp[] RequestedLamps => Array.Empty<GamelogicEngineLamp>();
    public GamelogicEngineWire[] AvailableWires => Array.Empty<GamelogicEngineWire>();
    public event EventHandler<CoilEventArgs> OnCoilChanged;
    public event EventHandler<LampEventArgs> OnLampChanged;
#pragma warning disable CS0067 // Les interfaces VPE demandent les sorties non utilisées par le HUD uGUI.
    public event EventHandler<LampsEventArgs> OnLampsChanged;
    public event EventHandler<RequestedDisplays> OnDisplaysRequested;
    public event EventHandler<string> OnDisplayClear;
    public event EventHandler<DisplayFrameData> OnDisplayUpdateFrame;
#pragma warning restore CS0067
    public event EventHandler<SwitchEventArgs2> OnSwitchChanged;
    public event EventHandler<EventArgs> OnStarted;

    private void Awake()
    {
        if (game == null) game = FindAnyObjectByType<GameManager>();
        if (score == null) score = FindAnyObjectByType<ScoreManager>();
        if (input == null) input = FindAnyObjectByType<InputRouter>();
        if (nextShotText == null && objectivesText != null && compactObjectivesLabel)
            nextShotText = objectivesText.transform.parent?.Find("MissionsLabel")?.GetComponent<TMPro.TMP_Text>();
        cabinet = new PinballControls();
        audio = GetComponent<IndustriesAudio>();
        cabinet.GamePlayPF.Enable();
    }

    public Task OnInit(Player p, TableApi tableApi, VpeBallManager balls, CancellationToken ct)
    {
        if (ct.IsCancellationRequested || initialized) return Task.CompletedTask;
        player = p;
        api = tableApi;
        left = api.Flipper("LeftFlipper"); right = api.Flipper("RightFlipper");
        plunger = api.Plunger("Plunger"); trough = api.Trough("Trough"); scoop = api.Kicker("Kicker1");
        var troughComponent = GetComponentInChildren<TroughComponent>();
        var releaseComponent = troughComponent != null ? troughComponent.PlayfieldExitKicker : null;
        if (releaseComponent != null)
        {
            ballRelease = api.Kicker(releaseComponent);
            foreach (var coil in releaseComponent.Coils)
            {
                if (coil.Id != troughComponent.PlayfieldExitKickerItem) continue;
                if (ballRelease != null)
                    retryBallRelease = () => ballRelease.Kick(coil.Angle, coil.Speed, coil.Inclination);
                break;
            }
        }
        if (config == null || game == null || score == null || input == null ||
            left == null || right == null || plunger == null || trough == null)
        {
            Debug.LogWarning("[Industries] Config, gestionnaires ou éléments VPE manquants : partie inactive.", this);
            return Task.CompletedTask;
        }
        foreach (var c in GetComponentsInChildren<BumperComponent>())
        {
            bool boss = automateBumper != null ? c == automateBumper : c.name == "Bumper3";
            BindHit(api.Bumper(c), config.bumperPoints, boss);
            if (boss) automateBumper = c;
        }
        var targetNames = new[] { "sw1", "sw2", "sw3", "sw11", "sw12", "sw13" };
        foreach (var c in GetComponentsInChildren<DropTargetComponent>())
        {
            var target = api.DropTarget(c);
            if (target == null) continue;
            int bit = Array.IndexOf(targetNames, c.name);
            if (bit < 0) continue;
            EventHandler<HitEventArgs> handler = (_, __) => HitTarget(bit);
            target.Hit += handler; unbind.Add(() => target.Hit -= handler); dropTargets.Add((target, bit));
        }
        foreach (var c in GetComponentsInChildren<HitTargetComponent>())
        {
            var target = api.HitTarget(c);
            if (target == null) continue;
            int bit = Array.IndexOf(targetNames, c.name);
            if (bit < 0) continue;
            EventHandler<HitEventArgs> handler = (_, __) => HitTarget(bit);
            target.Hit += handler; unbind.Add(() => target.Hit -= handler);
        }
        foreach (var c in GetComponentsInChildren<TriggerComponent>())
        {
            if (!(c.name.StartsWith("sw") || c.name.EndsWith("Inlane"))) continue;
            var trigger = api.Trigger(c);
            EventHandler<HitEventArgs> handler = (_, __) => Award(config.lanePoints);
            trigger.Hit += handler; unbind.Add(() => trigger.Hit -= handler);
        }
        if (config.productionEnabled)
        {
            var sensors = config.sharedProductionRamp
                ? new[] { IndustriesProduction.TextileEntry, IndustriesProduction.TextileExit }
                : new[] { IndustriesProduction.WoodEntry, IndustriesProduction.WoodExit,
                    IndustriesProduction.TextileEntry, IndustriesProduction.TextileExit };
            var routes = Array.ConvertAll(sensors, name => api.Trigger(name));
            if (Array.Exists(routes, route => route == null))
                Debug.LogWarning("[Industries] Un capteur de production manque : règle des six cibles conservée. Appliquer le layout Atelier.", this);
            else
            {
                production = new IndustriesProduction(config);
                for (int i = 0; i < routes.Length; i++)
                {
                    var trigger = routes[i]; int line = i / 2; bool entry = i % 2 == 0;
                    EventHandler<HitEventArgs> handler = (_, e) =>
                    {
                        if (config.sharedProductionRamp) OnSharedRoute(entry, e.BallId);
                        else OnRoute(line, entry, e.BallId);
                    };
                    trigger.Hit += handler; unbind.Add(() => trigger.Hit -= handler);
                }
            }
        }
        foreach (var c in GetComponentsInChildren<SurfaceComponent>())
        {
            if (!c.name.Contains("SlingShot")) continue;
            var surface = api.Surface(c);
            EventHandler handler = (_, __) => Award(config.slingshotPoints);
            surface.Slingshot += handler; unbind.Add(() => surface.Slingshot -= handler);
        }
        if (production != null)
        {
            if (automateModel == null) automateModel = Resources.Load<GameObject>("Industries/Automate");
            if (automateBumper != null && api.Bumper(automateBumper) != null && automateModel != null &&
                IndustriesAutomateFx.TryCreate(automateBumper.transform, automateModel, out automateFx))
                automate = new IndustriesAutomate(config.automateHits, config.automateHitCooldown);
            else
                Debug.LogWarning("[Industries] Bumper3 ou modèle de l’automate manquant. Le mode boss est désactivé.", this);
        }
        foreach (var c in GetComponentsInChildren<SpinnerComponent>())
        {
            var spinner = api.Spinner(c);
            if (spinner == null) continue;
            EventHandler handler = (_, __) => Award(config.spinnerPoints);
            spinner.Spin += handler; unbind.Add(() => spinner.Spin -= handler);
        }
        var drain = api.Kicker("Drain");
        if (drain != null) { drain.Hit += OnDrain; unbind.Add(() => drain.Hit -= OnDrain); }
        if (troughComponent != null &&
            troughComponent.PlayfieldEntrySwitch is KickerComponent drainComponent &&
            api.Kicker(drainComponent) == drain)
            troughDrain = drain;
        if (troughDrain == null)
            Debug.LogWarning("[Industries] Drain VPE non relié au trough : récupération hors zone indisponible.", this);
        if (scoop != null) { scoop.Hit += OnScoop; unbind.Add(() => scoop.Hit -= OnScoop); }
        p.OnBallCreated += TrackBallCreated;
        p.OnBallDestroyed += TrackBallDestroyed;
        unbind.Add(() => { p.OnBallCreated -= TrackBallCreated; p.OnBallDestroyed -= TrackBallDestroyed; });
        initialized = true;
        game.ExternalBallRequested += Serve;
        game.NewGameStarted += ResetForNewGame;
        game.StateChanged += OnState;
        for (int i = 1; i <= 16; i++) player.SetLamp("gi_" + i, 1f);
        if (TableSessionTransfer.TryConsume(gameObject.scene.name, out var session))
            game.ResumeExternalSession(session.Score, session.Balls, session.Multiplier);
        else game.StartExternalGame(config.startingBalls);
        RefreshObjectives();
        OnStarted?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private void BindHit(BumperApi bumper, int points, bool boss)
    {
        if (bumper == null) return;
        EventHandler<HitEventArgs> handler = (_, __) =>
        {
            if (!CanScore()) return;
            Award(points);
            audio?.Play(IndustriesSound.Bumper);
            if (boss) HitAutomate();
        };
        bumper.Hit += handler; unbind.Add(() => bumper.Hit -= handler);
    }
    private void Award(int points)
    {
        if (CanScore()) score.Add(points);
    }
    private void HitTarget(int index)
    {
        if (!CanScore()) return;
        score.RecordHit(ScoreElement.SimpleTarget, this, config.targetPoints, allowRepeat: true, courseValidated: true);
        audio?.Play(IndustriesSound.Target);
        if (production != null)
        {
            bool loaded = production.HitTarget(index);
            targets = production.Targets;
            if (automate?.QualifyTarget(index) == true)
            {
                automateFx.Activate();
                audio?.PlayAutomateMotor();
                game.ShowMessage("AUTOMATE DÉRÉGLÉ · VISEZ LE BUMPER ROUGE", 3.2f);
            }
            else if (loaded && !AutomateActive)
                game.ShowMessage((index < 3 ? "BOIS" : "TEXTILE") + " CHARGÉ · VISER LA RAMPE", 2f);
        }
        else targets |= 1 << index;
        if (production == null && targets == 63)
        {
            score.AddBonus(config.sixTargetsBonus);
            audio?.Play(IndustriesSound.Bonus);
            game.ShowMessage("CHAÎNE DE PRODUCTION COMPLÈTE +" + config.sixTargetsBonus, 2f);
            targets = 0;
            StartCoroutine(ResetTargets());
        }
        RefreshObjectives();
    }
    private IEnumerator ResetTargets()
    {
        yield return new WaitForSeconds(config.targetResetSeconds);
        foreach (var (target, bit) in dropTargets)
            if ((targets & (1 << bit)) == 0) target.IsDropped = false;
    }
    private void RefreshObjectives()
    {
        if (nextShotText != null && production != null)
            nextShotText.text = MultiballActive ? "JACKPOT AU SCOOP"
                : startingMultiball ? "MULTIBALL"
                : AutomateActive ? "TOUCHEZ L’AUTOMATE"
                : production.NeedsRamp ? "VISEZ LA RAMPE"
                : production.CanDeliver ? "VISEZ LA LIVRAISON"
                : automate != null ? "CIBLES BOSS  " + automate.QualificationHits + "/" + IndustriesAutomate.RequiredTargets
                : "TOUCHEZ LES CIBLES";
        if (objectivesText != null) objectivesText.text = AutomateActive
            ? "AUTOMATE" + (compactObjectivesLabel ? "\n" : "  ") + automate.Hits + "/" + automate.RequiredHits
            : MultiballActive ? "MULTIBALL\n2 BILLES"
            : production != null
            ? "BOIS " + production.Status(0) + (compactObjectivesLabel ? "\n" : " · ") + "TEXTILE " + production.Status(1)
            : compactObjectivesLabel ? TargetsLit+" / 6" : "PRODUCTION  " + TargetsLit + "/6  ·  BONUS " + config.sixTargetsBonus.ToString("N0");
        for (int i = 0; i < 3; i++)
        {
            api.Light("l" + (i+1))?.OnLamp((targets & (1 << i)) != 0 ? 1f : .08f);
            api.Light("l" + (i+11))?.OnLamp((targets & (1 << (i+3))) != 0 ? 1f : .08f);
        }
    }
    private void OnDrain(object sender, HitEventArgs e)
    {
        HandleDrainBall(e.BallId);
    }

    private void HandleDrainBall(int ballId)
    {
        if (!initialized || (game.State != GameManager.GameState.Playing && game.State != GameManager.GameState.ReadyToLaunch) ||
            !drains.Add(ballId)) return;
        production?.ForgetBall(ballId);
        if (startingMultiball) return;
        if (MultiballActive)
        {
            MultiballActive = false;
            RefreshObjectives();
            if (liveBalls.Count > (liveBalls.ContainsKey(ballId) ? 1 : 0))
            {
                game.ShowMessage("FIN DU MULTIBALL · UNE BILLE EN JEU", 2f);
                return;
            }
        }
        LoseBall();
    }

    private void TrackBallCreated(object sender, BallEvent e) => liveBalls[e.BallId] = e.Ball.transform;
    private void TrackBallDestroyed(object sender, BallEvent e) => liveBalls.Remove(e.BallId);

    private void LoseBall()
    {
        if (ballSaverDuration > 0f && !ballSaverUsed && Time.time < ballSaverEnd)
        {
            ballSaverUsed = true;
            audio?.Play(IndustriesSound.Bonus);
            game.NotifyBallReturnedToLane();
            game.ShowMessage("BILLE SAUVÉE !", 1.8f);
            QueueServe();
            return;
        }
        audio?.Play(IndustriesSound.Drain);
        game.LoseBall();
    }
    private void OnRoute(int line, bool entry, int ball)
    {
        if (!CanScore() || production == null) return;
        if (entry) production.Enter(line, ball, Time.time);
        else if (production.Exit(line, ball, Time.time))
        {
            score.RecordHit(ScoreElement.IutRamp, this, config.processingBonus, allowRepeat: true);
            audio?.Play(IndustriesSound.Bonus);
            if (!AutomateActive)
                game.ShowMessage((line == 0 ? "BOIS" : "TEXTILE") + " TRANSFORMÉ · VISER LIVRAISON", 2f);
            RefreshObjectives();
        }
    }
    private void OnSharedRoute(bool entry, int ball)
    {
        if (!CanScore() || production == null) return;
        int transformed = 0;
        for (int line = 0; line < 2; line++)
        {
            if (entry) production.Enter(line, ball, Time.time);
            else if (production.Exit(line, ball, Time.time)) transformed |= 1 << line;
        }
        if (transformed == 0) return;
        score.RecordHit(ScoreElement.IutRamp, this, config.processingBonus * (transformed == 3 ? 2 : 1), allowRepeat: true);
        audio?.Play(IndustriesSound.Bonus);
        if (!AutomateActive)
        {
            string material = transformed == 3 ? "BOIS ET TEXTILE" : transformed == 1 ? "BOIS" : "TEXTILE";
            game.ShowMessage(material + " TRANSFORMÉ" + (transformed == 3 ? "S" : "") + " · VISER LIVRAISON", 2f);
        }
        RefreshObjectives();
    }
    private bool CanScore() => initialized && Time.timeScale > 0f && !SceneTransition.IsPlaying &&
        (game.State == GameManager.GameState.Playing || game.State == GameManager.GameState.ReadyToLaunch);

    private void HitAutomate()
    {
        if (!AutomateActive) return;
        var hit = automate.Hit(Time.time);
        if (hit == IndustriesAutomateHit.Ignored) return;
        bool defeated = hit == IndustriesAutomateHit.Defeated;
        automateFx.Impact(defeated);
        string progress = automate.Hits + "/" + automate.RequiredHits;
        if (defeated)
        {
            score.AddBonus(config.automateBonus);
            audio?.Play(IndustriesSound.Bonus);
            foreach (var (target, _) in dropTargets) target.IsDropped = false;
            game.ShowMessage(progress + "  AUTOMATE ARRÊTÉ  +" + config.automateBonus.ToString("N0"), 2.6f);
        }
        else game.ShowMessage(progress + "  AUTOMATE TOUCHÉ", 1.3f);
        RefreshObjectives();
    }

    private void OnScoop(object sender, HitEventArgs e)
    {
        if (!CanScore()) return;
        Award(config.scoopPoints);
        audio?.Play(IndustriesSound.Scoop);
        bool jackpot = MultiballActive;
        bool multiStarted = false;
        if (production != null && production.Processed != 0)
        {
            bool doubleDelivery = production.Processed == 0b11;
            int bonus = production.Deliver();
            score.AddBonus(bonus);
            targets = production.Targets;
            StartCoroutine(ResetTargets());
            audio?.Play(IndustriesSound.Bonus);
            if (doubleDelivery && !startingMultiball && !MultiballActive)
            {
                startingMultiball = true;
                ballSaverEnd = float.NegativeInfinity;
                multiStarted = true;
                game.ShowMessage("DOUBLE LIVRAISON · MULTIBALL", 2.5f);
                StartCoroutine(StartMultiball());
            }
            if (!AutomateActive && !multiStarted && !jackpot)
                game.ShowMessage("LIVRAISON +" + bonus.ToString("N0"), 2f);
            RefreshObjectives();
        }
        if (jackpot)
        {
            score.AddBonus(config.multiballJackpot);
            audio?.Play(IndustriesSound.Bonus);
            game.ShowMessage("JACKPOT +" + config.multiballJackpot.ToString("N0"), 2.2f);
        }
        if (!multiStarted) StartCoroutine(EjectScoop());
    }

    private IEnumerator StartMultiball()
    {
        // Le scoop tient la première bille pendant le chargement du lanceur.
        plunger.PullBack();
        bool served = false;
        yield return MoveBallToLane(value => served = value);
        if (served) yield return new WaitForSeconds(.65f);
        plunger.FireFromFullRetract();
        if (served) audio?.Play(IndustriesSound.Launch);
        KickScoopBall();
        startingMultiball = false;
        MultiballActive = served && liveBalls.Count >= 2;
        if (MultiballActive)
            game.ShowMessage("MULTIBALL · VISEZ LE SCOOP", 2.6f);
        else if (liveBalls.Count == 0)
            LoseBall();
        RefreshObjectives();
    }
    private IEnumerator EjectScoop()
    {
        yield return new WaitForSeconds(config.scoopHoldSeconds);
        KickScoopBall();
    }

    private void KickScoopBall()
    {
        if (scoop != null && scoop.HasBall()) scoop.Kick(config.scoopKickAngle, config.scoopKickSpeed);
    }
    private void Serve()
    {
        drains.Clear();
        ballSaverUsed = false;
        ballSaverEnd = float.NegativeInfinity;
        QueueServe();
    }
    private void ResetForNewGame()
    {
        StopAllCoroutines();
        serveRoutine = null;
        startingMultiball = false;
        MultiballActive = false;
        targets = 0;
        production?.Reset();
        automate?.Reset();
        automateFx?.RestoreBumper();
        foreach (var (target, _) in dropTargets) target.IsDropped = false;
        RefreshObjectives();
    }
    private void QueueServe()
    {
        if (serveRoutine != null) StopCoroutine(serveRoutine);
        serveRoutine = StartCoroutine(MoveBallToLane());
    }
    private IEnumerator MoveBallToLane(Action<bool> completed = null)
    {
        yield return new WaitForSeconds(config.serveDelay);
        bool served = trough.EjectBall();
        for (int attempt = 0; !served && attempt < 4; attempt++)
        {
            yield return new WaitForSeconds(0.25f);
            served = trough.EjectBall();
        }
        if (!served)
        {
            Debug.LogWarning("[Industries] Le trough n'a pas pu fournir de bille ; vérifier ses références.", this);
            completed?.Invoke(false);
            yield break;
        }

        yield return new WaitForSeconds(0.5f);
        if (ballRelease != null && ballRelease.HasBall())
        {
            // Relâcher la bille déjà servie sans la prélever une seconde fois dans le trough.
            retryBallRelease?.Invoke();
            yield return new WaitForSeconds(0.5f);
            if (ballRelease.HasBall())
                Debug.LogWarning("[Industries] BallRelease retient toujours la bille après la rééjection.", this);
        }
        completed?.Invoke(true);
    }
    private void Update()
    {
        if (!initialized) return;
        bool canPlay = !SceneTransition.IsPlaying && Time.timeScale > 0f &&
            (game.State == GameManager.GameState.Playing || game.State == GameManager.GameState.ReadyToLaunch);
        bool l = canPlay && (input.LeftFlipperHeld || cabinet.GamePlayPF.LeftFlipper.IsPressed());
        bool r = canPlay && (input.RightFlipperHeld || cabinet.GamePlayPF.RightFlipper.IsPressed());
        if (l != lastLeft) { if (l) left.RotateToEnd(); else left.RotateToStart(); audio?.Play(l?IndustriesSound.FlipperUp:IndustriesSound.FlipperDown); lastLeft = l; }
        if (r != lastRight) { if (r) right.RotateToEnd(); else right.RotateToStart(); audio?.Play(r?IndustriesSound.FlipperUp:IndustriesSound.FlipperDown); lastRight = r; }
        bool releaseOccupied = ballRelease != null && ballRelease.HasBall();
        bool launch = canPlay && !startingMultiball && !releaseOccupied && (input.PlungerHeld || cabinet.GamePlayPF.LaunchBall.IsPressed());
        if (launch && !lastLaunch) plunger.PullBack();
        if (!launch && lastLaunch && canPlay && !startingMultiball && !releaseOccupied)
        {
            plunger.Fire();
            audio?.Play(IndustriesSound.Launch);
            if (ballSaverDuration > 0f && !ballSaverUsed) ballSaverEnd = Time.time + ballSaverDuration;
            game.NotifyBallLaunched();
        }
        lastLaunch = launch;

        if (canPlay && game.State == GameManager.GameState.Playing && troughDrain != null)
        {
            foreach (var (id, ball) in liveBalls)
            {
                if (ball == null) continue;
                Vector3 p = ball.position;
                // Bille tombée dans le bac du bas (sous le drain, hors couloir de lancement)
                if (p.z < 1.85f && p.x < 3.0f && !drains.Contains(id))
                {
                    // Le drain natif recycle également le prefab dans le stock du trough.
                    troughDrain.OnHit(id, false);
                    break;
                }
            }
        }
    }
    private void OnState(GameManager.GameState state)
    {
        if (state != GameManager.GameState.GameOver) return;
        MultiballActive = false;
        automate?.Reset();
        automateFx?.RestoreBumper();
        RefreshObjectives();
        left.RotateToStart(); right.RotateToStart();
    }
    private void OnDestroy()
    {
        foreach (var action in unbind) action();
        if (game != null)
        {
            game.ExternalBallRequested -= Serve;
            game.NewGameStarted -= ResetForNewGame;
            game.StateChanged -= OnState;
        }
        cabinet?.Dispose();
    }
    public void Switch(string id, bool closed) => OnSwitchChanged?.Invoke(this, new SwitchEventArgs2(id, closed));
    public void SetCoil(string id, bool enabled) => OnCoilChanged?.Invoke(this, new CoilEventArgs(id, enabled));
    public void SetLamp(string id, float value, bool isCoil = false, LampSource source = LampSource.Lamp)
        => OnLampChanged?.Invoke(this, new LampEventArgs(id, value, isCoil, source));
    public bool GetSwitch(string id) => player != null && player.SwitchStatuses.TryGetValue(id, out var s) && s.IsSwitchEnabled;
    public bool GetCoil(string id) => player != null && player.CoilStatuses.TryGetValue(id, out var c) && c;
    public LampState GetLamp(string id) => player != null && player.LampStatuses.TryGetValue(id, out var l) ? l : LampState.Default;
    public void DisplayChanged(DisplayFrameData data) { }
}
