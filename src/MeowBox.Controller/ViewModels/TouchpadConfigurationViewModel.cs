using MeowBox.Core.Models;
using MeowBox.Core.Services;

namespace MeowBox.Controller.ViewModels;

public sealed class TouchpadConfigurationViewModel : ObservableObject
{
    private bool _enabled;
    private int _lightPressThreshold;
    private int _lightPressReleaseThreshold;
    private int _deepPressThreshold;
    private int _deepPressReleaseThreshold;
    private int _longPressDurationMs;
    private int _pressSensitivityLevel;
    private int _feedbackLevel;
    private int _feedbackStrength;
    private int _deepPressFeedbackStrength;
    private bool _deepPressHapticsEnabled;
    private TouchpadTriggerActionEditorViewModel? _selectedActionEditor;

    public TouchpadConfigurationViewModel(TouchpadConfiguration? model = null)
    {
        model ??= new TouchpadConfiguration();
        _enabled = model.Enabled;
        var pressThresholds = TouchpadHardwareSettings.NormalizeAutomaticPressThresholds(
            model.LightPressThreshold,
            model.DeepPressThreshold);
        _lightPressThreshold = pressThresholds.LightStart;
        _lightPressReleaseThreshold = pressThresholds.LightRelease;
        _deepPressThreshold = pressThresholds.DeepStart;
        _deepPressReleaseThreshold = pressThresholds.DeepRelease;
        _longPressDurationMs = model.LongPressDurationMs <= 0
            ? RuntimeDefaults.DefaultTouchpadCornerLongPressDurationMs
            : model.LongPressDurationMs;
        _pressSensitivityLevel = TouchpadHardwareSettings.MapThresholdToPressSensitivityLevel(_lightPressThreshold);
        var feedbackStrengths = TouchpadHardwareSettings.NormalizeFeedbackStrengths(
            model.FeedbackStrength > 0
                ? model.FeedbackStrength
                : TouchpadHardwareSettings.MapFeedbackLevelToStrength(model.FeedbackLevel),
            model.DeepPressFeedbackStrength > 0
                ? model.DeepPressFeedbackStrength
                : TouchpadHardwareSettings.MapFeedbackLevelToDeepPressStrength(model.FeedbackLevel));
        _feedbackStrength = feedbackStrengths.Normal;
        _deepPressFeedbackStrength = feedbackStrengths.DeepPress;
        _feedbackLevel = TouchpadHardwareSettings.MapFeedbackStrengthToLevel(_feedbackStrength);
        _deepPressHapticsEnabled = model.DeepPressHapticsEnabled;

        SurfaceWidth = model.SurfaceWidth > 0
            ? model.SurfaceWidth
            : RuntimeDefaults.DefaultTouchpadSurfaceWidth;
        SurfaceHeight = model.SurfaceHeight > 0
            ? model.SurfaceHeight
            : RuntimeDefaults.DefaultTouchpadSurfaceHeight;

        MainRegionDeepPress = new TouchpadTriggerActionEditorViewModel(
            ResourceStringService.GetString("Touchpad.Trigger.MainDeepPress.Title", "Main region · Deep press"),
            ResourceStringService.GetString("Touchpad.Trigger.MainDeepPress.Description", "Runs once when a touch in the main region reaches the built-in deep press level. L, R, LT, and RT are excluded."),
            null,
            model.DeepPressAction);
        FiveFingerPinchIn = new TouchpadTriggerActionEditorViewModel(
            ResourceStringService.GetString("Touchpad.Trigger.FiveFingerPinchIn.Title", "Five fingers · Pinch in"),
            ResourceStringService.GetString("Touchpad.Trigger.FiveFingerPinchIn.Description", "Runs once when five contacts move inward together far enough, then stays latched until all fingers are lifted."),
            null,
            model.FiveFingerPinchInAction);
        FiveFingerPinchOut = new TouchpadTriggerActionEditorViewModel(
            ResourceStringService.GetString("Touchpad.Trigger.FiveFingerPinchOut.Title", "Five fingers · Pinch out"),
            ResourceStringService.GetString("Touchpad.Trigger.FiveFingerPinchOut.Description", "Runs once when five contacts spread outward together far enough, then stays latched until all fingers are lifted."),
            null,
            model.FiveFingerPinchOutAction);
        LeftEdgeSlide = new TouchpadTriggerActionEditorViewModel(
            ResourceStringService.GetString("Touchpad.Trigger.LeftSlide.Title", "Left side · Drag"),
            ResourceStringService.GetString("Touchpad.Trigger.LeftSlide.Description", "Choose what vertical dragging in the left edge region controls."),
            null,
            model.LeftEdgeSlideAction,
            simpleEdgeSlideMapping: true);
        RightEdgeSlide = new TouchpadTriggerActionEditorViewModel(
            ResourceStringService.GetString("Touchpad.Trigger.RightSlide.Title", "Right side · Drag"),
            ResourceStringService.GetString("Touchpad.Trigger.RightSlide.Description", "Choose what vertical dragging in the right edge region controls."),
            null,
            model.RightEdgeSlideAction,
            simpleEdgeSlideMapping: true);
        LeftTopCorner = new TouchpadCornerRegionViewModel(
            TouchpadCornerRegionId.LeftTop,
            model.LeftTopCorner);
        RightTopCorner = new TouchpadCornerRegionViewModel(
            TouchpadCornerRegionId.RightTop,
            model.RightTopCorner);
        GuidanceLines =
        [
            ResourceStringService.GetString("Touchpad.Guidance.MainExcludesCorners", "Main-region deep press excludes L, R, LT, and RT."),
            ResourceStringService.GetString("Touchpad.Guidance.FiveFingerPinchIn", "Five-finger pinch-in fires once per touch session after the group contracts enough."),
            ResourceStringService.GetString("Touchpad.Guidance.FiveFingerPinchOut", "Five-finger pinch-out also fires once per touch session after the group expands enough."),
            ResourceStringService.GetString("Touchpad.Guidance.EdgeSlideMapping", "Left-side and right-side drag can each be mapped to volume or brightness."),
            ResourceStringService.GetString("Touchpad.Guidance.CornerActions", "LT and RT can each run separate deep-press and long-press actions."),
            ResourceStringService.GetString("Touchpad.Guidance.LongPressAdjustable", "Long-press timing can be adjusted from the touchpad settings panel.")
        ];

        CornerRegions = [LeftTopCorner, RightTopCorner];
        AllActionEditors =
        [
            MainRegionDeepPress,
            FiveFingerPinchIn,
            FiveFingerPinchOut,
            LeftEdgeSlide,
            RightEdgeSlide,
            LeftTopCorner.DeepPress,
            RightTopCorner.DeepPress,
            LeftTopCorner.LongPress,
            RightTopCorner.LongPress
        ];
        SelectedActionEditor = AllActionEditors.FirstOrDefault();

        foreach (var editor in AllActionEditors)
        {
            editor.Action.PropertyChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(HasAnyAssignedAction));
                OnPropertyChanged(nameof(EdgeSlideEnabled));
            };
        }
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public int DeepPressThreshold
    {
        get => _deepPressThreshold;
        set
        {
            var thresholds = TouchpadHardwareSettings.NormalizeAutomaticPressThresholds(
                LightPressThreshold,
                value);
            if (SetProperty(ref _deepPressThreshold, thresholds.DeepStart))
            {
                DeepPressReleaseThreshold = thresholds.DeepRelease;
            }
        }
    }

    public int DeepPressReleaseThreshold
    {
        get => _deepPressReleaseThreshold;
        set => SetProperty(ref _deepPressReleaseThreshold, TouchpadHardwareSettings.NormalizePressThresholds(
            LightPressThreshold,
            LightPressReleaseThreshold,
            DeepPressThreshold,
            value).DeepRelease);
    }

    public int LightPressThreshold
    {
        get => _lightPressThreshold;
        set
        {
            var thresholds = TouchpadHardwareSettings.NormalizeAutomaticPressThresholds(
                value,
                DeepPressThreshold);
            if (SetProperty(ref _lightPressThreshold, thresholds.LightStart))
            {
                LightPressReleaseThreshold = thresholds.LightRelease;
                _pressSensitivityLevel = TouchpadHardwareSettings.MapThresholdToPressSensitivityLevel(thresholds.LightStart);
                OnPropertyChanged(nameof(PressSensitivityLevel));
            }
        }
    }

    public int LightPressReleaseThreshold
    {
        get => _lightPressReleaseThreshold;
        set => SetProperty(ref _lightPressReleaseThreshold, TouchpadHardwareSettings.NormalizePressThresholds(
            LightPressThreshold,
            value,
            DeepPressThreshold,
            DeepPressReleaseThreshold).LightRelease);
    }

    public int LongPressDurationMs
    {
        get => _longPressDurationMs;
        set => SetProperty(ref _longPressDurationMs, Math.Clamp(value, 200, 3000));
    }

    public int PressSensitivityLevel
    {
        get => _pressSensitivityLevel;
        set
        {
            var normalized = TouchpadHardwareSettings.NormalizeLevel(value);
            if (SetProperty(ref _pressSensitivityLevel, normalized))
            {
                LightPressThreshold = TouchpadHardwareSettings.MapPressSensitivityLevelToThreshold(normalized);
                LightPressReleaseThreshold = TouchpadHardwareSettings.CalculateDefaultLightPressReleaseThreshold(LightPressThreshold);
            }
        }
    }

    public int FeedbackLevel
    {
        get => _feedbackLevel;
        set
        {
            var normalized = TouchpadHardwareSettings.NormalizeLevel(value);
            if (SetProperty(ref _feedbackLevel, normalized))
            {
                FeedbackStrength = TouchpadHardwareSettings.MapFeedbackLevelToStrength(normalized);
                DeepPressFeedbackStrength = TouchpadHardwareSettings.MapFeedbackLevelToDeepPressStrength(normalized);
            }
        }
    }

    public int FeedbackStrength
    {
        get => _feedbackStrength;
        set
        {
            var strengths = TouchpadHardwareSettings.NormalizeFeedbackStrengths(value, DeepPressFeedbackStrength);
            if (SetProperty(ref _feedbackStrength, strengths.Normal))
            {
                _feedbackLevel = TouchpadHardwareSettings.MapFeedbackStrengthToLevel(strengths.Normal);
                OnPropertyChanged(nameof(FeedbackLevel));
            }
        }
    }

    public int DeepPressFeedbackStrength
    {
        get => _deepPressFeedbackStrength;
        set => SetProperty(ref _deepPressFeedbackStrength, TouchpadHardwareSettings.NormalizeFeedbackStrengths(
            FeedbackStrength,
            value).DeepPress);
    }

    public bool DeepPressHapticsEnabled
    {
        get => _deepPressHapticsEnabled;
        set => SetProperty(ref _deepPressHapticsEnabled, value);
    }

    public bool EdgeSlideEnabled => LeftEdgeSlide.Action.HasAssignedAction || RightEdgeSlide.Action.HasAssignedAction;

    public TouchpadTriggerActionEditorViewModel? SelectedActionEditor
    {
        get => _selectedActionEditor;
        set => SetProperty(ref _selectedActionEditor, value);
    }

    public int SurfaceWidth { get; }

    public int SurfaceHeight { get; }

    public bool HasAnyAssignedAction => AllActionEditors.Any(item => item.Action.HasAssignedAction);

    public TouchpadTriggerActionEditorViewModel MainRegionDeepPress { get; }

    public ActionDefinitionViewModel DeepPressAction => MainRegionDeepPress.Action;

    public TouchpadTriggerActionEditorViewModel FiveFingerPinchIn { get; }

    public TouchpadTriggerActionEditorViewModel FiveFingerPinchOut { get; }

    public TouchpadTriggerActionEditorViewModel LeftEdgeSlide { get; }

    public TouchpadTriggerActionEditorViewModel RightEdgeSlide { get; }

    public TouchpadCornerRegionViewModel LeftTopCorner { get; }

    public TouchpadCornerRegionViewModel RightTopCorner { get; }

    public IReadOnlyList<string> GuidanceLines { get; }

    public IReadOnlyList<TouchpadCornerRegionViewModel> CornerRegions { get; }

    public IReadOnlyList<TouchpadTriggerActionEditorViewModel> AllActionEditors { get; }

    public TouchpadConfiguration ToConfiguration()
    {
        return new TouchpadConfiguration
        {
            Enabled = HasAnyAssignedAction,
            LightPressThreshold = LightPressThreshold,
            LightPressReleaseThreshold = LightPressReleaseThreshold,
            PressSensitivityLevel = TouchpadHardwareSettings.MapThresholdToPressSensitivityLevel(LightPressThreshold),
            DeepPressThreshold = DeepPressThreshold,
            DeepPressReleaseThreshold = DeepPressReleaseThreshold,
            LongPressDurationMs = LongPressDurationMs,
            FeedbackLevel = TouchpadHardwareSettings.MapFeedbackStrengthToLevel(FeedbackStrength),
            FeedbackStrength = FeedbackStrength,
            DeepPressFeedbackStrength = DeepPressFeedbackStrength,
            DeepPressHapticsEnabled = DeepPressHapticsEnabled,
            EdgeSlideEnabled = EdgeSlideEnabled,
            SurfaceWidth = SurfaceWidth,
            SurfaceHeight = SurfaceHeight,
            DeepPressAction = MainRegionDeepPress.Action.ToConfiguration(),
            FiveFingerPinchInAction = FiveFingerPinchIn.Action.ToConfiguration(),
            FiveFingerPinchOutAction = FiveFingerPinchOut.Action.ToConfiguration(),
            LeftEdgeSlideAction = LeftEdgeSlide.Action.ToConfiguration(),
            RightEdgeSlideAction = RightEdgeSlide.Action.ToConfiguration(),
            LeftTopCorner = LeftTopCorner.ToConfiguration(),
            RightTopCorner = RightTopCorner.ToConfiguration()
        };
    }
}
