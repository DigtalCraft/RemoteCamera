namespace RemoteCamera
{
    partial class frmRemoteCamera
    {
        /// <summary>
        ///  必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        private TableLayoutPanel rootLayout = null!;
        private Panel heroPanel = null!;
        private TableLayoutPanel heroLayout = null!;
        private PictureBox heroIconBox = null!;
        private TableLayoutPanel heroTextLayout = null!;
        private Label titleLabel = null!;
        private Label subtitleLabel = null!;
        private Label endpointLabel = null!;
        private Label heroStatusLabel = null!;
        private FlowLayoutPanel commandBar = null!;
        private ComboBox cameraDeviceComboBox = null!;
        private Button buttonApplyCamera = null!;
        private Button buttonRefreshDevices = null!;
        private Button buttonSelectPath = null!;
        private Button buttonStartRecord = null!;
        private Button buttonStopRecord = null!;
        private Button buttonStopPreview = null!;
        private Button buttonExit = null!;
        private SplitContainer contentSplit = null!;
        private Panel infoCard = null!;
        private TableLayoutPanel infoLayout = null!;
        private Label infoHeaderLabel = null!;
        private Label statusTitleLabel = null!;
        private Label statusValueLabel = null!;
        private Label cameraTitleLabel = null!;
        private Label cameraValueLabel = null!;
        private Label recordingPathTitleLabel = null!;
        private Label recordingPathValueLabel = null!;
        private Label localUrlTitleLabel = null!;
        private Label localUrlValueLabel = null!;
        private Label tailscaleUrlTitleLabel = null!;
        private Label tailscaleUrlValueLabel = null!;
        private Panel previewCard = null!;
        private TableLayoutPanel previewHeaderLayout = null!;
        private Label previewHeaderLabel = null!;
        private Label previewStateLabel = null!;
        private Panel previewSurface = null!;
        private PictureBox previewBox = null!;
        private Label previewPlaceholderLabel = null!;
        private ToolTip toolTipMain = null!;
        private NotifyIcon trayIcon = null!;
        private ContextMenuStrip trayMenu = null!;
        private ToolStripMenuItem trayOpenMenuItem = null!;
        private ToolStripMenuItem trayExitMenuItem = null!;

        /// <summary>
        ///  使用中のリソースを破棄します。
        /// </summary>
        /// <param name="disposing">管理リソースを破棄する場合は true。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  デザイナー サポートに必要なメソッドです。
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            rootLayout = new TableLayoutPanel();
            heroPanel = new Panel();
            heroLayout = new TableLayoutPanel();
            heroIconBox = new PictureBox();
            heroTextLayout = new TableLayoutPanel();
            titleLabel = new Label();
            subtitleLabel = new Label();
            endpointLabel = new Label();
            heroStatusLabel = new Label();
            commandBar = new FlowLayoutPanel();
            cameraDeviceComboBox = new ComboBox();
            buttonApplyCamera = new Button();
            buttonRefreshDevices = new Button();
            buttonSelectPath = new Button();
            buttonStartRecord = new Button();
            buttonStopRecord = new Button();
            buttonStopPreview = new Button();
            buttonExit = new Button();
            contentSplit = new SplitContainer();
            infoCard = new Panel();
            infoLayout = new TableLayoutPanel();
            infoHeaderLabel = new Label();
            statusTitleLabel = new Label();
            statusValueLabel = new Label();
            cameraTitleLabel = new Label();
            cameraValueLabel = new Label();
            recordingPathTitleLabel = new Label();
            recordingPathValueLabel = new Label();
            localUrlTitleLabel = new Label();
            localUrlValueLabel = new Label();
            tailscaleUrlTitleLabel = new Label();
            tailscaleUrlValueLabel = new Label();
            previewCard = new Panel();
            previewHeaderLayout = new TableLayoutPanel();
            previewHeaderLabel = new Label();
            previewStateLabel = new Label();
            previewSurface = new Panel();
            previewBox = new PictureBox();
            previewPlaceholderLabel = new Label();
            toolTipMain = new ToolTip(components);
            trayIcon = new NotifyIcon(components);
            trayMenu = new ContextMenuStrip(components);
            trayOpenMenuItem = new ToolStripMenuItem();
            trayExitMenuItem = new ToolStripMenuItem();
            rootLayout.SuspendLayout();
            heroPanel.SuspendLayout();
            heroLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)heroIconBox).BeginInit();
            heroTextLayout.SuspendLayout();
            commandBar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)contentSplit).BeginInit();
            contentSplit.Panel1.SuspendLayout();
            contentSplit.Panel2.SuspendLayout();
            contentSplit.SuspendLayout();
            infoCard.SuspendLayout();
            infoLayout.SuspendLayout();
            previewCard.SuspendLayout();
            previewHeaderLayout.SuspendLayout();
            previewSurface.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)previewBox).BeginInit();
            trayMenu.SuspendLayout();
            SuspendLayout();
            // 
            // rootLayout
            // 
            rootLayout.ColumnCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.Controls.Add(heroPanel, 0, 0);
            rootLayout.Controls.Add(commandBar, 0, 1);
            rootLayout.Controls.Add(contentSplit, 0, 2);
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.Location = new Point(0, 0);
            rootLayout.Name = "rootLayout";
            rootLayout.Padding = new Padding(14);
            rootLayout.RowCount = 3;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.Size = new Size(1360, 860);
            rootLayout.TabIndex = 0;
            // 
            // heroPanel
            // 
            heroPanel.Controls.Add(heroLayout);
            heroPanel.Dock = DockStyle.Fill;
            heroPanel.Location = new Point(21, 21);
            heroPanel.Name = "heroPanel";
            heroPanel.Padding = new Padding(14);
            heroPanel.Size = new Size(1318, 142);
            heroPanel.TabIndex = 0;
            // 
            // heroLayout
            // 
            heroLayout.ColumnCount = 3;
            heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84F));
            heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            heroLayout.Controls.Add(heroIconBox, 0, 0);
            heroLayout.Controls.Add(heroTextLayout, 1, 0);
            heroLayout.Controls.Add(heroStatusLabel, 2, 0);
            heroLayout.Dock = DockStyle.Fill;
            heroLayout.Location = new Point(18, 18);
            heroLayout.Name = "heroLayout";
            heroLayout.RowCount = 1;
            heroLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            heroLayout.Size = new Size(1282, 106);
            heroLayout.TabIndex = 0;
            // 
            // heroIconBox
            // 
            heroIconBox.Dock = DockStyle.Fill;
            heroIconBox.Location = new Point(3, 3);
            heroIconBox.Name = "heroIconBox";
            heroIconBox.Size = new Size(78, 76);
            heroIconBox.SizeMode = PictureBoxSizeMode.Zoom;
            heroIconBox.TabIndex = 0;
            heroIconBox.TabStop = false;
            // 
            // heroTextLayout
            // 
            heroTextLayout.ColumnCount = 1;
            heroTextLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            heroTextLayout.Controls.Add(titleLabel, 0, 0);
            heroTextLayout.Controls.Add(subtitleLabel, 0, 1);
            heroTextLayout.Controls.Add(endpointLabel, 0, 2);
            heroTextLayout.Dock = DockStyle.Fill;
            heroTextLayout.Location = new Point(87, 3);
            heroTextLayout.Name = "heroTextLayout";
            heroTextLayout.RowCount = 3;
            heroTextLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            heroTextLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            heroTextLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            heroTextLayout.Size = new Size(1067, 102);
            heroTextLayout.TabIndex = 1;
            // 
            // titleLabel
            // 
            titleLabel.AutoSize = false;
            titleLabel.Dock = DockStyle.Fill;
            titleLabel.Location = new Point(3, 0);
            titleLabel.Name = "titleLabel";
            titleLabel.Size = new Size(1061, 42);
            titleLabel.TabIndex = 0;
            titleLabel.Text = "RemoteCamera";
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // subtitleLabel
            // 
            subtitleLabel.AutoEllipsis = true;
            subtitleLabel.Dock = DockStyle.Fill;
            subtitleLabel.Location = new Point(3, 34);
            subtitleLabel.Name = "subtitleLabel";
            subtitleLabel.Size = new Size(1061, 32);
            subtitleLabel.TabIndex = 1;
            subtitleLabel.Text = "USBカメラを常駐操作しながら、Tailscale 経由でスマホ監視できるようにした画面です。";
            subtitleLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // endpointLabel
            // 
            endpointLabel.AutoEllipsis = true;
            endpointLabel.Dock = DockStyle.Fill;
            endpointLabel.Location = new Point(3, 64);
            endpointLabel.Name = "endpointLabel";
            endpointLabel.Size = new Size(1061, 28);
            endpointLabel.TabIndex = 2;
            endpointLabel.Text = "http://localhost:8765/";
            endpointLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // heroStatusLabel
            // 
            heroStatusLabel.AutoSize = true;
            heroStatusLabel.Dock = DockStyle.Right;
            heroStatusLabel.Location = new Point(1160, 24);
            heroStatusLabel.Margin = new Padding(12, 24, 0, 24);
            heroStatusLabel.Name = "heroStatusLabel";
            heroStatusLabel.Padding = new Padding(16, 10, 16, 10);
            heroStatusLabel.Size = new Size(119, 34);
            heroStatusLabel.TabIndex = 2;
            heroStatusLabel.Text = "起動中";
            heroStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // commandBar
            // 
            commandBar.AutoSize = true;
            commandBar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            commandBar.Dock = DockStyle.Fill;
            commandBar.Location = new Point(18, 145);
            commandBar.Margin = new Padding(0, 4, 0, 8);
            commandBar.Name = "commandBar";
            commandBar.Padding = new Padding(0, 2, 0, 2);
            commandBar.Size = new Size(1324, 56);
            commandBar.TabIndex = 1;
            commandBar.WrapContents = true;
            // 
            // cameraDeviceComboBox
            // 
            cameraDeviceComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            cameraDeviceComboBox.FormattingEnabled = true;
            cameraDeviceComboBox.Location = new Point(5, 11);
            cameraDeviceComboBox.Margin = new Padding(3, 7, 10, 3);
            cameraDeviceComboBox.Name = "cameraDeviceComboBox";
            cameraDeviceComboBox.Size = new Size(260, 23);
            cameraDeviceComboBox.TabIndex = 0;
            // 
            // buttonApplyCamera
            // 
            buttonApplyCamera.AutoSize = true;
            buttonApplyCamera.FlatAppearance.BorderSize = 1;
            buttonApplyCamera.FlatStyle = FlatStyle.Flat;
            buttonApplyCamera.Location = new Point(278, 7);
            buttonApplyCamera.Margin = new Padding(3, 3, 10, 3);
            buttonApplyCamera.MinimumSize = new Size(120, 42);
            buttonApplyCamera.Name = "buttonApplyCamera";
            buttonApplyCamera.Padding = new Padding(12, 0, 12, 0);
            buttonApplyCamera.Size = new Size(120, 42);
            buttonApplyCamera.TabIndex = 1;
            buttonApplyCamera.Text = "カメラ変更";
            buttonApplyCamera.UseVisualStyleBackColor = false;
            // 
            // buttonRefreshDevices
            // 
            buttonRefreshDevices.AutoSize = true;
            buttonRefreshDevices.FlatAppearance.BorderSize = 1;
            buttonRefreshDevices.FlatStyle = FlatStyle.Flat;
            buttonRefreshDevices.Location = new Point(411, 7);
            buttonRefreshDevices.Margin = new Padding(3, 3, 10, 3);
            buttonRefreshDevices.MinimumSize = new Size(92, 42);
            buttonRefreshDevices.Name = "buttonRefreshDevices";
            buttonRefreshDevices.Padding = new Padding(12, 0, 12, 0);
            buttonRefreshDevices.Size = new Size(92, 42);
            buttonRefreshDevices.TabIndex = 2;
            buttonRefreshDevices.Text = "再読込";
            buttonRefreshDevices.UseVisualStyleBackColor = false;
            // 
            // buttonSelectPath
            // 
            buttonSelectPath.AutoSize = true;
            buttonSelectPath.FlatAppearance.BorderSize = 1;
            buttonSelectPath.FlatStyle = FlatStyle.Flat;
            buttonSelectPath.Location = new Point(516, 7);
            buttonSelectPath.Margin = new Padding(3, 3, 10, 3);
            buttonSelectPath.MinimumSize = new Size(142, 42);
            buttonSelectPath.Name = "buttonSelectPath";
            buttonSelectPath.Padding = new Padding(12, 0, 12, 0);
            buttonSelectPath.Size = new Size(142, 42);
            buttonSelectPath.TabIndex = 3;
            buttonSelectPath.Text = "新規選択";
            buttonSelectPath.TextImageRelation = TextImageRelation.ImageBeforeText;
            buttonSelectPath.UseVisualStyleBackColor = false;
            // 
            // buttonStartRecord
            // 
            buttonStartRecord.AutoSize = true;
            buttonStartRecord.FlatAppearance.BorderSize = 1;
            buttonStartRecord.FlatStyle = FlatStyle.Flat;
            buttonStartRecord.Location = new Point(679, 7);
            buttonStartRecord.Margin = new Padding(3, 3, 10, 3);
            buttonStartRecord.MinimumSize = new Size(120, 42);
            buttonStartRecord.Name = "buttonStartRecord";
            buttonStartRecord.Padding = new Padding(12, 0, 12, 0);
            buttonStartRecord.Size = new Size(120, 42);
            buttonStartRecord.TabIndex = 4;
            buttonStartRecord.Text = "録画";
            buttonStartRecord.TextImageRelation = TextImageRelation.ImageBeforeText;
            buttonStartRecord.UseVisualStyleBackColor = false;
            // 
            // buttonStopRecord
            // 
            buttonStopRecord.AutoSize = true;
            buttonStopRecord.FlatAppearance.BorderSize = 1;
            buttonStopRecord.FlatStyle = FlatStyle.Flat;
            buttonStopRecord.Location = new Point(818, 7);
            buttonStopRecord.Margin = new Padding(3, 3, 10, 3);
            buttonStopRecord.MinimumSize = new Size(120, 42);
            buttonStopRecord.Name = "buttonStopRecord";
            buttonStopRecord.Padding = new Padding(12, 0, 12, 0);
            buttonStopRecord.Size = new Size(120, 42);
            buttonStopRecord.TabIndex = 5;
            buttonStopRecord.Text = "停止";
            buttonStopRecord.TextImageRelation = TextImageRelation.ImageBeforeText;
            buttonStopRecord.UseVisualStyleBackColor = false;
            // 
            // buttonStopPreview
            // 
            buttonStopPreview.AutoSize = true;
            buttonStopPreview.FlatAppearance.BorderSize = 1;
            buttonStopPreview.FlatStyle = FlatStyle.Flat;
            buttonStopPreview.Location = new Point(957, 7);
            buttonStopPreview.Margin = new Padding(3, 3, 10, 3);
            buttonStopPreview.MinimumSize = new Size(146, 42);
            buttonStopPreview.Name = "buttonStopPreview";
            buttonStopPreview.Padding = new Padding(12, 0, 12, 0);
            buttonStopPreview.Size = new Size(146, 42);
            buttonStopPreview.TabIndex = 6;
            buttonStopPreview.Text = "プレビュー停止";
            buttonStopPreview.TextImageRelation = TextImageRelation.ImageBeforeText;
            buttonStopPreview.UseVisualStyleBackColor = false;
            // 
            // buttonExit
            // 
            buttonExit.AutoSize = true;
            buttonExit.FlatAppearance.BorderSize = 1;
            buttonExit.FlatStyle = FlatStyle.Flat;
            buttonExit.Location = new Point(1120, 7);
            buttonExit.Margin = new Padding(3, 3, 10, 3);
            buttonExit.MinimumSize = new Size(120, 42);
            buttonExit.Name = "buttonExit";
            buttonExit.Padding = new Padding(12, 0, 12, 0);
            buttonExit.Size = new Size(120, 42);
            buttonExit.TabIndex = 7;
            buttonExit.Text = "終了";
            buttonExit.TextImageRelation = TextImageRelation.ImageBeforeText;
            buttonExit.UseVisualStyleBackColor = false;
            commandBar.Controls.AddRange(new Control[] { cameraDeviceComboBox, buttonApplyCamera, buttonRefreshDevices, buttonSelectPath, buttonStartRecord, buttonStopRecord, buttonStopPreview, buttonExit });
            // 
            // contentSplit
            // 
            contentSplit.Dock = DockStyle.Fill;
            contentSplit.Location = new Point(18, 211);
            contentSplit.Margin = new Padding(0);
            contentSplit.Name = "contentSplit";
            // 
            // contentSplit.Panel1
            // 
            contentSplit.Panel1.Controls.Add(infoCard);
            contentSplit.Panel1.Padding = new Padding(0, 0, 6, 0);
            // 
            // contentSplit.Panel2
            // 
            contentSplit.Panel2.Controls.Add(previewCard);
            contentSplit.Panel2.Padding = new Padding(6, 0, 0, 0);
            contentSplit.Size = new Size(1324, 631);
            contentSplit.SplitterDistance = 500;
            contentSplit.SplitterWidth = 10;
            contentSplit.TabIndex = 2;
            // 
            // infoCard
            // 
            infoCard.Controls.Add(infoLayout);
            infoCard.Dock = DockStyle.Fill;
            infoCard.Location = new Point(0, 0);
            infoCard.Name = "infoCard";
            infoCard.Padding = new Padding(14);
            infoCard.Size = new Size(492, 631);
            infoCard.TabIndex = 0;
            // 
            // infoLayout
            // 
            infoLayout.ColumnCount = 2;
            infoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            infoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            infoLayout.Controls.Add(infoHeaderLabel, 0, 0);
            infoLayout.Controls.Add(statusTitleLabel, 0, 1);
            infoLayout.Controls.Add(statusValueLabel, 1, 1);
            infoLayout.Controls.Add(cameraTitleLabel, 0, 2);
            infoLayout.Controls.Add(cameraValueLabel, 1, 2);
            infoLayout.Controls.Add(recordingPathTitleLabel, 0, 3);
            infoLayout.Controls.Add(recordingPathValueLabel, 1, 3);
            infoLayout.Controls.Add(localUrlTitleLabel, 0, 4);
            infoLayout.Controls.Add(localUrlValueLabel, 1, 4);
            infoLayout.Controls.Add(tailscaleUrlTitleLabel, 0, 5);
            infoLayout.Controls.Add(tailscaleUrlValueLabel, 1, 5);
            infoLayout.Dock = DockStyle.Fill;
            infoLayout.Location = new Point(18, 18);
            infoLayout.Name = "infoLayout";
            infoLayout.Padding = new Padding(2, 0, 2, 2);
            infoLayout.RowCount = 6;
            infoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            infoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            infoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            infoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
            infoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            infoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            infoLayout.Size = new Size(456, 595);
            infoLayout.TabIndex = 0;
            // 
            // infoHeaderLabel
            // 
            infoHeaderLabel.AutoSize = true;
            infoLayout.SetColumnSpan(infoHeaderLabel, 2);
            infoHeaderLabel.Dock = DockStyle.Fill;
            infoHeaderLabel.Location = new Point(7, 2);
            infoHeaderLabel.Name = "infoHeaderLabel";
            infoHeaderLabel.Size = new Size(442, 40);
            infoHeaderLabel.TabIndex = 0;
            infoHeaderLabel.Text = "監視情報";
            infoHeaderLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // statusTitleLabel
            // 
            statusTitleLabel.AutoSize = true;
            statusTitleLabel.Dock = DockStyle.Fill;
            statusTitleLabel.Location = new Point(7, 42);
            statusTitleLabel.Name = "statusTitleLabel";
            statusTitleLabel.Size = new Size(134, 60);
            statusTitleLabel.TabIndex = 1;
            statusTitleLabel.Text = "状態";
            statusTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // statusValueLabel
            // 
            statusValueLabel.AutoEllipsis = true;
            statusValueLabel.Dock = DockStyle.Fill;
            statusValueLabel.Location = new Point(147, 42);
            statusValueLabel.Name = "statusValueLabel";
            statusValueLabel.Size = new Size(302, 60);
            statusValueLabel.TabIndex = 2;
            statusValueLabel.Text = "未起動";
            statusValueLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // cameraTitleLabel
            // 
            cameraTitleLabel.AutoSize = true;
            cameraTitleLabel.Dock = DockStyle.Fill;
            cameraTitleLabel.Location = new Point(7, 102);
            cameraTitleLabel.Name = "cameraTitleLabel";
            cameraTitleLabel.Size = new Size(134, 60);
            cameraTitleLabel.TabIndex = 3;
            cameraTitleLabel.Text = "カメラ";
            cameraTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // cameraValueLabel
            // 
            cameraValueLabel.AutoEllipsis = true;
            cameraValueLabel.Dock = DockStyle.Fill;
            cameraValueLabel.Location = new Point(147, 102);
            cameraValueLabel.Name = "cameraValueLabel";
            cameraValueLabel.Size = new Size(302, 60);
            cameraValueLabel.TabIndex = 4;
            cameraValueLabel.Text = "未検出";
            cameraValueLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // recordingPathTitleLabel
            // 
            recordingPathTitleLabel.AutoSize = true;
            recordingPathTitleLabel.Dock = DockStyle.Fill;
            recordingPathTitleLabel.Location = new Point(7, 162);
            recordingPathTitleLabel.Name = "recordingPathTitleLabel";
            recordingPathTitleLabel.Size = new Size(134, 78);
            recordingPathTitleLabel.TabIndex = 5;
            recordingPathTitleLabel.Text = "録画パス";
            recordingPathTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // recordingPathValueLabel
            // 
            recordingPathValueLabel.AutoEllipsis = true;
            recordingPathValueLabel.Dock = DockStyle.Fill;
            recordingPathValueLabel.Location = new Point(147, 162);
            recordingPathValueLabel.Name = "recordingPathValueLabel";
            recordingPathValueLabel.Size = new Size(302, 78);
            recordingPathValueLabel.TabIndex = 6;
            recordingPathValueLabel.Text = "未選択";
            recordingPathValueLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // localUrlTitleLabel
            // 
            localUrlTitleLabel.AutoSize = true;
            localUrlTitleLabel.Dock = DockStyle.Fill;
            localUrlTitleLabel.Location = new Point(7, 240);
            localUrlTitleLabel.Name = "localUrlTitleLabel";
            localUrlTitleLabel.Size = new Size(134, 70);
            localUrlTitleLabel.TabIndex = 7;
            localUrlTitleLabel.Text = "ローカルURL";
            localUrlTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // localUrlValueLabel
            // 
            localUrlValueLabel.AutoEllipsis = true;
            localUrlValueLabel.Dock = DockStyle.Fill;
            localUrlValueLabel.Location = new Point(147, 240);
            localUrlValueLabel.Name = "localUrlValueLabel";
            localUrlValueLabel.Size = new Size(302, 70);
            localUrlValueLabel.TabIndex = 8;
            localUrlValueLabel.Text = "http://localhost:8765/";
            localUrlValueLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // tailscaleUrlTitleLabel
            // 
            tailscaleUrlTitleLabel.AutoSize = true;
            tailscaleUrlTitleLabel.Dock = DockStyle.Fill;
            tailscaleUrlTitleLabel.Location = new Point(7, 310);
            tailscaleUrlTitleLabel.Name = "tailscaleUrlTitleLabel";
            tailscaleUrlTitleLabel.Size = new Size(134, 281);
            tailscaleUrlTitleLabel.TabIndex = 9;
            tailscaleUrlTitleLabel.Text = "Tailscale";
            tailscaleUrlTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // tailscaleUrlValueLabel
            // 
            tailscaleUrlValueLabel.AutoEllipsis = true;
            tailscaleUrlValueLabel.Dock = DockStyle.Fill;
            tailscaleUrlValueLabel.Location = new Point(147, 310);
            tailscaleUrlValueLabel.Name = "tailscaleUrlValueLabel";
            tailscaleUrlValueLabel.Size = new Size(302, 281);
            tailscaleUrlValueLabel.TabIndex = 10;
            tailscaleUrlValueLabel.Text = "未検出";
            tailscaleUrlValueLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // previewCard
            // 
            previewCard.Controls.Add(previewSurface);
            previewCard.Controls.Add(previewHeaderLayout);
            previewCard.Dock = DockStyle.Fill;
            previewCard.Location = new Point(8, 0);
            previewCard.Name = "previewCard";
            previewCard.Padding = new Padding(14);
            previewCard.Size = new Size(806, 631);
            previewCard.TabIndex = 1;
            // 
            // previewHeaderLayout
            // 
            previewHeaderLayout.ColumnCount = 2;
            previewHeaderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            previewHeaderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            previewHeaderLayout.Controls.Add(previewHeaderLabel, 0, 0);
            previewHeaderLayout.Controls.Add(previewStateLabel, 1, 0);
            previewHeaderLayout.Dock = DockStyle.Top;
            previewHeaderLayout.Location = new Point(18, 18);
            previewHeaderLayout.Name = "previewHeaderLayout";
            previewHeaderLayout.RowCount = 1;
            previewHeaderLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            previewHeaderLayout.Size = new Size(778, 40);
            previewHeaderLayout.TabIndex = 0;
            // 
            // previewHeaderLabel
            // 
            previewHeaderLabel.AutoSize = true;
            previewHeaderLabel.Dock = DockStyle.Fill;
            previewHeaderLabel.Location = new Point(3, 0);
            previewHeaderLabel.Name = "previewHeaderLabel";
            previewHeaderLabel.Size = new Size(679, 44);
            previewHeaderLabel.TabIndex = 0;
            previewHeaderLabel.Text = "ライブプレビュー";
            previewHeaderLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // previewStateLabel
            // 
            previewStateLabel.AutoSize = true;
            previewStateLabel.Dock = DockStyle.Right;
            previewStateLabel.Location = new Point(700, 5);
            previewStateLabel.Margin = new Padding(8, 5, 0, 5);
            previewStateLabel.Name = "previewStateLabel";
            previewStateLabel.Padding = new Padding(14, 8, 14, 8);
            previewStateLabel.Size = new Size(67, 34);
            previewStateLabel.TabIndex = 1;
            previewStateLabel.Text = "LIVE";
            previewStateLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // previewSurface
            // 
            previewSurface.Controls.Add(previewPlaceholderLabel);
            previewSurface.Controls.Add(previewBox);
            previewSurface.Dock = DockStyle.Fill;
            previewSurface.Location = new Point(18, 62);
            previewSurface.Name = "previewSurface";
            previewSurface.Size = new Size(770, 551);
            previewSurface.TabIndex = 1;
            // 
            // previewBox
            // 
            previewBox.BackColor = Color.Black;
            previewBox.Dock = DockStyle.Fill;
            previewBox.Location = new Point(0, 0);
            previewBox.Name = "previewBox";
            previewBox.Size = new Size(770, 551);
            previewBox.SizeMode = PictureBoxSizeMode.Zoom;
            previewBox.TabIndex = 0;
            previewBox.TabStop = false;
            // 
            // previewPlaceholderLabel
            // 
            previewPlaceholderLabel.BackColor = Color.FromArgb(24, 28, 40);
            previewPlaceholderLabel.Dock = DockStyle.Fill;
            previewPlaceholderLabel.ForeColor = Color.FromArgb(200, 210, 228);
            previewPlaceholderLabel.Location = new Point(0, 0);
            previewPlaceholderLabel.Name = "previewPlaceholderLabel";
            previewPlaceholderLabel.Padding = new Padding(24);
            previewPlaceholderLabel.Size = new Size(770, 551);
            previewPlaceholderLabel.TabIndex = 1;
            previewPlaceholderLabel.Text = "映像を待っています。";
            previewPlaceholderLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // toolTipMain
            // 
            toolTipMain.AutomaticDelay = 120;
            toolTipMain.AutoPopDelay = 6000;
            toolTipMain.InitialDelay = 160;
            toolTipMain.ReshowDelay = 60;
            toolTipMain.ShowAlways = true;
            // 
            // trayIcon
            // 
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Text = "RemoteCamera";
            trayIcon.Visible = true;
            // 
            // trayMenu
            // 
            trayMenu.ImageScalingSize = new Size(24, 24);
            trayMenu.Items.AddRange(new ToolStripItem[] { trayOpenMenuItem, trayExitMenuItem });
            trayMenu.Name = "trayMenu";
            trayMenu.Size = new Size(107, 48);
            // 
            // trayOpenMenuItem
            // 
            trayOpenMenuItem.Name = "trayOpenMenuItem";
            trayOpenMenuItem.Size = new Size(106, 22);
            trayOpenMenuItem.Text = "開く";
            // 
            // trayExitMenuItem
            // 
            trayExitMenuItem.Name = "trayExitMenuItem";
            trayExitMenuItem.Size = new Size(106, 22);
            trayExitMenuItem.Text = "終了";
            // 
            // frmRemoteCamera
            // 
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1360, 860);
            Controls.Add(rootLayout);
            MinimumSize = new Size(980, 680);
            Name = "frmRemoteCamera";
            Text = "RemoteCamera";
            rootLayout.ResumeLayout(false);
            rootLayout.PerformLayout();
            heroPanel.ResumeLayout(false);
            heroLayout.ResumeLayout(false);
            heroLayout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)heroIconBox).EndInit();
            heroTextLayout.ResumeLayout(false);
            heroTextLayout.PerformLayout();
            commandBar.ResumeLayout(false);
            commandBar.PerformLayout();
            contentSplit.Panel1.ResumeLayout(false);
            contentSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)contentSplit).EndInit();
            contentSplit.ResumeLayout(false);
            infoCard.ResumeLayout(false);
            infoLayout.ResumeLayout(false);
            infoLayout.PerformLayout();
            previewCard.ResumeLayout(false);
            previewHeaderLayout.ResumeLayout(false);
            previewHeaderLayout.PerformLayout();
            previewSurface.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)previewBox).EndInit();
            trayMenu.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}
