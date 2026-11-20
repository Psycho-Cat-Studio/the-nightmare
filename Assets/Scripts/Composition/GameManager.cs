using System;
using System.Collections.Generic;
using Nightmare.Core.Match;
using Nightmare.Configuration;
using UnityEngine;

/// <summary>Composes one match and orders operations between its rules, world, players, transport and presentation.</summary>
public class GameManager : MonoBehaviour, Nightmare.Core.Ports.IMatchStateView
{
    [SerializeField] private CampaignDefinition _campaign;
    [SerializeField] private ContentCatalog _content;
    [SerializeField] private StageBinding[] _stageBindings;
    [SerializeField] private MatchSceneBindings _sceneBindings;
    [SerializeField] private MatchSettings _matchSettings;
    [SerializeField] private List<CameraFacing> _billboards = new List<CameraFacing>();
    [SerializeField] private GameObject[] _floorsToDisable;
    private IMatchTransport _transport;
    private IMatchSceneRegistration _registration;
    private readonly Nightmare.Core.Ports.IClock _clockSource = new UnityGameClock();
    private const float SpawnCutoffSeconds = 10f;
    public MatchSession Session { get; } = new MatchSession();
    public MatchWorldRegistry World { get; private set; }
    public MatchPlayerCoordinator Players { get; private set; }
    public EnemyWaveSpawner Spawner { get; private set; }
    public MatchHudPresenter Presenter { get; private set; }
    public ContentCatalog Content => _content;
    public MatchSceneBindings SceneBindings => _sceneBindings;
    public GameObject[] FloorsToDisable => _floorsToDisable;
    public GameObject[] Weapons { get; private set; }
    public PhaseClock Clock => Session.Clock;
    public MatchProgress Progress => Session.Progress;
    public MatchPhase Phase => Progress.Phase;
    public int StageIndex => Progress.StageIndex;
    public int StageCount => _campaign.Stages.Length;
    public bool CanDamagePlayers => Progress.CanDamagePlayers;
    public bool IsMatchOver => Progress.IsOver;
    public bool isServer => _transport != null && _transport.IsAuthority;
    public bool TransportActive => _transport != null && _transport.IsActive;
    public int ReadyPlayersCnt { get; set; }
    /// <summary>Raised after ordered local transition effects, including stage cleanup, have completed.</summary>
    public event Action<MatchProgress> ProgressChanged;

    private void Awake()
    {
        if (_campaign == null || _content == null || _sceneBindings == null ||
            _stageBindings == null || _stageBindings.Length != _campaign.Stages.Length)
        {
            Debug.LogWarning("This archived scene has no configured campaign. Match startup is disabled.", this);
            enabled = false;
            return;
        }
        _transport = GetComponent<IMatchTransport>();
        World = new MatchWorldRegistry(_sceneBindings.RoomsRoot, _billboards);
        Session.Apply(new MatchProgress(MatchPhase.Lobby), 0f);
        Players = new MatchPlayerCoordinator(this);
        Spawner = new EnemyWaveSpawner(this, _transport, World, Players, _matchSettings);
        Weapons = Array.ConvertAll(_content.Weapons, weapon => weapon.Prefab);
        Presenter = gameObject.AddComponent<MatchHudPresenter>();
        Presenter.Initialize(this, _sceneBindings);
        _registration = SessionServices.Current as IMatchSceneRegistration;
        _registration?.SetMatch(this);
        foreach (var root in gameObject.scene.GetRootGameObjects()) MatchBindings.BindHierarchy(root, this);
        ResetPresentation();
    }

    private void Start()
    {
        if (World == null) return;
        foreach (var component in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (component is IMatchParticipant participant) participant.InitializeMatch(this);
        World.TurnOnGridRenders(false);
    }
    private void Update()
    {
        Clock.Tick(_clockSource.DeltaTime);
        if (ShouldStopSpawning) Spawner.Stop();
    }
    private bool ShouldStopSpawning => Phase == MatchPhase.Defend && Clock.IsRunning &&
        Clock.Remaining > 0f && Clock.Remaining <= SpawnCutoffSeconds;
    private void LateUpdate()
    {
        if (!isServer || IsMatchOver) return;
        ServerSetProgress(Session.Resolve(Players.AllPlayersDown(), World.HasLostCurrentArea(), StageCount));
    }

    /// <summary>Applies a backend-confirmed transition without publishing it again.</summary>
    public void ApplyReplicatedProgress(MatchProgress progress)
    {
        if (World == null || Progress.Equals(progress)) return;
        bool completed = Session.CompletesStage(progress);
        if (completed)
        {
            Spawner.Stop();
            if (isServer) World.RetireSurvivors();
        }
        float duration = 0f;
        if (progress.Phase == MatchPhase.Prepare || progress.Phase == MatchPhase.Defend)
        {
            var stage = _campaign.Stages[progress.StageIndex];
            duration = progress.Phase == MatchPhase.Prepare ? stage.PreparationSeconds : stage.DefenseSeconds;
        }
        Session.Apply(progress, duration);
        if (Phase == MatchPhase.Prepare)
        {
            Spawner.Stop();
            var local = Players.LocalPlayer;
            World.Activate(_stageBindings[StageIndex], local != null ? local.GetComponent<PlayerSetup>().ActionCamera : null);
        }
        else if (Phase == MatchPhase.Defend) Spawner.Start(_campaign.Stages[StageIndex], StageIndex);
        else if (IsMatchOver) Spawner.Stop();
        ProgressChanged?.Invoke(progress);
    }

    /// <summary>Authority-only transition; repeated notifications and attempts to replace a final result are ignored.</summary>
    public void ServerSetProgress(MatchProgress progress)
    {
        if (!isServer || IsMatchOver || Progress.Equals(progress)) return;
        ApplyReplicatedProgress(progress);
        _transport.PublishProgress(progress);
    }
    /// <summary>Whether a candle area belongs to the active defense; completed and future rooms cannot drain.</summary>
    public bool CanCapture(CaptureArea area) => Phase == MatchPhase.Defend && World.ContainsCurrentArea(area);
    /// <summary>Accepts a connected player once while in lobby; starts the campaign only when the current roster is ready.</summary>
    public void ServerRegisterReady(string playerId)
    {
        bool canRegister = isServer && Players.Players.ContainsKey(playerId);
        if (!canRegister || !Session.RegisterReady(playerId)) return;
        PublishReadiness();
    }
    /// <summary>Withdraws readiness after authoritative player removal and re-evaluates the remaining lobby.</summary>
    public void PlayerDisconnected(string id)
    {
        Session.RemoveReady(id);
        PublishReadiness();
    }
    private void PublishReadiness()
    {
        ReadyPlayersCnt = Session.ReadyCount;
        _transport.PublishReady(ReadyPlayersCnt);
        if (!Session.CanBegin(Players.Players.Count)) return;
        ServerSetProgress(new MatchProgress(MatchPhase.Prepare));
        _transport.BeginCampaign();
    }
    /// <summary>Performs local setup after the backend confirms campaign entry.</summary>
    public void EnterCampaign()
    {
        if (Players.HasEnteredCampaign) return;
        ApplyReplicatedProgress(new MatchProgress(MatchPhase.Prepare));
        Players.EnterCampaign();
    }
    /// <summary>Orders player controls, authority spawning and local grid presentation for the requested control mode.</summary>
    public void SetControlMode(PlayerControlMode mode)
    {
        Players.SetControlMode(mode);
        if (mode == PlayerControlMode.Building && isServer) Spawner.Stop();
        World.TurnOnGridRenders(mode == PlayerControlMode.Building);
    }
    private static void ResetPresentation()
    {
        ButtonsControll.screensOver = false;
        WinLoseScreens.winLoseActive = false;
        PauseGame.menuActive = false;
    }
    private void OnDestroy()
    {
        if (World == null) return;
        Spawner.Stop();
        World.SetCameraForBillboards(null);
        _registration?.SetMatch(null);
        foreach (var component in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (component.gameObject.scene.name == "DontDestroyOnLoad" && component is IMatchParticipant participant)
                participant.InitializeMatch(null);
        ResetPresentation();
    }
}
