RemoteCamera 説明書 / RemoteCamera Manual
=========================================

[日本語]

1. 概要
-------
RemoteCamera は、Windows PC 上でカメラ映像を取得し、録画し、スマホのブラウザから確認する監視アプリです。

2. できること
-------------
- Windows が認識するローカルカメラを選択
- NetworkCameras.json に登録した RTSP カメラを選択
- PC 画面でライブプレビュー
- MP4 形式で録画
- スマホのブラウザで監視ページを表示
- Tailscale 経由でのアクセス
- PC のマイク音声をスマホへ配信
- スマホ側で音量スライダーと音声レベルメーターを表示
- PC 側とブラウザ側の両方からネットワークカメラ設定を編集
- ONVIF 対応カメラと一般的な RTSP ポートを自動検出

3. ネットワークカメラ設定
-------------------------
NetworkCameras.json はアプリ配置フォルダに置く設定ファイルです。

主な項目:
- enabled: true で利用
- cameraId: 識別子
- displayName: 画面表示名
- hostAddress: ホスト名または IP アドレス
- rtspUrl: 接続先 RTSP URL

例:
[
  {
    "enabled": true,
    "cameraId": "entrance-camera",
    "displayName": "玄関カメラ",
    "hostAddress": "192.168.1.10",
    "rtspUrl": "rtsp://user:password@192.168.1.10:554/stream1"
  }
]

4. 自動検出
-----------
自動検出は次の順で探します。
- ONVIF の WS-Discovery
- RTSP の簡易ポート確認

ONVIF 対応の Wi-Fi カメラは見つかることがあります。
ただし、認証が必要な機種や ONVIF 非対応機種は手動登録が必要です。

5. 使い方
--------
1. Windows PC で RemoteCamera を起動します。
2. ローカルカメラまたはネットワークカメラを選びます。
3. 必要に応じて「ネットワーク設定」で保存・編集・自動検出を行います。
4. 画面に出るローカル URL か Tailscale URL をスマホで開きます。
5. スマホ側でカメラを選び、録画や音声を操作します。

6. 注意事項
-----------
- PC がロックしてもアプリが動き続けることが可能です。
- スリープ / 休止 / サインアウトに入ると監視は止まります。
- スマホのロック画面は対象外です。
- Tailscale は同梱していません。別途インストールしてください。

[English]

1. Overview
-----------
RemoteCamera captures camera video on a Windows PC, records MP4 files, and exposes a smartphone-friendly monitor page over the local network or Tailscale.

2. Features
-----------
- Select a local camera recognized by Windows
- Select an RTSP camera registered in NetworkCameras.json
- Show live preview on the PC
- Record MP4 files
- Open the monitor page from a smartphone browser
- Access the app over Tailscale
- Stream microphone audio to the smartphone
- Adjust smartphone playback volume and view an audio level meter
- Edit network camera settings from both the PC app and the browser
- Discover cameras with ONVIF WS-Discovery and common RTSP port checks

3. Network camera configuration
------------------------------
NetworkCameras.json lives in the application folder.

Main fields:
- enabled: set to true to use the camera
- cameraId: unique identifier
- displayName: label shown in the UI
- hostAddress: host name or IP address
- rtspUrl: RTSP connection URL

Example:
[
  {
    "enabled": true,
    "cameraId": "entrance-camera",
    "displayName": "Entrance Camera",
    "hostAddress": "192.168.1.10",
    "rtspUrl": "rtsp://user:password@192.168.1.10:554/stream1"
  }
]

4. Auto discovery
----------------
Auto discovery runs in this order:
- ONVIF WS-Discovery
- Simple RTSP port checks

Typical ONVIF-capable Wi-Fi cameras can often be found automatically.
If the device requires authentication or does not support ONVIF, manual setup may still be needed.

5. How to use
------------
1. Start RemoteCamera on the Windows PC.
2. Select a local camera or a network camera.
3. Use "ネットワーク設定" if you want to save, edit, or auto-discover cameras.
4. Open the displayed local URL or Tailscale URL in the smartphone browser.
5. Select a camera on the browser and control recording or audio.

6. Notes
-------
- This design assumes the PC app keeps running while the PC is locked.
- Sleep, hibernate, or sign-out will stop monitoring.
- Continuous viewing on the smartphone lock screen is not a target in this app.
- Tailscale is not bundled and must be installed separately.


---

# ⚠️ Installation Notice / インストール時の注意

## 🇯🇵 日本語

現在、このインストーラーはコードサイニング証明書によるデジタル署名を行っていません。

そのため、Windows Defender SmartScreen やブラウザによってセキュリティ警告が表示される場合があります。

本ソフトウェアは **この GitHub Releases ページのみ** で配布しています。
ダウンロード元が正しいことを確認したうえでインストールしてください。

### SmartScreen が表示された場合

1. **「詳細情報」** をクリックします。
2. ファイル名が **RemoteCameraSetup.msi**（または公開されているファイル名）であることを確認します。
3. **「実行」** をクリックしてインストールしてください。

> **注意**
>
> - 証明書をインストールする必要はありません。
> - Windows が証明書のインストールを求めた場合は **「いいえ」** を選択してください。
> - GitHub Releases 以外から入手したインストーラーは実行しないでください。

---

## 🇺🇸 English

This installer is currently **not digitally signed** with a code-signing certificate.

Because of this, Windows Defender SmartScreen or your web browser may display a security warning before the installer starts.

This software is distributed **only through this GitHub Releases page**.
Please verify that you downloaded the installer from the official release before running it.

### If Windows Defender SmartScreen appears

1. Click **More info**.
2. Verify that the file name is **RemoteCameraSetup.msi** (or the published installer name).
3. Click **Run anyway** to continue the installation.

> **Note**
>
> - You do **not** need to install any certificate.
> - If Windows asks you to install a certificate, select **No**.
> - Do not run installers downloaded from websites other than the official GitHub Releases page.

