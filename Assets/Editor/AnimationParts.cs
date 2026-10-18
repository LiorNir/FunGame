using UnityEditor;
using UnityEngine;
using Views;

namespace FunGame.EditorTools
{
    /// <summary>
    /// The parts a pilot picture needs to play the pilot's clips (a board slot's Pilot sprite or a hand card's Picture): a
    /// legacy Animation, a Sprite Frames that shows the frame the clips key, and the Pilot Animation View that loads and
    /// plays them. Adds whatever is missing and wires them together; existing parts are kept.
    /// </summary>
    public static class AnimationParts
    {
        public static PilotAnimationView Ensure(GameObject picture)
        {
            var animation = picture.GetComponent<Animation>();
            if (animation == null)
            {
                animation = picture.AddComponent<Animation>();
            }

            animation.playAutomatically = false;
            var frames = picture.GetComponent<SpriteFrames>();
            if (frames == null)
            {
                frames = picture.AddComponent<SpriteFrames>();
            }

            var view = picture.GetComponent<PilotAnimationView>();
            if (view == null)
            {
                view = picture.AddComponent<PilotAnimationView>();
            }

            var serialized = new SerializedObject(view);
            serialized.FindProperty("_animation").objectReferenceValue = animation;
            serialized.FindProperty("_frames").objectReferenceValue = frames;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }
    }
}
