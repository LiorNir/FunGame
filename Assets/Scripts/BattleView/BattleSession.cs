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

            ShowIdle();
            if (_startAutomatically)
            {
                StartBattle();
            }
        }

        private void OnDestroy()
        {
            CancelBattle();
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

            CancelBattle();
            ClearViews();
            _battleCancellation = CancellationTokenSource.CreateLinkedTokenSource(_destroyToken);

            var seed = _seed != 0 ? _seed : UnityEngine.Random.Range(1, int.MaxValue);
            var setup = BattleSetupBuilder.Build(_encounter, _gameRules, _deck, seed);
            _engine = new BattleEngine(setup);
            _engine.Start();
            _engine.DrainEvents();

            _board.Build(_encounter.Board, _engine, _visuals);
            _enemies.Build(_engine, _visuals, _enemyAnchors);
            RefreshAll();
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

        public void Place(int handIndex, string slotId)
        {
            if (!IsBusy && _engine.TryPlace(handIndex, slotId))
            {
                RefreshAll();
                _board.GetSlot(slotId)?.Punch();
            }
        }

        public void Eject(string slotId)
        {
            if (!IsBusy && _engine.TryEject(slotId))
            {
                RefreshAll();
            }
        }

        public void Unlock(string slotId)
        {
            if (!IsBusy && _engine.TryUnlock(slotId))
            {
                RefreshAll();
                _board.GetSlot(slotId)?.PlayUnlock();
            }
        }

        public void ClearFallen(string slotId)
        {
            if (!IsBusy && _engine.ClearFallen(slotId))
            {
                RefreshAll();
            }
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
                            culprit.Punch(0.6f);
                        }

                        await Wait(0.9f, token);
                        break;

                    case BattleEventType.PlayerAttacked:
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
                        foreach (var slotId in battleEvent.Slots)
                        {
                            _board.GetSlot(slotId)?.PlayFallen();
                        }

                        PopupAtFirstSlot(battleEvent, "Chain destroyed!", _visuals.DefenseColor);
                        await Wait(0.8f, token);
                        break;

                    case BattleEventType.PlayerDamaged:
                        _hud.FlashHealth();
                        ViewFactory.ShowPopup(_playerAnchor != null ? _playerAnchor.position : new Vector3(-7.5f, 4.4f, 0f), "-" + DisplayFormat.Value(battleEvent.A), Color.red, 0.6f);
                        await _hud.SetHealthAnimated(battleEvent.B).AwaitForComplete(cancellationToken: token);
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
                        _enemies.SetIntent(battleEvent.LaneId, battleEvent.Subject, Mathf.RoundToInt(battleEvent.A), Mathf.RoundToInt(battleEvent.B));
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
            if (_engine.Outcome == BattleOutcome.Victory)
            {
                details += $"\nReward: {_encounter.RewardSkillPoints} SP";
            }

            _hud.ShowOutcome(_engine.Outcome, details);
        }

        private static UniTask Wait(float seconds, CancellationToken token)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token);
        }
    }
}
