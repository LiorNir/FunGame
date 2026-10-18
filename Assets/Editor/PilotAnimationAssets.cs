using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Configurations;
using UnityEditor;
using UnityEngine;
using Views;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Makes a pilot's animation prefab: a Sprite Renderer (for previewing), an Animation holding its Idle, Attack and
    /// Defeated clips (legacy) and a Sprite Frames holding its frames. The clips key Sprite Frames' Frame number, so they
    /// play on board pictures and hand cards alike. Edit the clips in the Animation window with the prefab open.
    /// </summary>
    public static class PilotAnimationAssets
    {
        public const string Folder = "Assets/Art/PilotAnimations";
        public const string IdleName = "Idle";
        public const string AttackName = "Attack";
        public const string DefeatedName = "Defeated";

        private const float DefaultFrameTime = 0.15f;

        /// <summary>
        /// For every pilot that has no animation prefab yet (upgraded copies use their base pilot's): a prefab whose frames
        /// are all the sprites cut from the pilot picture's sprite sheet, an Idle clip that loops through them, and empty
        /// Attack and Defeated clips to fill in. Existing prefabs are never touched.
        /// </summary>
        [MenuItem("Tools/Fun Game/Create Pilot Animation Prefabs")]
        public static void CreateMissing()
        {
            var made = 0;
            foreach (var pilot in AllPilots().Where(pilot => pilot.AnimationPrefab == null && pilot.BasePilot == null && pilot.Sprite != null))
            {
                var frames = SheetOf(pilot.Sprite);
                Build(pilot, frames, Enumerable.Range(0, frames.Count).ToList(), new List<int>(), new List<int>(), DefaultFrameTime);
                made++;
            }

            Debug.Log(made == 0 ? "Every pilot already has an animation prefab." : $"Made {made} pilot animation prefab(s) in {Folder}.");
        }

        /// <summary>
        /// Writes the pilot's prefab and clips (in Assets/Art/PilotAnimations/&lt;pilot&gt;) and assigns the prefab to the pilot.
        /// Each clip shows the listed frame numbers one after another, <paramref name="frameTime"/> seconds each; an empty
        /// list makes an empty clip (the pilot then has no animation for that moment).
        /// </summary>
        public static GameObject Build(PilotTokenConfiguration pilot, List<Sprite> frames, List<int> idle, List<int> attack,
                                       List<int> defeated, float frameTime)
        {
            var folder = $"{Folder}/{pilot.name}";
            Directory.CreateDirectory(folder);

            var clips = new[]
            {
                WriteClip($"{folder}/{IdleName}.anim", IdleName, idle, frameTime, WrapMode.Loop),
                WriteClip($"{folder}/{AttackName}.anim", AttackName, attack, frameTime, WrapMode.Once),
                WriteClip($"{folder}/{DefeatedName}.anim", DefeatedName, defeated, frameTime, WrapMode.ClampForever)
            };

            var root = new GameObject(pilot.name + " Animation");
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = frames.FirstOrDefault(sprite => sprite != null) ?? pilot.Sprite;
            var animation = root.AddComponent<Animation>();
            animation.playAutomatically = false;
            foreach (var clip in clips)
            {
                animation.AddClip(clip, clip.name);
            }

            animation.clip = clips[0];
            var spriteFrames = root.AddComponent<SpriteFrames>();
            spriteFrames.SetFrames(frames);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{pilot.name} Animation.prefab");
            Object.DestroyImmediate(root);

            pilot.AnimationPrefab = prefab;
            EditorUtility.SetDirty(pilot);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static AnimationClip WriteClip(string path, string name, List<int> frameNumbers, float frameTime, WrapMode wrapMode)
        {
            var clip = new AnimationClip { name = name, legacy = true, wrapMode = wrapMode };
            if (frameNumbers.Count > 0)
            {
                // one key per frame, held until the next (constant tangents); the last frame is held for its full time too
                var curve = new AnimationCurve();
                for (var index = 0; index < frameNumbers.Count; index++)
                {
                    curve.AddKey(new Keyframe(index * frameTime, frameNumbers[index]));
                }

                curve.AddKey(new Keyframe(frameNumbers.Count * frameTime, frameNumbers[frameNumbers.Count - 1]));
                for (var index = 0; index < curve.length; index++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Constant);
                }

                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(SpriteFrames), nameof(SpriteFrames.Frame)), curve);
            }

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                return existing;
            }

            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        /// <summary>All sprites cut from the same texture as <paramref name="sprite"/>, in the order of the number at the end of their names.</summary>
        public static List<Sprite> SheetOf(Sprite sprite)
        {
            var path = AssetDatabase.GetAssetPath(sprite);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
            if (sprites.Count == 0)
            {
                return new List<Sprite> { sprite };
            }

            return sprites.OrderBy(NumberAtEnd).ThenBy(candidate => candidate.name).ToList();
        }

        private static int NumberAtEnd(Sprite sprite)
        {
            var match = Regex.Match(sprite.name, @"(\d+)$");
            return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
        }

        private static IEnumerable<PilotTokenConfiguration> AllPilots()
        {
            return AssetDatabase.FindAssets("t:PilotTokenConfiguration")
                .Select(guid => AssetDatabase.LoadAssetAtPath<PilotTokenConfiguration>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(pilot => pilot != null);
        }
    }
}
