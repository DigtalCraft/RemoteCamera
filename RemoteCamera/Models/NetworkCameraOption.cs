namespace RemoteCamera
{
    /// <summary>
    /// ネットワークカメラ選択用の候補情報。
    /// </summary>
    /// <param name="CameraId">設定ファイル内の識別子。</param>
    /// <param name="DisplayName">画面に表示する名称。</param>
    /// <param name="HostAddress">カメラのホスト名または IP アドレス。</param>
    /// <param name="RtspUrl">接続先の RTSP URL。</param>
    internal sealed record NetworkCameraOption(
        string CameraId,
        string DisplayName,
        string HostAddress,
        string RtspUrl)
    {
        /// <summary>
        /// コンボボックス表示用の文字列を返す。
        /// </summary>
        /// <returns>表示文字列。</returns>
        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(HostAddress))
            {
                return $"{DisplayName}  (RTSPカメラ)";
            }

            return $"{DisplayName} / {HostAddress}  (RTSPカメラ)";
        }
    }
}
