using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace RemoteCamera
{
    /// <summary>
    /// ネットワークカメラ設定を編集するマスタ画面。
    /// </summary>
    internal sealed class frmNetworkCameraMaster : Form
    {
        private readonly NetworkCameraCatalog networkCameraCatalog = new();

        private readonly ListBox registeredListBox = new();
        private readonly ListBox detectedListBox = new();
        private readonly TextBox cameraIdTextBox = new();
        private readonly TextBox displayNameTextBox = new();
        private readonly TextBox hostAddressTextBox = new();
        private readonly TextBox rtspUrlTextBox = new();
        private readonly CheckBox enabledCheckBox = new();
        private readonly Label statusLabel = new();
        private readonly Label titleLabel = new();
        private readonly Label subtitleLabel = new();
        private readonly Label headerBadgeLabel = new();
        private readonly Label registeredTitleLabel = new();
        private readonly Label editorTitleLabel = new();
        private readonly Label detectedTitleLabel = new();
        private readonly Button buttonSave = new();
        private readonly Button buttonDelete = new();
        private readonly Button buttonCheck = new();
        private readonly Button buttonDetect = new();
        private readonly Button buttonLoadDetected = new();
        private readonly Button buttonNew = new();
        private readonly Button buttonClose = new();
        private readonly Panel headerCard = new();
        private readonly Panel registeredCard = new();
        private readonly Panel editorCard = new();
        private readonly Panel detectedCard = new();

        private IReadOnlyList<NetworkCameraConfigItem> registeredItems = Array.Empty<NetworkCameraConfigItem>();
        private IReadOnlyList<DetectedNetworkCameraItem> detectedItems = Array.Empty<DetectedNetworkCameraItem>();
        private bool selectionSyncing;

        /// <summary>
        /// 設定が更新されたかどうかを返す。
        /// </summary>
        public bool SettingsChanged { get; private set; }

        /// <summary>
        /// 画面を初期化する。
        /// </summary>
        public frmNetworkCameraMaster()
        {
            InitializeComponent();
            InitializeTheme();
            HandleCreated += frmNetworkCameraMaster_HandleCreated;
            LoadRegisteredItems();
            ClearEditor();
        }

        /// <summary>
        /// ハンドル生成後にタイトルバーの色を整える。
        /// </summary>
        private void frmNetworkCameraMaster_HandleCreated(object? sender, EventArgs e)
        {
            ApplyWindowChrome();
        }

        /// <summary>
        /// 画面部品を組み立てる。
        /// </summary>
        private void InitializeComponent()
        {
            Text = "ネットワークカメラ設定";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1120, 720);
            Size = new Size(1180, 760);

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(16),
                BackColor = Color.FromArgb(5, 8, 14)
            };
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320F));
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 108F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 210F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
            Controls.Add(rootLayout);

            headerCard.Dock = DockStyle.Fill;
            headerCard.Margin = new Padding(0, 0, 0, 10);
            headerCard.Padding = new Padding(16, 14, 16, 14);
            rootLayout.Controls.Add(headerCard, 0, 0);
            rootLayout.SetColumnSpan(headerCard, 2);

            var headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            headerCard.Controls.Add(headerLayout);

            titleLabel.Text = "ネットワークカメラ設定";
            titleLabel.Dock = DockStyle.Fill;
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            headerLayout.Controls.Add(titleLabel, 0, 0);

            headerBadgeLabel.Text = "RTSP / ONVIF";
            headerBadgeLabel.AutoSize = true;
            headerBadgeLabel.Anchor = AnchorStyles.Right;
            headerBadgeLabel.Margin = new Padding(0, 6, 0, 0);
            headerBadgeLabel.Padding = new Padding(12, 6, 12, 6);
            headerBadgeLabel.TextAlign = ContentAlignment.MiddleCenter;
            headerLayout.Controls.Add(headerBadgeLabel, 1, 0);

            subtitleLabel.Text = "登録済み設定の編集、自動検出、通信確認をまとめて扱う画面です。";
            subtitleLabel.Dock = DockStyle.Fill;
            subtitleLabel.TextAlign = ContentAlignment.MiddleLeft;
            headerLayout.Controls.Add(subtitleLabel, 0, 1);

            var headerSpacer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            headerLayout.Controls.Add(headerSpacer, 1, 1);

            registeredCard.Dock = DockStyle.Fill;
            registeredCard.Margin = new Padding(0, 0, 10, 0);
            registeredCard.Padding = new Padding(14);
            rootLayout.Controls.Add(registeredCard, 0, 1);
            rootLayout.SetRowSpan(registeredCard, 2);

            var registeredLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            registeredLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            registeredLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            registeredCard.Controls.Add(registeredLayout);

            registeredTitleLabel.Text = "登録済みカメラ";
            registeredTitleLabel.Dock = DockStyle.Fill;
            registeredTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
            registeredLayout.Controls.Add(registeredTitleLabel, 0, 0);

            registeredListBox.Dock = DockStyle.Fill;
            registeredListBox.IntegralHeight = false;
            registeredListBox.BorderStyle = BorderStyle.FixedSingle;
            registeredListBox.SelectedIndexChanged += RegisteredListBox_SelectedIndexChanged;
            registeredLayout.Controls.Add(registeredListBox, 0, 1);

            editorCard.Dock = DockStyle.Fill;
            editorCard.Margin = new Padding(0, 0, 0, 10);
            editorCard.Padding = new Padding(14);
            rootLayout.Controls.Add(editorCard, 1, 1);

            var editorCardLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            editorCardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            editorCardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            editorCard.Controls.Add(editorCardLayout);

            editorTitleLabel.Text = "設定内容";
            editorTitleLabel.Dock = DockStyle.Fill;
            editorTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
            editorCardLayout.Controls.Add(editorTitleLabel, 0, 0);

            var editorLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                Padding = new Padding(0, 2, 0, 0),
                BackColor = Color.Transparent
            };
            editorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            editorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            editorCardLayout.Controls.Add(editorLayout, 0, 1);

            AddEditorRow(editorLayout, 0, "識別子", cameraIdTextBox);
            AddEditorRow(editorLayout, 1, "表示名", displayNameTextBox);
            AddEditorRow(editorLayout, 2, "ホスト名 / IP", hostAddressTextBox);
            AddEditorRow(editorLayout, 3, "RTSP URL", rtspUrlTextBox);

            enabledCheckBox.Text = "有効にする";
            enabledCheckBox.Dock = DockStyle.Left;
            enabledCheckBox.AutoSize = true;
            editorLayout.Controls.Add(new Label
            {
                Text = "利用設定",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 4);
            editorLayout.Controls.Add(enabledCheckBox, 1, 4);

            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.TopLeft;
            statusLabel.Padding = new Padding(0, 6, 0, 0);
            statusLabel.Text = "ここに通信確認や自動検出の結果を表示します。";
            editorLayout.Controls.Add(new Label
            {
                Text = "状態",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft
            }, 0, 5);
            editorLayout.Controls.Add(statusLabel, 1, 5);

            detectedCard.Dock = DockStyle.Fill;
            detectedCard.Margin = new Padding(0, 0, 0, 0);
            detectedCard.Padding = new Padding(14);
            rootLayout.Controls.Add(detectedCard, 1, 2);

            var detectedCardLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            detectedCardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            detectedCardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            detectedCard.Controls.Add(detectedCardLayout);

            detectedTitleLabel.Text = "自動検出候補";
            detectedTitleLabel.Dock = DockStyle.Fill;
            detectedTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
            detectedCardLayout.Controls.Add(detectedTitleLabel, 0, 0);

            var detectedLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 2, 0, 0),
                BackColor = Color.Transparent
            };
            detectedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            detectedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            detectedCardLayout.Controls.Add(detectedLayout, 0, 1);

            detectedListBox.Dock = DockStyle.Fill;
            detectedListBox.IntegralHeight = false;
            detectedListBox.BorderStyle = BorderStyle.FixedSingle;
            detectedListBox.SelectedIndexChanged += DetectedListBox_SelectedIndexChanged;
            detectedLayout.Controls.Add(detectedListBox, 0, 0);

            var detectedButtonLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            detectedLayout.Controls.Add(detectedButtonLayout, 1, 0);

            buttonDetect.Text = "自動検出";
            buttonDetect.AutoSize = true;
            buttonDetect.Click += async (_, _) => await DetectCandidatesAsync();
            detectedButtonLayout.Controls.Add(buttonDetect);

            buttonLoadDetected.Text = "候補を読込";
            buttonLoadDetected.AutoSize = true;
            buttonLoadDetected.Click += ButtonLoadDetected_Click;
            detectedButtonLayout.Controls.Add(buttonLoadDetected);

            var footerLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 6, 0, 0)
            };
            rootLayout.Controls.Add(footerLayout, 0, 3);
            rootLayout.SetColumnSpan(footerLayout, 2);

            buttonClose.Text = "閉じる";
            buttonClose.AutoSize = true;
            buttonClose.Click += (_, _) => Close();
            footerLayout.Controls.Add(buttonClose);

            buttonCheck.Text = "通信確認";
            buttonCheck.AutoSize = true;
            buttonCheck.Click += async (_, _) => await CheckCurrentItemAsync();
            footerLayout.Controls.Add(buttonCheck);

            buttonDelete.Text = "削除";
            buttonDelete.AutoSize = true;
            buttonDelete.Click += (_, _) => DeleteCurrentItem();
            footerLayout.Controls.Add(buttonDelete);

            buttonSave.Text = "保存";
            buttonSave.AutoSize = true;
            buttonSave.Click += async (_, _) => await SaveCurrentItemAsync();
            footerLayout.Controls.Add(buttonSave);

            buttonNew.Text = "新規";
            buttonNew.AutoSize = true;
            buttonNew.Click += (_, _) => ClearEditor();
            footerLayout.Controls.Add(buttonNew);
        }

        /// <summary>
        /// 入力行を追加する。
        /// </summary>
        /// <param name="layout">追加先レイアウト。</param>
        /// <param name="rowIndex">行番号。</param>
        /// <param name="labelText">ラベル文字列。</param>
        /// <param name="textBox">入力欄。</param>
        private static void AddEditorRow(TableLayoutPanel layout, int rowIndex, string labelText, TextBox textBox)
        {
            layout.Controls.Add(new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(154, 170, 194)
            }, 0, rowIndex);

            textBox.Dock = DockStyle.Fill;
            layout.Controls.Add(textBox, 1, rowIndex);
        }

        /// <summary>
        /// 登録済み一覧を読み込む。
        /// </summary>
        private void LoadRegisteredItems()
        {
            registeredItems = networkCameraCatalog.GetConfigItems();

            selectionSyncing = true;
            try
            {
                registeredListBox.Items.Clear();
                foreach (var item in registeredItems)
                {
                    registeredListBox.Items.Add(new RegisteredCameraListItem(item));
                }
            }
            finally
            {
                selectionSyncing = false;
            }
        }

        /// <summary>
        /// 編集欄を初期化する。
        /// </summary>
        private void ClearEditor()
        {
            selectionSyncing = true;
            try
            {
                registeredListBox.ClearSelected();
                cameraIdTextBox.Text = string.Empty;
                displayNameTextBox.Text = string.Empty;
                hostAddressTextBox.Text = string.Empty;
                rtspUrlTextBox.Text = string.Empty;
                enabledCheckBox.Checked = true;
                statusLabel.Text = "ここに通信確認や自動検出の結果を表示します。";
            }
            finally
            {
                selectionSyncing = false;
            }
        }

        /// <summary>
        /// 登録済み一覧選択時に編集欄へ反映する。
        /// </summary>
        private void RegisteredListBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (selectionSyncing || registeredListBox.SelectedItem is not RegisteredCameraListItem item)
            {
                return;
            }

            cameraIdTextBox.Text = item.ConfigItem.CameraId ?? string.Empty;
            displayNameTextBox.Text = item.ConfigItem.DisplayName ?? string.Empty;
            hostAddressTextBox.Text = item.ConfigItem.HostAddress ?? string.Empty;
            rtspUrlTextBox.Text = item.ConfigItem.RtspUrl ?? string.Empty;
            enabledCheckBox.Checked = item.ConfigItem.Enabled;
            statusLabel.Text = "登録済み設定を読み込みました。";
        }

        /// <summary>
        /// 自動検出候補を編集欄へ反映する。
        /// </summary>
        private void DetectedListBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (selectionSyncing || detectedListBox.SelectedItem is not DetectedCameraListItem item)
            {
                return;
            }

            cameraIdTextBox.Text = item.DetectedItem.CameraId;
            displayNameTextBox.Text = item.DetectedItem.DisplayName;
            hostAddressTextBox.Text = item.DetectedItem.HostAddress;
            rtspUrlTextBox.Text = item.DetectedItem.RtspUrl;
            enabledCheckBox.Checked = true;
            statusLabel.Text = item.DetectedItem.StatusText;
        }

        /// <summary>
        /// 選択中の検出候補を編集欄へ読み込む。
        /// </summary>
        private void ButtonLoadDetected_Click(object? sender, EventArgs e)
        {
            if (detectedListBox.SelectedItem is not DetectedCameraListItem)
            {
                statusLabel.Text = "自動検出候補を選択してください。";
                return;
            }

            DetectedListBox_SelectedIndexChanged(sender, e);
        }

        /// <summary>
        /// 現在の入力内容を保存する。
        /// </summary>
        private async Task SaveCurrentItemAsync()
        {
            try
            {
                networkCameraCatalog.SaveConfigItem(CreateCurrentConfigItem());
                SettingsChanged = true;
                LoadRegisteredItems();
                statusLabel.Text = "設定を保存しました。";
                await CheckCurrentItemAsync();
            }
            catch (Exception ex)
            {
                statusLabel.Text = ex.Message;
            }
        }

        /// <summary>
        /// 現在の入力内容で通信確認を行う。
        /// </summary>
        private async Task CheckCurrentItemAsync()
        {
            buttonCheck.Enabled = false;
            try
            {
                var result = await networkCameraCatalog.CheckConnectionAsync(CreateCurrentConfigItem());
                statusLabel.Text = $"{result.StatusText}{Environment.NewLine}{result.CheckedAt:yyyy/MM/dd HH:mm:ss}";
            }
            catch (Exception ex)
            {
                statusLabel.Text = ex.Message;
            }
            finally
            {
                buttonCheck.Enabled = true;
            }
        }

        /// <summary>
        /// 自動検出を実行する。
        /// </summary>
        private async Task DetectCandidatesAsync()
        {
            buttonDetect.Enabled = false;
            statusLabel.Text = "同一 LAN の RTSP 候補を検出しています。";

            try
            {
                detectedItems = await networkCameraCatalog.DetectNetworkCamerasAsync();

                selectionSyncing = true;
                try
                {
                    detectedListBox.Items.Clear();
                    foreach (var item in detectedItems)
                    {
                        detectedListBox.Items.Add(new DetectedCameraListItem(item));
                    }
                }
                finally
                {
                    selectionSyncing = false;
                }

                statusLabel.Text = detectedItems.Count == 0
                    ? "候補は見つかりませんでした。"
                    : $"{detectedItems.Count} 件の候補を検出しました。";
            }
            catch (Exception ex)
            {
                statusLabel.Text = ex.Message;
            }
            finally
            {
                buttonDetect.Enabled = true;
            }
        }

        /// <summary>
        /// 現在の入力内容を削除する。
        /// </summary>
        private void DeleteCurrentItem()
        {
            var cameraId = cameraIdTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(cameraId))
            {
                statusLabel.Text = "削除する識別子がありません。";
                return;
            }

            if (MessageBox.Show(this, "この設定を削除します。", "削除確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            networkCameraCatalog.DeleteConfigItem(cameraId);
            SettingsChanged = true;
            LoadRegisteredItems();
            ClearEditor();
            statusLabel.Text = "設定を削除しました。";
        }

        /// <summary>
        /// 現在の入力内容から設定オブジェクトを作る。
        /// </summary>
        /// <returns>設定オブジェクト。</returns>
        private NetworkCameraConfigItem CreateCurrentConfigItem()
        {
            return new NetworkCameraConfigItem
            {
                Enabled = enabledCheckBox.Checked,
                CameraId = cameraIdTextBox.Text.Trim(),
                DisplayName = displayNameTextBox.Text.Trim(),
                HostAddress = hostAddressTextBox.Text.Trim(),
                RtspUrl = rtspUrlTextBox.Text.Trim()
            };
        }

        /// <summary>
        /// 画面全体の配色と部品の外観をまとめて調整する。
        /// </summary>
        private void InitializeTheme()
        {
            BackColor = Color.FromArgb(5, 8, 14);
            ForeColor = Color.FromArgb(240, 245, 252);
            Font = new Font("Yu Gothic UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            DoubleBuffered = true;
            Icon = UiIconFactory.CreateSettingsIcon();

            titleLabel.Font = new Font("Yu Gothic UI Semibold", 20F, FontStyle.Regular, GraphicsUnit.Point);
            subtitleLabel.Font = new Font("Yu Gothic UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            headerBadgeLabel.Font = new Font("Yu Gothic UI Semibold", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            registeredTitleLabel.Font = new Font("Yu Gothic UI Semibold", 13F, FontStyle.Regular, GraphicsUnit.Point);
            editorTitleLabel.Font = new Font("Yu Gothic UI Semibold", 13F, FontStyle.Regular, GraphicsUnit.Point);
            detectedTitleLabel.Font = new Font("Yu Gothic UI Semibold", 13F, FontStyle.Regular, GraphicsUnit.Point);
            statusLabel.Font = new Font("Yu Gothic UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            foreach (var label in new[]
            {
                titleLabel,
                subtitleLabel,
                headerBadgeLabel,
                registeredTitleLabel,
                editorTitleLabel,
                detectedTitleLabel,
                statusLabel
            })
            {
                label.UseCompatibleTextRendering = true;
            }

            headerCard.BackColor = Color.FromArgb(11, 17, 29);
            registeredCard.BackColor = Color.FromArgb(11, 17, 29);
            editorCard.BackColor = Color.FromArgb(11, 17, 29);
            detectedCard.BackColor = Color.FromArgb(11, 17, 29);

            headerCard.Paint += (_, e) => PaintCardChrome(headerCard, e.Graphics, Color.FromArgb(60, 165, 255));
            registeredCard.Paint += (_, e) => PaintCardChrome(registeredCard, e.Graphics, Color.FromArgb(90, 132, 255));
            editorCard.Paint += (_, e) => PaintCardChrome(editorCard, e.Graphics, Color.FromArgb(47, 204, 196));
            detectedCard.Paint += (_, e) => PaintCardChrome(detectedCard, e.Graphics, Color.FromArgb(255, 194, 88));

            headerBadgeLabel.BackColor = Color.FromArgb(18, 28, 44);
            headerBadgeLabel.ForeColor = Color.FromArgb(194, 214, 240);

            registeredTitleLabel.ForeColor = Color.FromArgb(232, 238, 246);
            editorTitleLabel.ForeColor = Color.FromArgb(232, 238, 246);
            detectedTitleLabel.ForeColor = Color.FromArgb(232, 238, 246);

            statusLabel.ForeColor = Color.FromArgb(232, 238, 246);
            statusLabel.BackColor = Color.FromArgb(14, 19, 30);
            statusLabel.Padding = new Padding(10, 8, 10, 8);

            foreach (var textBox in new[] { cameraIdTextBox, displayNameTextBox, hostAddressTextBox, rtspUrlTextBox })
            {
                textBox.BackColor = Color.FromArgb(16, 24, 38);
                textBox.ForeColor = Color.White;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }

            enabledCheckBox.ForeColor = Color.FromArgb(240, 245, 252);

            StyleCommandButton(buttonNew, Color.FromArgb(21, 28, 40), Color.FromArgb(174, 188, 206));
            StyleCommandButton(buttonSave, Color.FromArgb(20, 35, 60), Color.FromArgb(117, 197, 255));
            StyleCommandButton(buttonDelete, Color.FromArgb(66, 44, 14), Color.FromArgb(255, 194, 88));
            StyleCommandButton(buttonCheck, Color.FromArgb(68, 23, 31), Color.FromArgb(255, 106, 126));
            StyleCommandButton(buttonDetect, Color.FromArgb(20, 35, 60), Color.FromArgb(117, 197, 255));
            StyleCommandButton(buttonLoadDetected, Color.FromArgb(21, 28, 40), Color.FromArgb(174, 188, 206));
            StyleCommandButton(buttonClose, Color.FromArgb(21, 28, 40), Color.FromArgb(174, 188, 206));

            foreach (var button in new[] { buttonNew, buttonSave, buttonDelete, buttonCheck, buttonDetect, buttonLoadDetected, buttonClose })
            {
                button.AutoSize = true;
                button.MinimumSize = new Size(120, 40);
                button.Padding = new Padding(12, 0, 12, 0);
            }

            foreach (var listBox in new[] { registeredListBox, detectedListBox })
            {
                listBox.BackColor = Color.FromArgb(16, 24, 38);
                listBox.ForeColor = Color.White;
            }
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
        /// ボタンの共通スタイルを設定する。
        /// </summary>
        /// <param name="button">対象ボタン。</param>
        /// <param name="backColor">背景色。</param>
        /// <param name="accentColor">枠線色。</param>
        private static void StyleCommandButton(Button button, Color backColor, Color accentColor)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = backColor;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = accentColor;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor);
            button.UseVisualStyleBackColor = false;
        }

        /// <summary>
        /// カードの上部にアクセントと境界線を描画する。
        /// </summary>
        /// <param name="control">描画対象。</param>
        /// <param name="graphics">描画先。</param>
        /// <param name="accentColor">アクセント色。</param>
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

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref uint pvAttribute, int cbAttribute);

        private const int DwmWindowAttributeUseImmersiveDarkMode = 20;
        private const int DwmWindowAttributeCaptionColor = 35;
        private const int DwmWindowAttributeTextColor = 36;
        private const int DwmWindowAttributeBorderColor = 34;

        /// <summary>
        /// 登録済み一覧表示用のラッパー。
        /// </summary>
        /// <param name="ConfigItem">設定本体。</param>
        private sealed record RegisteredCameraListItem(NetworkCameraConfigItem ConfigItem)
        {
            /// <summary>
            /// 一覧表示用の文字列を返す。
            /// </summary>
            /// <returns>表示文字列。</returns>
            public override string ToString()
            {
                var enabledText = ConfigItem.Enabled ? "有効" : "無効";
                return $"{ConfigItem.DisplayName} [{enabledText}]";
            }
        }

        /// <summary>
        /// 自動検出一覧表示用のラッパー。
        /// </summary>
        /// <param name="DetectedItem">検出候補本体。</param>
        private sealed record DetectedCameraListItem(DetectedNetworkCameraItem DetectedItem)
        {
            /// <summary>
            /// 一覧表示用の文字列を返す。
            /// </summary>
            /// <returns>表示文字列。</returns>
            public override string ToString()
            {
                return $"{DetectedItem.DisplayName} [{DetectedItem.HostAddress}]";
            }
        }
    }
}
