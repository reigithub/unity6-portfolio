## TL;DR
* このプロジェクトは主に個人または小規模のUnityゲーム開発におけるスターターキットを目指して作成されています
* コードの再利用性を高め、実装のしやすさや可読性、保守性が向上するような作りを意識しています
* インゲーム/アウトゲーム共にマスターデータで動作しています(データ駆動型)(調整中の部分を除く)
* MVCパターン
---
## 機能一覧
* プレハブシーン/ダイアログ遷移機能(async/await)
* マスターデータ更新/読込/バイナリ作成用のUnityエディタ拡張
* ステートマシーン実装(ジェネリック型コンテキスト付き)
* 簡単なデモゲーム
* その他簡易的なオーディオ再生などの各種ゲームサービスクラス
---
## 機能詳細
### シーン/ダイアログ遷移機能
1. 非同期処理(async/await)で実装
2. 前のシーンが破棄されていても遷移履歴から再遷移が可能
3. 現在シーンをスリープさせて次のシーンへ遷移でき、戻るとスリープ状態から復帰可能
4. シーン実装は起動前/ロード時/初期化時/スリープ時/復帰時/終了時など様々なタイミングで追加処理を挟む事ができます
5. シーンに任意で引数や戻り値を追加で設定できます
6. 引数つきのシーンであっても、履歴から状態を復元して再度引数を渡して遷移する事が可能
7. ダイアログ(オーバーレイ)は複数開く事が可能で、不正な挙動を防止するためにシーン遷移時に全て破棄されます

### ステートマシーン
1. ジェネリック型コンテキストを持ち、任意の型を指定できます。
2. 各ステートからコンテキストを参照して、状態管理を行う事ができます
3. 初期時に遷移テーブルを構築でき、各ステートがどのステートから遷移するかルールを設定できます。遷移ルールが1ヶ所に集約/可視化され保守性が向上します。
4. 任意ステートから遷移先に指定できる特別なステートを設定可能で、適切な設定が遷移テーブルに無い場合に遷移が検証/実行されます。IdleState等の様々なステートの中継地点や不正な遷移のハンドリング用ステート等を指定する事で遷移挙動の実装サンクコストや不具合を抑える事ができる仕様になります。
5. ジェネリック型のイベントキー型を指定でき、遷移イベント名をenum等で集約管理できます。遷移先ステート名と一致させると可読性/保守性が向上します。
6. 通常のUpdateに加え、MonoBehaivior.FixedUpdate/LateUpdateにも対応。これにより物理演算やカメラ等の状態と相互に連携できます。

### その他
* シーン遷移やオーディオ再生などの共通機能は主にゲームサービスとして分離されています
* マスターデータエディタ拡張はTSVから簡単にバイナリを作成でき、TSV更新後すぐにデータをテストできます。これによって検証サイクルを早めています。テストしたバイナリをそのままビルドやアセット配信で使用できます。
* インゲームシーンはPrefabシーン＋Unityシーンで構成されており、ステージとなるUnityシーンはロジックから分離されています。その為、コード修正なしで新しいステージを追加できます
* アウトゲームシーンは遷移挙動のカスタマイズ性を担保するため、全てPrefabシーンを採用しています
---
## 機能コードリンク
* シーン遷移サービス : [GameSceneService.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/Core/Services/GameSceneService.cs)
* シーン基底クラス : [GameScene.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/Core/Scenes/GameScene.cs)
* マスターデータエディタ拡張 : [MasterDataWindow.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/Core/MasterData/Editor/MasterDataWindow.cs)
* ステートマシーン本体 : [StateMachine.cs](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/Core/StateMachine.cs)
* ステートマシーン実装 : [PlayerController](https://github.com/reigithub/unity6-sample/blob/master/Assets/Programs/Runtime/Contents/Player/SDUnityChanPlayerController.cs)
---
## 使用言語/ライブラリ/ツール

| 言語・フレームワーク等 | バージョン  |
| -------------------- | ---------- |
| Unity                | 6000.3.2f1 |
| C#                   | 9.0        |
| cysharp/MessagePipe  | 1.8.1      |
| cysharp/R3           | 1.3.0      |
| cysharp/UniTask      | 2.5.10     |
| cysharp/MasterMemory | 3.0.4      |
| cysharp/MemoryPack   | 3.1.3      |
| DOTween              | 1.2.790    |
| HotReload            | 1.13.13    |
---
| IDE等                | バージョン   |
| -------------------- |------------|
| JetBrains Rider      | 2025.3.0.2 |
| VSCode               | 1.107.1    |
---
## 主なライブラリ採用理由
* MessagePipe: MessageBrokerを用いたUIイベント、ゲームイベントの疎結合なメッセージング処理(Pub/Sub)のため。
* R3 : UIボタンの押下間隔の設定や複雑な非同期イベント処理、Animatorステート等のイベント合成が簡潔に記述可能。保守性/再利用性の向上のため。
* UniTask : Unityに最適化された非同期処理全般のため。現在は主にダイアログのエラーハンドリングに使用しており、随時利用範囲拡大予定。
* MasterMemory: ゲームロジックとデータを分離し、ロジック修正を抑えつつ、開発サイクルを効率化するため。また、デモゲームが大量の音声ファイル(約400個)を使用するため。
* MemoryPack: 主にMasterMemoryのデータシリアライザーとして。
---
# アセット
* 主にUnityAssetStoreのもので自作は含まれません
* Unityちゃん: https://unity-chan.com/ (© Unity Technologies Japan/UCL)
---
# 制作期間
* 2週間程度 (2026/1/5時点)
---
# 今後の予定
* 単体テストコード追加
* (済)プレイヤー／エネミー挙動周りのStateMachine化
* TaskをUniTaskへ随時変更検討(主にシーン遷移機能)
* MemoryPackを用いた簡易的なセーブ機能(PlayerPrefs代替機能として)
* PlayerLoopへの介入サンプル
* EnhancedScroller実装サンプル
* リストのソート／フィルタ機能サンプル
* オーディオ音量オプション画面
* ボイス／SEの再生制御周り調整
* MessageBroker周りの冗長な呼び出しコードの改善検討
* デモゲームの高度化を目指した仕様変更または新規実装
* マルチ解像度対応
* VContainerなどDIライブラリ導入検討
---
# デモゲームについて(2026/1/5時点)
* 制限時間内に全3ステージに配置されたアイテムを規定数集めるタイムアタックです
* 動作環境: PC／マウス&キーボード
* 操作: 移動(WASD), ジャンプ(Space), 走る(LShift+移動), カメラ操作(マウスドラッグ)
* ダウンロード(実行形式): [デモゲームDLリンク](https://drive.google.com/file/d/1_9vWOvT8leUjd2jB5uTzziSyA5goPmJx/view?usp=drive_link) ※解凍できない場合は7Zipを推奨