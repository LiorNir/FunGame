namespace BoardAuthoring
{
    /// <summary>
    /// Implemented by the playable look of a slot prefab (the part that knows what the slot's picture is). It says how big
    /// the slot is, so the authoring tools (gizmos, "+" buttons, default positions) read the size from the prefab instead
    /// of keeping their own copy of it.
    /// </summary>
    public interface ISlotFootprint
    {
        /// <summary>Radius of the slot's base, in the slot object's own units (before any scale of the object itself).</summary>
        float Radius { get; }
    }

    public static class SlotFootprint
    {
        /// <summary>Only used for a slot prefab that has no playable look at all, which is not a valid slot prefab.</summary>
        public const float FallbackRadius = 0.5f;
    }
}
