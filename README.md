# Unity6Portfolio

Unity 6 を使用したゲーム開発ポートフォリオプロジェクト（モノレポ構成）

## プロジェクト構成

```
Unity6Portfolio/
├── src/
│   ├── Game.Client/        # Unity クライアント (Unity 6)
│   ├── Game.Server/        # ゲームサーバー (ASP.NET Core 9)
│   └── Game.Shared/        # 共有ライブラリ (.NET + Unity Package)
└── test/
    └── Game.Server.Tests/  # サーバーテスト
```

---

## TL;DR
* このプロジェクトは主に個人または小規模のUnityゲーム開発におけるスターターキットを目指して作成されています
* コードの再利用性を高め、実装のしやすさや可読性、保守性が向上するような作りを意識しています
* インゲーム/アウトゲーム共にマスターデータで動作しています(データ駆動型)(調整中の部分を除く)
* **アセンブリ分割によるモジュラー設計**を採用し、MVC/MVP両パターンのゲームモードを共存可能
* **マスターデータ定義をGame.Shared**として分離し、クライアント・サーバー間で再利用可能
* 起動時のゲームモード選択画面から、異なるアーキテクチャのゲームを切り替えて起動可能

---

[English version is here](README.en.md)

---

## 環境構築

### 必要環境

| 項目 | バージョン |
|-----|-----------|
| Unity | 6000.3.2f1 以上 |
| .NET SDK | 9.0 以上 |
| OS | Windows 10/11 |

### セットアップ手順

#### クライアント (Unity)

1. リポジトリをクローン
   ```bash
   git clone https://github.com/your-username/Unity6Portfolio.git
   ```
2. Unity Hub で `src/Game.Client/` フォルダを開く
3. 初回起動時、パッケージの復元に数分かかる場合があります
4. `Assets/ProjectAssets/UnityScenes/GameRootScene.unity` を開いて再生

#### サーバー

```bash
cd src/Game.Server
dotnet restore
dotnet run
```

#### テスト実行

```bash
# サーバーテスト
dotnet test

# Unity テスト（Unity Editor内）
# Window > General > Test Runner
```

### 注意事項
* NuGetForUnity経由でインストールされるパッケージがあるため、初回ビルド時にエラーが出る場合は再度ビルドしてください
* Addressablesのビルドが必要な場合は `Window > Asset Management > Addressables > Groups` からビルドを実行

---

## スクリーンショット

### MVC: ScoreTimeAttack（スコアアタックゲーム）
| タイトル | ゲームプレイ | リザルト |
|---------|------------|---------|
| ![タイトル](src/Game.Client/Documentation/Screenshots/mvc_title.png) | ![ゲームプレイ](src/Game.Client/Documentation/Screenshots/mvc_gameplay.png) | ![リザルト](src/Game.Client/Documentation/Screenshots/mvc_result.png) |

### MVP: Survivor（サバイバーゲーム）
| タイトル | ゲームプレイ | レベルアップ |
|---------|------------|-------------|
| ![タイトル](src/Game.Client/Documentation/Screenshots/mvp_title.png) | ![ゲームプレイ](src/Game.Client/Documentation/Screenshots/mvp_gameplay.png) | ![レベルアップ](src/Game.Client/Documentation/Screenshots/mvp_levelup.png) |

### シェーダー・エフェクト
| トゥーンシェーダー | ディゾルブエフェクト |
|------------------|-------------------|
| ![トゥーン](src/Game.Client/Documentation/Screenshots/shader_toon.png) | ![ディゾルブ](src/Game.Client/Documentation/Screenshots/shader_dissolve.png) |

### エディター拡張
![エディターウィンドウ](src/Game.Client/Documentation/Screenshots/editor_window.png)

---

## ゲームプレイ動画

### MVC: ScoreTimeAttack
![MVCゲームプレイ](src/Game.Client/Documentation/GIFs/mvc_gameplay.gif)

### MVP: Survivor
![MVPゲームプレイ](src/Game.Client/Documentation/GIFs/mvp_gameplay.gif)

### シーン遷移・エフェクト
| シーン遷移 | エフェクト集 |
|-----------|-------------|
| ![シーン遷移](src/Game.Client/Documentation/GIFs/scene_transition.gif) | ![エフェクト](src/Game.Client/Documentation/GIFs/effects_showcase.gif) |

### エディターツール
![エディターツール](src/Game.Client/Documentation/GIFs/editor_tool.gif)

---

## アーキテクチャ概要

### モノレポ構成
```
┌─────────────────────────────────────────────────────────────┐
│                     Unity6Portfolio                          │
│                      (モノレポ)                               │
└─────────────────────────────────────────────────────────────┘
        ↓                    ↓                    ↓
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│   Game.Client   │  │   Game.Server   │  │   Game.Shared   │
│  (Unity 6)      │  │ (ASP.NET Core)  │  │ (.NET + Unity)  │
└─────────────────┘  └─────────────────┘  └─────────────────┘
        ↘                    ↓                    ↙
                    ┌─────────────────┐
                    │  共有DTO/IF     │
                    │  (Game.Shared)  │
                    └─────────────────┘
```

### クライアント内アーキテクチャ
```
┌─────────────────────────────────────────────────────────────┐
│                        Game.App                              │
│              (エントリーポイント・ゲームモード選択)             │
└─────────────────────────────────────────────────────────────┘
                    ↓                    ↓
┌─────────────────────────────┐  ┌─────────────────────────────┐
│      Game.MVC.Core          │  │      Game.MVP.Core          │
│   (MVCパターン基盤)          │  │   (MVPパターン基盤)          │
│   GameServiceManager        │  │   VContainer/DI             │
└─────────────────────────────┘  └─────────────────────────────┘
            ↓                                ↓
┌─────────────────────────────┐  ┌─────────────────────────────┐
│  Game.MVC.ScoreTimeAttack   │  │    Game.MVP.Survivor        │
│    (タイムアタックゲーム)     │  │   (サバイバーゲーム)          │
└─────────────────────────────┘  └─────────────────────────────┘
            ↖                                ↗
               └──────────────┬──────────────┘
                              ↓
              ┌─────────────────────────────┐
              │         Game.Shared         │
              │  (共通ユーティリティ/DTO)    │
              └─────────────────────────────┘
```

---

## 機能一覧
* **ゲームモード選択システム**: 起動時のタイトル画面から異なるアーキテクチャのゲームモードを選択可能
* **アセンブリ分割設計**: MVC/MVPパターンを独立したアセンブリで管理し、循環参照を防止
* **クライアント・サーバー共有**: Game.Sharedによりマスターデータ定義をクライアント・サーバー間で共有
* **プレハブシーン/ダイアログ遷移機能**: async/awaitによる非同期シーン遷移
* **ステートマシーン実装**: ジェネリック型コンテキスト付き、遷移テーブルによる状態管理
* **マスターデータ管理**: TSV→バイナリ変換、エディタ拡張によるデータ駆動開発
* **各種ゲームサービス**: オーディオ、シーン遷移、メッセージングなどの共通機能
* **DIコンテナ対応**: VContainerによる依存性注入（MVPパターン用）
* **戦闘システム**: ICombatTarget/IDamageable/IKnockbackableによる統一的な戦闘インターフェース
* **武器システム**: 自動発射・地面設置型武器、汎用オブジェクトプール（WeaponObjectPool<T>）
* **敵AIシステム**: ステートマシン駆動（Idle/Chase/Attack/HitStun/Death）、ウェーブスポーン
* **アイテムシステム**: ドロップ抽選、吸引機能、オブジェクトプーリング
* **ロックオンシステム**: 自動ターゲット追跡、射程管理
* **セーブデータシステム**: MemoryPackによるバイナリシリアライズ、自動保存

---

## 機能詳細
<details><summary>ゲームモード選択システム</summary>

1. アプリ起動時にGame.Appのタイトル画面を表示
2. 選択されたゲームモードに応じて対応するランチャーを起動
3. 各ゲームモードは独立したアセンブリで実装され、相互に影響しない
4. ゲーム終了時はランチャーをシャットダウンし、タイトル画面に戻ることが可能
5. ApplicationEventsによる疎結合なイベント通知（下位→上位アセンブリ）
</details>

<details><summary>アセンブリ分割設計</summary>

| アセンブリ | 役割 | 依存関係 |
|-----------|------|---------|
| Game.Shared | 共通ユーティリティ、インターフェース、DTO | なし（最下層） |
| Game.App | エントリーポイント、ゲームモード選択 | Shared, MVC.*, MVP.* |
| Game.MVC.Core | MVCパターン基盤、GameServiceManager | Shared |
| Game.MVC.ScoreTimeAttack | タイムアタックゲーム実装 | Shared, MVC.Core |
| Game.MVP.Core | MVPパターン基盤、VContainer | Shared |
| Game.MVP.Survivor | サバイバーゲーム実装 | Shared, MVP.Core |
| **Game.Server** | ASP.NET Core API サーバー | Shared |

</details>

<details><summary>Game.Shared（共有ライブラリ）</summary>

マスターデータ定義ファイルを共有ライブラリとして分離し、以下のメリットを実現：

1. **クライアント・サーバー共有**: 同じDTOをUnityとASP.NET Coreで共有可能
2. **依存関係の明確化**: 最下層に配置することで循環参照を防止
3. **ビルド時間短縮**: 変更頻度の低いコードを分離することでインクリメンタルビルドを効率化
4. **バージョン管理**: パッケージ単位でバージョン管理が可能

**含まれるコンテンツ:**
- MasterMemory用マスターデータ定義クラス（AudioMaster, ScoreTimeAttackStageMaster等）
- 共通Enum定義（AudioCategory, AudioPlayTag等）
- 共有インターフェース、DTO

**Survivorマスターデータ（11種類）:**
- `SurvivorStageMaster`: ステージ定義（制限時間、初期武器等）
- `SurvivorStageWaveMaster`: ウェーブ定義（出現タイミング、敵数）
- `SurvivorStageWaveEnemyMaster`: ウェーブ内敵構成
- `SurvivorEnemyMaster`: 敵ステータス（HP、攻撃力、移動速度等）
- `SurvivorPlayerMaster`: プレイヤー基本ステータス
- `SurvivorPlayerLevelMaster`: レベル別ステータス（吸引範囲等）
- `SurvivorWeaponMaster`: 武器定義（タイプ、ダメージ、クールダウン等）
- `SurvivorWeaponLevelMaster`: 武器レベル別ステータス
- `SurvivorItemMaster`: アイテム定義（効果値、レアリティ等）
- `SurvivorItemDropMaster`: ドロップ抽選テーブル

</details>

<details><summary>シーン/ダイアログ遷移機能</summary>

1. 非同期処理(async/await)で実装
2. 前のシーンが破棄されていても遷移履歴から再遷移が可能
3. 現在シーンをスリープさせて次のシーンへ遷移でき、戻るとスリープ状態から復帰可能
4. シーン実装は起動前/ロード時/初期化時/スリープ時/復帰時/終了時など様々なタイミングで追加処理を挟む事ができます
5. シーンに任意で引数や戻り値を追加で設定できます
6. 引数つきのシーンであっても、履歴から状態を復元して再度引数を渡して遷移する事が可能
7. ダイアログ(オーバーレイ)は複数開く事が可能で、不正な挙動を防止するためにシーン遷移時に全て破棄されます
</details>

<details><summary>ステートマシーン</summary>

1. ジェネリック型コンテキストを持ち、任意の型を指定できます。
2. 各ステートからコンテキストを参照して、状態管理を行う事ができます
3. 初期時に遷移テーブルを構築でき、各ステートがどのステートから遷移するかルールを設定できます。遷移ルールが1ヶ所に集約/可視化され保守性が向上します。
4. 任意ステートから遷移先に指定できる特別なステートを設定可能で、適切な設定が遷移テーブルに無い場合に遷移が検証/実行されます。
5. ジェネリック型のイベントキー型を指定でき、遷移イベント名をenum等で集約管理できます。遷移先ステート名と一致させると可読性/保守性が向上します。
6. 通常のUpdateに加え、MonoBehaivior.FixedUpdate/LateUpdateにも対応。これにより物理演算やカメラ等の状態と相互に連携できます。
</details>

<details><summary>サバイバーゲームシステム（MVP）</summary>

**戦闘システム**
- `ICombatTarget`: ダメージ・ノックバック・ターゲット機能を統合した戦闘インターフェース
- `IDamageable`, `IKnockbackable`, `ITargetable`: 個別機能のインターフェース
- 敵・プレイヤー共通の戦闘ロジックを実現

**武器システム**
- `SurvivorWeaponBase`: 武器の基底クラス（ダメージ計算、クリティカル、発動率）
- `SurvivorAutoFireWeapon`: 自動発射型武器（最寄りの敵に向けて弾を発射）
- `SurvivorGroundWeapon`: 地面設置型武器（ターゲット位置に円形パターンでダメージエリア生成）
- `WeaponObjectPool<T>`: 汎用オブジェクトプール（弾・エリア共通）
- マスターデータ駆動（レベルごとのステータス・アセット変更対応）

**敵AIシステム**
- `SurvivorEnemyController`: ステートマシン駆動の敵AI
- 状態遷移: Idle → Chase → Attack → HitStun → Death
- `SurvivorEnemySpawner`: ウェーブ管理・スポーン制御
- NavMeshAgentによる経路探索

**アイテムシステム**
- `SurvivorItemSpawner`: 敵撃破時のドロップ管理
- ドロップグループ抽選（確率テーブルによるアイテム決定）
- マグネット吸引機能（範囲内アイテムの自動回収）

**プレイヤーシステム**
- `SurvivorPlayerController`: 移動・HP・スタミナ・無敵管理
- ステートマシン: Normal → Invincible → Dead
- アイテム吸引範囲のレベル連動

**セーブデータシステム**
- `SurvivorSaveService`: ステージ進行状況・クリア記録の保存
- MemoryPackによる高速バイナリシリアライズ
- 自動保存（30秒間隔・バックグラウンド移行時）
- 勝敗確定時の即時保存（データ整合性保証）

</details>

<details><summary>その他</summary>

* シーン遷移やオーディオ再生などの共通機能は主にゲームサービスとして分離されています
* マスターデータエディタ拡張はTSVから簡単にバイナリを作成でき、TSV更新後すぐにデータをテストできます。これによって検証サイクルを早めています。テストしたバイナリをそのままビルドやアセット配信で使用できます。
* インゲームシーンはPrefabシーン＋Unityシーンで構成されており、ステージとなるUnityシーンはロジックから分離されています。その為、コード修正なしで新しいステージを追加できます
* アウトゲームシーンは遷移挙動のカスタマイズ性を担保するため、全てPrefabシーンを採用しています
</details>

---

## 主なフォルダ構成
```
Unity6Portfolio/
├── src/
│   ├── Game.Client/                    # Unity クライアント
│   │   ├── Assets/
│   │   │   ├── MasterData/             マスターデータ(TSV, バイナリ)
│   │   │   └── Programs/
│   │   │       ├── Editor/             エディタ拡張
│   │   │       │   └── Tests/          単体テスト
│   │   │       └── Runtime/
│   │   │           ├── Shared/         共通ユーティリティ
│   │   │           ├── App/            エントリーポイント
│   │   │           ├── MVC/            MVCパターン実装
│   │   │           │   ├── Core/       基盤(Services, Scenes)
│   │   │           │   └── ScoreTimeAttack/
│   │   │           └── MVP/            MVPパターン実装
│   │   │               ├── Core/       基盤(VContainer)
│   │   │               └── Survivor/   サバイバーゲーム
│   │   ├── Packages/
│   │   ├── ProjectSettings/
│   │   └── Documentation/              スクリーンショット、GIF
│   │
│   ├── Game.Client.Linked/             # MasterDataブリッジ(.NET SDK形式)
│   │
│   ├── Game.Server/                    # ASP.NET Core 9 サーバー
│   │   ├── Controllers/
│   │   ├── Services/
│   │   └── Program.cs
│   │
│   ├── Game.Shared/                    # 共有ライブラリ
│   │   ├── Game.Shared.csproj          .NET プロジェクト
│   │   ├── package.json                Unity パッケージ定義
│   │   └── Runtime/
│   │       └── Shared/
│   │           ├── Enums/              AudioCategory等
│   │           └── MasterData/         マスターデータ定義
│   │
│   └── Game.Tools/                     # CLIツール(.NET 9)
│
├── masterdata/                         # Protobufスキーマ + TSVデータ
│
├── docker/                             # Docker構成
│   ├── unity-ci/                       # Unity CI Runner
│   └── game-server/                    # Game.Server用
│
├── docs/                               # 技術ドキュメント
│
├── scripts/                            # ビルド・フォーマットスクリプト
│
└── test/
    └── Game.Server.Tests/              # サーバーテスト
```

---

## パフォーマンス改善・検証サンプル
<details><summary>シーン遷移機能</summary>

* GameSceneService
  - 各種シーン遷移機能をTaskからUniTaskへ変更し、パフォーマンス改善を検証
  - イテレーション数: 10,000
  - CPU実行時間が約40%削減、ゼロアロケーション化、メモリ使用量100%削減

</details>

<details><summary>ステートマシーン</summary>

* 改善項目
  - ステート管理をHashSet→Dictionaryに変更、ステート検索がO(n)からO(1)に改善
  - 遷移時のDictionary検索回数を削減、LINQ使用箇所を改善しアロケーション削減
  - メソッドのインライン化でオーバーヘッドを削減

* 状態遷移のスループット向上
  - イテレーション数: 30,000
  - 遷移時間が平均15%短縮、スループットが平均15%向上

  | 項目 | 旧StateMachine | 新StateMachine | 改善率 |
  |:-----|---------------:|---------------:|-------:|
  | 総実行時間 (ms) | 44.848 | 35.295 | 1.27x |
  | 平均遷移時間 (μs) | 0.300 | 0.146 | 2.05x |
  | スループット (ops/s) | 668,934 | 849,991 | 1.27x |

* 状態遷移のメモリアロケーション改善
  - イテレーション数: 10,000

  | 項目 | 旧StateMachine | 新StateMachine | 改善率 |
  |:-----|---------------:|---------------:|-------:|
  | メモリ (bytes) | 2,760,704 | 1,290,240 | 2.14x |

</details>

---

## 使用言語/ライブラリ/ツール

| 言語・フレームワーク等   | バージョン   |
|----------------------|------------|
| Unity                | 6000.3.2f1 |
| .NET SDK             | 9.0        |
| C#                   | 9.0        |
| cysharp/MessagePipe  | 1.8.1      |
| cysharp/R3           | 1.3.0      |
| cysharp/UniTask      | 2.5.10     |
| cysharp/MasterMemory | 3.0.4      |
| cysharp/MessagePack  | 3.1.3      |
| cysharp/MemoryPack   | 1.21.3     |
| hadashiA/VContainer  | 1.17.0     |
| NSubstitute          | 5.3.0      |
| xUnit                | 2.x        |
| DOTween              | 1.2.790    |
| HotReload            | 1.13.13    |
| JetBrains Rider      | 2025.3.0.2 |
| Claude Code          | -          |

---

## 主なライブラリ・ツール採用理由
* **VContainer**: MVPパターンにおける依存性注入(DI)コンテナとして。
* **MessagePipe**: 疎結合なメッセージング処理(Pub/Sub)のため。
* **R3**: 複雑な非同期イベント処理、保守性/再利用性の向上のため。
* **UniTask**: Unityに最適化された非同期処理全般のため。
* **MasterMemory**: ゲームロジックとデータを分離し、開発サイクルを効率化するため。
* **MemoryPack**: セーブデータの高速バイナリシリアライズ。
* **xUnit**: サーバーサイドテスト用フレームワーク。

---

## アセット
* 主にUnityAssetStoreのもので自作は含まれません
* Unityちゃん: https://unity-chan.com/ (© Unity Technologies Japan/UCL)

---

## 制作期間
* 約4週間 (2026/1/24時点)

---

## 今後の予定
* ネットワーク機能（クライアント・サーバー通信）
* サバイバーゲームモード追加機能（スキルシステム、ボス戦等）
* PlayerLoopへの介入サンプル
* リストのソート／フィルタ機能サンプル
* マルチ解像度対応

---

## デモゲームについて
### タイムアタック（MVC）
* 制限時間内に全3ステージに配置されたアイテムを規定数集めるタイムアタックです
* 動作環境: PC／マウス&キーボード
* 操作: 移動(WASD), ジャンプ(Space), 走る(LShift+移動), カメラ操作(マウスドラッグ)

### サバイバー（MVP）
* VContainerを用いたMVPパターンで実装
* ウェーブ制の敵を倒しながら生き残るサバイバーゲーム
* 動作環境: PC／マウス&キーボード
* 操作: 移動(WASD), ダッシュ(LShift+移動)
* 主要機能:
  - 自動攻撃武器システム（マスターデータ駆動）
  - ウェーブ管理（敵の段階的出現）
  - アイテムドロップ・吸引
  - ステージクリア・記録保存

### ダウンロード
* 実行形式: [デモゲームDLリンク](https://drive.google.com/file/d/1_9vWOvT8leUjd2jB5uTzziSyA5goPmJx/view?usp=drive_link) ※解凍できない場合は7Zipを推奨

---

## 詳細ドキュメント
- [アーキテクチャ詳細](ARCHITECTURE.md)

---

## ライセンス
[LICENSE](LICENSE)
