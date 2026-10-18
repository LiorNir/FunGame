using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Views
{
    /// <summary>
    /// Shows one sprite of a list, picked by <see cref="Frame"/>, on the sprite renderer or UI image of its object. Animation
    /// clips key the Frame number (legacy clips cannot swap sprites themselves), so one clip drives a board picture and a
    /// hand card alike. Runs in edit mode too, so scrubbing a clip in the Animation window shows the frames.
    /// </summary>
    [ExecuteAlways]
    public class SpriteFrames : MonoBehaviour
    {
        [Tooltip("Keyed by animation clips: which sprite of the list is shown (0 = the first; a fraction counts as the frame it is in).")]
        public float Frame;

        [Tooltip("The pilot's frames in order, for example a sliced sprite sheet.")]
        [SerializeField] private List<Sprite> _frames = new List<Sprite>();

        [Tooltip("Where the frame is shown on the board. Found on this object when empty.")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Tooltip("Where the frame is shown in the UI (a hand card). Found on this object when empty.")]
        [SerializeField] private Image _image;

        private Sprite _shown;

        /// <summary>
        /// In Play mode the frames are only shown while this is on (an animation is playing), so the picture's still sprite is
        /// left alone otherwise. In edit mode they are always shown, for previewing clips.
        /// </summary>
        public bool Driving { get; set; }

        public IReadOnlyList<Sprite> Frames => _frames;
        public bool HasFrames => _frames != null && _frames.Exists(sprite => sprite != null);

        public void SetFrames(IEnumerable<Sprite> frames)
        {
            _frames = frames != null ? new List<Sprite>(frames) : new List<Sprite>();
            _shown = null;
        }

        /// <summary>Shows the frame now (also called after every animation step).</summary>
        public void Apply()
        {
            if ((Application.isPlaying && !Driving) || _frames == null || _frames.Count == 0)
            {
                return;
            }

            var index = Mathf.Clamp(Mathf.FloorToInt(Frame + 0.0001f), 0, _frames.Count - 1);
            var sprite = _frames[index];
            if (sprite == null || sprite == _shown && Application.isPlaying)
            {
                return;
            }

            FindTargets();
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sprite = sprite;
            }
            else if (_image != null)
            {
                _image.sprite = sprite;
            }

            _shown = sprite;
        }

        private void FindTargets()
        {
            if (_spriteRenderer == null && _image == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
                _image = GetComponent<Image>();
            }
        }

        // the animation system wrote new values (also while previewing in the Animation window)
        private void OnDidApplyAnimationProperties()
        {
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }
    }
}
