namespace Wobblefolk.LookAt
{
    public enum LookPreset
    {
        /// <summary>Eyes lead, head follows, shoulders and torso catch up last with human-like reaction delays.</summary>
        Realistic = 0,
        /// <summary>Fast, stiff and almost delay-free, like a robot or a security camera.</summary>
        RoboticSnappy = 1,
        /// <summary>Slow, heavy and late, like a tired or drowsy character.</summary>
        Sluggish = 2,
    }

    /// <summary>Ready-made tunings that overwrite a profile's chain settings.</summary>
    public static class LookProfilePresets
    {
        public static void Apply(LookProfile profile, LookPreset preset)
        {
            switch (preset)
            {
                case LookPreset.RoboticSnappy:
                    profile.torso = LookSegmentSettings.Create(0.2f, 60f, 15f, 0f, 5f, 0.08f, 0f, 0f);
                    profile.upperChest = LookSegmentSettings.Create(0.3f, 40f, 20f, 5f, 10f, 0.06f, 0f, 0f);
                    profile.neck = LookSegmentSettings.Create(0.5f, 0f, 35f, 20f, 25f, 0.05f, 0f, 0f);
                    profile.head = LookSegmentSettings.Create(1f, 0f, 70f, 40f, 45f, 0.04f, 0f, 0f);
                    profile.eyes = LookSegmentSettings.Create(1f, 0f, 20f, 15f, 15f, 0.02f, 0f, 0f);
                    break;

                case LookPreset.Sluggish:
                    profile.torso = LookSegmentSettings.Create(0.3f, 40f, 20f, 5f, 10f, 0.9f, 60f, 0.5f);
                    profile.upperChest = LookSegmentSettings.Create(0.35f, 25f, 25f, 10f, 15f, 0.7f, 80f, 0.35f);
                    profile.neck = LookSegmentSettings.Create(0.4f, 10f, 30f, 15f, 20f, 0.5f, 100f, 0.2f);
                    profile.head = LookSegmentSettings.Create(0.6f, 5f, 55f, 30f, 35f, 0.4f, 120f, 0.15f);
                    profile.eyes = LookSegmentSettings.Create(1f, 0f, 30f, 20f, 25f, 0.12f, 0f, 0.05f);
                    break;

                default:
                    profile.torso = LookSegmentSettings.Create(0.3f, 45f, 20f, 5f, 10f, 0.45f, 0f, 0.25f);
                    profile.upperChest = LookSegmentSettings.Create(0.35f, 30f, 25f, 10f, 15f, 0.35f, 0f, 0.15f);
                    profile.neck = LookSegmentSettings.Create(0.4f, 10f, 30f, 15f, 20f, 0.2f, 0f, 0.06f);
                    profile.head = LookSegmentSettings.Create(0.6f, 5f, 60f, 35f, 40f, 0.15f, 0f, 0.05f);
                    profile.eyes = LookSegmentSettings.Create(1f, 0f, 35f, 25f, 30f, 0.04f, 0f, 0f);
                    break;
            }
        }
    }
}
