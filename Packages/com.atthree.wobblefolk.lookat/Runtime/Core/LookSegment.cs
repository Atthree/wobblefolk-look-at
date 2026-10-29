namespace Wobblefolk.LookAt
{
    /// <summary>Links of the look chain, ordered from the base of the spine to the eyes. Values are array indices.</summary>
    public enum LookSegment
    {
        Torso = 0,
        UpperChest = 1,
        Neck = 2,
        Head = 3,
        Eyes = 4,
    }

    public static class LookSegments
    {
        public const int Count = 5;

        /// <summary>Segments before the eyes. They share the target angle; the eyes cover what remains.</summary>
        public const int BodyCount = 4;
    }
}
