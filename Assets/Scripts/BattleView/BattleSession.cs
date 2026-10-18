using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Configurations;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Encounters;
using FunGame.Battle;
using FunGame.Board;
using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// A playable battle for testing. Assign the encounter (or fill the list and browse it with the on-screen arrows),
    /// press Start, play, and stop or restart at any time, even in the middle of a turn. It builds the battle engine from
    /// the encounter, builds the views, forwards the player's choices to the engine, and plays a turn's resolution back
    /// step by step as animations.
    /// </summary>
    public class BattleSession : MonoBehaviour
    {
        [Header("What to play")]
        [Tooltip("The encounter that Start will play. Can be changed while the scene is running; it takes effect on the next Start.")]
        [SerializeField] private EncounterDefinition _encounter;

        [Tooltip("Encounters to browse with the arrows of the on-screen test panel. Use the button in this component's Inspector to fill it with every encounter in the project.")]
        [SerializeField] private List<EncounterDefinition> _encounters = new List<EncounterDefinition>();

        [SerializeField] private GameRules _gameRules;
        [SerializeField] private PilotDeck _deck;
        [SerializeField] private BattleVisuals _visuals;

        [Tooltip("0 picks a new random seed for every battle. Any other number repeats the same shuffle and random enemy choices.")]
        [SerializeField] private int _seed;

        [Tooltip("Start the encounter as soon as the scene starts.")]
        [SerializeField] private bool _startAutomatically;

        [Header("Scene")]
        [SerializeField] private Canvas _canvas;

        [Tooltip("The board root. Moving or scaling this object moves or scales the whole board. Created if empty.")]
        [SerializeField] private BoardView _boardView;

        [Tooltip("Created if empty. Only used for lanes that have no anchor.")]
        [SerializeField] private EnemiesView _enemiesView;

        [Tooltip("Optional: shows the encounter's world look (board art and background).")]
        [SerializeField] private WorldLookView _worldLook;

        [Tooltip("Where the enemy of each lane stands, in lane order. Move these objects by hand to place the enemies.")]
        [SerializeField] private List<Transform> _enemyAnchors = new List<Transform>();

        [Tooltip("Where things that belong to the player appear, such as the damage numbers when the player is hit. Move it by hand.")]
        [SerializeField] private Transform _playerAnchor;

        private BattleEngine _engine;
        private BoardView _board;
        private EnemiesView _enemies;
        private HandView _hand;
        private HudView _hud;
        private ActionMenuView _menu;
        private TestPanelView _testPanel;
        private SelectionController _selection;
        private CancellationTokenSource _battleCancellation;
        private CancellationToken _destroyToken;
        private bool _isResolving;

        // true when the battle was started from the campaign map: the player's own pilots, rewards on a win, back to the map
        private bool _campaignMode;

        [Tooltip("The campaign map scene, for the end screen's way back (campaign battles only).")]
        [SerializeField] private string _mapScene = "MapScene";

        [Tooltip("Every tutorial line of the game (shown once per save in campaign battles, always on the test bench).")]
        [SerializeField] private TutorialScript _tutorial;

        public BattleVisuals Visuals => _visuals;

        public BattleEngine Engine => _engine;
        public bool IsRunning => _engine != null;
        public EncounterDefinition Encounter => _encounter;

        /// <summary>True while no battle is running or a turn is resolving; the player cannot act.</summary>
        public bool IsBusy => _engine == null || _isResolving;

        private void Start()
        {
            _destroyToken = this.GetCancellationTokenOnDestroy();
            if (!BuildViews())
            {
                return;
            }

            if (Campaign.CampaignSession.PendingEncounter != null && Campaign.CampaignSession.Campaign != null)
            {
                _campaignMode = true;
                _encounter = Campaign.CampaignSession.PendingEncounter;
                _testPanel.gameObject.SetActive(false);
                _hud.SetMapButtonVisible(true);
                StartBattle();
                return;
            }

            ShowIdle();
            if (_startAutomatically)
            {
                StartBattle();
            }
        }

        private void OnDestroy()
        {
            WriteUnfinishedLog("left unfinished");
            CancelBattle();
        }

        // ---------- tutorial ----------

        // shows the encounter's lines for a trigger that were not seen yet (in campaign battles: once per save)
        private void ShowTutorial(TutorialTrigger trigger)
        {
            if (_tutorial == null || _engine == null)
            {
                return;
            }

            var profile = _campaignMode ? Campaign.CampaignSession.Profile : null;
            foreach (var line in _tutorial.Lines.Where(line => line.Encounter == _encounter && line.Trigger == trigger))
            {
                if (trigger == TutorialTrigger.TurnStart && line.Turn != _engine.Turn)
                {
                    continue;
                }

                if (profile != null)
                {
                    if (profile.SeenTutorials.Contains(line.Id))
                    {
                        continue;
                    }

                    profile.SeenTutorials.Add(line.Id);
                    Campaign.CampaignSession.Save();
                }
                else if (!_shownTutorials.Add(line.Id))
                {
                    continue;
                }

                _hud.ShowTutorial(line.Text);
            }
        }

        // on the test bench a line is shown once per battle
        private readonly HashSet<string> _shownTutorials = new HashSet<string>();

        // after a turn: what happened during it, then the new turn and the enemies' new intents
        private void ShowTurnTutorials(List<BattleEvent> events)
        {
            if (events.Any(e => e.Type == BattleEventType.ChainAbsorbed && e.C <= 0.001f))
            {
                ShowTutorial(TutorialTrigger.ChainAtZero);
            }

            if (events.Any(e => e.Type == BattleEventType.ChainDestroyed))
            {
                ShowTutorial(TutorialTrigger.ChainBroken);
            }

            if (events.Any(e => e.Type == BattleEventType.ChargeBroken))
            {
                ShowTutorial(TutorialTrigger.ChargeBroken);
            }

            if (events.Any(e => e.Type == BattleEventType.EnemySpawned && e.C >= 2f))
            {
                ShowTutorial(TutorialTrigger.NextEnemy);
            }

            ShowTutorial(TutorialTrigger.TurnStart);
            ShowIntentTutorials();
        }

        private void ShowIntentTutorials()
        {
            foreach (var lane in _engine.Lanes.Where(lane => lane.Current?.Intent != null))
            {
                var effects = lane.Current.Intent.Effects;
                var texts = _engine.DescribeIntent(lane);
                if (effects.Any(effect => effect is LockEffect))
                {
                    ShowTutorial(TutorialTrigger.EnemyLocks);
                }

                if (texts.Contains(SnipeEffect.AssassinateText))
                {
                    ShowTutorial(TutorialTrigger.EnemyAssassinates);
                }
                else if (effects.Any(effect => effect is SnipeEffect))
                {
                    ShowTutorial(TutorialTrigger.EnemySnipes);
                }

                if (effects.Any(effect => effect is ChargeEffect))
                {
                    ShowTutorial(TutorialTrigger.EnemyCharges);
                }
            }
        }

        // ---------- playtest log ----------

        // true once the current battle's playtest file is written (one file per battle)
        private bool _battleLogged;

        private void WriteBattleLog(string result)
        {
            if (_engine == null || _encounter == null || _battleLogged)
            {
                return;
            }

            _battleLogged = true;
            var setupText = FunGame.Sim.SetupText.Write(_encounter.DisplayName, _campaignMode ? "campaign" : "test bench", _engine.Setup);
            Campaign.PlaytestLog.WriteBattle(_encounter.DisplayName, result, setupText, _engine.Log);
        }

        private void WriteUnfinishedLog(string result)
        {
            if (_engine != null && _engine.Outcome == BattleOutcome.InProgress)
            {
                WriteBattleLog($"{result} on turn {_engine.Turn}");
            }
        }

        // ---------- setup ----------

        private bool BuildViews()
        {
            if (_deck == null || _visuals == null)
            {
                Debug.LogError("BattleSession needs a Pilot Deck and Battle Visuals assigned.", this);
                return false;
            }

            if (_canvas == null)
            {
                _canvas = FindFirstObjectByType<Canvas>();
            }

            if (_canvas == null)
            {
                Debug.LogError("BattleSession needs a Canvas in the scene.", this);
                return false;
            }

            if (_visuals.HudPrefab == null ||
                _visuals.EnemyPrefab == null || _visuals.CardPrefab == null)
            {
                Debug.LogError("The Battle Visuals asset is missing prefabs. Create the defaults with Tools > Fun Game > Create Battle Prefabs.", _visuals);
                return false;
            }

            ViewFactory.PopupPrefab = _visuals.PopupPrefab;

            if (_enemyAnchors.Count == 0 || _enemyAnchors.Any(anchor => anchor == null))
            {
                Debug.LogWarning("The Battle Session has no (or incomplete) enemy lane anchors, so enemies use fallback positions. Use the button in its Inspector to create the anchors, then move them by hand.", this);
            }

            _board = _boardView != null ? _boardView : CreateChild<BoardView>("Board");
            _enemies = _enemiesView != null ? _enemiesView : CreateChild<EnemiesView>("Enemies");
            if (_worldLook == null && _visuals.WorldLookPrefab != null)
            {
                _worldLook = Instantiate(_visuals.WorldLookPrefab, transform);
                _worldLook.name = "World Look";
            }
            _selection = CreateChild<SelectionController>("Selection");

            _hud = Instantiate(_visuals.HudPrefab, _canvas.transform);
            _hud.name = "Battle HUD";
            _hand = _hud.GetComponentInChildren<HandView>(true);
            _menu = _hud.GetComponentInChildren<ActionMenuView>(true);
            _testPanel = _hud.GetComponentInChildren<TestPanelView>(true);
            if (_hand == null || _menu == null || _testPanel == null)
            {
                Debug.LogError("The HUD prefab must contain a HandView, an ActionMenuView and a TestPanelView.", _visuals.HudPrefab);
                return false;
            }

            _hand.Init(_visuals);
            _selection.Init(this, _board, _hand, _hud, _menu);

            _hud.PlayTurnClicked += () => PlayTurn().Forget();
            _hud.RestartClicked += StartBattle;
            _hud.MapClicked += () =>
            {
                Campaign.CampaignSession.PendingEncounter = null;
                UnityEngine.SceneManagement.SceneManager.LoadScene(_mapScene);
            };
            _hud.GiveUpConfirmed += GiveUp;
            _hud.UndoClicked += Undo;

            // hover explanations
            _board.SlotHovered += (slot, entered) =>
            {
                if (entered) _hud.ShowTooltip(_board.DescribeSlot(slot));
                else _hud.HideTooltip();
            };
            _board.OperatorHovered += (view, entered) =>
            {
                if (entered) _hud.ShowTooltip(HoverTexts.Operator(view.Operation));
                else _hud.HideTooltip();
            };
            _enemies.EnemyHovered += (laneId, entered) =>
            {
                if (entered) _hud.ShowTooltip(_enemies.Describe(laneId));
                else _hud.HideTooltip();
            };
            _testPanel.StartClicked += StartBattle;
            _testPanel.StopClicked += StopBattle;
            _testPanel.PreviousClicked += () => BrowseEncounters(-1);
            _testPanel.NextClicked += () => BrowseEncounters(1);
            return true;
        }

        private T CreateChild<T>(string childName) where T : Component
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        // ---------- starting and stopping ----------

        /// <summary>Starts the chosen encounter, replacing any battle in progress.</summary>
        public void StartBattle()
        {
            if (_selection == null)
            {
                return; // the scene has not started yet
            }

            if (_encounter == null)
            {
                Debug.LogError("Assign an Encounter to the Battle Session first.", this);
                return;
            }

            var problems = EncounterChecks.Validate(_encounter).Where(issue => issue.Severity == IssueSeverity.Error).ToList();
            if (problems.Count > 0)
            {
                Debug.LogError($"The encounter '{_encounter.name}' has errors:\n" + string.Join("\n", problems.Select(problem => problem.Message)), _encounter);
                return;
            }

            WriteUnfinishedLog("restarted");
            CancelBattle();
            ClearViews();
            _undoSnapshots.Clear();
            _undoLogLengths.Clear();
            _battleLogged = false;
            _shownTutorials.Clear();
            _battleCancellation = CancellationTokenSource.CreateLinkedTokenSource(_destroyToken);

            var seed = _seed != 0 ? _seed : UnityEngine.Random.Range(1, int.MaxValue);
            var setup = _campaignMode
                ? BattleSetupBuilder.Build(_encounter, _gameRules, Campaign.CampaignSession.BuildDeck(), seed, useTestDeck: false)
                : BattleSetupBuilder.Build(_encounter, _gameRules, _deck, seed);
            _engine = new BattleEngine(setup);
            _engine.Start();
            _engine.DrainEvents();

            _board.Build(_encounter.Board, _engine, _visuals);
            if (_worldLook != null)
            {
                _worldLook.Show(_encounter.Look, _visuals.DefaultLook);
            }
            _enemies.Build(_engine, _visuals, _enemyAnchors);
            RefreshAll();
            _hud.ClearTutorial();
            _hud.HideTooltip();
            ShowTutorial(TutorialTrigger.BattleStart);
            ShowTutorial(TutorialTrigger.TurnStart);
            ShowIntentTutorials();
            Debug.Log($"Started '{_encounter.name}' with seed {seed}.");
        }

        /// <summary>Ends the battle in progress (even mid-resolution) and clears the board.</summary>
        public void StopBattle()
        {
            if (_selection == null)
            {
                return;
            }

            CancelBattle();
            ClearViews();
            ShowIdle();
        }

        private void CancelBattle()
        {
            if (_battleCancellation != null)
            {
                _battleCancellation.Cancel();
                _battleCancellation.Dispose();
                _battleCancellation = null;
            }

            DOTween.KillAll();
            _isResolving = false;
        }

        private void ClearViews()
        {
            _selection.Cancel();
            _menu.Close();
            _hud.HideOutcome();
            _board.Clear();
            _enemies.Clear();
            _hand.Clear();
            _engine = null;
        }

        private void ShowIdle()
        {
            _hud.ShowIdle();
            ShowTestPanel(false);
        }

        private void ShowTestPanel(bool running)
        {
            if (_campaignMode)
            {
                return;
            }

            _testPanel.Show(_encounter != null ? _encounter.DisplayName : null, _encounters.IndexOf(_encounter) + 1, _encounters.Count, running);
        }

        private void BrowseEncounters(int step)
        {
            if (_encounters.Count == 0)
            {
                return;
            }

            var index = _encounters.IndexOf(_encounter);
            index = index < 0 ? 0 : (index + step + _encounters.Count) % _encounters.Count;
            _encounter = _encounters[index];
            ShowTestPanel(IsRunning);
        }

        // ---------- player commands (called by the selection controller) ----------

        // the battle before each action of this turn, newest last; cleared when the turn is played
        private readonly List<BattleEngine> _undoSnapshots = new List<BattleEngine>();

        /// <summary>
        /// Takes back the last action of this turn. Planning reveals nothing hidden (no draws, no dice), so this only
        /// fixes a misplay; a played turn cannot be taken back.
        /// </summary>
        public void Undo()
        {
            if (IsBusy || _undoSnapshots.Count == 0)
            {
                return;
            }

            // the copy has no log: carry the log over up to the moment of the copy, and mark the undo
            var log = _engine.Log;
            var keep = _undoLogLengths[_undoLogLengths.Count - 1];
            _undoLogLengths.RemoveAt(_undoLogLengths.Count - 1);
            _engine = _undoSnapshots[_undoSnapshots.Count - 1];
            _undoSnapshots.RemoveAt(_undoSnapshots.Count - 1);
            _engine.Log.AddRange(log.Take(keep));
            _engine.Log.Add($"  Undo: took back \"{(log.Count > keep ? log[keep].Trim() : "an action")}\"");
            _engine.Logging = true;
            _engine.RecordEvents = true;
            _selection.Cancel();
            _menu.Close();
            _board.Rebind(_engine);
            _enemies.Rebind(_engine);
            RefreshAll();
        }

        // the log's length when each snapshot was taken (the copies have no log of their own)
        private readonly List<int> _undoLogLengths = new List<int>();

        private void SaveUndo()
        {
            _undoSnapshots.Add(_engine.Clone());
            _undoLogLengths.Add(_engine.Log.Count);
        }

        public void Place(int handIndex, string slotId)
        {
            if (!IsBusy && _engine.CanPlace(handIndex, slotId))
            {
                SaveUndo();
            }

            if (!IsBusy && _engine.TryPlace(handIndex, slotId))
            {
                RefreshAll();
                _board.GetSlot(slotId)?.Punch();
            }
        }

        public void Activate(string slotId)
        {
            if (!IsBusy && _engine.CanActivate(slotId))
            {
                SaveUndo();
            }

            if (!IsBusy && _engine.TryActivate(slotId))
            {
                var slot = _board.GetSlot(slotId);
                RefreshAll();
                if (slot != null)
                {
                    slot.Punch(0.3f);
                    slot.PlayAttack();
                    ViewFactory.ShowPopup(slot.transform.position + Vector3.up * 0.9f, _engine.PilotBySlot[slotId].Ability.Name + "!", new Color(1f, 0.82f, 0.29f), 0.5f);
                }

                ShowTutorial(TutorialTrigger.AbilityUsed);
            }
        }

        public void Eject(string slotId)
        {
            if (!IsBusy && _engine.CanEject(slotId))
            {
                SaveUndo();
            }

            if (!IsBusy && _engine.TryEject(slotId))
            {
                RefreshAll();
            }
        }

        public void Unlock(string slotId)
        {
            if (!IsBusy && _engine.CanUnlock(slotId))
            {
                SaveUndo();
            }

            if (!IsBusy && _engine.TryUnlock(slotId))
            {
                RefreshAll();
                _board.GetSlot(slotId)?.PlayUnlock();
            }
        }

        public void GiveUp()
        {
            if (IsBusy || _engine.Outcome != BattleOutcome.InProgress)
            {
                return;
            }

            _selection.Cancel();
            _menu.Close();
            _engine.GiveUp();
            _engine.DrainEvents();
            RefreshAll();
            ShowOutcome();
        }

        public void RefreshAll()
        {
            if (_engine == null)
            {
                return;
            }

            _board.Refresh();
            _enemies.Refresh();
            _hand.Refresh(_engine, _selection.IsPlacing ? _selection.SelectedHandIndex : -1);
            _hud.Refresh(_engine, !IsBusy);
            _hud.SetUndoEnabled(!IsBusy && _undoSnapshots.Count > 0 && _engine.Outcome == BattleOutcome.InProgress);
            ShowTestPanel(true);
        }

        // ---------- turn resolution ----------

        private async UniTaskVoid PlayTurn()
        {
            if (IsBusy || _engine.Outcome != BattleOutcome.InProgress)
            {
                return;
            }

            var token = _battleCancellation.Token;
            var engine = _engine;
            _undoSnapshots.Clear();
            _undoLogLengths.Clear();
            _isResolving = true;
            _selection.Cancel();
            _hud.SetPlayEnabled(false);

            engine.ResolveTurn();
            var events = engine.DrainEvents();
            try
            {
                await PlayEvents(events, token);
            }
            catch (OperationCanceledException)
            {
                return; // the battle was stopped or restarted
            }

            if (token.IsCancellationRequested || engine != _engine)
            {
                return;
            }

            _isResolving = false;
            RefreshAll();
            if (_engine.Outcome != BattleOutcome.InProgress)
            {
                ShowOutcome();
            }
            else
            {
                ShowTurnTutorials(events);
            }
        }

        private async UniTask PlayEvents(List<BattleEvent> events, CancellationToken token)
        {
            foreach (var battleEvent in events)
            {
                token.ThrowIfCancellationRequested();
                switch (battleEvent.Type)
                {
                    case BattleEventType.DivideByZero:
                        var culprit = _board.GetSlot(battleEvent.Slots.FirstOrDefault() ?? string.Empty);
                        var position = culprit != null ? culprit.transform.position : Vector3.zero;
                        ViewFactory.ShowPopup(position + Vector3.up * 0.9f, "DIVIDE BY ZERO!", Color.red, 0.6f);
                        if (culprit != null)
                        {
                            culprit.PlayDivideByZero();
                        }

                        await Wait(1.2f, token);
                        break;

                    case BattleEventType.PlayerAttacked:
                        if (battleEvent.A <= 0.0001f)
                        {
                            break; // no attack value: nothing to show
                        }

                        _board.PulseChains(battleEvent.LaneId, ChainRole.Attack);
                        if (battleEvent.C > 0.0001f)
                        {
                            _enemies.PlayHit(battleEvent.LaneId);
                        }

                        var damageText = battleEvent.C > 0.0001f ? "-" + DisplayFormat.Value(battleEvent.C) : "Blocked";
                        ViewFactory.ShowPopup(_enemies.PositionOf(battleEvent.LaneId) + Vector3.up * 0.4f, damageText,
                            battleEvent.C > 0.0001f ? _visuals.AttackColor : Color.gray, 0.6f);
                        await Wait(0.7f, token);
                        break;

                    case BattleEventType.EnemyDamaged:
                        await _enemies.SetHealthAnimated(battleEvent.LaneId, battleEvent.A, battleEvent.B).AwaitForComplete(cancellationToken: token);
                        break;

                    case BattleEventType.EnemyDefeated:
                        await _enemies.PlayDefeat(battleEvent.LaneId).AwaitForComplete(cancellationToken: token);
                        break;

                    case BattleEventType.EnemyAttacked:
                        if (battleEvent.A > 0f)
                        {
                            _enemies.PlayAttack(battleEvent.LaneId);
                            ViewFactory.ShowPopup(_enemies.PositionOf(battleEvent.LaneId) + Vector3.down * 1.9f,
                                $"{battleEvent.Subject}: {DisplayFormat.Value(battleEvent.A)}", _visuals.AttackColor, 0.4f);
                            await Wait(0.5f, token);
                        }

                        break;

                    case BattleEventType.ChainAbsorbed:
                        for (var index = 0; index < battleEvent.Slots.Count; index++)
                        {
                            _board.GetSlot(battleEvent.Slots[index])?.SetWoundAnimated(battleEvent.SlotValues[index], 0.5f);
                        }

                        PopupAtFirstSlot(battleEvent, "Absorbed " + DisplayFormat.Value(battleEvent.A), _visuals.DefenseColor);
                        await Wait(0.7f, token);
                        break;

                    case BattleEventType.ChainDestroyed:
                        var destroyed = PlayFalls(battleEvent);
                        PopupAtFirstSlot(battleEvent, "Chain destroyed!", _visuals.DefenseColor);
                        // the turn goes on once the popup has been read and every Defeated animation has played
                        await UniTask.WhenAll(destroyed, Wait(0.8f, token));
                        break;

                    case BattleEventType.PlayerDamaged:
                        _hud.FlashHealth();
                        ViewFactory.ShowPopup(_playerAnchor != null ? _playerAnchor.position : new Vector3(-7.5f, 4.4f, 0f), "-" + DisplayFormat.Value(battleEvent.A), Color.red, 0.6f);
                        await _hud.SetHealthAnimated(battleEvent.B).AwaitForComplete(cancellationToken: token);
                        break;

                    case BattleEventType.PilotLocked:
                        var lockedSlot = _board.GetSlot(battleEvent.Slots.FirstOrDefault() ?? string.Empty);
                        if (lockedSlot != null)
                        {
                            lockedSlot.Punch(0.3f);
                        }

                        PopupAtFirstSlot(battleEvent, $"Locked {DisplayFormat.Value(battleEvent.A)}!", new Color(1f, 0.55f, 0.35f));
                        await Wait(0.6f, token);
                        break;

                    case BattleEventType.ChargeBroken:
                        ViewFactory.ShowPopup(_enemies.PositionOf(battleEvent.LaneId) + Vector3.down * 1.9f, "Charge broken!", new Color(1f, 0.85f, 0.3f), 0.6f);
                        await Wait(0.7f, token);
                        break;

                    case BattleEventType.EnemyStunned:
                        ViewFactory.ShowPopup(_enemies.PositionOf(battleEvent.LaneId) + Vector3.down * 1.9f, "Stunned!", new Color(1f, 0.85f, 0.3f), 0.6f);
                        await Wait(0.7f, token);
                        break;

                    case BattleEventType.PilotAssassinated:
                        var assassinated = PlayFalls(battleEvent);
                        PopupAtFirstSlot(battleEvent, "Assassinated!", new Color(1f, 0.3f, 0.3f));
                        await UniTask.WhenAll(assassinated, Wait(0.8f, token));
                        break;

                    case BattleEventType.EffectFizzled:
                        ViewFactory.ShowPopup(_enemies.PositionOf(battleEvent.LaneId) + Vector3.down * 1.9f, $"{battleEvent.Subject} missed: {battleEvent.Detail}", Color.gray, 0.4f);
                        await Wait(0.5f, token);
                        break;

                    case BattleEventType.SlotUnlocked:
                        _board.GetSlot(battleEvent.Subject)?.PlayUnlock();
                        await Wait(0.4f, token);
                        break;

                    case BattleEventType.EnemySpawned:
                        _enemies.ShowSpawn(battleEvent.LaneId, Mathf.RoundToInt(battleEvent.C));
                        await Wait(0.5f, token);
                        break;

                    case BattleEventType.IntentRevealed:
                        _enemies.SetIntent(battleEvent.LaneId);
                        break;
                }
            }
        }

        private void PopupAtFirstSlot(BattleEvent battleEvent, string text, Color color)
        {
            var slot = battleEvent.Slots.Select(slotId => _board.GetSlot(slotId)).FirstOrDefault(view => view != null);
            if (slot != null)
            {
                ViewFactory.ShowPopup(slot.transform.position + Vector3.up * 0.9f, text, color, 0.4f);
            }
        }

        private void ShowOutcome()
        {
            var details = $"Turns: {_engine.Turn}   HP left: {DisplayFormat.Value(_engine.PlayerHealth)}";
            if (_engine.DefeatReason != null)
            {
                details = $"({_engine.DefeatReason})\n" + details;
            }
            // the stars this battle met; in the campaign they are kept (each star stays once earned)
            var stars = _encounter.Stars;
            var metStars = FunGame.Progress.StarRating.Evaluate(stars, FunGame.Progress.BattleEnd.Of(_engine));
            var newStars = 0;
            if (_engine.Outcome == BattleOutcome.Victory && _campaignMode)
            {
                // the win is recorded and saved right away
                var id = Campaign.CampaignDefinition.IdOf(_encounter);
                var reward = Campaign.CampaignSession.Progression.ClaimVictory(id);
                newStars = Campaign.CampaignSession.Progression.RecordStars(id, metStars);
                Campaign.CampaignSession.Save();
                details += reward.FirstClear ? $"\nReward: {Campaign.CampaignSession.Describe(reward)}" : "\n(already cleared: no new reward)";
            }
            else if (_engine.Outcome == BattleOutcome.Victory)
            {
                details += $"\nReward: {_encounter.RewardSkillPoints} SP";
            }

            details += "\n\n" + Campaign.StarText.Lines(stars, metStars, newStars, _engine.Setup.Rules.PlayerMaxHealth);
            _hud.ShowOutcome(_engine.Outcome, details);
            var starsLogged = $", stars {FunGame.Progress.StarRating.Count(metStars)}/{stars.Count}";
            WriteBattleLog(_engine.Outcome == BattleOutcome.Defeat && _engine.DefeatReason != null
                ? $"Defeat ({_engine.DefeatReason}) on turn {_engine.Turn}{starsLogged}"
                : $"{_engine.Outcome} on turn {_engine.Turn}, HP {DisplayFormat.Value(_engine.PlayerHealth)}{starsLogged}");
        }

        /// <summary>Every pilot of the event falls at once; the task ends when the last Defeated animation has played.</summary>
        private UniTask PlayFalls(BattleEvent battleEvent)
        {
            return UniTask.WhenAll(battleEvent.Slots
                .Select(slotId => _board.GetSlot(slotId))
                .Where(slot => slot != null)
                .Select(slot => slot.PlayFallen()));
        }

        private static UniTask Wait(float seconds, CancellationToken token)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token);
        }
    }
}
