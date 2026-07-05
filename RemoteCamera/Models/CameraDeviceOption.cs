namespace RemoteCamera
{
    /// <summary>
    /// カメラ選択用の候補情報。
    /// </summary>
    /// <param name="DisplayName">画面に表示する名称。</param>
    /// <param name="CaptureIndex">OpenCV で開くデバイス番号。</param>
    /// <param name="SourceType">入力元の種類。</param>
    internal sealed record CameraDeviceOption(
        string DisplayName,
        int CaptureIndex,
        CameraSourceType SourceType)
    {
        /// <summary>
        /// コンボボックス表示用の文字列を返す。
        /// </summary>
        /// <returns>表示文字列。</returns>
        public override string ToString()
        {
            return $"{DisplayName}  ({CaptureIndex})";
        }
    }
}
