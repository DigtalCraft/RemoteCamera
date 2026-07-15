using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;

namespace RemoteCamera
{
    /// <summary>
    /// スマホから確認できる監視ページを提供する Web サーバー。
    /// </summary>
    internal sealed class RemoteMonitorServer : IAsyncDisposable
    {
        private const string AssetFolderName = "MonitorPage";

        private readonly CameraService cameraService;
        private readonly CameraDeviceCatalog cameraDeviceCatalog = new();
        private readonly NetworkCameraCatalog networkCameraCatalog = new();
        private readonly AudioBroadcastService audioBroadcastService = new();
        private readonly int port;
        private readonly object syncRoot = new();
        private readonly SemaphoreSlim restartGate = new(1, 1);
        private readonly string monitorHtmlTemplate;
        private readonly string monitorCssText;
        private readonly string monitorJsText;

        private WebApplication? app;
        private bool disposed;
        private string statusText = "未起動";
        private string? listeningTailscaleAddress;
        private CancellationTokenSource? tailscaleWatchCts;
        private Task? tailscaleWatchTask;

        /// <summary>
        /// サーバーを初期化する。
        /// </summary>
        /// <param name="cameraService">プレビューと録画を担当するサービス。</param>
        /// <param name="port">待ち受けポート。</param>
        public RemoteMonitorServer(CameraService cameraService, int port)
        {
            this.cameraService = cameraService;
            this.port = port;
            monitorHtmlTemplate = LoadAssetText("monitor.html");
            monitorCssText = LoadAssetText("monitor.css");
            monitorJsText = LoadAssetText("monitor.js");
        }

        /// <summary>
        /// ローカル確認用の URL を返す。
        /// </summary>
        public string LocalUrl => $"http://localhost:{port}/";

        /// <summary>
        /// Tailscale の IPv4 アドレスから組み立てた URL を返す。
        /// </summary>
        public string? TailscaleUrl
        {
            get
            {
                var address = NetworkHelper.TryGetTailscaleIpv4Address();
                if (string.IsNullOrWhiteSpace(address))
                {
                    return null;
                }

                return $"http://{address}:{port}/";
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
        /// サーバーが起動済みかどうかを返す。
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (syncRoot)
                {
                    return app is not null && !disposed;
                }
            }
        }

        /// <summary>
        /// ローカルと Tailscale の URL を見やすい文字列で返す。
        /// </summary>
        public string GetAccessibleUrlText()
        {
            var urls = new List<string> { LocalUrl };
            var tailscaleUrl = TailscaleUrl;

            if (!string.IsNullOrWhiteSpace(tailscaleUrl))
            {
                urls.Add(tailscaleUrl);
            }

            return string.Join(" / ", urls);
        }

        /// <summary>
        /// 監視 Web サーバーを起動する。
        /// </summary>
        public async Task StartAsync()
        {
            ThrowIfDisposed();

            lock (syncRoot)
            {
                if (app is not null)
                {
                    return;
                }

                statusText = "監視ページを起動しています。";
            }

            var tailscaleAddress = NetworkHelper.TryGetTailscaleIpv4Address();
            await StartWebApplicationAsync(tailscaleAddress);
            StartTailscaleWatchLoop();
            audioBroadcastService.Start();
        }

        /// <summary>
        /// 監視ページの待ち受け先をローカル端末と Tailscale に限定する。
        /// </summary>
        /// <param name="options">Kestrel の設定。</param>
        /// <param name="listenPort">待ち受けポート。</param>
        /// <param name="tailscaleAddress">Tailscale の IPv4 アドレス。</param>
        private static void ConfigureListenEndpoints(KestrelServerOptions options, int listenPort, string? tailscaleAddress)
        {
            options.Listen(IPAddress.Loopback, listenPort);

            if (IPAddress.TryParse(tailscaleAddress, out var parsedAddress))
            {
                options.Listen(parsedAddress, listenPort);
            }
        }

        /// <summary>
        /// 監視 Web サーバーを構築して起動する。
        /// </summary>
        /// <param name="tailscaleAddress">Tailscale の IPv4 アドレス。</param>
        private async Task StartWebApplicationAsync(string? tailscaleAddress)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options =>
            {
                ConfigureListenEndpoints(options, port, tailscaleAddress);
            });

            var webApp = BuildWebApplication(builder);

            try
            {
                await webApp.StartAsync();
            }
            catch
            {
                await webApp.DisposeAsync();
                lock (syncRoot)
                {
                    statusText = "監視ページの起動に失敗しました。";
                }

                throw;
            }

            lock (syncRoot)
            {
                app = webApp;
                listeningTailscaleAddress = tailscaleAddress;
                statusText = tailscaleAddress is null
                    ? "監視ページを起動しました。Tailscale アドレスを待機しています。"
                    : $"監視ページを起動しました。Tailscale: {tailscaleAddress}";
            }
        }

        /// <summary>
        /// 監視ページのルートと API を登録する。
        /// </summary>
        /// <param name="builder">WebApplication のビルダー。</param>
        /// <returns>構築済みの WebApplication。</returns>
        private WebApplication BuildWebApplication(WebApplicationBuilder builder)
        {
            var webApp = builder.Build();
            webApp.UseWebSockets();

            webApp.MapGet("/", async context =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
                await context.Response.WriteAsync(RenderMonitorPage());
            });

            webApp.MapGet("/monitor.css", async context =>
            {
                context.Response.ContentType = "text/css; charset=utf-8";
                context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
                await context.Response.WriteAsync(monitorCssText);
            });

            webApp.MapGet("/monitor.js", async context =>
            {
                context.Response.ContentType = "text/javascript; charset=utf-8";
                context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
                await context.Response.WriteAsync(monitorJsText);
            });

            webApp.MapGet("/site.webmanifest", () => Results.Json(new
            {
                name = "RemoteCamera Monitor",
                short_name = "Monitor",
                description = "RemoteCamera のスマホ監視ページ",
                start_url = "/",
                scope = "/",
                display = "standalone",
                background_color = "#0b1220",
                theme_color = "#10223a",
                icons = new[]
                {
                    new
                    {
                        src = "/web-icon-192.png",
                        sizes = "192x192",
                        type = "image/png",
                        purpose = "any maskable"
                    },
                    new
                    {
                        src = "/web-icon-512.png",
                        sizes = "512x512",
                        type = "image/png",
                        purpose = "any maskable"
                    }
                }
            }));

            webApp.MapGet("/web-icon-180.png", () => CreateBrowserIconResult(180));
            webApp.MapGet("/web-icon-192.png", () => CreateBrowserIconResult(192));
            webApp.MapGet("/web-icon-512.png", () => CreateBrowserIconResult(512));

            webApp.MapGet("/status", () => Results.Json(CreateStatus()));
            webApp.MapGet("/devices", () => Results.Json(CreateCameraDevicesResponse()));
            webApp.MapGet("/network-cameras", () => Results.Json(CreateNetworkCameraResponse()));
            webApp.MapGet("/network-camera-configs", () => Results.Json(CreateNetworkCameraConfigResponse()));

            webApp.MapPost("/camera/select", async (CameraSelectRequest request) =>
            {
                var devices = cameraDeviceCatalog.GetCameraDevices();
                var selectedDevice = devices.FirstOrDefault(device => device.CaptureIndex == request.CaptureIndex);
                if (selectedDevice is null)
                {
                    return Results.BadRequest(new
                    {
                        message = "指定されたカメラが見つかりません。",
                        status = CreateStatus()
                    });
                }

                try
                {
                    await cameraService.InitializeAsync(selectedDevice);
                    return Results.Json(CreateStatus());
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new
                    {
                        message = ex.Message,
                        status = CreateStatus()
                    });
                }
            });

            webApp.MapPost("/network-camera/select", async (NetworkCameraSelectRequest request) =>
            {
                var networkCameras = networkCameraCatalog.GetNetworkCameras();
                var selectedCamera = networkCameras.FirstOrDefault(camera => camera.CameraId == request.CameraId);
                if (selectedCamera is null)
                {
                    return Results.BadRequest(new
                    {
                        message = "指定されたネットワークカメラが見つかりません。",
                        status = CreateStatus()
                    });
                }

                try
                {
                    await cameraService.InitializeAsync(selectedCamera);
                    return Results.Json(CreateStatus());
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new
                    {
                        message = ex.Message,
                        status = CreateStatus()
                    });
                }
            });

            webApp.MapPost("/network-camera-config/save", (NetworkCameraConfigSaveRequest request) =>
            {
                try
                {
                    networkCameraCatalog.SaveConfigItem(new NetworkCameraConfigItem
                    {
                        Enabled = request.Enabled,
                        CameraId = request.CameraId,
                        DisplayName = request.DisplayName,
                        HostAddress = request.HostAddress,
                        RtspUrl = request.RtspUrl
                    });

                    return Results.Json(new
                    {
                        message = "設定を保存しました。",
                        configs = CreateNetworkCameraConfigResponse()
                    });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new
                    {
                        message = ex.Message,
                        configs = CreateNetworkCameraConfigResponse()
                    });
                }
            });

            webApp.MapPost("/network-camera-config/delete", (NetworkCameraConfigDeleteRequest request) =>
            {
                networkCameraCatalog.DeleteConfigItem(request.CameraId);
                return Results.Json(new
                {
                    message = "設定を削除しました。",
                    configs = CreateNetworkCameraConfigResponse()
                });
            });

            webApp.MapPost("/network-camera-config/check", async (NetworkCameraConfigSaveRequest request) =>
            {
                var result = await networkCameraCatalog.CheckConnectionAsync(new NetworkCameraConfigItem
                {
                    Enabled = request.Enabled,
                    CameraId = request.CameraId,
                    DisplayName = request.DisplayName,
                    HostAddress = request.HostAddress,
                    RtspUrl = request.RtspUrl
                });

                return Results.Json(new
                {
                    result.CameraId,
                    result.IsSuccess,
                    result.StatusText,
                    checkedAt = result.CheckedAt
                });
            });

            webApp.MapPost("/network-camera-config/detect", async () =>
            {
                var detectedItems = await networkCameraCatalog.DetectNetworkCamerasAsync();
                return Results.Json(new NetworkCameraDetectResponse(
                    detectedItems
                        .Select(item => new NetworkCameraDetectItem(
                            item.CameraId,
                            item.DisplayName,
                            item.HostAddress,
                            item.RtspUrl,
                            item.StatusText))
                        .ToArray()));
            });

            webApp.MapPost("/record/start", async () =>
            {
                try
                {
                    await cameraService.StartRecordingAsync();
                    return Results.Json(CreateStatus());
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new
                    {
                        message = ex.Message,
                        status = CreateStatus()
                    });
                }
            });

            webApp.MapPost("/record/stop", async () =>
            {
                try
                {
                    await cameraService.StopRecordingAsync();
                    return Results.Json(CreateStatus());
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new
                    {
                        message = ex.Message,
                        status = CreateStatus()
                    });
                }
            });

            webApp.MapPost("/preview/toggle", () =>
            {
                cameraService.TogglePreviewEnabled();
                return Results.Json(CreateStatus());
            });

            webApp.Map("/audio/ws", async context =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                await audioBroadcastService.HandleWebSocketAsync(socket, context.RequestAborted);
            });

            webApp.MapGet("/snapshot.jpg", async context =>
            {
                context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
                context.Response.Headers.Pragma = "no-cache";

                var bytes = cameraService.GetLatestSnapshotJpeg();
                if (bytes is null)
                {
                    context.Response.StatusCode = StatusCodes.Status204NoContent;
                    return;
                }

                context.Response.ContentType = "image/jpeg";
                context.Response.ContentLength = bytes.Length;
                await context.Response.Body.WriteAsync(bytes);
            });

            webApp.MapGet("/favicon.ico", () => CreateBrowserIconResult(64));

            return webApp;
        }

        /// <summary>
        /// Tailscale の変化を監視し、必要なら待ち受けを張り直す。
        /// </summary>
        private void StartTailscaleWatchLoop()
        {
            lock (syncRoot)
            {
                if (disposed || tailscaleWatchTask is not null)
                {
                    return;
                }

                tailscaleWatchCts = new CancellationTokenSource();
                tailscaleWatchTask = WatchTailscaleAddressAsync(tailscaleWatchCts.Token);
            }
        }

        /// <summary>
        /// Tailscale の IPv4 変化を定期確認する。
        /// </summary>
        /// <param name="cancellationToken">停止要求。</param>
        private async Task WatchTailscaleAddressAsync(CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    if (disposed)
                    {
                        return;
                    }

                    var currentAddress = NetworkHelper.TryGetTailscaleIpv4Address();
                    if (string.Equals(currentAddress, listeningTailscaleAddress, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    await RestartWebApplicationAsync(currentAddress, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 終了処理ではそのまま抜ける。
            }
            catch (Exception ex)
            {
                lock (syncRoot)
                {
                    statusText = $"Tailscale の待ち受け確認に失敗しました。{ex.Message}";
                }
            }
        }

        /// <summary>
        /// Tailscale の変化に合わせて監視 Web サーバーを張り直す。
        /// </summary>
        /// <param name="tailscaleAddress">現在の Tailscale IPv4 アドレス。</param>
        /// <param name="cancellationToken">停止要求。</param>
        private async Task RestartWebApplicationAsync(string? tailscaleAddress, CancellationToken cancellationToken)
        {
            await restartGate.WaitAsync(cancellationToken);
            try
            {
                if (disposed)
                {
                    return;
                }

                WebApplication? currentApp;
                lock (syncRoot)
                {
                    if (disposed || string.Equals(listeningTailscaleAddress, tailscaleAddress, StringComparison.Ordinal))
                    {
                        return;
                    }

                    currentApp = app;
                    app = null;
                    listeningTailscaleAddress = null;
                    statusText = tailscaleAddress is null
                        ? "Tailscale を待機中です。"
                        : $"Tailscale を検出しました。再接続しています。{tailscaleAddress}";
                }

                if (currentApp is not null)
                {
                    try
                    {
                        await currentApp.StopAsync(cancellationToken);
                    }
                    catch
                    {
                        // 張り直しを優先する。
                    }

                    await currentApp.DisposeAsync();
                }

                await StartWebApplicationAsync(tailscaleAddress);
                audioBroadcastService.Start();
            }
            finally
            {
                restartGate.Release();
            }
        }

        /// <summary>
        /// 監視 Web サーバーを停止する。
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            WebApplication? webApp;

            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                webApp = app;
                app = null;
            }

            CancellationTokenSource? watchCts;
            Task? watchTask;
            lock (syncRoot)
            {
                watchCts = tailscaleWatchCts;
                watchTask = tailscaleWatchTask;
                tailscaleWatchCts = null;
                tailscaleWatchTask = null;
            }

            if (watchCts is not null)
            {
                watchCts.Cancel();
                watchCts.Dispose();
            }

            if (watchTask is not null)
            {
                try
                {
                    await watchTask;
                }
                catch
                {
                    // 終了時は続行する。
                }
            }

            if (webApp is not null)
            {
                try
                {
                    await webApp.StopAsync();
                }
                catch
                {
                    // 終了時は続行する。
                }

                await webApp.DisposeAsync();
            }

            audioBroadcastService.Dispose();

            lock (syncRoot)
            {
                statusText = "停止";
            }

            restartGate.Dispose();
        }

        /// <summary>
        /// 監視ページの現在状態を生成する。
        /// </summary>
        private MonitorStatus CreateStatus()
        {
            return new MonitorStatus(
                cameraService.StatusText,
                cameraService.IsRecording,
                cameraService.RecordingPath,
                LocalUrl,
                TailscaleUrl,
                cameraService.IsReady,
                cameraService.SelectedDeviceName,
                cameraService.SelectedSourceType?.ToString(),
                cameraService.SelectedCaptureIndex,
                cameraService.SelectedNetworkCameraId,
                cameraService.FrameVersion,
                cameraService.IsPreviewEnabled,
                cameraService.RecordingTargetPath,
                CameraService.DefaultRecordingDirectory,
                audioBroadcastService.IsRunning,
                audioBroadcastService.StatusText,
                audioBroadcastService.SampleRate);
        }

        /// <summary>
        /// カメラ一覧と現在選択を返す。
        /// </summary>
        private CameraDevicesResponse CreateCameraDevicesResponse()
        {
            var devices = cameraDeviceCatalog
                .GetCameraDevices()
                .Select(device => new CameraDeviceItem(device.DisplayName, device.CaptureIndex))
                .ToArray();

            return new CameraDevicesResponse(
                devices,
                cameraService.SelectedCaptureIndex,
                cameraService.SelectedDeviceName);
        }

        /// <summary>
        /// ネットワークカメラ一覧と現在選択を返す。
        /// </summary>
        private NetworkCameraResponse CreateNetworkCameraResponse()
        {
            var networkCameras = networkCameraCatalog
                .GetNetworkCameras()
                .Select(camera => new NetworkCameraItem(camera.CameraId, camera.DisplayName, camera.HostAddress))
                .ToArray();

            return new NetworkCameraResponse(
                networkCameras,
                cameraService.SelectedNetworkCameraId,
                cameraService.SelectedDeviceName);
        }

        /// <summary>
        /// ネットワークカメラ設定一覧を返す。
        /// </summary>
        /// <returns>設定一覧応答。</returns>
        private NetworkCameraConfigResponse CreateNetworkCameraConfigResponse()
        {
            var items = networkCameraCatalog
                .GetConfigItems()
                .Select(item => new NetworkCameraConfigItemResponse(
                    item.Enabled,
                    item.CameraId ?? string.Empty,
                    item.DisplayName ?? string.Empty,
                    item.HostAddress ?? string.Empty,
                    item.RtspUrl ?? string.Empty))
                .ToArray();

            return new NetworkCameraConfigResponse(
                networkCameraCatalog.GetConfigFilePath(),
                items);
        }

        /// <summary>
        /// 監視ページの HTML を生成する。
        /// </summary>
        private string RenderMonitorPage()
        {
            var localUrl = WebUtility.HtmlEncode(LocalUrl);
            var tailscaleUrl = WebUtility.HtmlEncode(TailscaleUrl ?? "未検出");

            return monitorHtmlTemplate
                .Replace("__LOCAL_URL__", localUrl, StringComparison.Ordinal)
                .Replace("__TAILSCALE_URL__", tailscaleUrl, StringComparison.Ordinal);
        }

        /// <summary>
        /// 監視ページの静的ファイルを読み込む。
        /// </summary>
        /// <param name="fileName">ファイル名。</param>
        /// <returns>読み込んだテキスト。</returns>
        private static string LoadAssetText(string fileName)
        {
            var path = Path.Combine(AppContext.BaseDirectory, AssetFolderName, fileName);
            return File.ReadAllText(path, Encoding.UTF8);
        }

        /// <summary>
        /// ブラウザのホーム画面用アイコンを PNG で返す。
        /// </summary>
        /// <param name="size">生成する画像サイズ。</param>
        /// <returns>PNG 画像のレスポンス。</returns>
        private static IResult CreateBrowserIconResult(int size)
        {
            using var bitmap = CreateBrowserIconBitmap(size);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);

            return Results.File(stream.ToArray(), "image/png");
        }

        /// <summary>
        /// スマホのホーム画面で見分けやすいブラウザ専用アイコンを作成する。
        /// </summary>
        /// <param name="size">画像サイズ。</param>
        /// <returns>生成したビットマップ。</returns>
        private static Bitmap CreateBrowserIconBitmap(int size)
        {
            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            var scale = size / 512f;
            graphics.ScaleTransform(scale, scale);
            DrawBrowserIcon(graphics);

            return bitmap;
        }

        /// <summary>
        /// ブラウザ監視ページ用のアイコンを描画する。
        /// </summary>
        /// <param name="graphics">描画先。</param>
        private static void DrawBrowserIcon(Graphics graphics)
        {
            var backgroundRect = new RectangleF(24, 24, 464, 464);
            using var backgroundPath = CreateRoundedRectPath(backgroundRect, 112f);
            using var backgroundBrush = new LinearGradientBrush(
                backgroundRect,
                Color.FromArgb(255, 16, 34, 58),
                Color.FromArgb(255, 5, 12, 24),
                45f);
            using var borderPen = new Pen(Color.FromArgb(210, 107, 211, 255), 8f);
            graphics.FillPath(backgroundBrush, backgroundPath);
            graphics.DrawPath(borderPen, backgroundPath);

            var phoneRect = new RectangleF(148, 82, 216, 348);
            using var phonePath = CreateRoundedRectPath(phoneRect, 42f);
            using var phoneBrush = new SolidBrush(Color.FromArgb(255, 226, 241, 255));
            using var phoneShadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
            using var phoneShadowPath = CreateRoundedRectPath(new RectangleF(158, 94, 216, 348), 42f);
            graphics.FillPath(phoneShadowBrush, phoneShadowPath);
            graphics.FillPath(phoneBrush, phonePath);

            var screenRect = new RectangleF(170, 120, 172, 260);
            using var screenPath = CreateRoundedRectPath(screenRect, 26f);
            using var screenBrush = new LinearGradientBrush(
                screenRect,
                Color.FromArgb(255, 12, 25, 43),
                Color.FromArgb(255, 24, 70, 98),
                90f);
            graphics.FillPath(screenBrush, screenPath);

            using var mountBrush = new SolidBrush(Color.FromArgb(255, 107, 211, 255));
            using var cameraBodyBrush = new SolidBrush(Color.FromArgb(255, 45, 62, 88));
            using var lensOuterBrush = new SolidBrush(Color.FromArgb(255, 132, 223, 255));
            using var lensInnerBrush = new SolidBrush(Color.FromArgb(255, 8, 17, 31));
            using var alertBrush = new SolidBrush(Color.FromArgb(255, 255, 95, 112));

            graphics.FillRectangle(mountBrush, 218, 170, 76, 18);
            using var cameraBodyPath = CreateRoundedRectPath(new RectangleF(198, 184, 116, 74), 24f);
            graphics.FillPath(cameraBodyBrush, cameraBodyPath);
            graphics.FillEllipse(lensOuterBrush, 224, 194, 64, 64);
            graphics.FillEllipse(lensInnerBrush, 238, 208, 36, 36);
            graphics.FillEllipse(alertBrush, 282, 162, 28, 28);

            using var wavePen = new Pen(Color.FromArgb(190, 107, 211, 255), 10f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawArc(wavePen, 122, 136, 112, 168, 128, 96);
            graphics.DrawArc(wavePen, 278, 136, 112, 168, -44, 96);

            using var homeBrush = new SolidBrush(Color.FromArgb(255, 132, 147, 168));
            graphics.FillEllipse(homeBrush, 240, 396, 32, 32);
        }

        /// <summary>
        /// 丸みのある矩形パスを作成する。
        /// </summary>
        /// <param name="rect">対象の矩形。</param>
        /// <param name="radius">角丸の半径。</param>
        /// <returns>作成したパス。</returns>
        private static GraphicsPath CreateRoundedRectPath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2f;

            if (diameter <= 0f)
            {
                path.AddRectangle(rect);
                path.CloseFigure();
                return path;
            }

            var arc = new RectangleF(rect.Location, new SizeF(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();

            return path;
        }

        /// <summary>
        /// 破棄済みかどうかを確認する。
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(RemoteMonitorServer));
            }
        }

        /// <summary>
        /// 例外を明示するための状態取得用レコード。
        /// </summary>
        private sealed record MonitorStatus(
            string CameraStatus,
            bool Recording,
            string? RecordingPath,
            string LocalUrl,
            string? TailscaleUrl,
            bool CameraReady,
            string? CameraName,
            string? CameraSourceType,
            int? CameraCaptureIndex,
            string? NetworkCameraId,
            int FrameVersion,
            bool PreviewEnabled,
            string? RecordingTargetPath,
            string DefaultRecordingDirectory,
            bool AudioRunning,
            string AudioStatus,
            int AudioSampleRate);

        /// <summary>
        /// スマホ側からのカメラ選択リクエスト。
        /// </summary>
        /// <param name="CaptureIndex">切替対象のカメラ番号。</param>
        private sealed record CameraSelectRequest(int CaptureIndex);

        /// <summary>
        /// スマホ側からのネットワークカメラ選択リクエスト。
        /// </summary>
        /// <param name="CameraId">切替対象のカメラ識別子。</param>
        private sealed record NetworkCameraSelectRequest(string CameraId);

        /// <summary>
        /// ネットワークカメラ設定の保存要求。
        /// </summary>
        /// <param name="Enabled">有効フラグ。</param>
        /// <param name="CameraId">カメラ識別子。</param>
        /// <param name="DisplayName">表示名。</param>
        /// <param name="HostAddress">ホスト名または IP アドレス。</param>
        /// <param name="RtspUrl">RTSP URL。</param>
        private sealed record NetworkCameraConfigSaveRequest(
            bool Enabled,
            string? CameraId,
            string? DisplayName,
            string? HostAddress,
            string? RtspUrl);

        /// <summary>
        /// ネットワークカメラ設定の削除要求。
        /// </summary>
        /// <param name="CameraId">削除対象の識別子。</param>
        private sealed record NetworkCameraConfigDeleteRequest(string CameraId);

        /// <summary>
        /// カメラ一覧応答。
        /// </summary>
        /// <param name="Devices">候補一覧。</param>
        /// <param name="CurrentCaptureIndex">現在選択中の番号。</param>
        /// <param name="CurrentDeviceName">現在の表示名。</param>
        private sealed record CameraDevicesResponse(
            CameraDeviceItem[] Devices,
            int? CurrentCaptureIndex,
            string? CurrentDeviceName);

        /// <summary>
        /// ネットワークカメラ一覧応答。
        /// </summary>
        /// <param name="Cameras">候補一覧。</param>
        /// <param name="CurrentCameraId">現在選択中の識別子。</param>
        /// <param name="CurrentCameraName">現在の表示名。</param>
        private sealed record NetworkCameraResponse(
            NetworkCameraItem[] Cameras,
            string? CurrentCameraId,
            string? CurrentCameraName);

        /// <summary>
        /// ネットワークカメラ設定一覧応答。
        /// </summary>
        /// <param name="ConfigPath">設定ファイルの保存先。</param>
        /// <param name="Items">設定一覧。</param>
        private sealed record NetworkCameraConfigResponse(
            string ConfigPath,
            NetworkCameraConfigItemResponse[] Items);

        /// <summary>
        /// ネットワークカメラ設定 1 件分。
        /// </summary>
        /// <param name="Enabled">有効フラグ。</param>
        /// <param name="CameraId">カメラ識別子。</param>
        /// <param name="DisplayName">表示名。</param>
        /// <param name="HostAddress">ホスト名または IP アドレス。</param>
        /// <param name="RtspUrl">RTSP URL。</param>
        private sealed record NetworkCameraConfigItemResponse(
            bool Enabled,
            string CameraId,
            string DisplayName,
            string HostAddress,
            string RtspUrl);

        /// <summary>
        /// カメラ候補1件分。
        /// </summary>
        /// <param name="DisplayName">表示名。</param>
        /// <param name="CaptureIndex">カメラ番号。</param>
        private sealed record CameraDeviceItem(string DisplayName, int CaptureIndex);

        /// <summary>
        /// ネットワークカメラ候補1件分。
        /// </summary>
        /// <param name="CameraId">識別子。</param>
        /// <param name="DisplayName">表示名。</param>
        /// <param name="HostAddress">ホスト名または IP アドレス。</param>
        private sealed record NetworkCameraItem(string CameraId, string DisplayName, string HostAddress);

        /// <summary>
        /// 自動検出応答。
        /// </summary>
        /// <param name="Items">検出結果一覧。</param>
        private sealed record NetworkCameraDetectResponse(NetworkCameraDetectItem[] Items);

        /// <summary>
        /// 自動検出 1 件分。
        /// </summary>
        /// <param name="CameraId">候補識別子。</param>
        /// <param name="DisplayName">表示名。</param>
        /// <param name="HostAddress">ホスト名または IP アドレス。</param>
        /// <param name="RtspUrl">RTSP URL 候補。</param>
        /// <param name="StatusText">補足メッセージ。</param>
        private sealed record NetworkCameraDetectItem(
            string CameraId,
            string DisplayName,
            string HostAddress,
            string RtspUrl,
            string StatusText);
    }
}
