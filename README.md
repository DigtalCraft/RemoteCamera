# RemoteCamera 

RemoteCamera は、Windows PC に接続されたカメラやネットワークカメラを、
スマホのブラウザから確認できるようにする監視アプリです。

高機能な防犯カメラシステムというより、  
「家・店舗・作業場に置いてある Windows PC を、そのまま簡易監視カメラとして使う」  
ことを目的にしています。

PC 側でアプリを起動しておけば、同じ LAN 内のスマホから映像を確認できます。  
外出先から確認したい場合は、Tailscale を使った安全な接続にも対応しています。

## このアプリの特徴

### Windows PC をそのまま監視用の親機にできる

専用の NVR やクラウド契約を前提にせず、手元の Windows PC を監視用の親機として使えます。

USB カメラ、内蔵カメラ、RTSP 対応のネットワークカメラを扱えるため、  
「まず手元の機材で試したい」場面に向いています。

### スマホ側はアプリ不要

スマホ側は専用アプリを入れず、ブラウザで監視ページを開くだけです。

同じ LAN 内なら、PC に表示される URL をスマホで開いて確認できます。  
ホーム画面に追加しておけば、スマホアプリに近い感覚で開けます。

### まず LAN 内で使えて、必要な人だけ外から見られる

RemoteCamera は、いきなりインターネットへ直接公開する前提ではありません。

まずは自宅や店舗などの同じネットワーク内で動かし、  
外出先から見たい人だけ Tailscale で外部アクセスを追加する考え方です。

ポート開放や固定グローバル IP を前提にしないため、家庭用回線でも扱いやすい構成です。

### PC 側とスマホ側の両方から操作できる

PC 側の画面だけでなく、スマホの監視ページからも操作できます。

- カメラ選択
- 録画開始
- 録画停止
- プレビュー表示の切り替え
- 音声の受信
- ネットワークカメラ設定

現場に置いた PC を直接触らなくても、スマホから最低限の確認と操作ができます。

### ネットワークカメラにも対応

RTSP URL を登録することで、IP カメラや Wi-Fi カメラも利用できます。

ONVIF の WS-Discovery と、一般的な RTSP ポートの確認による自動検出にも対応しています。  
自動検出で見つからない機種でも、RTSP URL が分かれば手動登録できます。

### スマホで見やすい監視画面

監視ページはスマホで使うことを前提にしています。

映像の確認、拡大、移動、録画操作、音声操作をブラウザ上で行えます。  
スマホで「ちょっと確認したい」ときに、PC 画面を開き直さなくて済むようにしています。

## できること

- Windows が認識するローカルカメラの表示
- RTSP ネットワークカメラの表示
- ONVIF 対応カメラの検出
- MP4 録画
- スマホブラウザからの映像確認
- スマホブラウザからのカメラ切り替え
- スマホブラウザからの録画開始・停止
- PC マイク音声のスマホ配信
- Tailscale 経由の外部アクセス
- Windows ログオン時の自動起動

## 想定している使い方

- 自宅の様子を別室やスマホから確認したい
- 店舗や作業場の PC を簡易監視端末にしたい
- 専用クラウドカメラを増やす前に、手元の PC とカメラで試したい
- LAN 内では簡単に使い、必要なときだけ外から確認したい
- コマンド操作ではなく、Windows アプリとして使いたい

## 使い方

1. Windows PC で RemoteCamera を起動します。
2. ローカルカメラ、またはネットワークカメラを選択します。
3. PC 画面に表示されるローカル URL をスマホのブラウザで開きます。
4. 外出先から見る場合は、PC とスマホの両方に Tailscale を入れて、Tailscale URL を開きます。
5. 必要に応じて、スマホ側から録画やカメラ切り替えを行います。

## ネットワークカメラ設定

ネットワークカメラは `NetworkCameras.json` に保存されます。  
アプリ内の設定画面から登録・編集できます。

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

## 注意事項

- PC がスリープ、休止、サインアウトすると監視は止まります。
- PC がロック画面の状態でも、アプリが動いていれば監視は継続できます。
- スマホのロック画面での継続視聴は対象外です。
- Tailscale は同梱していません。外部アクセスが必要な場合は別途インストールしてください。
- インターネットへ直接公開する用途は想定していません。
- カメラの種類やドライバによっては正常に映らない場合があります。

## 技術構成

- .NET Windows Forms
- ASP.NET Core minimal API
- OpenCvSharp
- NAudio
- RTSP
- ONVIF WS-Discovery
- Tailscale

## English

RemoteCamera is a Windows desktop app that turns a PC into a simple camera monitor host.

It can use local Windows cameras and RTSP network cameras, record MP4 files, and expose a smartphone-friendly monitor page over the local network.  
For remote access, it can also be used with Tailscale without directly exposing the app to the public internet.

The main goal is not to replace a full security camera system, but to make it easy to check a home, shop, workspace, or PC-side camera from a smartphone browser.

## Third-party software

See [ThirdPartyNotices.txt](./RemoteCamera/ThirdPartyNotices.txt).
