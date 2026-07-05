using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace RemoteCamera
{
    /// <summary>
    /// カメラ監視と録画操作を行うメイン画面。
    /// </summary>
    public partial class frmRemoteCamera : Form
    {
        private const int DwmWindowAttributeUseImmersiveDarkMode = 20;
        private const int DwmWindowAttributeCaptionColor = 35;
        private const int DwmWindowAttributeTextColor = 36;
        private const int DwmWindowAttributeBorderColor = 34;

        private readonly CameraService cameraService = new();
        private readonly CameraDeviceCatalog cameraDeviceCatalog = new();
        private readonly RemoteMonitorServer monitorServer;
        private readonly System.Windows.Forms.Timer previewTimer = new();

        private Icon? appIcon;
        private string? startupErrorMessage;
        private bool startupCompleted;
        private bool startupStarted;
        private bool startupUiApplied;
        private bool exitRequested;
        private bool compactLayout;
        private bool cameraSelectionSyncing;
        private bool cameraSwitching;
        private int lastPreviewVersion = -1;
        private string? selectedRecordingPath;

        /// <summary>
        /// 画面を初期化する。
        /// </summary>
        public frmRemoteCamera()
        {
            InitializeComponent();

            monitorServer = new RemoteMonitorServer(cameraService, 8765);

            previewTimer.Interval = 120;
            previewTimer.Tick += PreviewTimer_Tick;

            HandleCreated += frmRemoteCamera_HandleCreated;
            Resize += frmRemoteCamera_Resize;
            FormClosing += frmRemoteCamera_FormClosing;

            buttonSelectPath.Click += SelectPathButton_Click;
            buttonApplyCamera.Click += ApplyCameraButton_Click;
            buttonRefreshDevices.Click += RefreshDevicesButton_Click;
            buttonStartRecord.Click += StartRecordButton_Click;
            buttonStopRecord.Click += StopRecordButton_Click;
            buttonStopPreview.Click += StopPreviewButton_Click;
            buttonExit.Click += ExitButton_Click;
            cameraDeviceComboBox.SelectedIndexChanged += CameraDeviceComboBox_SelectedIndexChanged;

            trayOpenMenuItem.Click += TrayOpenMenuItem_Click;
            trayExitMenuItem.Click += TrayExitMenuItem_Click;
            trayIcon.DoubleClick += TrayIcon_DoubleClick;

            InitializeTheme();
            ApplyAppIcons();
            LoadCameraDevices();
            ApplyResponsiveLayout();
            UpdateUiState();

            _ = InitializeStartupAsync();
        }

        /// <summary>
        /// 画面のハンドル生成時に初期表示を整える。
        /// </summary>
        private async void frmRemoteCamera_HandleCreated(object? sender, EventArgs e)
        {
            ApplyWindowChrome();

            if (!startupCompleted || startupUiApplied)
            {
                return;
            }

            ApplyStartupUiState();
        }

        /// <summary>
        /// 監視ページとカメラを起動し、初期表示を整える。
        /// </summary>
        private async Task InitializeStartupAsync()
        {
            if (startupStarted)
            {
                return;
            }

            startupStarted = true;
            WriteStartupLog("InitializeStartupAsync: begin");

            var startupErrors = new List<string>();

            try
            {
                WriteStartupLog("InitializeStartupAsync: monitor server start");
                await monitorServer.StartAsync();
                WriteStartupLog("InitializeStartupAsync: monitor server started");
            }
            catch (Exception ex)
            {
                WriteStartupLog($"InitializeStartupAsync: monitor server error: {ex}");
                startupErrors.Add($"監視ページの起動に失敗しました。{Environment.NewLine}{ex.Message}");
            }

            try
            {
                var selectedDevice = GetSelectedCameraDevice();
                WriteStartupLog($"InitializeStartupAsync: camera start: {selectedDevice?.DisplayName}");
                await cameraService.InitializeAsync(selectedDevice);
                WriteStartupLog("InitializeStartupAsync: camera started");
            }
            catch (Exception ex)
            {
                WriteStartupLog($"InitializeStartupAsync: camera error: {ex}");
                startupErrors.Add($"カメラの起動に失敗しました。{Environment.NewLine}{ex.Message}");
            }

            startupErrorMessage = startupErrors.Count > 0
                ? string.Join(Environment.NewLine + Environment.NewLine, startupErrors)
                : null;

            startupCompleted = true;
            WriteStartupLog("InitializeStartupAsync: completed");

            if (IsHandleCreated && !IsDisposed)
            {
                try
                {
                    BeginInvoke(new Action(ApplyStartupUiState));
                }
                catch
                {
                    // 起動時の見た目の反映に失敗しても、監視処理は継続する。
                }
            }
        }

        /// <summary>
        /// 起動後の画面状態をまとめて反映する。
        /// </summary>
        private void ApplyStartupUiState()
        {
            if (startupUiApplied)
            {
                return;
            }

            startupUiApplied = true;
            previewTimer.Start();
            UpdateUiState();

            if (!string.IsNullOrWhiteSpace(startupErrorMessage))
            {
                MessageBox.Show(this, startupErrorMessage, "起動エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            HideToTray();
        }

        /// <summary>
        /// 起動状況を一時的に書き出す。
        /// </summary>
        /// <param name="message">記録するメッセージ。</param>
        [System.Diagnostics.Conditional("DEBUG")]
        private static void WriteStartupLog(string message)
        {
            try
            {
                var logPath = Path.Combine(Path.GetTempPath(), "RemoteCamera-startup.log");
                File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch
            {
                // デバッグ用ログの失敗は無視する。
            }
        }

        /// <summary>
        /// 最小化時はトレイに隠し、幅に応じてレイアウトを切り替える。
        /// </summary>
        private void frmRemoteCamera_Resize(object? sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                HideToTray();
                return;
            }

            ApplyResponsiveLayout();
        }

        /// <summary>
        /// 閉じる操作をトレイ退避に切り替える。
        /// </summary>
        private void frmRemoteCamera_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (exitRequested)
            {
                return;
            }

            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
            }
        }

        /// <summary>
        /// 録画保存先を選択する。
        /// </summary>
        private void SelectPathButton_Click(object? sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog
            {
                Title = "録画ファイルの保存先を選択",
                Filter = "MP4ファイル (*.mp4)|*.mp4|すべてのファイル (*.*)|*.*",
                DefaultExt = "mp4",
                AddExtension = true,
                FileName = BuildDefaultRecordingFileName(),
                InitialDirectory = GetDefaultInitialDirectory()
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            selectedRecordingPath = EnsureMp4Extension(dialog.FileName);
            cameraService.SetRecordingTargetPath(selectedRecordingPath);
            UpdateUiState();
        }

        /// <summary>
        /// 選択中のカメラへ切り替える。
        /// </summary>
        private async void ApplyCameraButton_Click(object? sender, EventArgs e)
        {
            await SwitchSelectedCameraAsync();
        }

        /// <summary>
        /// コンボボックス変更時にカメラを即時切り替える。
        /// </summary>
        private async void CameraDeviceComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cameraSelectionSyncing)
            {
                return;
            }

            await SwitchSelectedCameraAsync();
        }

        /// <summary>
        /// 現在のコンボ選択をそのままカメラへ反映する。
        /// </summary>
        private async Task SwitchSelectedCameraAsync()
        {
            var selectedDevice = GetSelectedCameraDevice();
            if (selectedDevice is null)
            {
                UpdateUiState();
                return;
            }

            if (cameraSwitching)
            {
                return;
            }

            if (cameraService.SelectedCaptureIndex == selectedDevice.CaptureIndex && cameraService.IsReady)
            {
                UpdateUiState();
                return;
            }

            cameraSwitching = true;
            UpdateUiState();

            try
            {
                await cameraService.InitializeAsync(selectedDevice);
                lastPreviewVersion = -1;
                previewBox.Image?.Dispose();
                previewBox.Image = null;
                SyncSelectedCameraDevice();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "カメラ切替エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LoadCameraDevices();
            }
            finally
            {
                cameraSwitching = false;
            }

            UpdateUiState();
        }

        /// <summary>
        /// カメラ候補を再読み込みする。
        /// </summary>
        private void RefreshDevicesButton_Click(object? sender, EventArgs e)
        {
            LoadCameraDevices();
            UpdateUiState();
        }

        /// <summary>
        /// 録画を開始する。
        /// </summary>
        private async void StartRecordButton_Click(object? sender, EventArgs e)
        {
            try
            {
                await cameraService.StartRecordingAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "録画開始エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateUiState();
        }

        /// <summary>
        /// 録画を停止する。
        /// </summary>
        private async void StopRecordButton_Click(object? sender, EventArgs e)
        {
            try
            {
                await cameraService.StopRecordingAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "録画停止エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateUiState();
        }

        /// <summary>
        /// プレビューの更新を停止または再開する。
        /// </summary>
        private void StopPreviewButton_Click(object? sender, EventArgs e)
        {
            cameraService.TogglePreviewEnabled();
            UpdateUiState();
        }

        /// <summary>
        /// アプリケーションを終了する。
        /// </summary>
        private async void ExitButton_Click(object? sender, EventArgs e)
        {
            await ExitApplicationAsync();
        }

        /// <summary>
        /// トレイメニューから画面を開く。
        /// </summary>
        private void TrayOpenMenuItem_Click(object? sender, EventArgs e)
        {
            ShowMainWindow();
        }

        /// <summary>
        /// トレイメニューからアプリケーションを終了する。
        /// </summary>
        private async void TrayExitMenuItem_Click(object? sender, EventArgs e)
        {
            await ExitApplicationAsync();
        }

        /// <summary>
        /// トレイアイコンのダブルクリックで画面を表示する。
        /// </summary>
        private void TrayIcon_DoubleClick(object? sender, EventArgs e)
        {
            ShowMainWindow();
        }

        /// <summary>
        /// プレビュー画像を最新状態へ更新する。
        /// </summary>
        private void PreviewTimer_Tick(object? sender, EventArgs e)
        {
            RefreshPreviewImage();
            UpdateUiState();
        }

        /// <summary>
        /// プレビューを最新フレームへ差し替える。
        /// </summary>
        private void RefreshPreviewImage()
        {
            if (!cameraService.TryGetLatestFrameSnapshot(out var latestFrame, out var frameVersion))
            {
                return;
            }

            if (frameVersion == lastPreviewVersion)
            {
                latestFrame?.Dispose();
                return;
            }

            lastPreviewVersion = frameVersion;

            var previousImage = previewBox.Image;
            previewBox.Image = latestFrame;
            previewBox.Visible = true;
            previewPlaceholderLabel.Visible = false;
            previewBox.BringToFront();
            previousImage?.Dispose();
        }

        /// <summary>
        /// 画面をトレイへ隠す。
        /// </summary>
        private void HideToTray()
        {
            ShowInTaskbar = false;
            Hide();
        }

        /// <summary>
        /// 画面を再表示する。
        /// </summary>
        private void ShowMainWindow()
        {
            ShowInTaskbar = true;

            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            Show();
            BringToFront();
            Activate();
        }

        /// <summary>
        /// 画面のラベルとボタン状態をまとめて更新する。
        /// </summary>
        private void UpdateUiState()
        {
            var isReady = cameraService.IsReady;
            var isRecording = cameraService.IsRecording;
            var isPreviewEnabled = cameraService.IsPreviewEnabled;
            var hasPreviewImage = previewBox.Image is not null;
            var recordingTargetPath = cameraService.RecordingTargetPath;
            var recordingTargetText = !string.IsNullOrWhiteSpace(recordingTargetPath)
                ? recordingTargetPath
                : $"{CameraService.DefaultRecordingDirectory}\\（既定）";

            statusValueLabel.Text = cameraService.StatusText;
            cameraValueLabel.Text = cameraService.SelectedDeviceName ?? (isReady ? "USBカメラ" : "未検出");
            recordingPathValueLabel.Text = isRecording && !string.IsNullOrWhiteSpace(cameraService.RecordingPath)
                ? cameraService.RecordingPath!
                : recordingTargetText;
            localUrlValueLabel.Text = monitorServer.IsRunning ? monitorServer.LocalUrl : $"{monitorServer.LocalUrl}（未起動）";
            tailscaleUrlValueLabel.Text = monitorServer.TailscaleUrl is { Length: > 0 }
                ? (monitorServer.IsRunning ? monitorServer.TailscaleUrl : $"{monitorServer.TailscaleUrl}（未起動）")
                : "Tailscale は見つかりませんでした";

            endpointLabel.Text = monitorServer.GetAccessibleUrlText();
            previewPlaceholderLabel.Text = isReady
                ? (isPreviewEnabled ? "プレビューを受信しています。" : "プレビューを停止中です。")
                : "カメラを初期化しています。";

            SetBadge(heroStatusLabel,
                isRecording ? "録画中" : !isPreviewEnabled ? "停止中" : isReady ? "待機中" : "起動中",
                isRecording ? Color.FromArgb(203, 68, 74) : !isPreviewEnabled ? Color.FromArgb(255, 194, 88) : isReady ? Color.FromArgb(43, 162, 202) : Color.FromArgb(96, 112, 136));
            SetBadge(previewStateLabel,
                !isPreviewEnabled ? "PAUSE" : isRecording ? "REC" : isReady ? "LIVE" : "WAIT",
                !isPreviewEnabled ? Color.FromArgb(255, 194, 88) : isRecording ? Color.FromArgb(203, 68, 74) : isReady ? Color.FromArgb(43, 162, 202) : Color.FromArgb(96, 112, 136));

            previewBox.Visible = hasPreviewImage;
            previewPlaceholderLabel.Visible = !hasPreviewImage || !isPreviewEnabled;
            if (hasPreviewImage)
            {
                if (isPreviewEnabled)
                {
                    previewBox.BringToFront();
                }
                else
                {
                    previewPlaceholderLabel.BringToFront();
                }
            }
            else
            {
                previewPlaceholderLabel.BringToFront();
            }

            toolTipMain.SetToolTip(heroStatusLabel, cameraService.StatusText);
            toolTipMain.SetToolTip(endpointLabel, monitorServer.GetAccessibleUrlText());
            toolTipMain.SetToolTip(recordingPathValueLabel, recordingTargetText);
            toolTipMain.SetToolTip(localUrlValueLabel, monitorServer.LocalUrl);
            toolTipMain.SetToolTip(tailscaleUrlValueLabel, monitorServer.TailscaleUrl ?? "Tailscale アドレスを検出できませんでした");

            buttonSelectPath.Enabled = !isRecording;
            buttonApplyCamera.Enabled = !isRecording && !cameraSwitching && cameraDeviceComboBox.SelectedItem is CameraDeviceOption;
            buttonRefreshDevices.Enabled = !isRecording;
            buttonStartRecord.Enabled = isReady && !isRecording;
            buttonStopRecord.Enabled = isRecording;
            buttonStopPreview.Enabled = isReady;
            buttonStopPreview.Text = isPreviewEnabled ? "プレビュー停止" : "プレビュー再開";
            buttonApplyCamera.Text = cameraSwitching ? "切替中" : "即時切替";

            SyncSelectedCameraDevice();

            if (isRecording)
            {
                buttonStartRecord.Text = "録画中";
            }
            else
            {
                buttonStartRecord.Text = "録画";
            }
        }

        /// <summary>
        /// カメラ候補をコンボボックスへ設定する。
        /// </summary>
        private void LoadCameraDevices()
        {
            var currentDeviceName = cameraService.SelectedDeviceName;
            var devices = cameraDeviceCatalog.GetCameraDevices();

            cameraSelectionSyncing = true;
            cameraDeviceComboBox.BeginUpdate();
            cameraDeviceComboBox.Items.Clear();
            foreach (var device in devices)
            {
                cameraDeviceComboBox.Items.Add(device);
            }

            if (cameraDeviceComboBox.Items.Count > 0)
            {
                cameraDeviceComboBox.SelectedIndex = FindDeviceIndex(devices, currentDeviceName);
            }

            cameraDeviceComboBox.EndUpdate();
            cameraSelectionSyncing = false;
        }

        /// <summary>
        /// 選択中のカメラ候補を取得する。
        /// </summary>
        /// <returns>カメラ候補。</returns>
        private CameraDeviceOption? GetSelectedCameraDevice()
        {
            return cameraDeviceComboBox.SelectedItem as CameraDeviceOption;
        }

        /// <summary>
        /// 表示名に一致する候補位置を探す。
        /// </summary>
        /// <param name="devices">カメラ候補一覧。</param>
        /// <param name="deviceName">現在のカメラ名。</param>
        /// <returns>候補位置。</returns>
        private static int FindDeviceIndex(IReadOnlyList<CameraDeviceOption> devices, string? deviceName)
        {
            if (!string.IsNullOrWhiteSpace(deviceName))
            {
                for (var index = 0; index < devices.Count; index++)
                {
                    if (devices[index].DisplayName.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        return index;
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// サービス側の現在選択へコンボボックス表示を合わせる。
        /// </summary>
        private void SyncSelectedCameraDevice()
        {
            if (cameraSelectionSyncing || cameraDeviceComboBox.Items.Count == 0)
            {
                return;
            }

            var selectedCaptureIndex = cameraService.SelectedCaptureIndex;
            if (!selectedCaptureIndex.HasValue)
            {
                return;
            }

            for (var index = 0; index < cameraDeviceComboBox.Items.Count; index++)
            {
                if (cameraDeviceComboBox.Items[index] is not CameraDeviceOption option)
                {
                    continue;
                }

                if (option.CaptureIndex != selectedCaptureIndex.Value)
                {
                    continue;
                }

                if (cameraDeviceComboBox.SelectedIndex == index)
                {
                    return;
                }

                cameraSelectionSyncing = true;
                cameraDeviceComboBox.SelectedIndex = index;
                cameraSelectionSyncing = false;
                return;
            }
        }

        /// <summary>
        /// 既定の録画ファイル名を作る。
        /// </summary>
        private static string BuildDefaultRecordingFileName()
        {
            return $"camera_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        }

        /// <summary>
        /// 保存先ダイアログの初期フォルダーを決める。
        /// </summary>
        private static string GetDefaultInitialDirectory()
        {
            var videosFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            if (Directory.Exists(videosFolder))
            {
                return videosFolder;
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        /// <summary>
        /// 拡張子を mp4 にそろえる。
        /// </summary>
        private static string EnsureMp4Extension(string path)
        {
            if (Path.GetExtension(path).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return Path.ChangeExtension(path, ".mp4");
        }

        /// <summary>
        /// 画面全体の色と部品の外観をまとめて調整する。
        /// </summary>
        private void InitializeTheme()
        {
            BackColor = Color.FromArgb(5, 8, 14);
            ForeColor = Color.FromArgb(240, 245, 252);
            Font = new Font("Yu Gothic UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            DoubleBuffered = true;

            rootLayout.BackColor = BackColor;
            heroPanel.BackColor = Color.FromArgb(10, 16, 28);
            infoCard.BackColor = Color.FromArgb(11, 17, 29);
            previewCard.BackColor = Color.FromArgb(11, 17, 29);
            previewSurface.BackColor = Color.FromArgb(6, 9, 16);
            previewBox.BackColor = Color.FromArgb(3, 4, 8);
            previewPlaceholderLabel.BackColor = Color.FromArgb(14, 19, 30);

            titleLabel.Font = new Font("Yu Gothic UI Semibold", 19F, FontStyle.Regular, GraphicsUnit.Point);
            subtitleLabel.Font = new Font("Yu Gothic UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            endpointLabel.Font = new Font("Yu Gothic UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
            heroStatusLabel.Font = new Font("Yu Gothic UI Semibold", 9.5F, FontStyle.Regular, GraphicsUnit.Point);

            infoHeaderLabel.Font = new Font("Yu Gothic UI Semibold", 13F, FontStyle.Regular, GraphicsUnit.Point);
            previewHeaderLabel.Font = new Font("Yu Gothic UI Semibold", 13F, FontStyle.Regular, GraphicsUnit.Point);
            previewStateLabel.Font = new Font("Yu Gothic UI Semibold", 10F, FontStyle.Regular, GraphicsUnit.Point);

            ApplyLabelStyle(statusTitleLabel, Color.FromArgb(154, 170, 194), bold: true);
            ApplyLabelStyle(cameraTitleLabel, Color.FromArgb(154, 170, 194), bold: true);
            ApplyLabelStyle(recordingPathTitleLabel, Color.FromArgb(154, 170, 194), bold: true);
            ApplyLabelStyle(localUrlTitleLabel, Color.FromArgb(154, 170, 194), bold: true);
            ApplyLabelStyle(tailscaleUrlTitleLabel, Color.FromArgb(154, 170, 194), bold: true);

            ApplyLabelStyle(statusValueLabel, Color.FromArgb(239, 244, 250));
            ApplyLabelStyle(cameraValueLabel, Color.FromArgb(239, 244, 250));
            ApplyLabelStyle(recordingPathValueLabel, Color.FromArgb(239, 244, 250));
            ApplyLabelStyle(localUrlValueLabel, Color.FromArgb(239, 244, 250));
            ApplyLabelStyle(tailscaleUrlValueLabel, Color.FromArgb(239, 244, 250));

            previewPlaceholderLabel.Font = new Font("Yu Gothic UI Semibold", 11F, FontStyle.Regular, GraphicsUnit.Point);
            previewPlaceholderLabel.TextAlign = ContentAlignment.MiddleCenter;

            foreach (var label in new[]
            {
                titleLabel,
                subtitleLabel,
                endpointLabel,
                heroStatusLabel,
                infoHeaderLabel,
                statusTitleLabel,
                statusValueLabel,
                cameraTitleLabel,
                cameraValueLabel,
                recordingPathTitleLabel,
                recordingPathValueLabel,
                localUrlTitleLabel,
                localUrlValueLabel,
                tailscaleUrlTitleLabel,
                tailscaleUrlValueLabel,
                previewHeaderLabel,
                previewStateLabel,
                previewPlaceholderLabel
            })
            {
                label.UseCompatibleTextRendering = true;
            }

            titleLabel.AutoSize = false;

            StyleCommandButton(buttonSelectPath, Color.FromArgb(20, 35, 60), Color.FromArgb(117, 197, 255));
            StyleCommandButton(buttonApplyCamera, Color.FromArgb(20, 35, 60), Color.FromArgb(117, 197, 255));
            StyleCommandButton(buttonRefreshDevices, Color.FromArgb(21, 28, 40), Color.FromArgb(174, 188, 206));
            StyleCommandButton(buttonStartRecord, Color.FromArgb(68, 23, 31), Color.FromArgb(255, 106, 126));
            StyleCommandButton(buttonStopRecord, Color.FromArgb(66, 44, 14), Color.FromArgb(255, 194, 88));
            StyleCommandButton(buttonExit, Color.FromArgb(21, 28, 40), Color.FromArgb(174, 188, 206));

            commandBar.Padding = new Padding(0, 0, 0, 0);
            cameraDeviceComboBox.BackColor = Color.FromArgb(16, 24, 38);
            cameraDeviceComboBox.ForeColor = Color.White;
            
            heroPanel.Paint += (_, e) => PaintCardChrome(heroPanel, e.Graphics, Color.FromArgb(60, 165, 255));
            infoCard.Paint += (_, e) => PaintCardChrome(infoCard, e.Graphics, Color.FromArgb(90, 132, 255));
            previewCard.Paint += (_, e) => PaintCardChrome(previewCard, e.Graphics, Color.FromArgb(47, 204, 196));

            toolTipMain.BackColor = Color.FromArgb(33, 38, 48);
            toolTipMain.ForeColor = Color.White;
        }

        /// <summary>
        /// Windows のタイトルバー色をアプリ全体の配色へ寄せる。
        /// </summary>
        private void ApplyWindowChrome()
        {
            if (!OperatingSystem.IsWindows() || !IsHandleCreated)
            {
                return;
            }

            try
            {
                var darkModeEnabled = 1;
                var captionColor = ToColorRef(Color.FromArgb(10, 16, 28));
                var textColor = ToColorRef(Color.FromArgb(239, 244, 250));
                var borderColor = ToColorRef(Color.FromArgb(32, 82, 150));

                DwmSetWindowAttribute(Handle, DwmWindowAttributeUseImmersiveDarkMode, ref darkModeEnabled, sizeof(int));
                DwmSetWindowAttribute(Handle, DwmWindowAttributeCaptionColor, ref captionColor, sizeof(uint));
                DwmSetWindowAttribute(Handle, DwmWindowAttributeTextColor, ref textColor, sizeof(uint));
                DwmSetWindowAttribute(Handle, DwmWindowAttributeBorderColor, ref borderColor, sizeof(uint));
            }
            catch
            {
                // 非対応環境では既定のタイトルバーをそのまま使う。
            }
        }

        /// <summary>
        /// アイコンとボタン画像を設定する。
        /// </summary>
        private void ApplyAppIcons()
        {
            appIcon = UiIconFactory.CreateAppIcon();
            Icon = appIcon;
            trayIcon.Icon = appIcon;
            heroIconBox.Image = UiIconFactory.CreateAppBitmap(128);

            buttonSelectPath.Image = UiIconFactory.CreateButtonBitmap(ButtonIconKind.Folder);
            buttonStartRecord.Image = UiIconFactory.CreateButtonBitmap(ButtonIconKind.Record);
            buttonStopRecord.Image = UiIconFactory.CreateButtonBitmap(ButtonIconKind.Stop);
            buttonExit.Image = UiIconFactory.CreateButtonBitmap(ButtonIconKind.Exit);
            trayOpenMenuItem.Image = UiIconFactory.CreateButtonBitmap(ButtonIconKind.Folder);
            trayExitMenuItem.Image = UiIconFactory.CreateButtonBitmap(ButtonIconKind.Exit);

            buttonSelectPath.ImageAlign = ContentAlignment.MiddleLeft;
            buttonStartRecord.ImageAlign = ContentAlignment.MiddleLeft;
            buttonStopRecord.ImageAlign = ContentAlignment.MiddleLeft;
            buttonExit.ImageAlign = ContentAlignment.MiddleLeft;
        }

        /// <summary>
        /// 幅に応じて縦横の並びを切り替える。
        /// </summary>
        private void ApplyResponsiveLayout()
        {
            var shouldUseCompactLayout = ClientSize.Width < 1100;
            if (compactLayout == shouldUseCompactLayout)
            {
                return;
            }

            compactLayout = shouldUseCompactLayout;
            contentSplit.SuspendLayout();

            contentSplit.Orientation = shouldUseCompactLayout ? Orientation.Horizontal : Orientation.Vertical;
            contentSplit.Panel1MinSize = shouldUseCompactLayout ? 280 : 380;
            contentSplit.Panel2MinSize = shouldUseCompactLayout ? 300 : 400;
            contentSplit.SplitterDistance = shouldUseCompactLayout
                ? Math.Min(360, Math.Max(280, contentSplit.Height / 2))
                : Math.Min(620, Math.Max(380, (int)(contentSplit.Width * 0.42)));

            contentSplit.ResumeLayout();
        }

        /// <summary>
        /// ボタンの共通スタイルを設定する。
        /// </summary>
        private static void StyleCommandButton(Button button, Color backColor, Color accentColor)
        {
            button.BackColor = backColor;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = accentColor;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor);
        }

        /// <summary>
        /// ラベルの共通スタイルを設定する。
        /// </summary>
        private static void ApplyLabelStyle(Label label, Color foreColor, bool bold = false)
        {
            label.ForeColor = foreColor;
            if (bold)
            {
                label.Font = new Font("Yu Gothic UI Semibold", label.Font.Size, FontStyle.Regular, GraphicsUnit.Point);
            }
        }

        /// <summary>
        /// 角丸バッジ風の表示に整える。
        /// </summary>
        /// <param name="label">対象ラベル。</param>
        /// <param name="text">表示文字列。</param>
        /// <param name="backColor">背景色。</param>
        private static void SetBadge(Label label, string text, Color backColor)
        {
            label.AutoSize = true;
            label.Text = text;
            label.BackColor = backColor;
            label.ForeColor = Color.White;
            label.Padding = new Padding(12, 8, 12, 8);
            label.TextAlign = ContentAlignment.MiddleCenter;
        }

        /// <summary>
        /// カードの上部にアクセントと境界線を描画する。
        /// </summary>
        private static void PaintCardChrome(Control control, Graphics graphics, Color accentColor)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(0, 0, Math.Max(1, control.Width), Math.Max(1, control.Height));
            using var backgroundBrush = new LinearGradientBrush(
                bounds,
                Color.FromArgb(24, 30, 44),
                Color.FromArgb(10, 14, 22),
                LinearGradientMode.Vertical);
            using var borderPen = new Pen(Color.FromArgb(90, 105, 128, 160));
            using var accentBrush = new LinearGradientBrush(
                new Rectangle(0, 0, bounds.Width, 5),
                Color.FromArgb(240, accentColor),
                Color.FromArgb(10, accentColor),
                LinearGradientMode.Horizontal);

            graphics.FillRectangle(backgroundBrush, bounds);
            graphics.FillRectangle(accentBrush, new Rectangle(0, 0, bounds.Width, 4));
            graphics.DrawRectangle(borderPen, new Rectangle(0, 0, Math.Max(0, bounds.Width - 1), Math.Max(0, bounds.Height - 1)));
        }

        /// <summary>
        /// DWM に渡す COLORREF 形式へ変換する。
        /// </summary>
        /// <param name="color">変換元の色。</param>
        /// <returns>COLORREF 値。</returns>
        private static uint ToColorRef(Color color)
        {
            return (uint)(color.R | (color.G << 8) | (color.B << 16));
        }

        /// <summary>
        /// アプリケーションを安全に終了する。
        /// </summary>
        private async Task ExitApplicationAsync()
        {
            if (exitRequested)
            {
                return;
            }

            exitRequested = true;
            previewTimer.Stop();
            previewTimer.Dispose();
            trayIcon.Visible = false;

            var previousImage = previewBox.Image;
            previewBox.Image = null;
            previousImage?.Dispose();

            try
            {
                await monitorServer.DisposeAsync();
            }
            catch
            {
                // 終了時は後続処理を優先する。
            }

            try
            {
                await cameraService.DisposeAsync();
            }
            catch
            {
                // 終了時は後続処理を優先する。
            }

            heroIconBox.Image?.Dispose();
            heroIconBox.Image = null;
            trayOpenMenuItem.Image?.Dispose();
            trayOpenMenuItem.Image = null;
            trayExitMenuItem.Image?.Dispose();
            trayExitMenuItem.Image = null;
            appIcon?.Dispose();
            appIcon = null;

            Close();
        }

        /// <summary>
        /// タイトルバー色などの DWM 属性を設定する。
        /// </summary>
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        /// <summary>
        /// タイトルバー色などの DWM 属性を設定する。
        /// </summary>
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref uint pvAttribute, int cbAttribute);
    }
}
