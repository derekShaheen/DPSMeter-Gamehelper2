using System.Numerics;
using GameHelper.Plugin;

namespace DPSMeter
{
    public sealed class DPSSettings : IPSettings
    {
        public bool DrawWhenGameInBackground = false;
        public bool OnlyRareUnique = false;
        public float ScreenRangePx = 1200f;

        public float RollingWindowSeconds = 8f;
        public float IdleResetSeconds = 4f;
        public float MinDamageSample = 0f;

        public bool ShowRolling = false;
        public bool ShowMax = true;
        public bool ShowSession = false;
        public bool ShowArea = false;
        public bool HumanizeNumbers = true;
        public bool ShowSparkline = true;

        public float BigNumberPointSize = 24f;
        public Vector2 Anchor = new Vector2(1300f, 10f);
        public float PanelWidth = 300f;          // minimum width; auto-expands based on content
        public Vector2 PanelPadding = new Vector2(12f, 10f);
        public float CornerRadius = 10f;
        public float RowSpacing = 2f;
        public float SparkHeight = 40f;

        public Vector4 PanelBg = new Vector4(0f, 0f, 0f, 0.50f);
        public Vector4 PanelBorder = new Vector4(0f, 0f, 0f, 0.85f);
        public Vector4 HeaderColor = new Vector4(0.75f, 0.78f, 0.90f, 1f);
        public Vector4 ValueColor = new Vector4(1.00f, 0.85f, 0.50f, 1f);

        public Vector4 AccentColor = new Vector4(1.00f, 0.68f, 0.25f, 1f);
        public float AccentGlow = 0.22f;

        public float BorderThickness = 1.5f;
        public float ShadowAlpha = 0.90f;

        public float ProgressHeight = 10f;
        public Vector4 ProgressBg = new Vector4(0f, 0f, 0f, 0.35f);
        public Vector4 ProgressFill = new Vector4(1.00f, 0.68f, 0.25f, 0.9f);

        public float SparkFillAlpha = 0.15f;
    }
}
