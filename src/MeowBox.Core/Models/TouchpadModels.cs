namespace MeowBox.Core.Models;

public sealed class TouchpadConfiguration
{
    public bool Enabled { get; set; } = true;

    public int LightPressThreshold { get; set; } = RuntimeDefaults.DefaultTouchpadLightPressThreshold;

    public int LightPressReleaseThreshold { get; set; }

    public int PressSensitivityLevel { get; set; }

    public int DeepPressThreshold { get; set; } = RuntimeDefaults.DefaultTouchpadDeepPressThreshold;

    public int DeepPressReleaseThreshold { get; set; }

    public int LongPressDurationMs { get; set; } = RuntimeDefaults.DefaultTouchpadCornerLongPressDurationMs;

    public int FeedbackLevel { get; set; }

    public int FeedbackStrength { get; set; }

    public int DeepPressFeedbackStrength { get; set; }

    public bool DeepPressHapticsEnabled { get; set; } = true;

    public bool EdgeSlideEnabled { get; set; }

    public int SurfaceWidth { get; set; } = RuntimeDefaults.DefaultTouchpadSurfaceWidth;

    public int SurfaceHeight { get; set; } = RuntimeDefaults.DefaultTouchpadSurfaceHeight;

    public ActionDefinitionConfiguration DeepPressAction { get; set; } = new();

    public ActionDefinitionConfiguration FiveFingerPinchInAction { get; set; } = new();

    public ActionDefinitionConfiguration FiveFingerPinchOutAction { get; set; } = new();

    public ActionDefinitionConfiguration LeftEdgeSlideAction { get; set; } = new();

    public ActionDefinitionConfiguration RightEdgeSlideAction { get; set; } = new();

    public TouchpadCornerRegionConfiguration LeftTopCorner { get; set; } = TouchpadCornerRegionConfiguration.CreateLeftTopDefault();

    public TouchpadCornerRegionConfiguration RightTopCorner { get; set; } = TouchpadCornerRegionConfiguration.CreateRightTopDefault();
}

public sealed class TouchpadCornerRegionConfiguration
{
    public string Id { get; set; } = string.Empty;

    public TouchpadRegionBoundsConfiguration Bounds { get; set; } = new();

    public ActionDefinitionConfiguration DeepPressAction { get; set; } = new();

    public ActionDefinitionConfiguration LongPressAction { get; set; } = new();

    public static TouchpadCornerRegionConfiguration CreateLeftTopDefault()
    {
        return new TouchpadCornerRegionConfiguration
        {
            Id = TouchpadCornerRegionId.LeftTop,
            Bounds = new TouchpadRegionBoundsConfiguration
            {
                Left = 0,
                Top = 0,
                Right = RuntimeDefaults.DefaultTouchpadCornerWidth,
                Bottom = RuntimeDefaults.DefaultTouchpadCornerHeight
            }
        };
    }

    public static TouchpadCornerRegionConfiguration CreateRightTopDefault()
    {
        return new TouchpadCornerRegionConfiguration
        {
            Id = TouchpadCornerRegionId.RightTop,
            Bounds = new TouchpadRegionBoundsConfiguration
            {
                Left = RuntimeDefaults.DefaultTouchpadSurfaceWidth - RuntimeDefaults.DefaultTouchpadCornerWidth,
                Top = 0,
                Right = RuntimeDefaults.DefaultTouchpadSurfaceWidth,
                Bottom = RuntimeDefaults.DefaultTouchpadCornerHeight
            }
        };
    }
}

public sealed class TouchpadRegionBoundsConfiguration
{
    public int Left { get; set; }

    public int Top { get; set; }

    public int Right { get; set; }

    public int Bottom { get; set; }
}

public static class TouchpadCornerRegionId
{
    public const string LeftTop = "left-top";
    public const string RightTop = "right-top";
}

public static class TouchpadHardwareSettings
{
    public const int Low = 1;
    public const int Medium = 2;
    public const int High = 3;
    public const int MinPressThreshold = 20;
    public const int DeepPressNeverThreshold = 5000;
    public const int MaxPressThreshold = DeepPressNeverThreshold;
    public const int MinPressReleaseThreshold = 0;
    public const int MinFeedbackStrength = 0;
    public const int MaxFeedbackStrength = 128;

    public static int NormalizeLevel(int level, int fallback = Medium)
    {
        return level is >= Low and <= High ? level : fallback;
    }

    public static int MapPressSensitivityLevelToThreshold(int level)
    {
        return NormalizeLevel(level) switch
        {
            Low => 150,
            High => 105,
            _ => RuntimeDefaults.DefaultTouchpadLightPressThreshold
        };
    }

    public static int MapThresholdToPressSensitivityLevel(int threshold)
    {
        var candidates = new[]
        {
            (Level: Low, Threshold: 150),
            (Level: Medium, Threshold: RuntimeDefaults.DefaultTouchpadLightPressThreshold),
            (Level: High, Threshold: 105)
        };

        return candidates
            .OrderBy(item => Math.Abs(item.Threshold - threshold))
            .ThenBy(item => item.Level)
            .First()
            .Level;
    }

    public static int MapFeedbackLevelToStrength(int level)
    {
        return NormalizeLevel(level) switch
        {
            Low => 56,
            High => 104,
            _ => RuntimeDefaults.DefaultTouchpadFeedbackStrength
        };
    }

    public static int MapFeedbackLevelToDeepPressStrength(int level)
    {
        return NormalizeLevel(level) switch
        {
            Low => 80,
            High => 128,
            _ => RuntimeDefaults.DefaultTouchpadDeepPressFeedbackStrength
        };
    }

    public static int MapFeedbackStrengthToLevel(int strength)
    {
        var candidates = new[]
        {
            (Level: Low, Strength: 56),
            (Level: Medium, Strength: RuntimeDefaults.DefaultTouchpadFeedbackStrength),
            (Level: High, Strength: 104)
        };

        return candidates
            .OrderBy(item => Math.Abs(item.Strength - strength))
            .ThenBy(item => item.Level)
            .First()
            .Level;
    }

    public static TouchpadPressThresholds NormalizePressThresholds(
        int lightStart,
        int lightRelease,
        int deepStart,
        int deepRelease)
    {
        var normalizedLightStart = Math.Clamp(
            lightStart <= 0 ? RuntimeDefaults.DefaultTouchpadLightPressThreshold : lightStart,
            MinPressThreshold,
            MaxPressThreshold - 1);
        var normalizedDeepStart = Math.Clamp(
            deepStart <= 0 ? RuntimeDefaults.DefaultTouchpadDeepPressThreshold : deepStart,
            normalizedLightStart + 1,
            MaxPressThreshold);
        var normalizedLightRelease = Math.Clamp(
            lightRelease <= 0 ? CalculateDefaultLightPressReleaseThreshold(normalizedLightStart) : lightRelease,
            MinPressReleaseThreshold,
            normalizedLightStart);
        var normalizedDeepRelease = Math.Clamp(
            deepRelease <= 0 ? CalculateDefaultDeepPressReleaseThreshold(normalizedDeepStart) : deepRelease,
            MinPressReleaseThreshold,
            normalizedDeepStart);

        return new TouchpadPressThresholds(
            normalizedLightStart,
            normalizedLightRelease,
            normalizedDeepStart,
            normalizedDeepRelease);
    }

    public static TouchpadPressThresholds NormalizeAutomaticPressThresholds(
        int lightStart,
        int deepStart)
    {
        var normalizedLightStart = Math.Clamp(
            lightStart <= 0 ? RuntimeDefaults.DefaultTouchpadLightPressThreshold : lightStart,
            MinPressThreshold,
            MaxPressThreshold - 1);
        var normalizedDeepStart = Math.Clamp(
            deepStart <= 0 ? RuntimeDefaults.DefaultTouchpadDeepPressThreshold : deepStart,
            normalizedLightStart + 1,
            MaxPressThreshold);

        return NormalizePressThresholds(
            normalizedLightStart,
            CalculateDefaultLightPressReleaseThreshold(normalizedLightStart),
            normalizedDeepStart,
            CalculateDefaultDeepPressReleaseThreshold(normalizedDeepStart));
    }

    public static TouchpadFeedbackStrengths NormalizeFeedbackStrengths(int normal, int deep)
    {
        var normalizedNormal = Math.Clamp(
            normal <= 0 ? RuntimeDefaults.DefaultTouchpadFeedbackStrength : normal,
            MinFeedbackStrength,
            MaxFeedbackStrength);
        var normalizedDeep = Math.Clamp(
            deep <= 0 ? normalizedNormal + 24 : deep,
            MinFeedbackStrength,
            MaxFeedbackStrength);

        return new TouchpadFeedbackStrengths(normalizedNormal, normalizedDeep);
    }

    public static int CalculateDefaultLightPressReleaseThreshold(int lightStart)
        => Math.Clamp((int)Math.Round(lightStart * 0.33d), MinPressReleaseThreshold, Math.Max(MinPressReleaseThreshold, lightStart));

    public static int CalculateDefaultDeepPressReleaseThreshold(int deepStart)
        => Math.Clamp((int)Math.Round(deepStart * 0.8d), MinPressReleaseThreshold, Math.Max(MinPressReleaseThreshold, deepStart));
}

public readonly record struct TouchpadPressThresholds(
    int LightStart,
    int LightRelease,
    int DeepStart,
    int DeepRelease);

public readonly record struct TouchpadFeedbackStrengths(
    int Normal,
    int DeepPress);

public sealed class TouchpadLiveStateSnapshot
{
    public bool IsRegistered { get; set; }

    public bool HasReceivedInput { get; set; }

    public bool SupportsPressure { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public bool HasInteraction { get; set; }

    public bool ButtonPressed { get; set; }

    public bool DeepPressed { get; set; }

    public int Pressure { get; set; }

    public int PeakPressure { get; set; }

    public int LightPressThreshold { get; set; } = RuntimeDefaults.DefaultTouchpadLightPressThreshold;

    public int DeepPressThreshold { get; set; } = RuntimeDefaults.DefaultTouchpadDeepPressThreshold;

    public ushort ScanTime { get; set; }

    public byte ContactCount { get; set; }

    public List<TouchpadLiveContactSnapshot> Contacts { get; set; } = [];
}

public sealed class TouchpadLiveContactSnapshot
{
    public int SlotIndex { get; set; }

    public bool Tip { get; set; }

    public bool Confidence { get; set; }

    public int ContactId { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int Pressure { get; set; }
}
