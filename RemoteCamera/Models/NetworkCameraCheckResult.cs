namespace RemoteCamera
{
    /// <summary>
    /// ネットワークカメラの接続確認結果を表す。
    /// </summary>
    /// <param name="CameraId">確認対象の識別子。</param>
    /// <param name="IsSuccess">確認結果が成功かどうか。</param>
    /// <param name="StatusText">画面表示用メッセージ。</param>
    /// <param name="CheckedAt">確認日時。</param>
    internal sealed record NetworkCameraCheckResult(
        string CameraId,
        bool IsSuccess,
        string StatusText,
        DateTime CheckedAt);
}
