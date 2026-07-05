using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace RemoteCamera
{
    /// <summary>
    /// 設定ファイルからネットワークカメラ候補を取得し、更新も行う。
    /// </summary>
    internal sealed class NetworkCameraCatalog
    {
        private const string ConfigFileName = "NetworkCameras.json";
        private const int DefaultRtspPort = 554;
        private static readonly int[] CommonRtspPorts = [554, 8554];

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private static readonly object FileSyncRoot = new();

        /// <summary>
        /// ネットワークカメラ候補を取得する。
        /// </summary>
        /// <returns>候補一覧。</returns>
        public IReadOnlyList<NetworkCameraOption> GetNetworkCameras()
        {
            try
            {
                return LoadConfigItems()
                    .Where(item => item.Enabled)
                    .Where(IsValidConfigItem)
                    .Select(ToOption)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<NetworkCameraOption>();
            }
        }

        /// <summary>
        /// 設定一覧を取得する。
        /// </summary>
        /// <returns>設定一覧。</returns>
        public IReadOnlyList<NetworkCameraConfigItem> GetConfigItems()
        {
            return LoadConfigItems();
        }

        /// <summary>
        /// 設定を追加または更新する。
        /// </summary>
        /// <param name="item">保存対象。</param>
        public void SaveConfigItem(NetworkCameraConfigItem item)
        {
            var normalizedItem = NormalizeConfigItem(item);
            if (!IsValidConfigItem(normalizedItem))
            {
                throw new InvalidOperationException("表示名と RTSP URL は必須です。");
            }

            var items = LoadConfigItems().ToList();
            var existingIndex = items.FindIndex(current => string.Equals(current.CameraId, normalizedItem.CameraId, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                items[existingIndex] = normalizedItem;
            }
            else
            {
                items.Add(normalizedItem);
            }

            SaveConfigItems(items);
        }

        /// <summary>
        /// 指定した設定を削除する。
        /// </summary>
        /// <param name="cameraId">削除対象の識別子。</param>
        public void DeleteConfigItem(string cameraId)
        {
            var normalizedCameraId = cameraId?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedCameraId))
            {
                return;
            }

            var items = LoadConfigItems()
                .Where(item => !string.Equals(item.CameraId, normalizedCameraId, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            SaveConfigItems(items);
        }

        /// <summary>
        /// 指定した設定の RTSP 通信を確認する。
        /// </summary>
        /// <param name="item">確認対象。</param>
        /// <param name="cancellationToken">キャンセル トークン。</param>
        /// <returns>確認結果。</returns>
        public async Task<NetworkCameraCheckResult> CheckConnectionAsync(NetworkCameraConfigItem item, CancellationToken cancellationToken = default)
        {
            var normalizedItem = NormalizeConfigItem(item);
            var checkedAt = DateTime.Now;

            if (!IsValidConfigItem(normalizedItem))
            {
                return new NetworkCameraCheckResult(
                    normalizedItem.CameraId ?? string.Empty,
                    false,
                    "表示名または RTSP URL が不足しています。",
                    checkedAt);
            }

            if (!TryParseRtspConnection(normalizedItem.RtspUrl!, normalizedItem.HostAddress, out var hostAddress, out var port, out var normalizedRtspUrl))
            {
                return new NetworkCameraCheckResult(
                    normalizedItem.CameraId ?? string.Empty,
                    false,
                    "RTSP URL の形式が不正です。",
                    checkedAt);
            }

            if (!await CanConnectTcpAsync(hostAddress, port, 1500, cancellationToken))
            {
                return new NetworkCameraCheckResult(
                    normalizedItem.CameraId ?? string.Empty,
                    false,
                    $"NG: {hostAddress}:{port} に接続できませんでした。",
                    checkedAt);
            }

            if (await CanRespondRtspAsync(hostAddress, port, normalizedRtspUrl, cancellationToken))
            {
                return new NetworkCameraCheckResult(
                    normalizedItem.CameraId ?? string.Empty,
                    true,
                    $"OK: {hostAddress}:{port} と RTSP 応答を確認しました。",
                    checkedAt);
            }

            return new NetworkCameraCheckResult(
                normalizedItem.CameraId ?? string.Empty,
                false,
                $"NG: {hostAddress}:{port} へ接続できましたが、RTSP 応答を確認できませんでした。",
                checkedAt);
        }

        /// <summary>
        /// 同一 LAN 内の RTSP / ONVIF 候補を検出する。
        /// </summary>
        /// <param name="cancellationToken">キャンセル トークン。</param>
        /// <returns>検出候補一覧。</returns>
        public async Task<IReadOnlyList<DetectedNetworkCameraItem>> DetectNetworkCamerasAsync(CancellationToken cancellationToken = default)
        {
            var detectedItems = new List<DetectedNetworkCameraItem>();

            detectedItems.AddRange(await DiscoverOnvifCandidatesAsync(cancellationToken));
            detectedItems.AddRange(await DiscoverRtspCandidatesAsync(cancellationToken));

            if (detectedItems.Count == 0)
            {
                return Array.Empty<DetectedNetworkCameraItem>();
            }

            return detectedItems
                .Where(item => !string.IsNullOrWhiteSpace(item.RtspUrl))
                .GroupBy(item => item.RtspUrl, StringComparer.OrdinalIgnoreCase)
                .Select(ChooseBestDetectedItem)
                .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }

        /// <summary>
        /// ONVIF の WS-Discovery で候補を検出する。
        /// </summary>
        /// <param name="cancellationToken">キャンセル トークン。</param>
        /// <returns>検出候補一覧。</returns>
        private async Task<IReadOnlyList<DetectedNetworkCameraItem>> DiscoverOnvifCandidatesAsync(CancellationToken cancellationToken)
        {
            var xAddrs = await ProbeOnvifDeviceServiceAddressesAsync(cancellationToken);
            if (xAddrs.Count == 0)
            {
                return Array.Empty<DetectedNetworkCameraItem>();
            }

            using var semaphore = new SemaphoreSlim(4);
            var detectedItems = new ConcurrentBag<DetectedNetworkCameraItem>();
            var tasks = xAddrs.Select(async xAddr =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var detectedItem = await BuildOnvifDetectedItemAsync(xAddr, cancellationToken);
                    if (detectedItem is not null)
                    {
                        detectedItems.Add(detectedItem);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            return detectedItems.ToArray();
        }

        /// <summary>
        /// 同一 LAN 内の RTSP 候補を簡易検出する。
        /// </summary>
        /// <param name="cancellationToken">キャンセル トークン。</param>
        /// <returns>検出候補一覧。</returns>
        private async Task<IReadOnlyList<DetectedNetworkCameraItem>> DiscoverRtspCandidatesAsync(CancellationToken cancellationToken)
        {
            var targets = BuildLocalScanTargets();
            if (targets.Count == 0)
            {
                return Array.Empty<DetectedNetworkCameraItem>();
            }

            using var semaphore = new SemaphoreSlim(24);
            var detectedItems = new ConcurrentBag<DetectedNetworkCameraItem>();
            var tasks = targets.Select(async address =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var hostAddress = address.ToString();
                    foreach (var port in CommonRtspPorts)
                    {
                        if (!await CanConnectTcpAsync(hostAddress, port, 180, cancellationToken))
                        {
                            continue;
                        }

                        detectedItems.Add(new DetectedNetworkCameraItem(
                            BuildCameraId($"{hostAddress}-{port}"),
                            $"{hostAddress} のRTSPカメラ",
                            hostAddress,
                            $"rtsp://{hostAddress}:{port}/",
                            $"RTSP ポート {port} を検出しました。"));
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            return detectedItems.ToArray();
        }

        /// <summary>
        /// ONVIF 候補の中から優先度の高い 1 件を返す。
        /// </summary>
        /// <param name="group">候補群。</param>
        /// <returns>代表候補。</returns>
        private static DetectedNetworkCameraItem ChooseBestDetectedItem(IGrouping<string, DetectedNetworkCameraItem> group)
        {
            return group
                .OrderByDescending(item => item.StatusText.Contains("ONVIF", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(item => item.RtspUrl.Length)
                .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .First();
        }

        /// <summary>
        /// WS-Discovery で ONVIF のデバイスサービス候補を探す。
        /// </summary>
        /// <param name="cancellationToken">キャンセル トークン。</param>
        /// <returns>デバイスサービスの XAddr 一覧。</returns>
        private static async Task<IReadOnlyList<string>> ProbeOnvifDeviceServiceAddressesAsync(CancellationToken cancellationToken)
        {
            var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var probeMessage = BuildOnvifProbeMessage();
            var probeBytes = Encoding.UTF8.GetBytes(probeMessage);

            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            await udp.SendAsync(probeBytes, probeBytes.Length, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 3702));

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(1500);

            while (!timeoutCts.IsCancellationRequested)
            {
                UdpReceiveResult packet;
                try
                {
                    packet = await udp.ReceiveAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    break;
                }

                var responseText = Encoding.UTF8.GetString(packet.Buffer);
                foreach (var xAddr in ExtractOnvifXAddrs(responseText))
                {
                    discovered.Add(xAddr);
                }
            }

            return discovered.ToArray();
        }

        /// <summary>
        /// ONVIF 候補 1 件を組み立てる。
        /// </summary>
        /// <param name="deviceServiceXAddr">デバイスサービスの XAddr。</param>
        /// <param name="cancellationToken">キャンセル トークン。</param>
        /// <returns>検出候補。失敗した場合は null。</returns>
        private static async Task<DetectedNetworkCameraItem?> BuildOnvifDetectedItemAsync(string deviceServiceXAddr, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(deviceServiceXAddr, UriKind.Absolute, out var deviceServiceUri))
            {
                return null;
            }

            if (!string.Equals(deviceServiceUri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(deviceServiceUri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var hostAddress = deviceServiceUri.Host;
            var cameraId = BuildCameraId(hostAddress);
            var displayName = $"{hostAddress} のONVIFカメラ";
            var fallbackRtspUrl = BuildFallbackRtspUrl(hostAddress);
            var statusText = "ONVIF 対応候補を検出しました。";
            var rtspUrl = fallbackRtspUrl;

            var capabilitiesResponse = await PostSoapRequestAsync(
                deviceServiceUri,
                "http://www.onvif.org/ver10/device/wsdl/GetCapabilities",
                BuildGetCapabilitiesBody(),
                cancellationToken);

            var mediaXAddr = TryGetAttributeValue(capabilitiesResponse, "Media", "XAddr");
            if (string.IsNullOrWhiteSpace(mediaXAddr) || !Uri.TryCreate(mediaXAddr, UriKind.Absolute, out var mediaUri))
            {
                return new DetectedNetworkCameraItem(
                    cameraId,
                    displayName,
                    hostAddress,
                    rtspUrl,
                    $"{statusText} RTSP URL は自動取得できなかったため、仮の URL を入れています。");
            }

            var profilesResponse = await PostSoapRequestAsync(
                mediaUri,
                "http://www.onvif.org/ver10/media/wsdl/GetProfiles",
                BuildGetProfilesBody(),
                cancellationToken);

            var profileToken = TryGetAttributeValue(profilesResponse, "Profiles", "token");
            if (string.IsNullOrWhiteSpace(profileToken))
            {
                return new DetectedNetworkCameraItem(
                    cameraId,
                    displayName,
                    hostAddress,
                    rtspUrl,
                    $"{statusText} プロファイルを取得できなかったため、仮の RTSP URL を入れています。");
            }

            var streamResponse = await PostSoapRequestAsync(
                mediaUri,
                "http://www.onvif.org/ver10/media/wsdl/GetStreamUri",
                BuildGetStreamUriBody(profileToken),
                cancellationToken);

            var streamUri = TryGetElementValue(streamResponse, "Uri");
            if (!string.IsNullOrWhiteSpace(streamUri) && Uri.TryCreate(streamUri, UriKind.Absolute, out var parsedStreamUri))
            {
                rtspUrl = parsedStreamUri.ToString();
                statusText = "ONVIF から RTSP URL を取得しました。";
            }
            else
            {
                statusText = "ONVIF は検出しましたが、RTSP URL の取得はできませんでした。";
            }

            return new DetectedNetworkCameraItem(
                cameraId,
                displayName,
                hostAddress,
                rtspUrl,
                statusText);
        }

        /// <summary>
        /// SOAP リクエストを送信する。
        /// </summary>
        /// <param name="requestUri">送信先 URI。</param>
        /// <param name="action">SOAP アクション。</param>
        /// <param name="bodyXml">SOAP ボディ。</param>
        /// <param name="cancellationToken">キャンセル トークン。</param>
        /// <returns>応答 XML。失敗時は null。</returns>
        private static async Task<string?> PostSoapRequestAsync(Uri requestUri, string action, string bodyXml, CancellationToken cancellationToken)
        {
            return await SendSoapRequestAsync(
                    requestUri,
                    action,
                    bodyXml,
                    "http://www.w3.org/2003/05/soap-envelope",
                    "application/soap+xml",
                    cancellationToken)
                ?? await SendSoapRequestAsync(
                    requestUri,
                    action,
                    bodyXml,
                    "http://schemas.xmlsoap.org/soap/envelope/",
                    "text/xml",
                    cancellationToken);
        }

        /// <summary>
        /// SOAP リクエストを 1 方式で送信する。
        /// </summary>
        /// <param name="requestUri">送信先 URI。</param>
        /// <param name="action">SOAP アクション。</param>
        /// <param name="bodyXml">SOAP ボディ。</param>
        /// <param name="envelopeNamespace">Envelope の名前空間。</param>
        /// <param name="contentType">Content-Type。</param>
        /// <param name="cancellationToken">キャンセル トークン。</param>
        /// <returns>応答 XML。失敗時は null。</returns>
        private static async Task<string?> SendSoapRequestAsync(
            Uri requestUri,
            string action,
            string bodyXml,
            string envelopeNamespace,
            string contentType,
            CancellationToken cancellationToken)
        {
            var requestBody = BuildSoapEnvelope(bodyXml, envelopeNamespace);

            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(4)
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{action}\"");

            var content = new StringContent(requestBody, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType)
            {
                CharSet = "utf-8"
            };
            request.Content = content;

            try
            {
                using var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// SOAP の Envelope を組み立てる。
        /// </summary>
        /// <param name="bodyXml">SOAP ボディ XML。</param>
        /// <param name="envelopeNamespace">Envelope の名前空間。</param>
        /// <returns>SOAP XML。</returns>
        private static string BuildSoapEnvelope(string bodyXml, string envelopeNamespace)
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""{envelopeNamespace}"">
  <s:Body>
    {bodyXml}
  </s:Body>
</s:Envelope>";
        }

        /// <summary>
        /// WS-Discovery の Probe メッセージを組み立てる。
        /// </summary>
        /// <returns>Probe XML。</returns>
        private static string BuildOnvifProbeMessage()
        {
            var messageId = $"uuid:{Guid.NewGuid():D}";
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<e:Envelope xmlns:e=""http://www.w3.org/2003/05/soap-envelope""
            xmlns:w=""http://schemas.xmlsoap.org/ws/2004/08/addressing""
            xmlns:d=""http://schemas.xmlsoap.org/ws/2005/04/discovery""
            xmlns:dn=""http://www.onvif.org/ver10/network/wsdl"">
  <e:Header>
    <w:MessageID>{messageId}</w:MessageID>
    <w:To>urn:schemas-xmlsoap-org:ws:2005:04:discovery</w:To>
    <w:Action>http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</w:Action>
  </e:Header>
  <e:Body>
    <d:Probe>
      <d:Types>dn:NetworkVideoTransmitter</d:Types>
    </d:Probe>
  </e:Body>
</e:Envelope>";
        }

        /// <summary>
        /// GetCapabilities 用のボディを組み立てる。
        /// </summary>
        /// <returns>SOAP ボディ。</returns>
        private static string BuildGetCapabilitiesBody()
        {
            return @"<tds:GetCapabilities xmlns:tds=""http://www.onvif.org/ver10/device/wsdl"">
  <tds:Category>All</tds:Category>
</tds:GetCapabilities>";
        }

        /// <summary>
        /// GetProfiles 用のボディを組み立てる。
        /// </summary>
        /// <returns>SOAP ボディ。</returns>
        private static string BuildGetProfilesBody()
        {
            return @"<trt:GetProfiles xmlns:trt=""http://www.onvif.org/ver10/media/wsdl"" />";
        }

        /// <summary>
        /// GetStreamUri 用のボディを組み立てる。
        /// </summary>
        /// <param name="profileToken">プロファイルトークン。</param>
        /// <returns>SOAP ボディ。</returns>
        private static string BuildGetStreamUriBody(string profileToken)
        {
            var safeToken = SecurityElement.Escape(profileToken) ?? profileToken;
            return $@"<trt:GetStreamUri xmlns:trt=""http://www.onvif.org/ver10/media/wsdl"" xmlns:tt=""http://www.onvif.org/ver10/schema"">
  <trt:StreamSetup>
    <tt:Stream>RTP-Unicast</tt:Stream>
    <tt:Transport>
      <tt:Protocol>RTSP</tt:Protocol>
    </tt:Transport>
  </trt:StreamSetup>
  <trt:ProfileToken>{safeToken}</trt:ProfileToken>
</trt:GetStreamUri>";
        }

        /// <summary>
        /// XAddrs を抽出する。
        /// </summary>
        /// <param name="xmlText">応答 XML。</param>
        /// <returns>XAddr 一覧。</returns>
        private static IReadOnlyList<string> ExtractOnvifXAddrs(string xmlText)
        {
            if (string.IsNullOrWhiteSpace(xmlText))
            {
                return Array.Empty<string>();
            }

            try
            {
                var document = XDocument.Parse(xmlText);
                var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var xAddrsElement in document.Descendants().Where(element => string.Equals(element.Name.LocalName, "XAddrs", StringComparison.OrdinalIgnoreCase)))
                {
                    var candidates = xAddrsElement.Value.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var candidate in candidates)
                    {
                        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                            && (string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)))
                        {
                            results.Add(uri.ToString());
                        }
                    }
                }

                return results.ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// 指定要素の属性値を取得する。
        /// </summary>
        /// <param name="xmlText">応答 XML。</param>
        /// <param name="elementLocalName">要素名。</param>
        /// <param name="attributeLocalName">属性名。</param>
        /// <returns>属性値。見つからない場合は null。</returns>
        private static string? TryGetAttributeValue(string? xmlText, string elementLocalName, string attributeLocalName)
        {
            if (string.IsNullOrWhiteSpace(xmlText))
            {
                return null;
            }

            try
            {
                var document = XDocument.Parse(xmlText);
                return document.Descendants()
                    .FirstOrDefault(element => string.Equals(element.Name.LocalName, elementLocalName, StringComparison.OrdinalIgnoreCase))
                    ?.Attributes()
                    .FirstOrDefault(attribute => string.Equals(attribute.Name.LocalName, attributeLocalName, StringComparison.OrdinalIgnoreCase))
                    ?.Value;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 指定要素の値を取得する。
        /// </summary>
        /// <param name="xmlText">応答 XML。</param>
        /// <param name="elementLocalName">要素名。</param>
        /// <returns>要素値。見つからない場合は null。</returns>
        private static string? TryGetElementValue(string? xmlText, string elementLocalName)
        {
            if (string.IsNullOrWhiteSpace(xmlText))
            {
                return null;
            }

            try
            {
                var document = XDocument.Parse(xmlText);
                return document.Descendants()
                    .FirstOrDefault(element => string.Equals(element.Name.LocalName, elementLocalName, StringComparison.OrdinalIgnoreCase))
                    ?.Value;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 予備の RTSP URL を作る。
        /// </summary>
        /// <param name="hostAddress">ホスト名または IP アドレス。</param>
        /// <returns>予備の RTSP URL。</returns>
        private static string BuildFallbackRtspUrl(string hostAddress)
        {
            return $"rtsp://{hostAddress}:{DefaultRtspPort}/";
        }

        /// <summary>
        /// 設定ファイルの絶対パスを返す。
        /// </summary>
        /// <returns>設定ファイルの絶対パス。</returns>
        public string GetConfigFilePath()
        {
            return Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        }

        /// <summary>
        /// 設定を読み込む。
        /// </summary>
        /// <returns>読み込んだ設定一覧。</returns>
        private IReadOnlyList<NetworkCameraConfigItem> LoadConfigItems()
        {
            lock (FileSyncRoot)
            {
                var configPath = GetConfigFilePath();
                if (!File.Exists(configPath))
                {
                    return Array.Empty<NetworkCameraConfigItem>();
                }

                var jsonText = File.ReadAllText(configPath, Encoding.UTF8);
                var items = JsonSerializer.Deserialize<List<NetworkCameraConfigItem>>(jsonText, SerializerOptions);
                if (items is null || items.Count == 0)
                {
                    return Array.Empty<NetworkCameraConfigItem>();
                }

                return items
                    .Select(NormalizeConfigItem)
                    .ToArray();
            }
        }

        /// <summary>
        /// 設定を保存する。
        /// </summary>
        /// <param name="items">保存対象一覧。</param>
        private void SaveConfigItems(IEnumerable<NetworkCameraConfigItem> items)
        {
            lock (FileSyncRoot)
            {
                var configPath = GetConfigFilePath();
                var directoryPath = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var normalizedItems = items
                    .Select(NormalizeConfigItem)
                    .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();

                var jsonText = JsonSerializer.Serialize(normalizedItems, SerializerOptions);
                File.WriteAllText(configPath, jsonText, new UTF8Encoding(false));
            }
        }

        /// <summary>
        /// 保存用に設定を整形する。
        /// </summary>
        /// <param name="item">整形対象。</param>
        /// <returns>整形済みの設定。</returns>
        private static NetworkCameraConfigItem NormalizeConfigItem(NetworkCameraConfigItem item)
        {
            var normalizedRtspUrl = item.RtspUrl?.Trim();
            var normalizedHostAddress = item.HostAddress?.Trim();

            if (string.IsNullOrWhiteSpace(normalizedHostAddress)
                && Uri.TryCreate(normalizedRtspUrl, UriKind.Absolute, out var uri))
            {
                normalizedHostAddress = uri.Host;
            }

            var displayName = item.DisplayName?.Trim();
            var hostForId = !string.IsNullOrWhiteSpace(normalizedHostAddress)
                ? normalizedHostAddress
                : displayName;

            return new NetworkCameraConfigItem
            {
                Enabled = item.Enabled,
                CameraId = string.IsNullOrWhiteSpace(item.CameraId) ? BuildCameraId(hostForId) : item.CameraId!.Trim(),
                DisplayName = displayName,
                HostAddress = normalizedHostAddress,
                RtspUrl = normalizedRtspUrl
            };
        }

        /// <summary>
        /// 設定が利用可能な形式かどうかを返す。
        /// </summary>
        /// <param name="item">判定対象。</param>
        /// <returns>利用可能な場合は true。</returns>
        private static bool IsValidConfigItem(NetworkCameraConfigItem item)
        {
            return !string.IsNullOrWhiteSpace(item.CameraId)
                && !string.IsNullOrWhiteSpace(item.DisplayName)
                && !string.IsNullOrWhiteSpace(item.RtspUrl);
        }

        /// <summary>
        /// 実運用用の候補に変換する。
        /// </summary>
        /// <param name="item">変換対象。</param>
        /// <returns>選択用候補。</returns>
        private static NetworkCameraOption ToOption(NetworkCameraConfigItem item)
        {
            return new NetworkCameraOption(
                item.CameraId!.Trim(),
                item.DisplayName!.Trim(),
                item.HostAddress?.Trim() ?? string.Empty,
                item.RtspUrl!.Trim());
        }

        /// <summary>
        /// RTSP 接続先を解釈する。
        /// </summary>
        /// <param name="rtspUrl">RTSP URL。</param>
        /// <param name="hostAddress">ホスト名または IP アドレス。</param>
        /// <param name="resolvedHost">解決したホスト名または IP アドレス。</param>
        /// <param name="resolvedPort">解決したポート番号。</param>
        /// <param name="normalizedRtspUrl">整形済み RTSP URL。</param>
        /// <returns>解釈できた場合は true。</returns>
        private static bool TryParseRtspConnection(string rtspUrl, string? hostAddress, out string resolvedHost, out int resolvedPort, out string normalizedRtspUrl)
        {
            resolvedHost = string.Empty;
            resolvedPort = DefaultRtspPort;
            normalizedRtspUrl = rtspUrl.Trim();

            if (!Uri.TryCreate(normalizedRtspUrl, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (!string.Equals(uri.Scheme, "rtsp", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            resolvedHost = string.IsNullOrWhiteSpace(hostAddress) ? uri.Host : hostAddress.Trim();
            if (string.IsNullOrWhiteSpace(resolvedHost))
            {
                return false;
            }

            resolvedPort = uri.Port > 0 ? uri.Port : DefaultRtspPort;
            return true;
        }

        /// <summary>
        /// TCP 接続できるかどうかを確認する。
        /// </summary>
        /// <param name="hostAddress">接続先ホスト。</param>
        /// <param name="port">接続先ポート。</param>
        /// <param name="timeoutMilliseconds">タイムアウト時間。</param>
        /// <param name="cancellationToken">キャンセル トークン。</param>
        /// <returns>接続できた場合は true。</returns>
        private static async Task<bool> CanConnectTcpAsync(string hostAddress, int port, int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutMilliseconds);

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(hostAddress, port, timeoutCts.Token);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// RTSP の OPTIONS 応答が返るかどうかを確認する。
        /// </summary>
        /// <param name="hostAddress">接続先ホスト。</param>
        /// <param name="port">接続先ポート。</param>
        /// <param name="rtspUrl">RTSP URL。</param>
        /// <param name="cancellationToken">キャンセル トークン。</param>
        /// <returns>応答を確認できた場合は true。</returns>
        private static async Task<bool> CanRespondRtspAsync(string hostAddress, int port, string rtspUrl, CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(2500);

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(hostAddress, port, timeoutCts.Token);

                await using var stream = client.GetStream();
                var requestText =
                    $"OPTIONS {rtspUrl} RTSP/1.0\r\n" +
                    "CSeq: 1\r\n" +
                    "User-Agent: RemoteCamera\r\n\r\n";

                var requestBytes = Encoding.ASCII.GetBytes(requestText);
                await stream.WriteAsync(requestBytes, timeoutCts.Token);
                await stream.FlushAsync(timeoutCts.Token);

                var buffer = new byte[1024];
                var readLength = await stream.ReadAsync(buffer, timeoutCts.Token);
                if (readLength <= 0)
                {
                    return false;
                }

                var responseText = Encoding.ASCII.GetString(buffer, 0, readLength);
                return responseText.Contains("RTSP/1.0", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 同一 LAN の /24 範囲をスキャン対象として組み立てる。
        /// </summary>
        /// <returns>スキャン対象アドレス一覧。</returns>
        private static IReadOnlyList<IPAddress> BuildLocalScanTargets()
        {
            var results = new List<IPAddress>();
            var seenAddresses = new HashSet<string>(StringComparer.Ordinal);

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    var bytes = unicastAddress.Address.GetAddressBytes();
                    if (bytes.Length != 4)
                    {
                        continue;
                    }

                    if (bytes[0] == 169 && bytes[1] == 254)
                    {
                        continue;
                    }

                    for (var lastOctet = 1; lastOctet <= 254; lastOctet++)
                    {
                        if (lastOctet == bytes[3])
                        {
                            continue;
                        }

                        var addressText = $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{lastOctet}";
                        if (!seenAddresses.Add(addressText))
                        {
                            continue;
                        }

                        if (IPAddress.TryParse(addressText, out var address))
                        {
                            results.Add(address);
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// カメラ識別子を生成する。
        /// </summary>
        /// <param name="sourceText">元文字列。</param>
        /// <returns>生成した識別子。</returns>
        private static string BuildCameraId(string? sourceText)
        {
            var value = sourceText?.Trim().ToLowerInvariant() ?? "network-camera";
            var builder = new StringBuilder();

            foreach (var current in value)
            {
                if ((current >= 'a' && current <= 'z') || (current >= '0' && current <= '9'))
                {
                    builder.Append(current);
                }
                else if (builder.Length == 0 || builder[^1] != '-')
                {
                    builder.Append('-');
                }
            }

            var result = builder.ToString().Trim('-');
            return string.IsNullOrWhiteSpace(result)
                ? $"network-camera-{DateTime.Now:yyyyMMddHHmmss}"
                : result;
        }
    }
}
