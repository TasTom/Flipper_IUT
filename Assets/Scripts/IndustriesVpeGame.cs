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
    [SerializeField] private bool compactObjectivesLabel;
    private Player player;
    private TableApi api;
    private FlipperApi left, right;
    private PlungerApi plunger;
    private TroughApi trough;
    private KickerApi scoop;
    private PinballControls cabinet;
    private IndustriesAudio audio;
    private bool initialized, lastLeft, lastRight, lastLaunch;
    private int targets;
    private IndustriesProduction production;
    private readonly HashSet<int> drains = new HashSet<int>();
    private readonly List<Action> unbind = new List<Action>();
    private readonly List<DropTargetApi> dropTargets = new List<DropTargetApi>();
    public bool IsInitialized => initialized;
    public IndustriesProduction Production => production;
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
        if (config == null || game == null || score == null || input == null ||
            left == null || right == null || plunger == null || trough == null)
        {
            Debug.LogWarning("[Industries] Config, gestionnaires ou éléments VPE manquants : partie inactive.", this);
            return Task.CompletedTask;
        }
        foreach (var c in GetComponentsInChildren<BumperComponent>()) BindHit(api.Bumper(c), config.bumperPoints);
        var targetNames = new[] { "sw1", "sw2", "sw3", "sw11", "sw12", "sw13" };
        foreach (var c in GetComponentsInChildren<DropTargetComponent>())
        {
            var target = api.DropTarget(c);
            if (target == null) continue;
            int bit = Array.IndexOf(targetNames, c.name);
            if (bit < 0) continue;
            EventHandler<HitEventArgs> handler = (_, __) => HitTarget(bit);
            target.Hit += handler; unbind.Add(() => target.Hit -= handler); dropTargets.Add(target);
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
        foreach (var c in GetComponentsInChildren<SpinnerComponent>())
        {
            var spinner = api.Spinner(c);
            EventHandler handler = (_, __) => Award(config.spinnerPoints);
            spinner.Spin += handler; unbind.Add(() => spinner.Spin -= handler);
        }
        var drain = api.Kicker("Drain");
        if (drain != null) { drain.Hit += OnDrain; unbind.Add(() => drain.Hit -= OnDrain); }
        if (scoop != null) { scoop.Hit += OnScoop; unbind.Add(() => scoop.Hit -= OnScoop); }
        initialized = true;
        game.ExternalBallRequested += Serve;
        game.StateChanged += OnState;
        for (int i = 1; i <= 16; i++) player.SetLamp("gi_" + i, 1f);
        if (TableSessionTransfer.TryConsume(gameObject.scene.name, out var session))
            game.ResumeExternalSession(session.Score, session.Balls, session.Multiplier);
        else game.StartExternalGame(config.startingBalls);
        RefreshObjectives();
        OnStarted?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private void BindHit(BumperApi bumper, int points)
    {
        if (bumper == null) return;
        EventHandler<HitEventArgs> handler = (_, __) => { Award(points); audio?.Play(IndustriesSound.Bumper); };
        bumper.Hit += handler; unbind.Add(() => bumper.Hit -= handler);
    }
    private void Award(int points)
    {
        if (CanScore()) score.Add(points);
    }
    private void HitTarget(int index)
    {
        if (!CanScore()) return;
        Award(config.targetPoints);
        audio?.Play(IndustriesSound.Target);
        if (production != null)
        {
            if (production.HitTarget(index))
                game.ShowMessage((index < 3 ? "BOIS" : "TEXTILE") + " CHARGÉ · VISER LA RAMPE", 2f);
            targets = production.Targets;
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
        if (production == null || (production.Loaded & 1) == 0)
            foreach (var t in dropTargets) t.IsDropped = false;
    }
    private void RefreshObjectives()
    {
        if (objectivesText != null) objectivesText.text = production != null
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
        if (initialized && drains.Add(e.BallId)) { production?.ForgetBall(e.BallId); audio?.Play(IndustriesSound.Drain); game.LoseBall(); }
    }
    private void OnRoute(int line, bool entry, int ball)
    {
        if (!CanScore() || production == null) return;
        if (entry) production.Enter(line, ball, Time.time);
        else if (production.Exit(line, ball, Time.time))
        {
            score.AddBonus(config.processingBonus);
            audio?.Play(IndustriesSound.Bonus);
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
        score.AddBonus(config.processingBonus * (transformed == 3 ? 2 : 1));
        audio?.Play(IndustriesSound.Bonus);
        string material = transformed == 3 ? "BOIS ET TEXTILE" : transformed == 1 ? "BOIS" : "TEXTILE";
        game.ShowMessage(material + " TRANSFORMÉ" + (transformed == 3 ? "S" : "") + " · VISER LIVRAISON", 2f);
        RefreshObjectives();
    }
    private bool CanScore() => initialized && Time.timeScale > 0f && !SceneTransition.IsPlaying &&
        (game.State == GameManager.GameState.Playing || game.State == GameManager.GameState.ReadyToLaunch);
    private void OnScoop(object sender, HitEventArgs e)
    {
        if (!CanScore()) return;
        Award(config.scoopPoints);
        audio?.Play(IndustriesSound.Scoop);
        if (production != null && production.Processed != 0)
        {
            int bonus = production.Deliver();
            score.AddBonus(bonus);
            audio?.Play(IndustriesSound.Bonus);
            game.ShowMessage("LIVRAISON +" + bonus.ToString("N0"), 2f);
            targets = production.Targets;
            StartCoroutine(ResetTargets());
            RefreshObjectives();
        }
        StartCoroutine(EjectScoop());
    }
    private IEnumerator EjectScoop()
    {
        yield return new WaitForSeconds(config.scoopHoldSeconds);
        if (scoop != null && scoop.HasBall()) scoop.Kick(config.scoopKickAngle, config.scoopKickSpeed);
    }
    private void Serve()
    {
        StopAllCoroutines();
        targets = 0;
        production?.Reset();
        foreach (var t in dropTargets) t.IsDropped = false;
        RefreshObjectives();
        StartCoroutine(ServeAfterDelay());
    }
    private IEnumerator ServeAfterDelay()
    {
        yield return new WaitForSeconds(config.serveDelay);
        if (!trough.EjectBall())
            Debug.LogWarning("[Industries] Le trough n'a pas pu fournir de bille ; vérifier ses références.", this);
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
        bool launch = canPlay && (input.PlungerHeld || cabinet.GamePlayPF.LaunchBall.IsPressed());
        if (launch && !lastLaunch) plunger.PullBack();
        if (!launch && lastLaunch && canPlay) { plunger.Fire(); audio?.Play(IndustriesSound.Launch); game.NotifyBallLaunched(); }
        lastLaunch = launch;
    }
    private void OnState(GameManager.GameState state)
    {
        if (state != GameManager.GameState.GameOver) return;
        left.RotateToStart(); right.RotateToStart();
    }
    private void OnDestroy()
    {
        foreach (var action in unbind) action();
        if (game != null) { game.ExternalBallRequested -= Serve; game.StateChanged -= OnState; }
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
