using System.Net;
using System.Net.WebSockets;
using System.Text;
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
        private readonly AudioBroadcastService audioBroadcastService = new();
        private readonly int port;
        private readonly object syncRoot = new();
        private readonly string monitorHtmlTemplate;
        private readonly string monitorCssText;

        private WebApplication? app;
        private bool disposed;
        private string statusText = "未起動";

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

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options =>
            {
                ConfigureListenEndpoints(options, port);
            });

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

            webApp.MapGet("/status", () => Results.Json(CreateStatus()));
            webApp.MapGet("/devices", () => Results.Json(CreateCameraDevicesResponse()));

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

            webApp.MapGet("/favicon.ico", () => Results.NoContent());

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
                statusText = "監視ページを起動しました。";
            }

            audioBroadcastService.Start();
        }

        /// <summary>
        /// 監視ページの待ち受け先をローカル端末と Tailscale に限定する。
        /// </summary>
        /// <param name="options">Kestrel の設定。</param>
        /// <param name="listenPort">待ち受けポート。</param>
        private static void ConfigureListenEndpoints(KestrelServerOptions options, int listenPort)
        {
            options.Listen(IPAddress.Loopback, listenPort);

            var tailscaleAddress = NetworkHelper.TryGetTailscaleIpv4Address();
            if (IPAddress.TryParse(tailscaleAddress, out var parsedAddress))
            {
                options.Listen(parsedAddress, listenPort);
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
                cameraService.SelectedCaptureIndex,
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
            int? CameraCaptureIndex,
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
        /// カメラ候補1件分。
        /// </summary>
        /// <param name="DisplayName">表示名。</param>
        /// <param name="CaptureIndex">カメラ番号。</param>
        private sealed record CameraDeviceItem(string DisplayName, int CaptureIndex);
    }
}
