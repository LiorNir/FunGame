using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardAuthoring
{
    /// <summary>
    /// Editor-only reference rectangles for laying out a battle scene: where enemies, the board, the hand and the
    /// HUD elements go on the reference screen. Drawn as labelled boxes in the Scene view.
    /// </summary>
    public class ScreenZonesGuide : MonoBehaviour
    {
        [Serializable]
        public class Zone
        {
            public string Name;
            public Vector2 Center;
            public Vector2 Size;
            public Color Color = new Color(1f, 1f, 1f, 0.12f);

            public Zone(string name, Vector2 center, Vector2 size, Color color)
            {
                Name = name;
                Center = center;
                Size = size;
                Color = color;
            }
        }

        [SerializeField] private List<Zone> _zones = new List<Zone>();

        public IReadOnlyList<Zone> Zones => _zones;

        private void Reset()
        {
            _zones = CreateDefaultZones();
        }

        public static List<Zone> CreateDefaultZones()
        {
            var enemy = new Color(1f, 0.4f, 0.2f, 0.14f);
            var hud = new Color(1f, 1f, 1f, 0.12f);
            var board = new Color(0.3f, 0.6f, 1f, 0.12f);
            var player = new Color(0.4f, 1f, 0.6f, 0.14f);
            return new List<Zone>
            {
                new Zone("Top bar", new Vector2(0f, 5.1f), new Vector2(19.2f, 0.6f), hud),
                new Zone("Enemy lane 1", new Vector2(-4.6f, 3.4f), new Vector2(7.6f, 2.75f), enemy),
                new Zone("Enemy lane 2", new Vector2(4.6f, 3.4f), new Vector2(7.6f, 2.75f), enemy),
                new Zone("Board", BoardScreenMetrics.BoardCenter, BoardScreenMetrics.BoardSize, board),
                new Zone("Hand", new Vector2(0f, -4f), new Vector2(11f, 2.8f), hud),
                new Zone("Summoner", new Vector2(0f, -3.5f), new Vector2(1.6f, 1.8f), player),
                new Zone("AP badge", new Vector2(-8f, -4.2f), new Vector2(2f, 2f), hud),
                new Zone("Play Turn", new Vector2(7.9f, -4.3f), new Vector2(2.8f, 1.4f), hud)
            };
        }
    }
}
