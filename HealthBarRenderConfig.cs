namespace ExtraOverlays
{
    public class HealthBarRenderConfig
    {
        public float FadeIn { get; set; }
        public float FadeOut { get; set; }

        public float Width { get; set; }
        public float Height { get; set; }
        public float YOffset { get; set; }

        public string HighHPColor { get; set; } = string.Empty;
        public string MidHPColor { get; set; } = string.Empty;
        public string LowHPColor { get; set; } = string.Empty;

        public float LowHPThreshold { get; set; }
        public float MidHPThreshold { get; set; }

        public int MaxVisibleEntities { get; set; }
        public float MaxDistanceBlocks { get; set; }
    }
}
