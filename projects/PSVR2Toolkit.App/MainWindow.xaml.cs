using System;
using System.Threading;
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
    private bool isUpdatingGaze = false;
    private CancellationTokenSource? gazeCancellationTokenSource;
    private bool ignoreRecalibrationThisSession = false;

    public MainWindow()
    {
        InitializeComponent();

        healthCheckService = new HealthCheckService();
        triggerProfileService = new TriggerProfileService();

        // Set window title with version
        Title = $"{AppConstants.APP_NAME} v{AppConstants.APP_VERSION}";

        // Initialize gaze update timer
        gazeUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AppConstants.GAZE_UPDATE_INTERVAL_MS)
        };
        gazeUpdateTimer.Tick += GazeUpdateTimer_Tick;
        gazeUpdateTimer.Start();

        gazeCancellationTokenSource = new CancellationTokenSource();

        // Load trigger profiles
        LoadTriggerProfiles();

        // Run initial health check
        RunHealthCheck();

        // Refresh calibration status
        RefreshCalibrationStatus();

        // Subscribe to recalibration notification
        var ipcClient = IpcClient.Instance();
        if (ipcClient != null)
        {
            ipcClient.RecalibrationNeeded += OnRecalibrationNeeded;
        }

        Logger.Info($"{AppConstants.APP_NAME} v{AppConstants.APP_VERSION} started");
    }

    protected override void OnClosed(EventArgs e)
    {
        // Clean up resources
        gazeUpdateTimer?.Stop();
        gazeCancellationTokenSource?.Cancel();
        gazeCancellationTokenSource?.Dispose();
        Logger.Info("Application closed");
        base.OnClosed(e);
    }

    // Health Check handlers
    private void RunHealthCheckButton_Click(object sender, RoutedEventArgs e)
    {
        RunHealthCheck();
    }

    private void RunHealthCheck()
    {
        try
        {
            Logger.Info("Running health check");
            var report = healthCheckService.RunHealthCheck();
            HealthCheckList.ItemsSource = report.Items;
            HealthSummary.Text = $"✓ {report.PassCount}  ✗ {report.FailCount}  ⚠ {report.WarningCount}";
        }
        catch (Exception ex)
        {
            Logger.Error("Health check failed", ex);
            HealthSummary.Text = "Health check failed - see logs";
        }
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

        if (profile == null)
        {
            Logger.Warning("No profile selected for test");
            return;
        }

        try
        {
            triggerProfileService?.ApplyProfile(profile, controller);
            CurrentEffectLabel.Text = $"Testing: {profile.Name}";

            await Task.Delay(AppConstants.TRIGGER_TEST_DURATION_MS);

            // Turn off after test duration
            var ipcClient = IpcClient.Instance();
            if (ipcClient != null)
            {
                ipcClient.TriggerEffectDisable(controller);
                CurrentEffectLabel.Text = "Off (test complete)";
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Test profile failed: {ex.Message}", ex);
            CurrentEffectLabel.Text = "Test failed - see logs";
        }
    }

    private void ApplySelectedProfile()
    {
        var controller = GetSelectedController();
        var profile = ProfileComboBox.SelectedItem as TriggerProfile;

        if (profile == null)
        {
            Logger.Warning("No profile selected to apply");
            return;
        }

        try
        {
            triggerProfileService?.ApplyProfile(profile, controller);
            CurrentEffectLabel.Text = $"{profile.Name} applied to {controller}";
        }
        catch (Exception ex)
        {
            Logger.Error($"Apply profile failed: {ex.Message}", ex);
            CurrentEffectLabel.Text = "Apply failed - see logs";
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
    private async void GazeUpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (gazeUpdatePaused || isUpdatingGaze)
            return;

        isUpdatingGaze = true;
        try
        {
            var ipcClient = IpcClient.Instance();
            if (ipcClient == null || !ipcClient.IsConnected)
            {
                SetGazeDataUnavailable();
                return;
            }

            // Move network I/O off UI thread
            var gazeData = await Task.Run(() =>
            {
                try
                {
                    return ipcClient.RequestEyeTrackingData();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to request eye tracking data: {ex.Message}", ex);
                    return default(CommandDataServerGazeDataResult2);
                }
            }, gazeCancellationTokenSource?.Token ?? CancellationToken.None);

            UpdateGazeDisplay(gazeData);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation, ignore
        }
        catch (Exception ex)
        {
            Logger.Error($"Gaze update failed: {ex.Message}", ex);
        }
        finally
        {
            isUpdatingGaze = false;
        }
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

    // Eye Calibration handlers
    private void LaunchCalibrationButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var assemblyPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (assemblyPath == null)
            {
                MessageBox.Show("Failed to locate application directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Error("Failed to get assembly path for calibration tool");
                return;
            }

            var calibrationToolPath = System.IO.Path.Combine(assemblyPath, "PSVR2EyeTrackingCalibration.exe");

            if (!System.IO.File.Exists(calibrationToolPath))
            {
                MessageBox.Show(
                    "Calibration tool not found. Please ensure PSVR2EyeTrackingCalibration.exe is in the application directory.",
                    "Calibration Tool Not Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Logger.Warning($"Calibration tool not found at: {calibrationToolPath}");
                return;
            }

            Logger.Info("Launching calibration tool");
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = calibrationToolPath,
                UseShellExecute = true
            });

            if (process != null)
            {
                MessageBox.Show(
                    "Calibration tool launched!\n\n" +
                    "1. Put on your VR headset\n" +
                    "2. Look at the red dots that appear\n" +
                    "3. Pull the right trigger when looking at each dot\n" +
                    "4. Calibration will complete automatically",
                    "Calibration Started",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Monitor process exit to refresh calibration status
                Task.Run(() =>
                {
                    process.WaitForExit();
                    Dispatcher.Invoke(() =>
                    {
                        RefreshCalibrationStatus();
                        Logger.Info("Calibration tool exited, status refreshed");
                    });
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to launch calibration tool: {ex.Message}", ex);
            MessageBox.Show(
                $"Failed to launch calibration tool:\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ClearCalibrationButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear the current calibration?\n\n" +
                "This will reset gaze offsets to zero. You can recalibrate at any time.",
                "Clear Calibration",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var calibrationPath = AppConstants.CalibrationPath;

                if (System.IO.File.Exists(calibrationPath))
                {
                    System.IO.File.Delete(calibrationPath);
                    Logger.Info("Calibration file deleted");
                }

                // Notify driver to reload (which will use 0,0 defaults)
                var ipcClient = IpcClient.Instance();
                if (ipcClient != null)
                {
                    ipcClient.StopGazeCalibration();
                }

                RefreshCalibrationStatus();

                MessageBox.Show(
                    "Calibration cleared successfully.\n\nGaze offsets have been reset to zero.",
                    "Calibration Cleared",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to clear calibration: {ex.Message}", ex);
            MessageBox.Show(
                $"Failed to clear calibration:\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RefreshCalibrationStatus()
    {
        try
        {
            var calibrationPath = AppConstants.CalibrationPath;

            CalibrationFilePathLabel.Text = calibrationPath;

            if (System.IO.File.Exists(calibrationPath))
            {
                var fileInfo = new System.IO.FileInfo(calibrationPath);
                CalibrationDateLabel.Text = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");

                // Read calibration values
                try
                {
                    var lines = System.IO.File.ReadAllText(calibrationPath);
                    var values = lines.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                    if (values.Length >= 2 &&
                        float.TryParse(values[0], out float offsetX) &&
                        float.TryParse(values[1], out float offsetY))
                    {
                        CalibrationOffsetsLabel.Text = $"X: {offsetX:F3}, Y: {offsetY:F3}";
                    }
                    else
                    {
                        CalibrationOffsetsLabel.Text = "Invalid calibration file format";
                    }
                }
                catch
                {
                    CalibrationOffsetsLabel.Text = "Error reading calibration values";
                }
            }
            else
            {
                CalibrationDateLabel.Text = "Never";
                CalibrationOffsetsLabel.Text = "X: 0.000, Y: 0.000 (No calibration)";
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to refresh calibration status: {ex.Message}", ex);
            CalibrationDateLabel.Text = "Error";
            CalibrationOffsetsLabel.Text = "Error reading status";
        }
    }

    private void GazeCursorCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            var ipcClient = IpcClient.Instance();
            if (ipcClient != null && ipcClient.IsConnected)
            {
                float sensitivity = (float)CursorSensitivitySlider.Value;
                float smoothing = (float)CursorSmoothingSlider.Value;
                float deadzone = 0.05f; // Fixed deadzone value

                ipcClient.EnableGazeCursor(sensitivity, smoothing, deadzone);
                Logger.Info($"Gaze cursor enabled (sensitivity: {sensitivity}, smoothing: {smoothing})");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to enable gaze cursor: {ex.Message}", ex);
            MessageBox.Show($"Failed to enable gaze cursor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            GazeCursorCheckBox.IsChecked = false;
        }
    }

    private void GazeCursorCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        try
        {
            var ipcClient = IpcClient.Instance();
            if (ipcClient != null && ipcClient.IsConnected)
            {
                ipcClient.DisableGazeCursor();
                Logger.Info("Gaze cursor disabled");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to disable gaze cursor: {ex.Message}", ex);
        }
    }

    private void CursorSettings_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        try
        {
            var ipcClient = IpcClient.Instance();
            // Only update if cursor is currently enabled
            if (GazeCursorCheckBox != null && GazeCursorCheckBox.IsChecked == true && ipcClient != null && ipcClient.IsConnected)
            {
                float sensitivity = (float)CursorSensitivitySlider.Value;
                float smoothing = (float)CursorSmoothingSlider.Value;
                float deadzone = 0.05f;

                ipcClient.EnableGazeCursor(sensitivity, smoothing, deadzone);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to update cursor settings: {ex.Message}", ex);
        }
    }

    private void OnRecalibrationNeeded(object sender, EventArgs e)
    {
        try
        {
            // Check if user wants to ignore notifications this session
            if (ignoreRecalibrationThisSession)
            {
                Logger.Info("Recalibration notification suppressed (user chose to ignore this session)");
                return;
            }

            Dispatcher.Invoke(() =>
            {
                var dialog = new RecalibrationDialog();
                var result = dialog.ShowDialog();

                if (result == true)
                {
                    switch (dialog.UserChoice)
                    {
                        case RecalibrationChoice.CalibrateNow:
                            // Auto-launch calibration tool
                            LaunchCalibrationButton_Click(this, new RoutedEventArgs());
                            Logger.Info("User chose to calibrate now");
                            break;

                        case RecalibrationChoice.IgnoreSession:
                            ignoreRecalibrationThisSession = true;
                            Logger.Info("User chose to ignore recalibration notifications this session");
                            break;

                        case RecalibrationChoice.RemindLater:
                            Logger.Info("User chose to be reminded later");
                            break;
                    }
                }
            });

            Logger.Info("Recalibration notification displayed to user");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to handle recalibration notification: {ex.Message}", ex);
        }
    }
}

public enum RecalibrationChoice
{
    CalibrateNow,
    RemindLater,
    IgnoreSession
}

public class RecalibrationDialog : Window
{
    public RecalibrationChoice UserChoice { get; private set; }

    public RecalibrationDialog()
    {
        Title = "Recalibration Recommended";
        Width = 450;
        Height = 250;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;

        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

        var messagePanel = new System.Windows.Controls.StackPanel();

        var titleBlock = new System.Windows.Controls.TextBlock
        {
            Text = "Eye Tracking Calibration Drift Detected",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 15)
        };
        messagePanel.Children.Add(titleBlock);

        var messageBlock = new System.Windows.Controls.TextBlock
        {
            Text = "Your gaze accuracy may have decreased over time.\n\n" +
                   "Recalibration takes about 1-2 minutes and will improve eye tracking accuracy.\n\n" +
                   "What would you like to do?",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        };
        messagePanel.Children.Add(messageBlock);

        System.Windows.Controls.Grid.SetRow(messagePanel, 0);
        grid.Children.Add(messagePanel);

        var buttonPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var calibrateButton = new System.Windows.Controls.Button
        {
            Content = "Calibrate Now",
            Padding = new Thickness(15, 5, 15, 5),
            Margin = new Thickness(0, 0, 10, 0),
            IsDefault = true
        };
        calibrateButton.Click += (s, e) =>
        {
            UserChoice = RecalibrationChoice.CalibrateNow;
            DialogResult = true;
            Close();
        };
        buttonPanel.Children.Add(calibrateButton);

        var remindButton = new System.Windows.Controls.Button
        {
            Content = "Remind Me Later",
            Padding = new Thickness(15, 5, 15, 5),
            Margin = new Thickness(0, 0, 10, 0)
        };
        remindButton.Click += (s, e) =>
        {
            UserChoice = RecalibrationChoice.RemindLater;
            DialogResult = true;
            Close();
        };
        buttonPanel.Children.Add(remindButton);

        var ignoreButton = new System.Windows.Controls.Button
        {
            Content = "Ignore This Session",
            Padding = new Thickness(15, 5, 15, 5)
        };
        ignoreButton.Click += (s, e) =>
        {
            UserChoice = RecalibrationChoice.IgnoreSession;
            DialogResult = true;
            Close();
        };
        buttonPanel.Children.Add(ignoreButton);

        System.Windows.Controls.Grid.SetRow(buttonPanel, 1);
        grid.Children.Add(buttonPanel);

        Content = grid;
    }
}
