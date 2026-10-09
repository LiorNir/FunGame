using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using FunGame.Battle;
using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// Puts one <see cref="EnemyView"/> prefab in each lane. A lane stands at the matching anchor object of the scene
    /// (move the anchors by hand); if there are fewer anchors than lanes the missing ones are placed using the
    /// fallback numbers below.
    /// </summary>
    public class EnemiesView : MonoBehaviour
    {
        [Header("Used only for lanes without an anchor")]
        [SerializeField] private float _enemyY = 3.1f;
        [SerializeField] private float _laneSpacing = 4.6f;
        [SerializeField] private float _wideSpread = 6.5f;

        private readonly Dictionary<string, EnemyView> _views = new Dictionary<string, EnemyView>();
        private BattleEngine _engine;

        public void Build(BattleEngine engine, BattleVisuals visuals, IReadOnlyList<Transform> anchors)
        {
            Clear();
            _engine = engine;

            var count = engine.Lanes.Count;
            for (var index = 0; index < count; index++)
            {
                var laneId = engine.Lanes[index].Spec.Id;
                var anchor = anchors != null && index < anchors.Count ? anchors[index] : null;
                var view = Instantiate(visuals.EnemyPrefab, anchor != null ? anchor : transform);
                view.name = "Enemy " + laneId;
                if (anchor != null)
                {
                    view.transform.localPosition = Vector3.zero;
                }
                else
                {
                    view.transform.localPosition = new Vector3(FallbackX(index, count), _enemyY, 0f);
                }

                view.Init(visuals);
                _views[laneId] = view;
            }

            Refresh();
        }

        public void Clear()
        {
            foreach (var view in _views.Values)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            _views.Clear();
        }

        public Vector3 PositionOf(string laneId)
        {
            return _views.TryGetValue(laneId, out var view) ? view.transform.position : transform.position;
        }

        /// <summary>Shows the current state of every lane.</summary>
        public void Refresh()
        {
            foreach (var lane in _engine.Lanes)
            {
                var view = _views[lane.Spec.Id];
                var enemy = lane.Current;
                view.gameObject.SetActive(enemy != null);
                if (enemy == null)
                {
                    continue;
                }

                view.transform.localScale = Vector3.one;
                view.Show(enemy.Spec, enemy.Health);
                view.SetIntent(enemy.Intent);
            }
        }

        public void ShowSpawn(string laneId, int ordinal)
        {
            var lane = _engine.Lanes.First(candidate => candidate.Spec.Id == laneId);
            var spec = lane.Spec.Enemies[ordinal - 1];
            var view = _views[laneId];
            view.gameObject.SetActive(true);
            view.Show(spec, spec.MaxHealth);
            view.PlaySpawn();
        }

        /// <summary>Shows the lane's revealed intent (read from the battle, whose state is already final while events play).</summary>
        public void SetIntent(string laneId)
        {
            var enemy = _engine.Lanes.First(candidate => candidate.Spec.Id == laneId).Current;
            if (enemy == null)
            {
                return;
            }

            _views[laneId].SetIntent(enemy.Intent);
            _views[laneId].PunchIntent();
        }

        public Tween SetHealthAnimated(string laneId, float health, float max)
        {
            return _views[laneId].SetHealthAnimated(health, max);
        }

        public void PlayHit(string laneId)
        {
            _views[laneId].PlayHit();
        }

        public void PlayAttack(string laneId)
        {
            _views[laneId].PlayAttack();
        }

        public Tween PlayDefeat(string laneId)
        {
            return _views[laneId].PlayDefeat();
        }

        private float FallbackX(int index, int count)
        {
            if (count <= 1)
            {
                return 0f;
            }

            if (count == 2)
            {
                return index == 0 ? -_laneSpacing : _laneSpacing;
            }

            return Mathf.Lerp(-_wideSpread, _wideSpread, index / (float)(count - 1));
        }
    }
}
