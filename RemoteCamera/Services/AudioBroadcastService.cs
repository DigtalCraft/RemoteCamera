using System.Net.WebSockets;
using NAudio.Wave;

namespace RemoteCamera
{
    /// <summary>
    /// 既定の録音デバイスから取得した音声を WebSocket で配信する。
    /// </summary>
    internal sealed class AudioBroadcastService : IDisposable
    {
        private const int DefaultSampleRate = 16000;
        private const int DefaultBitsPerSample = 16;
        private const int DefaultChannelCount = 1;

        private readonly object syncRoot = new();
        private readonly List<AudioClientConnection> clients = new();

        private WaveInEvent? waveIn;
        private bool started;
        private bool disposed;
        private string statusText = "音声は停止しています。";

        /// <summary>
        /// 音声配信の状態メッセージを返す。
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
        /// 音声入力が開始済みかどうかを返す。
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (syncRoot)
                {
                    return started && !disposed;
                }
            }
        }

        /// <summary>
        /// 配信用のサンプルレートを返す。
        /// </summary>
        public int SampleRate => DefaultSampleRate;

        /// <summary>
        /// 音声入力を開始する。
        /// </summary>
        public void Start()
        {
            ThrowIfDisposed();

            lock (syncRoot)
            {
                if (started)
                {
                    return;
                }
            }

            var recorder = new WaveInEvent
            {
                DeviceNumber = 0,
                BufferMilliseconds = 120,
                WaveFormat = new WaveFormat(DefaultSampleRate, DefaultBitsPerSample, DefaultChannelCount)
            };

            recorder.DataAvailable += WaveIn_DataAvailable;
            recorder.RecordingStopped += WaveIn_RecordingStopped;

            try
            {
                recorder.StartRecording();
            }
            catch (Exception ex)
            {
                recorder.DataAvailable -= WaveIn_DataAvailable;
                recorder.RecordingStopped -= WaveIn_RecordingStopped;
                recorder.Dispose();

                lock (syncRoot)
                {
                    statusText = $"音声入力を開始できませんでした。{ex.Message}";
                }

                return;
            }

            lock (syncRoot)
            {
                waveIn = recorder;
                started = true;
                statusText = "音声を配信しています。";
            }
        }

        /// <summary>
        /// WebSocket クライアントを登録して音声を配信する。
        /// </summary>
        /// <param name="socket">接続先 WebSocket。</param>
        /// <param name="cancellationToken">切断監視用トークン。</param>
        public async Task HandleWebSocketAsync(WebSocket socket, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            var client = new AudioClientConnection(socket);
            lock (syncRoot)
            {
                clients.Add(client);
            }

            var buffer = new byte[32];

            try
            {
                while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var receiveResult = await socket.ReceiveAsync(buffer, cancellationToken);
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }
            }
            catch
            {
                // モバイル側切断は通常系として扱う。
            }
            finally
            {
                RemoveClient(client);

                if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                {
                    try
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", CancellationToken.None);
                    }
                    catch
                    {
                        // 切断時は続行する。
                    }
                }
            }
        }

        /// <summary>
        /// 音声入力を停止してリソースを解放する。
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            WaveInEvent? recorder;
            List<AudioClientConnection> currentClients;

            lock (syncRoot)
            {
                recorder = waveIn;
                waveIn = null;
                started = false;
                statusText = "音声は停止しています。";
                currentClients = new List<AudioClientConnection>(clients);
                clients.Clear();
            }

            if (recorder is not null)
            {
                recorder.DataAvailable -= WaveIn_DataAvailable;
                recorder.RecordingStopped -= WaveIn_RecordingStopped;

                try
                {
                    recorder.StopRecording();
                }
                catch
                {
                    // 停止済みでも後続の解放を優先する。
                }

                recorder.Dispose();
            }

            foreach (var client in currentClients)
            {
                client.Dispose();
            }
        }

        /// <summary>
        /// 録音停止時に状態文字列を更新する。
        /// </summary>
        private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                started = false;
                statusText = e.Exception is null
                    ? "音声は停止しています。"
                    : $"音声入力が停止しました。{e.Exception.Message}";
            }
        }

        /// <summary>
        /// 受信した PCM 音声を接続中クライアントへ配信する。
        /// </summary>
        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            AudioClientConnection[] currentClients;

            lock (syncRoot)
            {
                if (disposed || clients.Count == 0)
                {
                    return;
                }

                currentClients = clients.ToArray();
            }

            foreach (var client in currentClients)
            {
                client.TrySend(e.Buffer, e.BytesRecorded);
            }
        }

        /// <summary>
        /// 切断済みクライアントを一覧から外す。
        /// </summary>
        /// <param name="client">削除対象。</param>
        private void RemoveClient(AudioClientConnection client)
        {
            lock (syncRoot)
            {
                clients.Remove(client);
            }

            client.Dispose();
        }

        /// <summary>
        /// 破棄済みかどうかを確認する。
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(AudioBroadcastService));
            }
        }

        /// <summary>
        /// WebSocket 単位の送信状態を管理する。
        /// </summary>
        private sealed class AudioClientConnection : IDisposable
        {
            private readonly WebSocket socket;
            private readonly SemaphoreSlim sendLock = new(1, 1);
            private bool disposed;

            /// <summary>
            /// 接続情報を初期化する。
            /// </summary>
            /// <param name="socket">クライアント WebSocket。</param>
            public AudioClientConnection(WebSocket socket)
            {
                this.socket = socket;
            }

            /// <summary>
            /// 送信中でない場合だけ音声チャンクを非同期送信する。
            /// </summary>
            /// <param name="buffer">PCM バッファ。</param>
            /// <param name="length">有効長。</param>
            public void TrySend(byte[] buffer, int length)
            {
                if (disposed || socket.State != WebSocketState.Open)
                {
                    return;
                }

                if (!sendLock.Wait(0))
                {
                    return;
                }

                var payload = new byte[length];
                Buffer.BlockCopy(buffer, 0, payload, 0, length);
                _ = SendInternalAsync(payload);
            }

            /// <summary>
            /// 非同期送信本体。
            /// </summary>
            /// <param name="payload">送信データ。</param>
            private async Task SendInternalAsync(byte[] payload)
            {
                try
                {
                    if (!disposed && socket.State == WebSocketState.Open)
                    {
                        await socket.SendAsync(payload, WebSocketMessageType.Binary, true, CancellationToken.None);
                    }
                }
                catch
                {
                    // 切断は呼び出し元で吸収する。
                }
                finally
                {
                    sendLock.Release();
                }
            }

            /// <summary>
            /// 保持リソースを解放する。
            /// </summary>
            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                sendLock.Dispose();

                try
                {
                    socket.Dispose();
                }
                catch
                {
                    // 破棄時は続行する。
                }
            }
        }
    }
}
