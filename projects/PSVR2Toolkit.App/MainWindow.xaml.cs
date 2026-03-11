using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using PSVR2Toolkit.CAPI;

namespace PSVR2Toolkit.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly HealthCheckService healthCheckService;
    private readonly TriggerProfileService triggerProfileService;
    private readonly DispatcherTimer gazeUpdateTimer;
    private bool gazeUpdatePaused = false;

    public MainWindow()
    {
        InitializeComponent();

        healthCheckService = new HealthCheckService();
        triggerProfileService = new TriggerProfileService();

        // Initialize gaze update timer
        gazeUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100) // 10 Hz
        };
        gazeUpdateTimer.Tick += GazeUpdateTimer_Tick;
        gazeUpdateTimer.Start();

        // Load trigger profiles
        LoadTriggerProfiles();

        // Run initial health check
        RunHealthCheck();
    }

    // Health Check handlers
    private void RunHealthCheckButton_Click(object sender, RoutedEventArgs e)
    {
        RunHealthCheck();
    }

    private void RunHealthCheck()
    {
        var report = healthCheckService.RunHealthCheck();
        HealthCheckList.ItemsSource = report.Items;

        HealthSummary.Text = $"✓ {report.PassCount}  ✗ {report.FailCount}  ⚠ {report.WarningCount}";
    }

    // Trigger Profile handlers
    private void LoadTriggerProfiles()
    {
        var profiles = triggerProfileService.GetProfiles();
        ProfileComboBox.ItemsSource = profiles;
        ProfileComboBox.DisplayMemberPath = "Name";
        if (profiles.Count > 0)
        {
            ProfileComboBox.SelectedIndex = 0;
        }
    }

    private void ProfileComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ProfileComboBox.SelectedItem is TriggerProfile profile)
        {
            CurrentEffectLabel.Text = $"{profile.Name} - {profile.Description}";
        }
    }

    private void ApplyProfileButton_Click(object sender, RoutedEventArgs e)
    {
        ApplySelectedProfile();
    }

    private async void TestProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var controller = GetSelectedController();
        var profile = ProfileComboBox.SelectedItem as TriggerProfile;

        if (profile != null)
        {
            triggerProfileService.ApplyProfile(profile, controller);
            CurrentEffectLabel.Text = $"Testing: {profile.Name}";

            await Task.Delay(1000);

            // Turn off after 1 second
            var ipcClient = IpcClient.Instance();
            ipcClient.TriggerEffectDisable(controller);
            CurrentEffectLabel.Text = "Off (test complete)";
        }
    }

    private void ApplySelectedProfile()
    {
        var controller = GetSelectedController();
        var profile = ProfileComboBox.SelectedItem as TriggerProfile;

        if (profile != null)
        {
            triggerProfileService.ApplyProfile(profile, controller);
            CurrentEffectLabel.Text = $"{profile.Name} applied to {controller}";
        }
    }

    private EVRControllerType GetSelectedController()
    {
        if (LeftControllerRadio.IsChecked == true)
            return EVRControllerType.Left;
        if (RightControllerRadio.IsChecked == true)
            return EVRControllerType.Right;
        return EVRControllerType.Both;
    }

    // Gaze Debug handlers
    private void GazeUpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (gazeUpdatePaused)
            return;

        var ipcClient = IpcClient.Instance();
        if (!ipcClient.IsConnected)
        {
            SetGazeDataUnavailable();
            return;
        }

        var gazeData = ipcClient.RequestEyeTrackingData();
        UpdateGazeDisplay(gazeData);
    }

    private void UpdateGazeDisplay(CommandDataServerGazeDataResult2 gazeData)
    {
        // Left eye
        UpdateEyeDisplay(
            gazeData.leftEye,
            LeftGazeOrigin,
            LeftGazeDirection,
            LeftPupilDiameter,
            LeftPupilPosition,
            LeftPosGuide,
            LeftBlink,
            LeftOpenness
        );

        // Right eye
        UpdateEyeDisplay(
            gazeData.rightEye,
            RightGazeOrigin,
            RightGazeDirection,
            RightPupilDiameter,
            RightPupilPosition,
            RightPosGuide,
            RightBlink,
            RightOpenness
        );
    }

    private void UpdateEyeDisplay(
        GazeEyeResult2 eye,
        System.Windows.Controls.TextBlock origin,
        System.Windows.Controls.TextBlock direction,
        System.Windows.Controls.TextBlock pupilDia,
        System.Windows.Controls.TextBlock pupilPos,
        System.Windows.Controls.TextBlock posGuide,
        System.Windows.Controls.TextBlock blink,
        System.Windows.Controls.TextBlock openness)
    {
        if (eye.isGazeOriginValid)
        {
            origin.Text = $"({eye.gazeOriginMm.x:F2}, {eye.gazeOriginMm.y:F2}, {eye.gazeOriginMm.z:F2})";
        }
        else
        {
            origin.Text = "Invalid";
        }

        if (eye.isGazeDirValid)
        {
            direction.Text = $"({eye.gazeDirNorm.x:F3}, {eye.gazeDirNorm.y:F3}, {eye.gazeDirNorm.z:F3})";
        }
        else
        {
            direction.Text = "Invalid";
        }

        if (eye.isPupilDiaValid)
        {
            pupilDia.Text = $"{eye.pupilDiaMm:F2} mm";
        }
        else
        {
            pupilDia.Text = "Invalid";
        }

        // Note: IPC v2 doesn't have pupilPosInSensor or posGuide
        pupilPos.Text = "N/A (requires IPC v3)";
        posGuide.Text = "N/A (requires IPC v3)";

        if (eye.isBlinkValid)
        {
            blink.Text = eye.blink ? "Yes" : "No";
        }
        else
        {
            blink.Text = "Invalid";
        }

        if (eye.isOpenEnabled)
        {
            openness.Text = $"{eye.open:F2}";
        }
        else
        {
            openness.Text = "Disabled";
        }
    }

    private void SetGazeDataUnavailable()
    {
        var unavailable = "Not connected";

        LeftGazeOrigin.Text = unavailable;
        LeftGazeDirection.Text = unavailable;
        LeftPupilDiameter.Text = unavailable;
        LeftPupilPosition.Text = unavailable;
        LeftPosGuide.Text = unavailable;
        LeftBlink.Text = unavailable;
        LeftOpenness.Text = unavailable;

        RightGazeOrigin.Text = unavailable;
        RightGazeDirection.Text = unavailable;
        RightPupilDiameter.Text = unavailable;
        RightPupilPosition.Text = unavailable;
        RightPosGuide.Text = unavailable;
        RightBlink.Text = unavailable;
        RightOpenness.Text = unavailable;
    }

    private void PauseResumeButton_Click(object sender, RoutedEventArgs e)
    {
        gazeUpdatePaused = !gazeUpdatePaused;
        PauseResumeButton.Content = gazeUpdatePaused ? "Resume" : "Pause";
    }
}
