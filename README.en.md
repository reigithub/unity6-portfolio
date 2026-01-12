## TL;DR

* This project is designed as a starter kit for individual or small-scale Unity game development
* Built with emphasis on code reusability, ease of implementation, readability, and maintainability
* Both in-game and out-game systems are driven by master data (data-driven design) (some parts still under adjustment)
* **Modular design with assembly separation** allows MVC/MVP pattern game modes to coexist
* **Master data definitions separated into a local package** (com.rei.unity6library) for improved reusability
* Different architecture game modes can be selected and launched from the title screen at startup
---

[日本語版はこちら](https://github.com/reigithub/unity6-sample/blob/master/README.md)

---
## Architecture Overview
```
┌─────────────────────────────────────────────────────────────┐
│                        Game.App                              │
│              (Entry Point / Game Mode Selection)             │
└─────────────────────────────────────────────────────────────┘
                    ↓                    ↓
┌─────────────────────────────┐  ┌─────────────────────────────┐
│      Game.MVC.Core          │  │      Game.MVP.Core          │
│   (MVC Pattern Foundation)  │  │   (MVP Pattern Foundation)  │
│   GameServiceManager        │  │   VContainer/DI             │
└─────────────────────────────┘  └─────────────────────────────┘
            ↓                                ↓
┌─────────────────────────────┐  ┌─────────────────────────────┐
│  Game.MVC.ScoreTimeAttack   │  │    Game.MVP.Survivor        │
│    (Time Attack Game)       │  │   (Survivor Game - Planned) │
└─────────────────────────────┘  └─────────────────────────────┘
            ↖                                ↗
               └──────────────┬──────────────┘
                              ↓
              ┌─────────────────────────────┐
              │         Game.Shared         │
              │    (Common Utilities)       │
              └─────────────────────────────┘
                              ↓
              ┌─────────────────────────────┐
              │  com.rei.unity6library      │
              │  (Local Package)            │
              │  Master Data Defs / Enums   │
              └─────────────────────────────┘
```
---
## Features
* **Game Mode Selection System**: Select different architecture game modes from the title screen at startup
* **Assembly Separation Design**: Manage MVC/MVP patterns in independent assemblies to prevent circular references
* **Local Package Separation**: Master data definitions separated into a local package (com.rei.unity6library) for improved reusability
* **Prefab Scene/Dialog Transition**: Asynchronous scene transitions using async/await
* **State Machine Implementation**: Generic context support with transition table-based state management
* **Master Data Management**: TSV to binary conversion, data-driven development with editor extensions
* **Various Game Services**: Common features like audio, scene transitions, messaging
* **DI Container Support**: Dependency injection via VContainer (for MVP pattern)
---
## Feature Details
<details><summary>Game Mode Selection System</summary>

1. Display Game.App title screen at application startup
2. Launch corresponding launcher based on selected game mode
3. Each game mode is implemented in independent assemblies without mutual interference
4. Launcher can be shut down and return to title screen when game ends
5. Loosely coupled event notification via ApplicationEvents (lower → upper assemblies)
</details>

<details><summary>Assembly Separation Design</summary>

| Assembly | Role | Dependencies |
|----------|------|--------------|
| Game.Shared | Common utilities, interfaces | Unity6Library |
| Game.App | Entry point, game mode selection | Shared, MVC.*, MVP.* |
| Game.MVC.Core | MVC pattern foundation, GameServiceManager | Shared, Unity6Library |
| Game.MVC.ScoreTimeAttack | Time attack game implementation | Shared, MVC.Core, Unity6Library |
| Game.MVP.Core | MVP pattern foundation, VContainer | Shared |
| Game.MVP.Survivor | Survivor game implementation (Planned) | Shared, MVP.Core |
| **com.rei.unity6library** | Master data definitions, common enums | None (bottom layer) |

</details>

<details><summary>Local Package (com.rei.unity6library)</summary>

Master data definition files are separated into a local package, providing these benefits:

1. **Reusability**: Share the same master data definitions across multiple projects
2. **Clear Dependencies**: Prevent circular references by placing at the bottom layer
3. **Reduced Build Time**: Efficient incremental builds by separating infrequently changed code
4. **Version Control**: Package-level version management

**Contents:**
- MasterMemory master data definition classes (AudioMaster, ScoreTimeAttackStageMaster, etc.)
- Common enum definitions (AudioCategory, AudioPlayTag, etc.)

</details>

<details><summary>Scene/Dialog Transition</summary>

1. Implemented with asynchronous processing (async/await)
2. Can re-transition from history even if previous scene was destroyed
3. Can transition to next scene while keeping current scene asleep, and resume from sleep state when returning
4. Scene implementations can insert additional processing at various timings: pre-startup, loading, initialization, sleep, resume, termination, etc.
5. Scenes can optionally have arguments and return values
6. Even scenes with arguments can restore state from history and pass arguments again for transition
7. Multiple dialogs (overlays) can be opened simultaneously, and all are destroyed on scene transition to prevent invalid behavior
</details>

<details><summary>State Machine</summary>

1. Has generic context, allowing any type to be specified
2. Each state can reference context for state management
3. Transition table can be built at initialization, setting rules for which states can transition from which. Transition rules are consolidated and visualized in one place, improving maintainability
4. Special states can be set as transition targets from any state, validated and executed when appropriate settings are not in the transition table
5. Generic event key type can be specified, managing transition event names with enums etc. Matching with target state names improves readability/maintainability
6. Supports MonoBehaviour.FixedUpdate/LateUpdate in addition to regular Update, enabling coordination with physics calculations and camera states
</details>

<details><summary>Others</summary>

* Common features like scene transitions and audio playback are primarily separated as game services
* Master data editor extension easily creates binaries from TSV, allowing immediate testing after TSV updates, accelerating verification cycles. Tested binaries can be used directly for builds and asset distribution
* In-game scenes consist of Prefab scenes + Unity scenes, with stage Unity scenes separated from logic. Therefore, new stages can be added without code modifications
* Out-game scenes all use Prefab scenes to ensure customizability of transition behavior
</details>

---
## Code Links
### Game.App (Entry Point)
* Game Bootstrap: [GameBootstrap.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/App/Bootstrap/GameBootstrap.cs)
* Game Mode Launcher Registry: [GameModeLauncherRegistry.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/App/Launcher/GameModeLauncherRegistry.cs)
* App Title Screen: [AppTitleSceneComponent.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/App/Title/AppTitleSceneComponent.cs)

### Game.Shared (Common)
* Application Events: [ApplicationEvents.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/Shared/Bootstrap/ApplicationEvents.cs)
* Game Mode Launcher Interface: [IGameModeLauncher.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/Shared/Bootstrap/IGameModeLauncher.cs)
* State Machine: [StateMachine.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/Shared/StateMachine.cs)

### Game.MVC.Core (MVC Foundation)
* Scene Transition Service: [GameSceneService.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVC/Core/Services/GameSceneService.cs)
* Scene Base Class: [GameScene.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVC/Core/Scenes/GameScene.cs)
* Service Manager: [GameServiceManager.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVC/Core/Services/GameServiceManager.cs)

### Game.MVC.ScoreTimeAttack (Time Attack Game)
* Launcher: [ScoreTimeAttackLauncher.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVC/ScoreTimeAttack/ScoreTimeAttackLauncher.cs)
* Player Controller: [SDUnityChanPlayerController.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVC/ScoreTimeAttack/Player/SDUnityChanPlayerController.cs)

### Game.MVP.Core (MVP Foundation)
* VContainer Launcher: [VContainerGameLauncher.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/MVP/Core/DI/VContainerGameLauncher.cs)

### Editor
* Master Data Editor Extension: [MasterDataWindow.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Editor/EditorWindow/MasterDataWindow.cs)

### com.rei.unity6library (Local Package)
* Audio Master Definition: [AudioMaster.cs](https://github.com/reigithub/unity6-sample/blob/master/Packages/com.rei.unity6library/Runtime/Shared/MasterData/MemoryTables/AudioMaster.cs)
* Stage Master Definition: [ScoreTimeAttackStageMaster.cs](https://github.com/reigithub/unity6-sample/blob/master/Packages/com.rei.unity6library/Runtime/Shared/MasterData/MemoryTables/ScoreTimeAttackStageMaster.cs)
* Audio Enum Definition: [AudioEnums.cs](https://github.com/reigithub/unity6-sample/blob/master/Packages/com.rei.unity6library/Runtime/Shared/Enums/AudioEnums.cs)

---
## Folder Structure
```
.
├── Assets
│   ├── MasterData          Master data (TSV, binary)
│   ├── Programs
│   │   ├── Editor          Editor extensions
│   │   │   └── Tests       Unit tests / Performance improvement test tools
│   │   └── Runtime
│   │       ├── Shared      Common utilities, interfaces
│   │       │   ├── Bootstrap   IGameLauncher, ApplicationEvents
│   │       │   ├── Constants   Common constants
│   │       │   ├── Enums       GameMode, etc.
│   │       │   └── Extensions  Extension methods
│   │       ├── App         Entry point
│   │       │   ├── Bootstrap   GameBootstrap
│   │       │   ├── Launcher    GameModeLauncherRegistry
│   │       │   └── Title       App title screen
│   │       ├── MVC         MVC pattern implementation
│   │       │   ├── Core        Foundation (Services, Scenes, MessagePipe)
│   │       │   └── ScoreTimeAttack  Time attack game
│   │       └── MVP         MVP pattern implementation
│   │           ├── Core        Foundation (VContainer, Base)
│   │           └── Survivor    Survivor game (Planned)
│   └── README.md
└── Packages
    └── com.rei.unity6library   Local package
        └── Runtime
            └── Shared
                ├── Enums           AudioCategory, AudioPlayTag, etc.
                └── MasterData
                    └── MemoryTables Master data definition classes
```

## Performance Improvement Samples
<details><summary>Scene Transition</summary>

* GameSceneService
  - Verified performance improvements by changing various scene transition functions from Task to UniTask
  - Iterations: 10,000
  - ~40% reduction in CPU execution time, zero allocation, 100% reduction in memory usage
  - !["Test Results"](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Editor/Tests/Logs/GameSceneServicePerformanceTests_2026-01-08_220131.png)
  - !["Test Results"](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Editor/Tests/Logs/GameSceneServicePerformanceTests_2026-01-09_015400.png)

</details>

<details><summary>State Machine</summary>

* Improvements
  - Changed state management from HashSet to Dictionary, improving state lookup from O(n) to O(1) (constant time regardless of state count)
  - Reduced Dictionary lookups during transitions, improved LINQ usage to reduce allocations
  - Reduced overhead through method inlining

* State Transition Throughput Improvement
  - Iterations: 30,000
  - Average 15% reduction in transition time, average 15% improvement in throughput
  - Benchmark results (overall results across game loops)

  | Item | Old StateMachine | New StateMachine | Improvement |
  |:-----|---------------:|---------------:|-------:|
  | Total Execution Time (ms) | 44.848 | 35.295 | 1.27x |
  | Avg Transition Time (μs) | 0.300 | 0.146 | 2.05x |
  | P99 Transition Time (μs) | 0.500 | 0.300 | 1.67x |
  | Max Transition Time (μs) | 9.500 | 5.100 | 1.86x |
  | Throughput (ops/s) | 668,934 | 849,991 | 1.27x |
  | Transitions/sec | 200,680 | 254,997 | 1.27x |
  | Memory (bytes) | 401,408 | 401,408 | 1.00x |
  | GC Count | 1 | 1 | 0 |

* State Transition Memory Allocation Improvement
  - Iterations: 10,000
  - Memory allocation comparison results (pure transition request execution)

  | Item | Old StateMachine | New StateMachine | Improvement |
  |:-----|---------------:|---------------:|-------:|
  | Memory (bytes) | 2,760,704 | 1,290,240 | 2.14x |
  | Bytes/Iteration | 276.07 | 129.02 | 2.14x |

</details>

---
## Languages/Libraries/Tools

| Language/Framework | Version |
|-------------------|---------|
| Unity | 6000.3.2f1 |
| C# | 9.0 |
| cysharp/MessagePipe | 1.8.1 |
| cysharp/R3 | 1.3.0 |
| cysharp/UniTask | 2.5.10 |
| cysharp/MasterMemory | 3.0.4 |
| cysharp/MessagePack | 3.1.3 |
| **hadashiA/VContainer** | **1.16.8** |
| NSubstitute | 5.3.0 |
| DOTween | 1.2.790 |
| HotReload | 1.13.13 |
| JetBrains Rider | 2025.3.0.2 |
| VSCode | 1.107.1 |
| Claude Code | - |
---
## Library/Tool Selection Rationale
* **VContainer**: DI (Dependency Injection) container for MVP pattern. Improves testability through constructor injection and automates lifecycle management.
* MessagePipe: Loosely coupled messaging (Pub/Sub) for UI events and game events using MessageBroker.
* R3: Enables concise description of UI button press intervals, complex async event processing, and Animator state event composition. Improves maintainability/reusability.
* UniTask: For all Unity-optimized async processing. Currently mainly used for dialog error handling, with planned expansion.
* MasterMemory: Separates game logic from data, minimizing logic modifications while streamlining development cycles. Also handles the demo game's large number of audio files (~400).
* MessagePack: Primarily as data serializer for MasterMemory.
* NSubstitute: Creating mocks for game services in test code.
* Claude Code: Test code generation, refactoring.
---
## Assets
* Primarily from Unity Asset Store, no self-made assets included
* Unity-chan: https://unity-chan.com/ (© Unity Technologies Japan/UCL)
---
## Development Period
* Approximately 3 weeks (as of 2026/1/13)
---
## Future Plans
* **Survivor Game Mode Implementation (MVP/VContainer)**
* Simple save functionality using MemoryPack (as PlayerPrefs alternative)
* PlayerLoop intervention sample
* EnhancedScroller implementation sample
* List sort/filter functionality sample
* Audio volume options screen
* Multi-resolution support
---
## About the Demo Game
### Time Attack (MVC)
* A time attack game where you collect a specified number of items placed across 3 stages within the time limit
* Platform: PC / Mouse & Keyboard
* Controls: Move (WASD), Jump (Space), Run (LShift+Move), Camera (Mouse Drag)

### Survivor (MVP) *Planned*
* Will be implemented using MVP pattern with VContainer

### Download
* Executable: [Demo Game Download Link](https://drive.google.com/file/d/1_9vWOvT8leUjd2jB5uTzziSyA5goPmJx/view?usp=drive_link) *If extraction fails, 7Zip is recommended
