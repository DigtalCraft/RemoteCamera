using OpenCvSharp;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using CvSize = OpenCvSharp.Size;

namespace RemoteCamera
{
    /// <summary>
    /// カメラのプレビュー取得と MP4 録画を担当するサービス。
    /// </summary>
    internal sealed class CameraService : IAsyncDisposable
    {
        private const string DefaultRecordingDirectoryPath = @"C:\RemoteCamera";

        private readonly object syncRoot = new();

        private VideoCapture? capture;
        private VideoWriter? writer;
        private CancellationTokenSource? captureCts;
        private Task? captureLoopTask;
        private Bitmap? latestFrame;
        private string statusText = "未起動";
        private string? selectedDeviceName;
        private int? selectedCaptureIndex;
        private string? selectedNetworkCameraId;
        private string? selectedNetworkCameraUrl;
        private string? recordingPath;
        private string? recordingTargetPath;
        private bool isRecording;
        private bool previewEnabled = true;
        private bool disposed;
        private int frameVersion;
        private CvSize recordingFrameSize;
        private CameraSourceType? selectedSourceType;

        /// <summary>
        /// カメラが利用可能な状態かどうかを返す。
        /// </summary>
        public bool IsReady
        {
            get
            {
                lock (syncRoot)
                {
                    return capture is not null && !disposed;
                }
            }
        }

        /// <summary>
        /// 録画中かどうかを返す。
        /// </summary>
        public bool IsRecording
        {
            get
            {
                lock (syncRoot)
                {
                    return isRecording;
                }
            }
        }

        /// <summary>
        /// プレビュー更新が有効かどうかを返す。
        /// </summary>
        public bool IsPreviewEnabled
        {
            get
            {
                lock (syncRoot)
                {
                    return previewEnabled;
                }
            }
        }

        /// <summary>
        /// 現在の状態メッセージを返す。
        /// </summary>
        public string StatusText
        {
            get
            {
                lock (syncRoot)
                {
                    return statusText;
                }
            }
        }

        /// <summary>
        /// 選択されているカメラ名を返す。
        /// </summary>
        public string? SelectedDeviceName
        {
            get
            {
                lock (syncRoot)
                {
                    return selectedDeviceName;
                }
            }
        }

        /// <summary>
        /// 選択されているカメラ番号を返す。
        /// </summary>
        public int? SelectedCaptureIndex
        {
            get
            {
                lock (syncRoot)
                {
                    return selectedCaptureIndex;
                }
            }
        }

        /// <summary>
        /// 選択されているネットワークカメラ識別子を返す。
        /// </summary>
        public string? SelectedNetworkCameraId
        {
            get
            {
                lock (syncRoot)
                {
                    return selectedNetworkCameraId;
                }
            }
        }

        /// <summary>
        /// 現在選択中の入力元種別を返す。
        /// </summary>
        public CameraSourceType? SelectedSourceType
        {
            get
            {
                lock (syncRoot)
                {
                    return selectedSourceType;
                }
            }
        }

        /// <summary>
        /// 最新フレームの更新番号を返す。
        /// </summary>
        public int FrameVersion
        {
            get
            {
                lock (syncRoot)
                {
                    return frameVersion;
                }
            }
        }

        /// <summary>
        /// 現在の録画先パスを返す。
        /// </summary>
        public string? RecordingPath
        {
            get
            {
                lock (syncRoot)
                {
                    return recordingPath;
                }
            }
        }

        /// <summary>
        /// 録画先として設定されているファイルパスを返す。
        /// </summary>
        public string? RecordingTargetPath
        {
            get
            {
                lock (syncRoot)
                {
                    return recordingTargetPath;
                }
            }
        }

        /// <summary>
        /// 既定の録画フォルダーを返す。
        /// </summary>
        public static string DefaultRecordingDirectory => DefaultRecordingDirectoryPath;

        /// <summary>
        /// カメラを初期化して、プレビュー取得を開始する。
        /// </summary>
        /// <param name="deviceOption">利用するカメラ候補。未指定の場合は最初に開けるカメラを使う。</param>
        public async Task InitializeAsync(CameraDeviceOption? deviceOption = null)
        {
            ThrowIfDisposed();

            int? currentCaptureIndex;
            lock (syncRoot)
            {
                currentCaptureIndex = selectedCaptureIndex;
                if (isRecording)
                {
                    throw new InvalidOperationException("録画中はカメラを切り替えられません。先に録画を停止してください。");
                }

                statusText = "カメラを検索しています。";
            }

            if (deviceOption is not null && currentCaptureIndex == deviceOption.CaptureIndex && IsReady)
            {
                lock (syncRoot)
                {
                    statusText = BuildStatusText();
                }

                return;
            }

            var (openedCapture, selectedDevice) = await Task.Run(() => OpenCapture(deviceOption));

            try
            {
                await ActivateOpenedCameraAsync(
                    openedCapture,
                    selectedDevice.SourceType,
                    selectedDevice.DisplayName,
                    selectedDevice.CaptureIndex,
                    null,
                    null);
            }
            catch
            {
                openedCapture.Release();
                openedCapture.Dispose();

                lock (syncRoot)
                {
                    statusText = BuildStatusText();
                }

                throw;
            }
        }

        /// <summary>
        /// RTSP ネットワークカメラを初期化して、プレビュー取得を開始する。
        /// </summary>
        /// <param name="cameraOption">利用するネットワークカメラ候補。</param>
        public async Task InitializeAsync(NetworkCameraOption cameraOption)
        {
            ThrowIfDisposed();

            string? currentCameraId;
            lock (syncRoot)
            {
                currentCameraId = selectedNetworkCameraId;
                if (isRecording)
                {
                    throw new InvalidOperationException("録画中はカメラを切り替えられません。先に録画を停止してください。");
                }

                statusText = "ネットワークカメラへ接続しています。";
            }

            if (currentCameraId == cameraOption.CameraId && selectedSourceType == CameraSourceType.NetworkRtsp && IsReady)
            {
                lock (syncRoot)
                {
                    statusText = BuildStatusText();
                }

                return;
            }

            var openedCapture = await Task.Run(() => OpenNetworkCapture(cameraOption));

            try
            {
                await ActivateOpenedCameraAsync(
                    openedCapture,
                    CameraSourceType.NetworkRtsp,
                    cameraOption.DisplayName,
                    null,
                    cameraOption.CameraId,
                    cameraOption.RtspUrl);
            }
            catch
            {
                openedCapture.Release();
                openedCapture.Dispose();

                lock (syncRoot)
                {
                    statusText = BuildStatusText();
                }

                throw;
            }
        }

        /// <summary>
        /// MP4 録画を開始する。
        /// </summary>
        /// <param name="targetPath">録画ファイルの保存先。未指定の場合は既定フォルダーを使う。</param>
        public Task StartRecordingAsync(string? targetPath = null)
        {
            ThrowIfDisposed();

            VideoCapture? currentCapture;
            string resolvedTargetPath;
            lock (syncRoot)
            {
                currentCapture = capture;
                if (isRecording)
                {
                    throw new InvalidOperationException("すでに録画中です。");
                }
            }

            if (currentCapture is null)
            {
                throw new InvalidOperationException("カメラが初期化されていません。");
            }

            resolvedTargetPath = ResolveRecordingTargetPath(targetPath);

            var directoryPath = Path.GetDirectoryName(resolvedTargetPath);
            var fileName = Path.GetFileName(resolvedTargetPath);
            if (string.IsNullOrWhiteSpace(directoryPath) || string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidOperationException("録画先のパスが正しくありません。");
            }

            Directory.CreateDirectory(directoryPath);

            var size = GetRecordingFrameSize();
            var videoWriter = new VideoWriter(resolvedTargetPath, VideoWriter.FourCC('m', 'p', '4', 'v'), 30, size);
            if (!videoWriter.IsOpened())
            {
                videoWriter.Dispose();
                throw new InvalidOperationException("MP4 の録画を開始できませんでした。");
            }

            lock (syncRoot)
            {
                writer = videoWriter;
                isRecording = true;
                recordingPath = resolvedTargetPath;
                statusText = BuildStatusText();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 録画を停止する。
        /// </summary>
        public Task StopRecordingAsync()
        {
            ThrowIfDisposed();

            lock (syncRoot)
            {
                if (!isRecording)
                {
                    return Task.CompletedTask;
                }

                writer?.Dispose();
                writer = null;
                isRecording = false;
                statusText = BuildStatusText();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 録画先のファイルパスを設定する。
        /// </summary>
        /// <param name="targetPath">録画先ファイルパス。</param>
        public void SetRecordingTargetPath(string? targetPath)
        {
            lock (syncRoot)
            {
                recordingTargetPath = string.IsNullOrWhiteSpace(targetPath)
                    ? null
                    : targetPath.Trim();
                statusText = BuildStatusText();
            }
        }

        /// <summary>
        /// プレビュー更新を有効/無効に切り替える。
        /// </summary>
        public void TogglePreviewEnabled()
        {
            lock (syncRoot)
            {
                previewEnabled = !previewEnabled;
                statusText = BuildStatusText();
            }
        }

        /// <summary>
        /// プレビュー更新の有効/無効を明示的に設定する。
        /// </summary>
        /// <param name="enabled">有効なら true。</param>
        public void SetPreviewEnabled(bool enabled)
        {
            lock (syncRoot)
            {
                previewEnabled = enabled;
                statusText = BuildStatusText();
            }
        }

        /// <summary>
        /// 最新のプレビュー画像を取得する。
        /// </summary>
        /// <param name="frame">取得した画像。取得できない場合は null。</param>
        /// <param name="version">フレームの更新番号。</param>
        /// <returns>画像がある場合は true。</returns>
        public bool TryGetLatestFrameSnapshot(out Bitmap? frame, out int version)
        {
            lock (syncRoot)
            {
                version = frameVersion;

                if (latestFrame is null)
                {
                    frame = null;
                    return false;
                }

                frame = (Bitmap)latestFrame.Clone();
                return true;
            }
        }

        /// <summary>
        /// 最新フレームを JPEG で返す。
        /// </summary>
        /// <returns>JPEG バイト列。画像がない場合は null。</returns>
        public byte[]? GetLatestSnapshotJpeg()
        {
            if (!TryGetLatestFrameSnapshot(out var frame, out _))
            {
                return null;
            }

            using var snapshot = frame!;
            using (var stream = new MemoryStream())
            {
                snapshot.Save(stream, ImageFormat.Jpeg);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// サービスを終了して、カメラと録画のリソースを解放する。
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            Task? loopTask;
            CancellationTokenSource? cts;
            VideoWriter? currentWriter;
            VideoCapture? currentCapture;

            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                loopTask = captureLoopTask;
                cts = captureCts;
                currentWriter = writer;
                currentCapture = capture;
                captureLoopTask = null;
                captureCts = null;
                writer = null;
                capture = null;
                selectedDeviceName = null;
                selectedCaptureIndex = null;
                selectedNetworkCameraId = null;
                selectedNetworkCameraUrl = null;
                selectedSourceType = null;
                isRecording = false;
            }

            try
            {
                cts?.Cancel();
            }
            catch
            {
                // 終了時は続行する。
            }

            if (loopTask is not null)
            {
                try
                {
                    await loopTask;
                }
                catch
                {
                    // 終了時は続行する。
                }
            }

            currentWriter?.Dispose();
            currentCapture?.Release();
            currentCapture?.Dispose();

            lock (syncRoot)
            {
                latestFrame?.Dispose();
                latestFrame = null;
                frameVersion = 0;
                statusText = "終了";
            }
        }

        /// <summary>
        /// 開けたカメラへ切り替えて、古いカメラを後から解放する。
        /// </summary>
        /// <param name="openedCapture">新しく開いたカメラ。</param>
        /// <param name="selectedDevice">反映するカメラ情報。</param>
        private async Task ActivateOpenedCameraAsync(
            VideoCapture openedCapture,
            CameraSourceType sourceType,
            string displayName,
            int? captureIndex,
            string? networkCameraId,
            string? networkCameraUrl)
        {
            Task? previousLoopTask;
            CancellationTokenSource? previousCts;
            VideoWriter? previousWriter;
            VideoCapture? previousCapture;
            Bitmap? previousFrame;

            lock (syncRoot)
            {
                previousLoopTask = captureLoopTask;
                previousCts = captureCts;
                previousWriter = writer;
                previousCapture = capture;
                previousFrame = latestFrame;

                capture = openedCapture;
                selectedDeviceName = displayName;
                selectedCaptureIndex = captureIndex;
                selectedNetworkCameraId = networkCameraId;
                selectedNetworkCameraUrl = networkCameraUrl;
                selectedSourceType = sourceType;
                previewEnabled = true;
                recordingFrameSize = new CvSize((int)openedCapture.FrameWidth, (int)openedCapture.FrameHeight);
                latestFrame = null;
                frameVersion = 0;
                statusText = BuildStatusText();
                captureCts = new CancellationTokenSource();
                captureLoopTask = Task.Run(() => CaptureLoopAsync(openedCapture, captureCts.Token));
            }

            try
            {
                previousCts?.Cancel();
            }
            catch
            {
                // 切替時は後続の解放を優先する。
            }

            if (previousLoopTask is not null)
            {
                try
                {
                    await previousLoopTask;
                }
                catch
                {
                    // 切替時は後続の解放を優先する。
                }
            }

            previousWriter?.Dispose();
            previousCapture?.Release();
            previousCapture?.Dispose();
            previousFrame?.Dispose();
        }

        /// <summary>
        /// フレーム到着時に最新画像を更新する。
        /// </summary>
        private async Task CaptureLoopAsync(VideoCapture camera, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    using var frame = new Mat();
                    var readSucceeded = camera.Read(frame);

                    if (!readSucceeded || frame.Empty())
                    {
                        await Task.Delay(30, token);
                        continue;
                    }

                    var previewFrame = ConvertMatToBitmap(frame);

                    lock (syncRoot)
                    {
                        if (disposed)
                        {
                            previewFrame.Dispose();
                            return;
                        }

                        if (previewEnabled)
                        {
                            latestFrame?.Dispose();
                            latestFrame = previewFrame;
                            frameVersion++;
                        }
                        else
                        {
                            previewFrame.Dispose();
                        }

                        recordingFrameSize = new CvSize(Math.Max(1, frame.Width), Math.Max(1, frame.Height));

                        if (writer is not null && isRecording)
                        {
                            writer.Write(frame);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常終了。
            }
            catch (Exception ex)
            {
                lock (syncRoot)
                {
                    statusText = $"カメラの取得に失敗しました。{ex.Message}";
                }
            }
        }

        /// <summary>
        /// OpenCV の Mat を GDI+ Bitmap に変換する。
        /// </summary>
        private static Bitmap ConvertMatToBitmap(Mat frame)
        {
            if (!Cv2.ImEncode(".bmp", frame, out var encoded))
            {
                throw new InvalidOperationException("プレビュー画像を作成できませんでした。");
            }

            using var stream = new MemoryStream(encoded);
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }

        /// <summary>
        /// 実際に録画へ使うファイルパスを解決する。
        /// </summary>
        /// <param name="targetPath">録画先ファイルパス。</param>
        /// <returns>録画に使うファイルパス。</returns>
        private static string ResolveRecordingTargetPath(string? targetPath)
        {
            var resolvedTargetPath = targetPath?.Trim();
            if (string.IsNullOrWhiteSpace(resolvedTargetPath))
            {
                return Path.Combine(DefaultRecordingDirectoryPath, BuildDefaultRecordingFileName());
            }

            return resolvedTargetPath;
        }

        /// <summary>
        /// 既定の録画ファイル名を作る。
        /// </summary>
        /// <returns>録画ファイル名。</returns>
        private static string BuildDefaultRecordingFileName()
        {
            return $"camera_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        }

        /// <summary>
        /// 録画に使うフレームサイズを返す。
        /// </summary>
        private CvSize GetRecordingFrameSize()
        {
            lock (syncRoot)
            {
                if (recordingFrameSize.Width > 1 && recordingFrameSize.Height > 1)
                {
                    return recordingFrameSize;
                }

                if (latestFrame is not null)
                {
                    return new CvSize(Math.Max(1, latestFrame.Width), Math.Max(1, latestFrame.Height));
                }

                return new CvSize(1280, 720);
            }
        }

        /// <summary>
        /// 利用可能なカメラを探して最初に開けたものを返す。
        /// </summary>
        private static (VideoCapture Capture, CameraDeviceOption Device) OpenCapture(CameraDeviceOption? deviceOption)
        {
            if (deviceOption is not null)
            {
                var selectedCamera = new VideoCapture(deviceOption.CaptureIndex, VideoCaptureAPIs.DSHOW);
                if (selectedCamera.IsOpened())
                {
                    return (selectedCamera, deviceOption);
                }

                selectedCamera.Release();
                selectedCamera.Dispose();
                throw new InvalidOperationException($"{deviceOption.DisplayName} を開けませんでした。別のカメラを選択してください。");
            }

            return OpenFirstAvailableCapture();
        }

        /// <summary>
        /// 指定された RTSP ネットワークカメラを開く。
        /// </summary>
        /// <param name="cameraOption">対象カメラ。</param>
        /// <returns>OpenCV のカメラオブジェクト。</returns>
        private static VideoCapture OpenNetworkCapture(NetworkCameraOption cameraOption)
        {
            var capture = new VideoCapture(cameraOption.RtspUrl, VideoCaptureAPIs.FFMPEG);
            if (capture.IsOpened())
            {
                return capture;
            }

            capture.Release();
            capture.Dispose();
            throw new InvalidOperationException($"{cameraOption.DisplayName} の RTSP 接続に失敗しました。URL と認証情報を確認してください。");
        }

        /// <summary>
        /// 利用可能なカメラを探して最初に開けたものを返す。
        /// </summary>
        private static (VideoCapture Capture, CameraDeviceOption Device) OpenFirstAvailableCapture()
        {
            for (var index = 0; index < 10; index++)
            {
                var camera = new VideoCapture(index, VideoCaptureAPIs.DSHOW);

                if (camera.IsOpened())
                {
                    return (camera, new CameraDeviceOption($"カメラ {index}", index, CameraSourceType.DeviceIndex));
                }

                camera.Release();
                camera.Dispose();
            }

            throw new InvalidOperationException("利用できるカメラが見つかりません。Windows 側でカメラとして認識されているか確認してください。");
        }

        /// <summary>
        /// 現在利用中のカメラを解放する。
        /// </summary>
        private async Task ReleaseCurrentCameraAsync()
        {
            Task? loopTask;
            CancellationTokenSource? cts;
            VideoWriter? currentWriter;
            VideoCapture? currentCapture;
            Bitmap? currentFrame;

            lock (syncRoot)
            {
                loopTask = captureLoopTask;
                cts = captureCts;
                currentWriter = writer;
                currentCapture = capture;
                currentFrame = latestFrame;

                captureLoopTask = null;
                captureCts = null;
                writer = null;
                capture = null;
                latestFrame = null;
                selectedDeviceName = null;
                selectedCaptureIndex = null;
                selectedNetworkCameraId = null;
                selectedNetworkCameraUrl = null;
                selectedSourceType = null;
                frameVersion = 0;
                isRecording = false;
            }

            try
            {
                cts?.Cancel();
            }
            catch
            {
                // 切替時は後続の解放を優先する。
            }

            if (loopTask is not null)
            {
                try
                {
                    await loopTask;
                }
                catch
                {
                    // 切替時は後続の解放を優先する。
                }
            }

            currentWriter?.Dispose();
            currentCapture?.Release();
            currentCapture?.Dispose();
            currentFrame?.Dispose();
        }

        /// <summary>
        /// 破棄済みかどうかを確認する。
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(CameraService));
            }
        }

        /// <summary>
        /// 現在の状態に応じた表示用テキストを作る。
        /// </summary>
        private string BuildStatusText()
        {
            if (disposed)
            {
                return "終了";
            }

            if (capture is null)
            {
                return "未起動";
            }

            var deviceName = selectedDeviceName ?? "カメラ";
            var sourceLabel = selectedSourceType == CameraSourceType.NetworkRtsp ? "RTSP" : "ローカル";
            var baseText = isRecording && !string.IsNullOrWhiteSpace(recordingPath)
                ? $"録画中: {Path.GetFileName(recordingPath)} / {deviceName} ({sourceLabel})"
                : $"{deviceName} ({sourceLabel}) を使用中";

            return previewEnabled
                ? baseText
                : $"{baseText} / プレビュー停止中";
        }
    }
}
