namespace RemoteCamera
{
    /// <summary>
    /// ネットワークカメラ設定 1 件分を表す。
    /// </summary>
    internal sealed class NetworkCameraConfigItem
    {
        /// <summary>
        /// 利用する場合は true。
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// カメラ識別子。
        /// </summary>
        public string? CameraId { get; set; }

        /// <summary>
        /// 表示名。
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// カメラのホスト名または IP アドレス。
        /// </summary>
        public string? HostAddress { get; set; }

        /// <summary>
        /// RTSP 接続先 URL。
        /// </summary>
        public string? RtspUrl { get; set; }
    }
}
