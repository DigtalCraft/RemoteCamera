RemoteCamera is a Windows desktop app that captures camera video on a PC, records MP4 files, and exposes a smartphone-friendly monitor page over the local network or Tailscale.

## 日本語

### 概要

RemoteCamera は、Windows PC 上でカメラ映像を取得し、録画し、スマホのブラウザから確認する監視アプリです。

### できること

- Windows が認識するローカルカメラを選択
- `NetworkCameras.json` に登録した RTSP カメラを選択
- PC 画面でライブプレビュー
- MP4 形式で録画
- スマホのブラウザで監視ページを表示
- Tailscale 経由でのアクセス
- PC のマイク音声をスマホへ配信
- スマホ側で音量スライダーと音声レベルメーターを表示
- PC 側とブラウザ側の両方からネットワークカメラ設定を編集
- 自動検出で ONVIF 対応カメラと一般的な RTSP ポートを確認

### 現在の構成

- ローカルカメラは OpenCvSharp の `VideoCapture(index, VideoCaptureAPIs.DSHOW)` で開く
- RTSP カメラは OpenCvSharp の `VideoCapture(rtspUrl, VideoCaptureAPIs.FFMPEG)` で開く
- 監視ページは ASP.NET Core の最小 API で提供する
- 音声は NAudio で取得して WebSocket で配信する
- 映像は `/snapshot.jpg` の定期取得で表示する

### ネットワークカメラ設定

`NetworkCameras.json` はアプリ配置フォルダに置く設定ファイルです。

主な項目:

- `enabled`: 利用する場合は `true`
- `cameraId`: 識別子
- `displayName`: 画面表示名
- `hostAddress`: ホスト名または IP アドレス
- `rtspUrl`: 接続先 RTSP URL

例:

```json
[
  {
    "enabled": true,
    "cameraId": "entrance-camera",
    "displayName": "玄関カメラ",
    "hostAddress": "192.168.1.10",
    "rtspUrl": "rtsp://user:password@192.168.1.10:554/stream1"
  }
]
```

### 自動検出

自動検出は次の順で探します。

- ONVIF の WS-Discovery
- RTSP の簡易ポート確認

ONVIF 対応の一般的な Wi-Fi カメラは、ここで見つかる可能性があります。  
ただし、認証が必要な機種や、ONVIF に対応していない機種は手動登録が必要です。

### 使い方

1. Windows PC で RemoteCamera を起動
2. ローカルカメラまたはネットワークカメラを選択
3. 必要なら `ネットワーク設定` で保存・編集・自動検出を実行
4. 画面に出るローカル URL か Tailscale URL をスマホのブラウザで開く
5. スマホ側でカメラを選び、録画や音声を操作する

### 注意事項

- PC がロックしてもアプリが動き続けることが前提です
- スリープ / 休止 / サインアウトに入ると監視は止まります
- スマホのロック画面での常時表示は対象外です
- Tailscale は同梱していません。別途インストールしてください

## English

### Overview

RemoteCamera captures camera video on a Windows PC, records MP4 files, and exposes a smartphone-friendly monitor page over the local network or Tailscale.

### Features

- Select a local camera recognized by Windows
- Select an RTSP camera registered in `NetworkCameras.json`
- Show live preview on the PC
- Record MP4 files
- Open the monitor page from a smartphone browser
- Access the app over Tailscale
- Stream microphone audio to the smartphone
- Adjust smartphone playback volume and view an audio level meter
- Edit network camera settings from both the PC app and the browser
- Discover cameras with ONVIF WS-Discovery and common RTSP port checks

### Current architecture

- Local cameras are opened with OpenCvSharp `VideoCapture(index, VideoCaptureAPIs.DSHOW)`
- RTSP cameras are opened with OpenCvSharp `VideoCapture(rtspUrl, VideoCaptureAPIs.FFMPEG)`
- The monitor page is served by ASP.NET Core minimal APIs
- Audio is captured with NAudio and streamed over WebSocket
- Video preview is rendered by repeatedly fetching `/snapshot.jpg`

### Network camera configuration

`NetworkCameras.json` lives in the application folder.

Main fields:

- `enabled`: set to `true` to use the camera
- `cameraId`: unique identifier
- `displayName`: label shown in the UI
- `hostAddress`: host name or IP address
- `rtspUrl`: RTSP connection URL

Example:

```json
[
  {
    "enabled": true,
    "cameraId": "entrance-camera",
    "displayName": "Entrance Camera",
    "hostAddress": "192.168.1.10",
    "rtspUrl": "rtsp://user:password@192.168.1.10:554/stream1"
  }
]
```

### Auto discovery

Auto discovery runs in this order:

- ONVIF WS-Discovery
- Simple RTSP port checks

Typical ONVIF-capable Wi-Fi cameras can often be found automatically.  
If the device requires authentication or does not support ONVIF, manual setup may still be needed.

### How to use

1. Start RemoteCamera on the Windows PC
2. Select a local camera or a network camera
3. Use `ネットワーク設定` if you want to save, edit, or auto-discover cameras
4. Open the displayed local URL or Tailscale URL in the smartphone browser
5. Select a camera on the browser and control recording or audio

### Notes

- This design assumes the PC app keeps running while the PC is locked
- Sleep, hibernate, or sign-out will stop monitoring
- Continuous viewing on the smartphone lock screen is not a target in this app
- Tailscale is not bundled and must be installed separately

## Third-party software

See [ThirdPartyNotices.txt](./ThirdPartyNotices.txt).
