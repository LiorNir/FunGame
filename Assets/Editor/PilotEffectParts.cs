using System.IO;
using BattleView;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// The particle effects of the pilot slot prefab: an Effects group with eight placeholder particle systems (summon,
    /// ability, hit, defeated, buff, debuff, and the looping targeted and ability ready), each a different colour so they can be told apart until they are
    /// restyled, and the Pilot Effects component that plays them. Adds what is missing; existing systems are never changed.
    /// </summary>
    public static class PilotEffectParts
    {
        private const string MaterialPath = "Assets/Art/Effects/Particle Placeholder.mat";
        private const int SortingOrder = 12;

        private sealed class Look
        {
            public string Name;
            public string Field;
            public Color Color;
            public int Burst;
            public float Speed;
            public float Lifetime;
            public float Gravity;
            public bool Loop;
        }

        private static readonly Look[] Looks =
        {
            new Look { Name = "Summon", Field = "_summon", Color = new Color(0.6f, 0.95f, 1f), Burst = 30, Speed = 1.5f, Lifetime = 0.6f },
            new Look { Name = "Ability", Field = "_ability", Color = new Color(1f, 0.82f, 0.3f), Burst = 40, Speed = 2f, Lifetime = 0.6f },
            new Look { Name = "Hit", Field = "_hit", Color = new Color(1f, 0.4f, 0.2f), Burst = 18, Speed = 2.5f, Lifetime = 0.35f },
            new Look { Name = "Defeated", Field = "_defeated", Color = new Color(0.45f, 0.35f, 0.55f), Burst = 30, Speed = 0.8f, Lifetime = 1f, Gravity = -0.3f },
            new Look { Name = "Buff", Field = "_buff", Color = new Color(0.45f, 1f, 0.45f), Burst = 20, Speed = 0.8f, Lifetime = 0.8f, Gravity = -0.6f },
            new Look { Name = "Debuff", Field = "_debuff", Color = new Color(0.8f, 0.3f, 0.9f), Burst = 20, Speed = 0.8f, Lifetime = 0.8f, Gravity = 0.6f },
            new Look { Name = "Targeted", Field = "_targeted", Color = new Color(1f, 0.25f, 0.25f), Speed = 0.1f, Lifetime = 0.8f, Loop = true },
            new Look { Name = "AbilityReady", Field = "_abilityReady", Color = new Color(1f, 0.9f, 0.45f), Speed = 0.1f, Lifetime = 0.8f, Gravity = -0.2f, Loop = true }
        };

        /// <summary>Makes sure the slot has its Pilot Effects and every effect; returns the component.</summary>
        public static PilotEffects Ensure(GameObject slotRoot, Transform parent)
        {
            var effects = slotRoot.GetComponent<PilotEffects>();
            if (effects == null)
            {
                effects = slotRoot.AddComponent<PilotEffects>();
            }

            var group = parent.Find("Effects");
            if (group == null)
            {
                group = new GameObject("Effects").transform;
                group.SetParent(parent, false);
            }

            var serialized = new SerializedObject(effects);
            foreach (var look in Looks)
            {
                var existing = group.Find(look.Name);
                var system = existing != null ? existing.GetComponent<ParticleSystem>() : Create(group, look);
                serialized.FindProperty(look.Field).objectReferenceValue = system;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return effects;
        }

        private static ParticleSystem Create(Transform group, Look look)
        {
            var go = new GameObject(look.Name);
            go.transform.SetParent(group, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = look.Loop;
            main.duration = look.Loop ? 1f : 0.8f;
            main.startLifetime = look.Lifetime;
            main.startSpeed = look.Speed;
            main.startSize = 0.12f;
            main.startColor = look.Color;
            main.gravityModifier = look.Gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 100;

            var emission = system.emission;
            if (look.Loop)
            {
                emission.rateOverTime = 14f;
            }
            else
            {
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)look.Burst) });
            }

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = look.Loop ? 0.55f : 0.35f;
            shape.radiusThickness = look.Loop ? 0f : 1f;

            var colour = system.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = PlaceholderMaterial();
            renderer.sortingOrder = SortingOrder;
            return system;
        }

        /// <summary>A soft round particle that blends normally; shared by every placeholder effect.</summary>
        private static Material PlaceholderMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader);
            var texture = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (texture == null)
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Placeholders/Circle.png");
            }

            material.mainTexture = texture;
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
