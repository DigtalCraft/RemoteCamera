namespace RemoteCamera
{
    /// <summary>
    /// 自動検出したネットワークカメラ候補を表す。
    /// </summary>
    /// <param name="CameraId">候補の識別子。</param>
    /// <param name="DisplayName">表示名。</param>
    /// <param name="HostAddress">ホスト名または IP アドレス。</param>
    /// <param name="RtspUrl">候補の RTSP URL。</param>
    /// <param name="StatusText">補足メッセージ。</param>
    internal sealed record DetectedNetworkCameraItem(
        string CameraId,
        string DisplayName,
        string HostAddress,
        string RtspUrl,
        string StatusText);
}
