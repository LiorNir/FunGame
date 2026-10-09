namespace BoardAuthoring
{
    /// <summary>
    /// How far apart new slots are placed in the authoring tools. Every distance is built from the sizes of the slot
    /// prefabs (passed in as radii) plus a gap, so resizing a slot prefab moves the defaults with it. The gaps are the only
    /// numbers kept here.
    /// </summary>
    public static class BoardLayoutMetrics
    {
        /// <summary>Space between the edge of a pilot slot and the edge of the operator slot next to it.</summary>
        public const float ConnectorGap = 0.05f;

        /// <summary>Space between two neighbouring root slots, for the wound bar and stats text under each.</summary>
        public const float RootNeighbourGap = 0.4f;

        /// <summary>How far a new root slot stays clear of the bottom edge of the board area (and of the summoner below it).</summary>
        public const float RootBottomClearance = 0.25f;

        /// <summary>
        /// For boards generated from code: how far a branch stands from the middle of the board, so that slots above the root
        /// row sit beside the enemy (which stands above the middle) rather than under it, as on the hand-made boards.
        /// </summary>
        public const float BranchSideOffset = 2.5f;

        /// <summary>From a pilot slot to the operator slot placed after it.</summary>
        public static float OperatorOffsetY(float pilotRadius, float operatorRadius)
        {
            return pilotRadius + operatorRadius + ConnectorGap;
        }

        /// <summary>From a pilot slot to the next pilot slot of the chain: the operator sits halfway between them.</summary>
        public static float NextSlotOffsetY(float pilotRadius, float operatorRadius)
        {
            return OperatorOffsetY(pilotRadius, operatorRadius) * 2f;
        }

        public static float RootSlotSpacing(float pilotRadius)
        {
            return pilotRadius * 2f + RootNeighbourGap;
        }

        /// <summary>How far above the bottom edge of the board area a new root slot is centred.</summary>
        public static float RootMarginFromBoardBottom(float pilotRadius)
        {
            return pilotRadius + RootBottomClearance;
        }
    }
}
