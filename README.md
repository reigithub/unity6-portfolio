## TL;DR
* このプロジェクトは主に個人または小規模のUnityゲーム開発におけるスターターキットを目指して作成されています
* コードの再利用性を高め、実装のしやすさや可読性、保守性が向上するような作りを意識しています
* インゲーム/アウトゲーム共にマスターデータで動作しています(データ駆動型)(調整中の部分を除く)
* **アセンブリ分割によるモジュラー設計**を採用し、MVC/MVP両パターンのゲームモードを共存可能
* **マスターデータ定義をローカルパッケージ**(com.rei.unity6library)として分離し、再利用性を向上
* 起動時のゲームモード選択画面から、異なるアーキテクチャのゲームを切り替えて起動可能
---

[English version is here](https://github.com/reigithub/unity6-sample/blob/master/README.en.md)

---
## 環境構築

### 必要環境
| 項目 | バージョン |
|-----|-----------|
| Unity | 6000.3.2f1 以上 |
| OS | Windows 10/11 |

### セットアップ手順
1. リポジトリをクローン
   ```bash
   git clone https://github.com/reigithub/unity6-sample.git
   ```
2. Unity Hub でプロジェクトを開く
3. 初回起動時、パッケージの復元に数分かかる場合があります
4. `Assets/ProjectAssets/UnityScenes/GameRootScene.unity` を開いて再生

### 注意事項
* NuGetForUnity経由でインストールされるパッケージがあるため、初回ビルド時にエラーが出る場合は再度ビルドしてください
* Addressablesのビルドが必要な場合は `Window > Asset Management > Addressables > Groups` からビルドを実行

---
## スクリーンショット

### MVC: ScoreTimeAttack（スコアアタックゲーム）
| タイトル | ゲームプレイ | リザルト |
|---------|------------|---------|
| ![タイトル](Documentation/Screenshots/mvc_title.png) | ![ゲームプレイ](Documentation/Screenshots/mvc_gameplay.png) | ![リザルト](Documentation/Screenshots/mvc_result.png) |

### MVP: Survivor（サバイバーゲーム）
| タイトル | ゲームプレイ | レベルアップ |
|---------|------------|-------------|
| ![タイトル](Documentation/Screenshots/mvp_title.png) | ![ゲームプレイ](Documentation/Screenshots/mvp_gameplay.png) | ![レベルアップ](Documentation/Screenshots/mvp_levelup.png) |

### シェーダー・エフェクト
| トゥーンシェーダー | ディゾルブエフェクト |
|------------------|-------------------|
| ![トゥーン](Documentation/Screenshots/shader_toon.png) | ![ディゾルブ](Documentation/Screenshots/shader_dissolve.png) |

### エディター拡張
![エディターウィンドウ](Documentation/Screenshots/editor_window.png)

---
## ゲームプレイ動画

### MVC: ScoreTimeAttack
![MVCゲームプレイ](Documentation/GIFs/mvc_gameplay.gif)

### MVP: Survivor
![MVPゲームプレイ](Documentation/GIFs/mvp_gameplay.gif)

### シーン遷移・エフェクト
| シーン遷移 | エフェクト集 |
|-----------|-------------|
| ![シーン遷移](Documentation/GIFs/scene_transition.gif) | ![エフェクト](Documentation/GIFs/effects_showcase.gif) |

### エディターツール
![エディターツール](Documentation/GIFs/editor_tool.gif)

---
## アーキテクチャ概要
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
              │    (共通ユーティリティ)       │
              └─────────────────────────────┘
                              ↓
              ┌─────────────────────────────┐
              │  com.rei.unity6library      │
              │  (ローカルパッケージ)         │
              │  マスターデータ定義/Enum     │
              └─────────────────────────────┘
```
---
## 機能一覧
* **ゲームモード選択システム**: 起動時のタイトル画面から異なるアーキテクチャのゲームモードを選択可能
* **アセンブリ分割設計**: MVC/MVPパターンを独立したアセンブリで管理し、循環参照を防止
* **ローカルパッケージ分離**: マスターデータ定義をローカルパッケージ(com.rei.unity6library)として分離し、再利用性を向上
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
| Game.Shared | 共通ユーティリティ、インターフェース | Unity6Library |
| Game.App | エントリーポイント、ゲームモード選択 | Shared, MVC.*, MVP.* |
| Game.MVC.Core | MVCパターン基盤、GameServiceManager | Shared, Unity6Library |
| Game.MVC.ScoreTimeAttack | タイムアタックゲーム実装 | Shared, MVC.Core, Unity6Library |
| Game.MVP.Core | MVPパターン基盤、VContainer | Shared |
| Game.MVP.Survivor | サバイバーゲーム実装 | Shared, MVP.Core |
| **com.rei.unity6library** | マスターデータ定義、共通Enum | なし（最下層） |

</details>

<details><summary>ローカルパッケージ (com.rei.unity6library)</summary>

マスターデータ定義ファイルをローカルパッケージとして分離し、以下のメリットを実現：

1. **再利用性**: 複数のプロジェクトで同じマスターデータ定義を共有可能
2. **依存関係の明確化**: 最下層に配置することで循環参照を防止
3. **ビルド時間短縮**: 変更頻度の低いコードを分離することでインクリメンタルビルドを効率化
4. **バージョン管理**: パッケージ単位でバージョン管理が可能

**含まれるコンテンツ:**
- MasterMemory用マスターデータ定義クラス（AudioMaster, ScoreTimeAttackStageMaster等）
- 共通Enum定義（AudioCategory, AudioPlayTag等）

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
## 機能コードリンク
### Game.App（エントリーポイント）
* ゲームブートストラップ : [GameBootstrap.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/App/Bootstrap/GameBootstrap.cs)
* ゲームモードランチャー管理 : [GameModeLauncherRegistry.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/App/Launcher/GameModeLauncherRegistry.cs)
* アプリタイトル画面 : [AppTitleSceneComponent.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/App/Title/AppTitleSceneComponent.cs)

### Game.Shared（共通）
* アプリケーションイベント : [ApplicationEvents.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/Shared/Bootstrap/ApplicationEvents.cs)
* ゲームモードランチャーIF : [IGameModeLauncher.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/Shared/Bootstrap/IGameModeLauncher.cs)
* ステートマシーン : [StateMachine.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/Shared/StateMachine.cs)

### Game.MVC.Core（MVC基盤）
* シーン遷移サービス : [GameSceneService.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVC/Core/Services/GameSceneService.cs)
* シーン基底クラス : [GameScene.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVC/Core/Scenes/GameScene.cs)
* サービスマネージャー : [GameServiceManager.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVC/Core/Services/GameServiceManager.cs)

### Game.MVC.ScoreTimeAttack（タイムアタックゲーム）
* ランチャー : [ScoreTimeAttackLauncher.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVC/ScoreTimeAttack/ScoreTimeAttackLauncher.cs)
* プレイヤー制御 : [SDUnityChanPlayerController.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVC/ScoreTimeAttack/Player/SDUnityChanPlayerController.cs)

### Game.MVP.Core（MVP基盤）
* VContainerランチャー : [VContainerGameLauncher.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVP/Core/DI/VContainerGameLauncher.cs)

### Game.MVP.Survivor（サバイバーゲーム）
* ステージシーン : [SurvivorStageScene.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVP/Survivor/Scenes/SurvivorStageScene.cs)
* プレイヤー制御 : [SurvivorPlayerController.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVP/Survivor/Player/SurvivorPlayerController.cs)
* 敵AI : [SurvivorEnemyController.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVP/Survivor/Enemy/SurvivorEnemyController.cs)
* 敵スポーナー : [SurvivorEnemySpawner.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVP/Survivor/Enemy/SurvivorEnemySpawner.cs)
* 武器基底 : [SurvivorWeaponBase.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVP/Survivor/Weapon/SurvivorWeaponBase.cs)
* 汎用プール : [WeaponObjectPool.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVP/Survivor/Weapon/WeaponObjectPool.cs)
* アイテムスポーナー : [SurvivorItemSpawner.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVP/Survivor/Item/SurvivorItemSpawner.cs)
* セーブサービス : [SurvivorSaveService.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVP/Survivor/SaveData/SurvivorSaveService.cs)

### Game.Shared（共通・追加）
* 戦闘インターフェース : [ICombatTarget.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/Shared/Combat/ICombatTarget.cs)
* 死亡イベント : [IDeathNotifier.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/Shared/Events/IDeathNotifier.cs)
* ロックオン : [ILockOnService.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/Shared/LockOn/ILockOnService.cs)

### Editor
* マスターデータエディタ拡張 : [MasterDataWindow.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Editor/EditorWindow/MasterDataWindow.cs)

### com.rei.unity6library（ローカルパッケージ）
* オーディオマスター定義 : [AudioMaster.cs](https://github.com/reigithub/unity6-sample/blob/master/Packages/com.rei.unity6library/Runtime/Shared/MasterData/MemoryTables/AudioMaster.cs)
* ステージマスター定義 : [ScoreTimeAttackStageMaster.cs](https://github.com/reigithub/unity6-sample/blob/master/Packages/com.rei.unity6library/Runtime/Shared/MasterData/MemoryTables/ScoreTimeAttackStageMaster.cs)
* オーディオEnum定義 : [AudioEnums.cs](https://github.com/reigithub/unity6-sample/blob/master/Packages/com.rei.unity6library/Runtime/Shared/Enums/AudioEnums.cs)

---
## 主なフォルダ構成
```
.
├── Assets
│   ├── MasterData          マスターデータ(TSV, バイナリ)
│   ├── Programs
│   │   ├── Editor          エディタ拡張
│   │   │   └── Tests       単体テスト／パフォーマンス改善テストツール
│   │   └── Runtime
│   │       ├── Shared      共通ユーティリティ、インターフェース
│   │       │   ├── Bootstrap   IGameLauncher, ApplicationEvents
│   │       │   ├── Combat      ICombatTarget, IDamageable等
│   │       │   ├── Constants   共通定数, LayerMaskConstants
│   │       │   ├── Enums       GameMode等
│   │       │   ├── Events      DeathEventData, IDeathNotifier
│   │       │   ├── Extensions  拡張メソッド
│   │       │   ├── LockOn      ロックオンサービス
│   │       │   └── SaveData    セーブデータ基盤
│   │       ├── App         エントリーポイント
│   │       │   ├── Bootstrap   GameBootstrap
│   │       │   ├── Launcher    GameModeLauncherRegistry
│   │       │   └── Title       アプリタイトル画面
│   │       ├── MVC         MVCパターン実装
│   │       │   ├── Core        基盤(Services, Scenes, MessagePipe)
│   │       │   └── ScoreTimeAttack  タイムアタックゲーム
│   │       └── MVP         MVPパターン実装
│   │           ├── Core        基盤(VContainer, Base)
│   │           └── Survivor    サバイバーゲーム
│   │               ├── DI          依存性注入設定
│   │               ├── Enemy       敵システム(AI, スポーナー)
│   │               ├── Item        アイテム(スポーナー, ドロップ)
│   │               ├── Models      ゲームモデル
│   │               ├── Player      プレイヤー制御
│   │               ├── SaveData    セーブ機能
│   │               ├── Scenes      シーン管理
│   │               ├── Services    ゲームサービス
│   │               └── Weapon      武器システム(プール, 弾)
│   └── README.md
└── Packages
    └── com.rei.unity6library   ローカルパッケージ
        └── Runtime
            └── Shared
                ├── Enums           AudioCategory, AudioPlayTag等
                └── MasterData
                    └── MemoryTables マスターデータ定義クラス
```

## パフォーマンス改善・検証サンプル
<details><summary>シーン遷移機能</summary>

* GameSceneService
  - 各種シーン遷移機能をTaskからUniTaskへ変更し、パフォーマンス改善を検証
  - イテレーション数: 10,000
  - CPU実行時間が約40%削減、ゼロアロケーション化、メモリ使用量100%削減
  - !["テスト結果"](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Editor/Tests/Logs/GameSceneServicePerformanceTests_2026-01-08_220131.png)
  - !["テスト結果"](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Editor/Tests/Logs/GameSceneServicePerformanceTests_2026-01-09_015400.png)

</details>

<details><summary>ステートマシーン</summary>

* 改善項目
  - ステート管理をHashSet→Dictionaryに変更、ステート検索がO(n)からO(1)に改善（ステート数に依存しない一定時間）
  - 遷移時のDictionary検索回数を削減、LINQ使用箇所を改善しアロケーション削減
  - メソッドのインライン化でオーバーヘッドを削減

* 状態遷移のスループット向上
  - イテレーション数: 30,000
  - 遷移時間が平均15%短縮、スループットが平均15%向上
  - ベンチマーク結果(ゲームループに違い総合結果)

  | 項目 | 旧StateMachine | 新StateMachine | 改善率 |
  |:-----|---------------:|---------------:|-------:|
  | 総実行時間 (ms) | 44.848 | 35.295 | 1.27x |
  | 平均遷移時間 (μs) | 0.300 | 0.146 | 2.05x |
  | P99遷移時間 (μs) | 0.500 | 0.300 | 1.67x |
  | 最大遷移時間 (μs) | 9.500 | 5.100 | 1.86x |
  | スループット (ops/s) | 668,934 | 849,991 | 1.27x |
  | 遷移/秒 | 200,680 | 254,997 | 1.27x |
  | メモリ (bytes) | 401,408 | 401,408 | 1.00x |
  | GC発生回数 | 1 | 1 | 0 |

* 状態遷移のメモリアロケーション改善
  - イテレーション数: 10,000
  - メモリアロケーション比較結果(純粋な遷移リクエスト実行時)

  | 項目 | 旧StateMachine | 新StateMachine | 改善率 |
  |:-----|---------------:|---------------:|-------:|
  | メモリ (bytes) | 2,760,704 | 1,290,240 | 2.14x |
  | バイト/イテレーション | 276.07 | 129.02 | 2.14x |

</details>

---
## 使用言語/ライブラリ/ツール

| 言語・フレームワーク等   | バージョン   |
|----------------------|------------|
| Unity                | 6000.3.2f1 |
| C#                   | 9.0        |
| cysharp/MessagePipe  | 1.8.1      |
| cysharp/R3           | 1.3.0      |
| cysharp/UniTask      | 2.5.10     |
| cysharp/MasterMemory | 3.0.4      |
| cysharp/MessagePack  | 3.1.3      |
| cysharp/MemoryPack   | 1.21.3     |
| **hadashiA/VContainer** | **1.16.8** |
| NSubstitute          | 5.3.0      |
| DOTween              | 1.2.790    |
| HotReload            | 1.13.13    |
| JetBrains Rider      | 2025.3.0.2 |
| VSCode               | 1.107.1    |
| Claude Code          | -          |
---
## 主なライブラリ・ツール採用理由・使用目的
* **VContainer**: MVPパターンにおける依存性注入(DI)コンテナとして。コンストラクタインジェクションによるテスタビリティ向上、ライフサイクル管理の自動化のため。
* MessagePipe: MessageBrokerを用いたUIイベント、ゲームイベントの疎結合なメッセージング処理(Pub/Sub)のため。
* R3 : UIボタンの押下間隔の設定や複雑な非同期イベント処理、Animatorステート等のイベント合成が簡潔に記述可能。保守性/再利用性の向上のため。
* UniTask : Unityに最適化された非同期処理全般のため。現在は主にダイアログのエラーハンドリングに使用しており、随時利用範囲拡大予定。
* MasterMemory: ゲームロジックとデータを分離し、ロジック修正を抑えつつ、開発サイクルを効率化するため。また、デモゲームが大量の音声ファイル(約400個)を使用するため。
* MessagePack: 主にMasterMemoryのデータシリアライザーとして。
* **MemoryPack**: セーブデータの高速バイナリシリアライズ。ゼロアロケーションでPlayerPrefsより高性能。
* NSubstitute: テストコードでゲームサービス等のモック作成
* Claude Code: テストコード生成、リファクタリング

### 代替案との比較
| 技術 | 採用理由 | 比較した代替案 |
|-----|---------|---------------|
| VContainer | 軽量、Source Generator対応、高速 | Zenject（重い）, Extenject |
| UniTask | ゼロアロケーション、Unity最適化 | Task/async標準（GC発生）, Coroutine（可読性低） |
| R3 | UniRx後継、UniTask統合、アクティブ開発 | UniRx（開発停滞）, System.Reactive |
| MasterMemory | 高速読み取り、型安全、IL2CPP対応 | ScriptableObject（大量データ非効率）, JSON |
| MemoryPack | 最速シリアライザ、ゼロアロケーション | MessagePack, JSON（低速） |

---
## アセット
* 主にUnityAssetStoreのもので自作は含まれません
* Unityちゃん: https://unity-chan.com/ (© Unity Technologies Japan/UCL)
---
## 制作期間
* 約4週間 (2026/1/24時点)
---
## 実装済み機能（今後の予定から移動）
* ✅ **サバイバーゲームモード実装（MVP/VContainer）** - 基本システム完成
* ✅ **MemoryPackを用いたセーブ機能** - ステージ進行・クリア記録保存

## 今後の予定
* サバイバーゲームモード追加機能（スキルシステム、ボス戦等）
* PlayerLoopへの介入サンプル
* EnhancedScroller実装サンプル
* リストのソート／フィルタ機能サンプル
* オーディオ音量オプション画面
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
