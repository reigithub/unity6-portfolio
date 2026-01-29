# Unity6Portfolio アーキテクチャ設計書

**バージョン**: 1.1
**最終更新**: 2026年1月29日

---

## 目次

1. [設計思想](#1-設計思想)
2. [システム全体図](#2-システム全体図)
3. [モノレポ構成](#3-モノレポ構成)
4. [アセンブリ構成](#4-アセンブリ構成)
5. [MVC vs MVP 比較](#5-mvc-vs-mvp-比較)
6. [シーン遷移設計](#6-シーン遷移設計)
7. [データフロー](#7-データフロー)
8. [クラス設計（UML）](#8-クラス設計uml)
9. [シーケンス図](#9-シーケンス図)
10. [CI/CD・品質管理](#10-cicd品質管理)
11. [設計判断の記録](#11-設計判断の記録)

---

## 1. 設計思想

### 1.1 アーキテクチャ選定の背景

本プロジェクトは**2つの異なるアーキテクチャパターン**（MVC/MVP）を意図的に採用しています。

| パターン | ゲームモード | 目的 |
|---------|-------------|------|
| **MVC** | ScoreTimeAttack | レガシー環境（uGUI中心）への適応スキル提示 |
| **MVP** | Survivor | モダン環境（VContainer + UIToolkit）への適応スキル提示 |

### 1.2 設計原則

```
┌─────────────────────────────────────────────────────────────┐
│  SOLID原則の適用                                            │
├─────────────────────────────────────────────────────────────┤
│  S: 単一責任 - Service/Scene/Componentの明確な役割分離      │
│  O: 開放閉鎖 - インターフェースによる拡張性確保             │
│  L: リスコフ - GameScene継承階層の置換可能性                │
│  I: インターフェース分離 - 細粒度のサービスインターフェース  │
│  D: 依存性逆転 - DIコンテナによる依存関係の制御             │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. システム全体図

### 2.1 レイヤードアーキテクチャ

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Application Layer                            │
│  ┌──────────────────────┐    ┌──────────────────────┐              │
│  │   GameRootScene      │    │  GameModeLauncher    │              │
│  │   (常駐シーン)        │───▶│  Registry            │              │
│  └──────────────────────┘    └──────────────────────┘              │
│              │                         │                            │
│              ▼                         ▼                            │
│  ┌──────────────────────┐    ┌──────────────────────┐              │
│  │  MVC GameLauncher    │    │  MVP GameLauncher    │              │
│  │  (ScoreTimeAttack)   │    │  (Survivor)          │              │
│  └──────────────────────┘    └──────────────────────┘              │
├─────────────────────────────────────────────────────────────────────┤
│                         Scene Layer                                 │
│  ┌──────────────────────┐    ┌──────────────────────┐              │
│  │  GameSceneService    │    │  GameSceneService    │              │
│  │  (MVC版)             │    │  (MVP版/VContainer)  │              │
│  └──────────────────────┘    └──────────────────────┘              │
│              │                         │                            │
│              ▼                         ▼                            │
│  ┌──────────────────────┐    ┌──────────────────────┐              │
│  │  GameScene           │    │  GameScene           │              │
│  │  (Prefab/Unity)      │    │  (Prefab + DI)       │              │
│  └──────────────────────┘    └──────────────────────┘              │
├─────────────────────────────────────────────────────────────────────┤
│                        Service Layer                                │
│  ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌────────────┐       │
│  │ AudioSvc   │ │ SaveSvc    │ │ MasterData │ │ LockOnSvc  │       │
│  └────────────┘ └────────────┘ └────────────┘ └────────────┘       │
├─────────────────────────────────────────────────────────────────────┤
│                      Infrastructure Layer                           │
│  ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌────────────┐       │
│  │Addressables│ │MasterMemory│ │ MemoryPack │ │ MessagePipe│       │
│  └────────────┘ └────────────┘ └────────────┘ └────────────┘       │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.2 コンポーネント関係図

```mermaid
graph TB
    subgraph "Entry Point"
        GRS[GameRootScene<br/>常駐]
        GML[GameModeLauncherRegistry]
    end

    subgraph "MVC Mode"
        MVCL[ScoreTimeAttack<br/>GameLauncher]
        GSM[GameServiceManager]
        MVCS[GameSceneService<br/>MVC版]
    end

    subgraph "MVP Mode"
        MVPL[Survivor<br/>GameLauncher]
        VC[VContainer<br/>LifetimeScope]
        MVPS[GameSceneService<br/>MVP版]
    end

    subgraph "Shared Services"
        AS[AudioService]
        SS[SaveService]
        MDS[MasterDataService]
        AAS[AddressableAssetService]
    end

    GRS --> GML
    GML --> MVCL
    GML --> MVPL

    MVCL --> GSM
    GSM --> MVCS
    GSM --> AS
    GSM --> SS
    GSM --> MDS

    MVPL --> VC
    VC --> MVPS
    VC --> AS
    VC --> SS
    VC --> MDS

    MVCS --> AAS
    MVPS --> AAS
```

---

## 3. モノレポ構成

### 3.1 プロジェクト構成

本プロジェクトはモノレポ構成を採用し、クライアント・サーバー・共有ライブラリを1つのリポジトリで管理しています。

```
Unity6Portfolio/
├── src/
│   ├── Game.Client/        # Unity クライアント (Unity 6)
│   │   ├── Assets/
│   │   │   └── Programs/   # ゲームコード
│   │   └── Packages/
│   │
│   ├── Game.Server/        # ゲームサーバー (ASP.NET Core 8)
│   │   ├── Controllers/
│   │   ├── Services/
│   │   └── Program.cs
│   │
│   └── Game.Shared/        # 共有ライブラリ (.NET + Unity Package)
│       ├── Runtime/
│       │   └── Shared/
│       │       ├── Enums/        # AudioCategory等
│       │       └── MasterData/   # マスターデータ定義
│       ├── Game.Shared.csproj    # .NET プロジェクト
│       └── package.json          # Unity パッケージ定義
│
├── test/
│   └── Game.Server.Tests/  # サーバーテスト
│
├── docs/                   # ドキュメント
├── docker/                 # Docker設定
├── scripts/                # ビルド・フォーマットスクリプト
└── .github/
    └── workflows/          # GitHub Actions
```

### 3.2 Game.Shared の役割

マスターデータ定義を共有ライブラリとして分離し、以下のメリットを実現:

| メリット | 説明 |
|---------|------|
| クライアント・サーバー共有 | 同じDTOをUnityとASP.NET Coreで共有可能 |
| 依存関係の明確化 | 最下層に配置することで循環参照を防止 |
| ビルド時間短縮 | 変更頻度の低いコードを分離 |
| バージョン管理 | パッケージ単位でバージョン管理が可能 |

### 3.3 プロジェクト間依存関係

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

---

## 4. アセンブリ構成

### 4.1 アセンブリ依存関係図

```
                    ┌─────────────────┐
                    │    Game.App     │
                    │   (起動制御)     │
                    └────────┬────────┘
                             │
            ┌────────────────┼────────────────┐
            │                │                │
            ▼                ▼                ▼
┌───────────────────┐ ┌───────────────┐ ┌───────────────────┐
│Game.MVC.ScoreTime │ │ Game.MVP.Core │ │ Game.MVP.Survivor │
│      Attack       │ │  (VContainer) │ │    (ゲーム実装)    │
└─────────┬─────────┘ └───────┬───────┘ └─────────┬─────────┘
          │                   │                   │
          ▼                   │                   │
┌───────────────────┐         │                   │
│  Game.MVC.Core    │         │                   │
│  (MessagePipe)    │         │                   │
└─────────┬─────────┘         │                   │
          │                   │                   │
          └─────────┬─────────┴───────────────────┘
                    │
                    ▼
          ┌─────────────────┐
          │   Game.Shared   │
          │   (共通基盤)     │
          └─────────────────┘
                    │
                    ▼
          ┌─────────────────┐
          │  Unity6Library  │
          │ (MasterMemory等)│
          └─────────────────┘
```

### 4.2 アセンブリ詳細

#### ランタイムアセンブリ

| アセンブリ | 役割 | 主要な依存 |
|-----------|------|-----------|
| **Game.Shared** | 共通基盤・インターフェース定義 | UniTask, R3, MessagePipe, Addressables |
| **Game.MVC.Core** | MVCパターン基盤 | Game.Shared, MessagePipe.Unity |
| **Game.MVC.ScoreTimeAttack** | スコアアタックゲーム実装 | Game.MVC.Core, UnityChan |
| **Game.MVP.Core** | MVPパターン基盤 | Game.Shared, VContainer, MessagePipe.VContainer |
| **Game.MVP.Survivor** | サバイバーゲーム実装 | Game.MVP.Core, AI.Navigation, Cinemachine |
| **Game.App** | アプリケーション起動制御 | 全アセンブリ参照 |

#### テストアセンブリ

| アセンブリ | 役割 | テスト数 |
|-----------|------|---------|
| **Game.Tests.Shared** | Shared層ユニットテスト | 100+ |
| **Game.Tests.MVC** | MVC層ユニットテスト | 150+ |
| **Game.Tests.MVP** | MVP層ユニットテスト | 170+ |
| **Game.Tests.PlayMode** | 統合・PlayModeテスト | 63 |

**合計テスト数**: 485テスト（EditMode 422 + PlayMode 63）

### 4.3 循環参照防止設計

```
【設計ルール】
1. Shared → 他アセンブリへの参照禁止
2. Core → 同レベルCoreへの参照禁止（MVC.Core ⇔ MVP.Core）
3. ゲーム実装 → 他ゲーム実装への参照禁止
4. App → 全体の結合点として例外的に全参照許可
```

---

## 5. MVC vs MVP 比較

### 5.1 アーキテクチャ比較表

| 観点 | MVC (ScoreTimeAttack) | MVP (Survivor) |
|-----|----------------------|----------------|
| **DI方式** | GameServiceManager（手動） | VContainer（自動） |
| **UI技術** | uGUI + TextMeshPro | uGUI + TextMeshPro |
| **状態管理** | StateMachine | StateMachine + R3 Reactive |
| **メッセージング** | MessagePipe（直接参照） | MessagePipe（DI注入） |
| **シーン読み込み** | Addressables直接呼び出し | IAddressableAssetService経由 |
| **衝突イベント** | IPlayerCollisionHandler（直接呼出） | MessagePipe経由 |
| **テスト容易性** | 中（サービスロケータ依存） | 高（完全DI） |

### 5.2 DI方式の違い

#### MVC: GameServiceManager（サービスロケータパターン）

```csharp
// サービス登録
GameServiceManager.Add<AudioService>();

// サービス取得
var audioService = GameServiceManager.Get<AudioService>();
```

#### MVP: VContainer（依存性注入パターン）

```csharp
// LifetimeScopeで登録
public class SurvivorLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<IAudioService, AudioService>(Lifetime.Singleton);
    }
}

// コンストラクタ注入
public class SurvivorStagePresenter
{
    private readonly IAudioService _audioService;

    [Inject]
    public SurvivorStagePresenter(IAudioService audioService)
    {
        _audioService = audioService;
    }
}
```

### 5.3 シーン管理の違い

```
【MVC】GamePrefabScene
┌─────────────────────────────────────────────┐
│ 1. AssetService.LoadAssetAsync<GameObject>  │
│ 2. Object.Instantiate(_asset)               │
│ 3. GetSceneComponent() で取得               │
│ ※ DIなし、直接参照                          │
└─────────────────────────────────────────────┘

【MVP】GamePrefabScene
┌─────────────────────────────────────────────┐
│ 1. AssetService.LoadAssetAsync<GameObject>  │
│ 2. Object.Instantiate(_asset)               │
│ 3. Resolver.InjectGameObject(_instance)     │  ← DI注入
│ 4. GetSceneComponent() + Resolver.Inject()  │  ← Component注入
└─────────────────────────────────────────────┘
```

---

## 6. シーン遷移設計

### 6.1 シーンライフサイクル

```
┌─────────────────────────────────────────────────────────────┐
│                    GameScene Lifecycle                      │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   PreInitialize()  ─▶  サーバー通信、モデル初期化          │
│         │                                                   │
│         ▼                                                   │
│   LoadAsset()      ─▶  Prefab/UnityScene読み込み           │
│         │                                                   │
│         ▼                                                   │
│   Startup()        ─▶  View初期化、イベント登録            │
│         │                                                   │
│         ▼                                                   │
│   Ready()          ─▶  開始演出、ゲーム開始                │
│         │                                                   │
│    ┌────┴────┐                                              │
│    ▼         ▼                                              │
│  Sleep()   Restart()  ─▶  ダイアログ表示時など             │
│    │         │                                              │
│    └────┬────┘                                              │
│         ▼                                                   │
│   Terminate()      ─▶  リソース解放、シーン破棄            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 6.2 シーン遷移フロー図

#### MVC: ScoreTimeAttack

```mermaid
stateDiagram-v2
    [*] --> TitleScene: 起動
    TitleScene --> StageSelectScene: ゲーム開始
    StageSelectScene --> StageScene: ステージ選択
    StageScene --> ResultScene: ゲーム終了
    ResultScene --> TitleScene: タイトルへ
    ResultScene --> StageScene: リトライ

    StageScene --> SettingsDialog: 設定
    SettingsDialog --> StageScene: 閉じる
```

#### MVP: Survivor

```mermaid
stateDiagram-v2
    [*] --> TitleScene: 起動
    TitleScene --> StageScene: ゲーム開始
    StageScene --> ResultScene: ゲーム終了
    StageScene --> PauseDialog: ポーズ
    PauseDialog --> StageScene: 再開

    StageScene --> LevelUpDialog: レベルアップ
    LevelUpDialog --> StageScene: 選択完了

    StageScene --> WeaponReplaceDialog: 武器入替
    WeaponReplaceDialog --> StageScene: 選択完了

    ResultScene --> TitleScene: タイトルへ
    ResultScene --> StageScene: リトライ
```

### 6.3 シーン継承階層

```
IGameScene (interface)
    │
    ├── GameScene (abstract)
    │       │
    │       ├── GameScene<TScene, TComponent>
    │       │       │
    │       │       ├── GamePrefabScene<TScene, TComponent>
    │       │       │       └── ScoreTimeAttackTitleScene
    │       │       │       └── ScoreTimeAttackStageScene
    │       │       │       └── SurvivorTitleScene
    │       │       │       └── SurvivorStageScene
    │       │       │
    │       │       ├── GameUnityScene<TScene, TComponent>
    │       │       │       └── (ステージ背景用)
    │       │       │
    │       │       └── GameDialogScene<TScene, TComponent, TResult>
    │       │               └── SettingsDialog
    │       │               └── PauseDialog
    │       │               └── LevelUpDialog
    │       │
    │       └── GameUnityScene (コンポーネントなし)
    │               └── (環境シーン用)
```

---

## 7. データフロー

### 7.1 マスターデータフロー

```
┌─────────────────────────────────────────────────────────────────┐
│                    Master Data Flow                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐      │
│  │  Excel/CSV   │───▶│MasterMemory  │───▶│MemoryDatabase│      │
│  │  (設計時)    │    │  Generator   │    │ (実行時)     │      │
│  └──────────────┘    └──────────────┘    └──────────────┘      │
│                                                 │               │
│                                                 ▼               │
│                                    ┌──────────────────────┐    │
│                                    │ IMasterDataService   │    │
│                                    │ .MemoryDatabase      │    │
│                                    └──────────────────────┘    │
│                                                 │               │
│                      ┌──────────────────────────┼───────────┐  │
│                      ▼                          ▼           ▼  │
│              ┌─────────────┐          ┌─────────────┐ ┌──────┐ │
│              │WeaponMaster │          │EnemyMaster  │ │ ...  │ │
│              │  Table      │          │  Table      │ │      │ │
│              └─────────────┘          └─────────────┘ └──────┘ │
│                      │                          │               │
│                      ▼                          ▼               │
│              ┌─────────────┐          ┌─────────────┐          │
│              │WeaponSystem │          │EnemySpawner │          │
│              │ (Runtime)   │          │ (Runtime)   │          │
│              └─────────────┘          └─────────────┘          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 7.2 セーブデータフロー

```
┌─────────────────────────────────────────────────────────────────┐
│                    Save Data Flow                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────────┐                                               │
│  │  Game State  │  ←─ Score, Settings, Progress                │
│  └──────┬───────┘                                               │
│         │                                                       │
│         ▼                                                       │
│  ┌──────────────┐                                               │
│  │ SaveService  │                                               │
│  │   Base       │                                               │
│  └──────┬───────┘                                               │
│         │                                                       │
│         ▼                                                       │
│  ┌──────────────┐    ┌──────────────┐                          │
│  │  MemoryPack  │───▶│  Binary Data │                          │
│  │ Serializer   │    │  (高速)      │                          │
│  └──────────────┘    └──────┬───────┘                          │
│                             │                                   │
│                             ▼                                   │
│                    ┌──────────────┐                             │
│                    │PlayerPrefs/  │                             │
│                    │ File System  │                             │
│                    └──────────────┘                             │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 7.3 イベントフロー（MessagePipe）

```
┌─────────────────────────────────────────────────────────────────┐
│                 Event Flow (MessagePipe)                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Publisher                    Broker                Subscriber  │
│  ─────────                    ──────                ──────────  │
│                                                                 │
│  ┌─────────┐                ┌──────────┐         ┌───────────┐ │
│  │ Player  │──OnDamage────▶│MessagePipe│────────▶│ HUD       │ │
│  │Controller│               │ Service  │         │ (HP表示)   │ │
│  └─────────┘                └──────────┘         └───────────┘ │
│                                  │                              │
│  ┌─────────┐                     │               ┌───────────┐ │
│  │ Enemy   │──OnDeath─────▶     │    ────────▶│ Score     │ │
│  │ Manager │                     │               │ Manager   │ │
│  └─────────┘                     │               └───────────┘ │
│                                  │                              │
│  ┌─────────┐                     │               ┌───────────┐ │
│  │ Item    │──OnPickup────▶     │    ────────▶│ Inventory │ │
│  │ System  │                     │               │ System    │ │
│  └─────────┘                     ▼               └───────────┘ │
│                                                                 │
│  【MVC側の改善】OnTriggerEnter/OnCollisionEnterでの高頻度       │
│  イベントはIPlayerCollisionHandlerによる直接呼び出しに変更済み  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 8. クラス設計（UML）

### 8.1 サービス層クラス図

```
┌─────────────────────────────────────────────────────────────────┐
│                     Service Layer UML                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  <<interface>>              <<interface>>                       │
│  ┌───────────────┐          ┌───────────────────┐              │
│  │IGameService   │          │IGameSceneService   │              │
│  ├───────────────┤          ├───────────────────┤              │
│  │+Startup()     │          │+TransitionAsync() │              │
│  │+Shutdown()    │          │+TransitionPrevAsync()│           │
│  └───────┬───────┘          │+TerminateAsync()  │              │
│          │                  └─────────┬─────────┘              │
│          │                            │                         │
│          ▼                            ▼                         │
│  ┌───────────────┐          ┌───────────────────┐              │
│  │AudioService   │          │GameSceneService   │              │
│  ├───────────────┤          ├───────────────────┤              │
│  │-_bgmSource    │          │-_currentScenes    │              │
│  │-_sfxSources[] │          │-_history          │              │
│  ├───────────────┤          ├───────────────────┤              │
│  │+PlayBgmAsync()│          │+TransitionAsync() │              │
│  │+PlaySfxAsync()│          │+IsProcessing()    │              │
│  │+SetVolume()   │          │+TerminateAsync()  │              │
│  └───────────────┘          └───────────────────┘              │
│                                                                 │
│  <<interface>>              <<abstract>>                        │
│  ┌───────────────────┐      ┌───────────────────┐              │
│  │IMasterDataService │      │SaveServiceBase    │              │
│  ├───────────────────┤      ├───────────────────┤              │
│  │+MemoryDatabase    │      │#_storage          │              │
│  │+LoadMasterData()  │      │#_autoSaveInterval │              │
│  └───────────────────┘      ├───────────────────┤              │
│                             │+LoadAsync()       │              │
│                             │+SaveAsync()       │              │
│                             │#Serialize()       │              │
│                             │#Deserialize()     │              │
│                             └───────────────────┘              │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 8.2 武器システムクラス図（MVP Survivor）

```
┌─────────────────────────────────────────────────────────────────┐
│                    Weapon System UML                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────┐                                        │
│  │ SurvivorWeaponManager│ ◆────────────────┐                   │
│  ├─────────────────────┤                   │                    │
│  │-_weapons: List      │                   │ 1..*               │
│  │-_factory            │                   ▼                    │
│  ├─────────────────────┤       ┌─────────────────────┐         │
│  │+AddWeapon()         │       │<<abstract>>         │         │
│  │+RemoveWeapon()      │       │SurvivorWeaponBase   │         │
│  │+UpdateWeapons()     │       ├─────────────────────┤         │
│  └─────────────────────┘       │#_weaponMaster       │         │
│           │                    │#_levelMasters       │         │
│           │                    │#_damage, _cooldown  │         │
│           ▼                    ├─────────────────────┤         │
│  ┌─────────────────────┐       │+InitializeAsync()   │         │
│  │SurvivorWeaponFactory│       │+UpdateWeapon()      │         │
│  ├─────────────────────┤       │+LevelUp()           │         │
│  │-_resolver           │       │#TryAttack()*       │         │
│  ├─────────────────────┤       └──────────┬──────────┘         │
│  │+Create(masterId)    │                  │                    │
│  └─────────────────────┘                  │                    │
│                               ┌───────────┴───────────┐        │
│                               ▼                       ▼        │
│                   ┌─────────────────┐     ┌─────────────────┐  │
│                   │SurvivorAutoFire │     │SurvivorGround   │  │
│                   │    Weapon       │     │    Weapon       │  │
│                   ├─────────────────┤     ├─────────────────┤  │
│                   │-_pool           │     │-_pool           │  │
│                   │-_projectilePrefab│    │-_effectPrefab   │  │
│                   ├─────────────────┤     ├─────────────────┤  │
│                   │#TryAttack()     │     │#TryAttack()     │  │
│                   │-SpawnProjectile()│    │-SpawnEffect()   │  │
│                   └─────────────────┘     └─────────────────┘  │
│                           │                       │            │
│                           ▼                       ▼            │
│                   ┌─────────────────────────────────┐          │
│                   │     WeaponObjectPool<T>         │          │
│                   ├─────────────────────────────────┤          │
│                   │-_pool: Stack<T>                 │          │
│                   │-_activeCount                    │          │
│                   ├─────────────────────────────────┤          │
│                   │+Get(): T                        │          │
│                   │+Return(item: T)                 │          │
│                   └─────────────────────────────────┘          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 8.3 ステートマシンクラス図

```
┌─────────────────────────────────────────────────────────────────┐
│                   StateMachine UML                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  <<interface>>                                                  │
│  ┌─────────────────────┐                                        │
│  │IStateMachineContext │                                        │
│  │    <TContext>       │                                        │
│  ├─────────────────────┤                                        │
│  │+Context: TContext   │                                        │
│  └─────────────────────┘                                        │
│            △                                                    │
│            │                                                    │
│  ┌─────────┴───────────────────────────────────────────┐       │
│  │                                                     │       │
│  │  ┌───────────────────────────────┐                  │       │
│  │  │StateMachine<TContext, TEvent> │                  │       │
│  │  ├───────────────────────────────┤                  │       │
│  │  │-_states: Dictionary           │                  │       │
│  │  │-_transitionTable: Dictionary  │  O(1)遷移       │       │
│  │  │-_currentState: IState         │                  │       │
│  │  │-_nextState: IState            │                  │       │
│  │  ├───────────────────────────────┤                  │       │
│  │  │+AddTransition<TFrom,TTo>()    │                  │       │
│  │  │+SetInitState<T>()             │                  │       │
│  │  │+Transition(event): Result     │                  │       │
│  │  │+Update()                      │                  │       │
│  │  │+IsCurrentState<T>(): bool     │                  │       │
│  │  └───────────────────────────────┘                  │       │
│  │                 ◆                                   │       │
│  │                 │ 1..*                              │       │
│  │                 ▼                                   │       │
│  │  ┌───────────────────────────────┐                  │       │
│  │  │<<abstract>>                   │                  │       │
│  │  │State<TContext, TEvent>        │                  │       │
│  │  ├───────────────────────────────┤                  │       │
│  │  │#StateMachine                  │                  │       │
│  │  │+Context: TContext             │                  │       │
│  │  ├───────────────────────────────┤                  │       │
│  │  │+Enter()                       │                  │       │
│  │  │+Update()                      │                  │       │
│  │  │+FixedUpdate()                 │                  │       │
│  │  │+LateUpdate()                  │                  │       │
│  │  │+Exit()                        │                  │       │
│  │  └───────────────────────────────┘                  │       │
│  │                                                     │       │
│  └─────────────────────────────────────────────────────┘       │
│                                                                 │
│  【特徴】                                                        │
│  • 遷移テーブルによるO(1)状態遷移                                │
│  • ジェネリックによる型安全なコンテキスト共有                      │
│  • Enter/Exit/Update分離による明確なライフサイクル                 │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 8.4 コンバットシステムインターフェース

```
┌─────────────────────────────────────────────────────────────────┐
│                Combat System Interfaces                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  <<interface>>           <<interface>>                          │
│  ┌───────────────┐       ┌─────────────────┐                   │
│  │ ITargetable   │       │ ICombatTarget   │                   │
│  ├───────────────┤       ├─────────────────┤                   │
│  │+TargetPoint   │       │+IsAlive         │                   │
│  │+IsTargetable  │       │+CurrentHealth   │                   │
│  │+TargetPriority│       │+MaxHealth       │                   │
│  └───────┬───────┘       │+Team            │                   │
│          │               └────────┬────────┘                   │
│          │                        │                             │
│          └────────┬───────────────┘                             │
│                   │                                             │
│                   ▼                                             │
│          <<interface>>                                          │
│          ┌─────────────────┐                                    │
│          │  IDamageable    │                                    │
│          ├─────────────────┤                                    │
│          │+TakeDamage()    │                                    │
│          │+OnDeath         │                                    │
│          └────────┬────────┘                                    │
│                   │                                             │
│                   ▼                                             │
│          <<interface>>                                          │
│          ┌─────────────────┐                                    │
│          │ IKnockbackable  │                                    │
│          ├─────────────────┤                                    │
│          │+ApplyKnockback()│                                    │
│          │+KnockbackResist │                                    │
│          └─────────────────┘                                    │
│                                                                 │
│  【実装例】                                                      │
│  Enemy : MonoBehaviour, IDamageable, IKnockbackable             │
│  Player : MonoBehaviour, IDamageable, ICombatTarget             │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 9. シーケンス図

### 9.1 ゲーム起動シーケンス

```
┌─────────────────────────────────────────────────────────────────┐
│                  Game Startup Sequence                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  User    GameRoot   Registry   Launcher   Services   Scene      │
│   │         │          │          │          │         │        │
│   │ Start   │          │          │          │         │        │
│   │────────▶│          │          │          │         │        │
│   │         │ GetMode  │          │          │         │        │
│   │         │─────────▶│          │          │         │        │
│   │         │          │ Create   │          │         │        │
│   │         │          │─────────▶│          │         │        │
│   │         │          │          │ Startup  │         │        │
│   │         │          │          │─────────▶│         │        │
│   │         │          │          │          │         │        │
│   │         │          │          │    ┌─────┴─────┐   │        │
│   │         │          │          │    │Initialize │   │        │
│   │         │          │          │    │ Services  │   │        │
│   │         │          │          │    └─────┬─────┘   │        │
│   │         │          │          │          │         │        │
│   │         │          │          │ LoadMasterData     │        │
│   │         │          │          │─────────▶│         │        │
│   │         │          │          │          │         │        │
│   │         │          │          │ Transition│        │        │
│   │         │          │          │──────────────────▶│        │
│   │         │          │          │          │         │        │
│   │         │          │          │          │  ┌──────┴──────┐ │
│   │         │          │          │          │  │PreInit      │ │
│   │         │          │          │          │  │LoadAsset    │ │
│   │         │          │          │          │  │Startup      │ │
│   │         │          │          │          │  │Ready        │ │
│   │         │          │          │          │  └──────┬──────┘ │
│   │         │          │          │          │         │        │
│   │◀────────────────────────────────────────────────────│        │
│   │         │          │          │          │         │        │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 9.2 シーン遷移シーケンス

```
┌─────────────────────────────────────────────────────────────────┐
│               Scene Transition Sequence                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Caller   SceneService   CurrentScene   NewScene   AssetService │
│    │          │              │             │            │       │
│    │Transition│              │             │            │       │
│    │─────────▶│              │             │            │       │
│    │          │              │             │            │       │
│    │          │ Terminate    │             │            │       │
│    │          │─────────────▶│             │            │       │
│    │          │              │             │            │       │
│    │          │   ┌──────────┴──────────┐  │            │       │
│    │          │   │ Cleanup Resources   │  │            │       │
│    │          │   │ Unload Assets       │  │            │       │
│    │          │   └──────────┬──────────┘  │            │       │
│    │          │              │             │            │       │
│    │          │ new()        │             │            │       │
│    │          │─────────────────────────▶│            │       │
│    │          │              │             │            │       │
│    │          │ PreInitialize│             │            │       │
│    │          │─────────────────────────▶│            │       │
│    │          │              │             │            │       │
│    │          │ LoadAsset    │             │            │       │
│    │          │─────────────────────────▶│            │       │
│    │          │              │             │ LoadAsync  │       │
│    │          │              │             │───────────▶│       │
│    │          │              │             │◀───────────│       │
│    │          │              │             │            │       │
│    │          │ Startup      │             │            │       │
│    │          │─────────────────────────▶│            │       │
│    │          │              │             │            │       │
│    │          │ Ready        │             │            │       │
│    │          │─────────────────────────▶│            │       │
│    │          │              │             │            │       │
│    │◀─────────│              │             │            │       │
│    │          │              │             │            │       │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 9.3 ダメージ処理シーケンス（Survivor）

```
┌─────────────────────────────────────────────────────────────────┐
│                  Damage Processing Sequence                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Weapon   Projectile   Enemy    VFXSpawner   HUD   StageModel   │
│    │          │          │          │         │         │       │
│    │ Spawn    │          │          │         │         │       │
│    │─────────▶│          │          │         │         │       │
│    │          │          │          │         │         │       │
│    │          │OnTrigger │          │         │         │       │
│    │          │─────────▶│          │         │         │       │
│    │          │          │          │         │         │       │
│    │          │          │ ┌────────┴────────┐│         │       │
│    │          │          │ │TakeDamage()     ││         │       │
│    │          │          │ │- Calculate      ││         │       │
│    │          │          │ │- Apply Knockback││         │       │
│    │          │          │ └────────┬────────┘│         │       │
│    │          │          │          │         │         │       │
│    │          │          │ SpawnHitEffect    │         │       │
│    │          │          │─────────▶│         │         │       │
│    │          │          │          │         │         │       │
│    │          │          │ ShowDamageNumber  │         │       │
│    │          │          │─────────────────▶│         │       │
│    │          │          │          │         │         │       │
│    │          │          │          │         │         │       │
│    │          │          │ [if Dead]│         │         │       │
│    │          │          │──────────────────────────────▶│       │
│    │          │          │          │         │AddScore │       │
│    │          │          │          │         │AddExp   │       │
│    │          │          │          │         │         │       │
│    │          │ Return   │          │         │         │       │
│    │◀─────────│          │          │         │         │       │
│    │          │          │          │         │         │       │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 10. CI/CD・品質管理

### 10.1 CI/CD パイプライン

GitHub Actions を使用した自動化パイプラインを構築しています。

```
┌─────────────────────────────────────────────────────────────────┐
│                     CI/CD Pipeline                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Push/PR                                                         │
│    │                                                             │
│    ▼                                                             │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐       │
│  │ Code Quality │───▶│  Unit Tests  │───▶│ Integration  │       │
│  │    Check     │    │  (EditMode)  │    │   Tests      │       │
│  └──────────────┘    └──────────────┘    │ (PlayMode)   │       │
│    - .editorconfig      - 422 tests      └──────────────┘       │
│    - Roslyn Analyzer                       - 63 tests           │
│    - Format Check                                                │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                    実行環境                               │   │
│  │  - Docker コンテナ (game-ci/unity-editor)                 │   │
│  │  - Self-hosted Runner (Windows/Linux)                    │   │
│  │  - GitHub App 認証                                       │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 10.2 ワークフロー構成

| ワークフロー | トリガー | 内容 |
|-------------|---------|------|
| `unity-ci-docker.yml` | push/PR | メインCI（Docker/Linux） |
| `unity-test.yml` | PR | PR用テスト |
| `code-quality.yml` | push/PR (*.cs) | フォーマット・静的解析 |
| `pr-review.yml` | PR | 自動レビューコメント |

### 10.3 コード品質管理

#### .editorconfig 階層

```
Unity6Portfolio/
├── .editorconfig              # ルート（共通設定）
└── src/Game.Client/
    └── .editorconfig          # Unity固有設定（UNT*ルール）
```

#### Roslyn Analyzer

| Analyzer | 用途 |
|----------|------|
| Microsoft.Unity.Analyzers | Unity固有の問題検出（UNT*） |
| StyleCop.Analyzers | コーディングスタイル（オプション） |

### 10.4 テストカバレッジ

| カテゴリ | テスト数 | 内容 |
|---------|---------|------|
| EditMode | 422 | ユニットテスト（Service, Model, Extension） |
| PlayMode | 63 | 統合テスト（Scene, Input, UI） |
| **合計** | **485** | |

---

## 11. 設計判断の記録

### 11.1 ADR (Architecture Decision Records)

#### ADR-001: MVC/MVP両方の採用

| 項目 | 内容 |
|-----|------|
| **決定** | 1プロジェクト内にMVCとMVPの両アーキテクチャを実装 |
| **背景** | 転職ポートフォリオとして、異なる開発環境への適応力を示す必要があった |
| **選択肢** | A) MVCのみ B) MVPのみ C) 両方 |
| **判断理由** | 多くの現場がまだuGUI/レガシー構成であり、同時にモダン開発スキルも求められる |
| **影響** | コードベースの複雑化、学習コストの増加 |
| **状態** | 採用済み |

#### ADR-002: VContainer選定

| 項目 | 内容 |
|-----|------|
| **決定** | MVP側のDIコンテナにVContainerを採用 |
| **背景** | Unity向け軽量DIコンテナが必要 |
| **選択肢** | A) Zenject B) VContainer C) 手動DI |
| **判断理由** | Zenjectより軽量、ソースジェネレータ対応、日本コミュニティ活発 |
| **影響** | IL2CPP対応良好、起動時間短縮 |
| **状態** | 採用済み |

#### ADR-003: StateMachine自作

| 項目 | 内容 |
|-----|------|
| **決定** | 汎用StateMachineを自作実装 |
| **背景** | 軽量かつ型安全なステートマシンが必要 |
| **選択肢** | A) Unity標準Animator B) 外部ライブラリ C) 自作 |
| **判断理由** | O(1)遷移、ジェネリック対応、Animator依存排除 |
| **影響** | 柔軟性向上、学習曲線あり |
| **状態** | 採用済み |

#### ADR-004: MessagePipe選定

| 項目 | 内容 |
|-----|------|
| **決定** | Pub/SubメッセージングにMessagePipeを採用 |
| **背景** | コンポーネント間の疎結合な通信が必要 |
| **選択肢** | A) UniRx MessageBroker B) MessagePipe C) イベント直接登録 |
| **判断理由** | VContainer統合、型安全、フィルタリング機能 |
| **影響** | 高頻度イベント（衝突等）は直接呼び出しに変更済み |
| **状態** | 採用済み・改善完了 |

### 11.2 既知の技術的負債

| 項目 | 内容 | 優先度 | 状態 |
|-----|------|-------|------|
| ~~MessageBroker過剰使用~~ | ~~OnTriggerEnter等でのPublish~~ | ~~中~~ | ✅ 改善完了 |
| ~~テストカバレッジ~~ | ~~現状約20%~~ | ~~高~~ | ✅ 485テスト達成 |
| ~~XMLドキュメント~~ | ~~一部未記載~~ | ~~低~~ | ✅ 主要IF完了 |
| P3機能追加 | ネットワーク、ローカライズ等 | 低 | 未着手（オプション） |

**改善完了項目**:
- MessageBroker: IPlayerCollisionHandlerによる直接呼び出しに変更
- テスト: EditMode 422 + PlayMode 63 = 485テスト
- XMLドキュメント: 主要インターフェース・拡張メソッドに追加完了
- Profilerマーカー: 27マーカー追加
- カスタム例外: 7クラス追加

---

## 付録

### A. 用語集

| 用語 | 説明 |
|-----|------|
| **GameScene** | 論理的なシーン単位（Prefab/UnityScene） |
| **SceneComponent** | GameSceneに紐づくMonoBehaviour |
| **LifetimeScope** | VContainerのDIコンテナスコープ |
| **MasterData** | 読み取り専用のゲーム設定データ |

### B. 関連ドキュメント

**プロジェクト概要**:
- [README.md](./README.md) - プロジェクト概要

---

*本ドキュメントはプロジェクトの設計を記録したものであり、実装の変更に応じて更新されます。*
