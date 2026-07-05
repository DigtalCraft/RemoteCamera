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
        private readonly NetworkCameraCatalog networkCameraCatalog = new();
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
