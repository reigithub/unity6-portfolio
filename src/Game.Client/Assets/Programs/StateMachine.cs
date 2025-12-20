using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
#if ENABLE_IL2CPP
using UnityEngine.Scripting;
#endif

namespace Sample
{
    #region 標準ステートマシン基底実装

    /// <summary>
    /// コンテキストを持つことのできるステートマシンクラスです
    /// </summary>
    /// <typeparam name="TContext">このステートマシンが持つコンテキストの型</typeparam>
    /// <typeparam name="TEvent">ステートマシンへ送信するイベントの型</typeparam>
    public class StateMachine<TContext, TEvent>
    {
        #region ステートクラス本体と特別ステートクラスの定義

        /// <summary>
        /// ステートマシンが処理する状態を表現するステートクラスです。
        /// </summary>
        public abstract class State
        {
            internal Dictionary<TEvent, State> transitionTable;
            internal StateMachine<TContext, TEvent> stateMachine;

            protected StateMachine<TContext, TEvent> StateMachine => stateMachine;
            protected TContext Context => stateMachine.Context;

            /// <summary>
            /// ステート開始時の処理
            /// </summary>
            protected internal virtual void Enter()
            {
            }

            /// <summary>
            /// ステート更新中の処理
            /// </summary>
            protected internal virtual void Update()
            {
            }

            /// <summary>
            /// ステート終了時の処理
            /// </summary>
            protected internal virtual void Exit()
            {
            }

            /// <summary>
            /// ステートマシンの未処理例外が発生した時の処理を行います。
            /// ただし UnhandledExceptionMode が CatchStateException である必要があります。
            /// </summary>
            /// <remarks>
            /// もし、この関数が false を返した場合は、例外が結局未処理状態と判断されステートマシンの
            /// Update() 関数が例外を送出することになります。
            /// </remarks>
            /// <param name="exception">発生した未処理の例外</param>
            /// <returns>例外を処理した場合は true を、未処理の場合は false を返します</returns>
            protected internal virtual bool Error(Exception exception) => false;

            /// <summary>
            /// ステートマシンがイベントを受ける時に、このステートがそのイベントをガードします
            /// </summary>
            /// <param name="eventId">渡されたイベントID</param>
            /// <returns>イベントの受付をガードする場合は true を、ガードせずイベントを受け付ける場合は false を返します</returns>
            protected internal virtual bool GuardEvent(TEvent eventId) => false;

            /// <summary>
            /// ステートマシンがスタックしたステートをポップする前に、このステートがそのポップをガードします
            /// </summary>
            /// <returns>ポップの動作をガードする場合は true を、ガードせずにポップ動作を続ける場合は false を返します</returns>
            protected internal virtual bool GuardPop() => false;
        }

        /// <summary>
        /// ステートマシンで "任意" を表現する特別なステートクラスです
        /// </summary>
#if ENABLE_IL2CPP
        [Preserve]
#endif
        public sealed class AnyState : State
        {
        }

        #endregion

        #region 列挙型定義

        /// <summary>
        /// ステートマシンのUpdate状態を表現します
        /// </summary>
        private enum UpdateState
        {
            Idle,
            Enter,
            Update,
            Exit,
        }

        #endregion

        // メンバ変数定義
        private UpdateState _updateState;
        private readonly List<State> _stateList;
        private State _currentState;
        private State _nextState;

        /// <summary>
        /// ステートマシンが保持しているコンテキスト
        /// </summary>
        public TContext Context { get; private set; }

        /// <summary>
        /// ステートマシンが起動しているかどうか
        /// </summary>
        public bool IsRunning => _currentState != null;

        /// <summary>
        /// ステートマシンが、更新処理中かどうか。
        /// Update 関数から抜けたと思っても、このプロパティが true を示す場合、
        /// Update 中に例外などで不正な終了の仕方をしている場合が考えられます。
        /// </summary>
        public bool IsUpdating => IsRunning && _updateState != UpdateState.Idle;

        /// <summary>
        /// 現在のステートの名前を取得します。
        /// まだステートマシンが起動していない場合は空文字列になります。
        /// </summary>
        public string CurrentStateName => IsRunning ? _currentState.GetType().Name : string.Empty;

        /// <summary>
        /// このステートマシンを最後にUpdateしたスレッドID
        /// </summary>
        public int LastUpdateThreadId { get; private set; }

        /// <summary>
        /// このステートマシンが最後に受け付けたイベントID
        /// </summary>
        public TEvent LastAcceptedEventId { get; private set; }

        /// <summary>
        /// インスタンスを初期化します
        /// </summary>
        /// <param name="context">このステートマシンが持つコンテキスト</param>
        /// <exception cref="ArgumentNullException">context が null です</exception>
        /// <exception cref="InvalidOperationException">ステートクラスのインスタンスの生成に失敗しました</exception>
        public StateMachine(TContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // メンバの初期化をする
            Context = context;
            _stateList = new List<State>();
            _updateState = UpdateState.Idle;
        }

        #region ステート遷移テーブル構築系

        /// <summary>
        /// ステートの任意遷移構造を追加します。
        /// </summary>
        /// <remarks>
        /// この関数は、遷移元が任意の状態からの遷移を希望する場合に利用してください。
        /// 任意の遷移は、通常の遷移（Any以外の遷移元）より優先度が低いことにも、注意をしてください。
        /// また、ステートの遷移テーブル設定はステートマシンが起動する前に完了しなければなりません。
        /// </remarks>
        /// <typeparam name="TNextState">任意状態から遷移する先になるステートの型</typeparam>
        /// <param name="eventId">遷移する条件となるイベントID</param>
        /// <exception cref="ArgumentException">既に同じ eventId が設定された遷移先ステートが存在します</exception>
        /// <exception cref="InvalidOperationException">ステートマシンは、既に起動中です</exception>
        public void AddAnyTransition<TNextState>(TEvent eventId) where TNextState : State, new()
        {
            // 単純に遷移元がAnyStateなだけの単純な遷移追加関数を呼ぶ
            AddTransition<AnyState, TNextState>(eventId);
        }

        /// <summary>
        /// ステートの遷移構造を追加します。
        /// また、ステートの遷移テーブル設定はステートマシンが起動する前に完了しなければなりません。
        /// </summary>
        /// <typeparam name="TPrevState">遷移する元になるステートの型</typeparam>
        /// <typeparam name="TNextState">遷移する先になるステートの型</typeparam>
        /// <param name="eventId">遷移する条件となるイベントID</param>
        /// <exception cref="ArgumentException">既に同じ eventId が設定された遷移先ステートが存在します</exception>
        /// <exception cref="InvalidOperationException">ステートマシンは、既に起動中です</exception>
        /// <exception cref="InvalidOperationException">ステートクラスのインスタンスの生成に失敗しました</exception>
        public void AddTransition<TPrevState, TNextState>(TEvent eventId)
            where TPrevState : State, new()
            where TNextState : State, new()
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("ステートマシンは、既に起動中です");
            }

            // 遷移元と遷移先のステートインスタンスを取得
            var prevState = GetOrCreateState<TPrevState>();
            var nextState = GetOrCreateState<TNextState>();

            if (prevState.transitionTable.ContainsKey(eventId))
            {
                throw new ArgumentException($"ステート'{prevState.GetType().Name}'には、既にイベントID'{eventId}'の遷移が設定済みです");
            }

            // 遷移テーブルに遷移を設定する
            prevState.transitionTable[eventId] = nextState;
        }

        /// <summary>
        /// ステートマシンが起動する時に、最初に開始するステートを設定します。
        /// </summary>
        /// <typeparam name="TStartState">ステートマシンが起動時に開始するステートの型</typeparam>
        /// <exception cref="InvalidOperationException">ステートマシンは、既に起動中です</exception>
        /// <exception cref="InvalidOperationException">ステートクラスのインスタンスの生成に失敗しました</exception>
        public void SetStartState<TStartState>() where TStartState : State, new()
        {
            // 既にステートマシンが起動してしまっている場合は
            if (IsRunning)
            {
                // 起動してしまったらこの関数の操作は許されない
                throw new InvalidOperationException("ステートマシンは、既に起動中です");
            }

            // 次に処理するステートの設定をする
            _nextState = GetOrCreateState<TStartState>();
        }

        #endregion

        #region ステートマシン制御系

        /// <summary>
        /// 現在実行中のステートが、指定されたステートかどうかを調べます。
        /// </summary>
        /// <typeparam name="TState">確認するステートの型</typeparam>
        /// <returns>指定されたステートの状態であれば true を、異なる場合は false を返します</returns>
        /// <exception cref="InvalidOperationException">ステートマシンは、まだ起動していません</exception>
        public bool IsCurrentState<TState>() where TState : State
        {
            // そもそもまだ現在実行中のステートが存在していないなら例外を投げる
            IfNotRunningThrowException();

            // 現在のステートと型が一致するかの条件式の結果をそのまま返す
            return _currentState.GetType() == typeof(TState);
        }

        /// <summary>
        /// ステートマシンにイベントを送信して、ステート遷移の準備を行います。
        /// </summary>
        /// <remarks>
        /// ステートの遷移は直ちに行われず、次の Update が実行された時に遷移処理が行われます。
        /// また、この関数によるイベント受付優先順位は、一番最初に遷移を受け入れたイベントのみであり Update によって遷移されるまで、後続のイベントはすべて失敗します。
        /// ただし AllowRetransition プロパティに true が設定されている場合は、再遷移が許されます。
        /// さらに、イベントはステートの Enter または Update 処理中でも受け付けることが可能で、ステートマシンの Update 中に
        /// 何度も遷移をすることが可能ですが Exit 中でイベントを送ると、遷移中になるため例外が送出されます。
        /// </remarks>
        /// <param name="eventId">ステートマシンに送信するイベントID</param>
        /// <returns>ステートマシンが送信されたイベントを受け付けた場合は true を、イベントを拒否または、イベントの受付ができない場合は false を返します</returns>
        /// <exception cref="InvalidOperationException">ステートマシンは、まだ起動していません</exception>
        /// <exception cref="InvalidOperationException">ステートが Exit 処理中のためイベントを受け付けることが出来ません</exception>
        public virtual bool SendEvent(TEvent eventId)
        {
            // そもそもまだ現在実行中のステートが存在していないなら例外を投げる
            IfNotRunningThrowException();

            // もし Exit 処理中なら
            if (_updateState == UpdateState.Exit)
            {
                // Exit 中の SendEvent は許されない
                throw new InvalidOperationException("ステートが Exit 処理中のためイベントを受け付けることが出来ません");
            }

            // 既に遷移準備をしていて かつ 再遷移が許可されていないなら
            if (_nextState != null)
            {
                // イベントの受付が出来なかったことを返す
                return false;
            }

            // 現在のステートにイベントガードを呼び出して、ガードされたら
            // if (_currentState.GuardEvent(eventId))
            // {
            //     // ガードされて失敗したことを返す
            //     return false;
            // }

            // 次に遷移するステートを現在のステートから取り出すが見つけられなかったら
            if (!_currentState.transitionTable.TryGetValue(eventId, out _nextState))
            {
                // 任意ステートからすらも遷移が出来なかったのなら
                if (!GetOrCreateState<AnyState>().transitionTable.TryGetValue(eventId, out _nextState))
                {
                    // イベントの受付が出来なかった
                    return false;
                }
            }

            // 最後に受け付けたイベントIDを覚えてイベントの受付をした事を返す
            LastAcceptedEventId = eventId;
            return true;
        }

        /// <summary>
        /// ステートマシンの状態を更新します。
        /// </summary>
        /// <remarks>
        /// ステートマシンの現在処理しているステートの更新を行いますが、まだ未起動の場合は SetStartState 関数によって設定されたステートが起動します。
        /// また、ステートマシンが初回起動時の場合、ステートのUpdateは呼び出されず、次の更新処理が実行される時になります。
        /// </remarks>
        /// <exception cref="InvalidOperationException">現在のステートマシンは、別のスレッドによって更新処理を実行しています。[UpdaterThread={LastUpdateThreadId}, CurrentThread={currentThreadId}]</exception>
        /// <exception cref="InvalidOperationException">現在のステートマシンは、既に更新処理を実行しています</exception>
        /// <exception cref="InvalidOperationException">開始ステートが設定されていないため、ステートマシンの起動が出来ません</exception>
        public virtual void Update()
        {
            // もしステートマシンの更新状態がアイドリング以外だったら
            if (_updateState != UpdateState.Idle)
            {
                // もし別スレッドからのUpdateによる多重Updateなら
                int currentThreadId = Thread.CurrentThread.ManagedThreadId;
                if (LastUpdateThreadId != currentThreadId)
                {
                    // 別スレッドからの多重Updateであることを例外で吐く
                    throw new InvalidOperationException(
                        $"現在のステートマシンは、別のスレッドによって更新処理を実行しています。[UpdaterThread={LastUpdateThreadId}, CurrentThread={currentThreadId}]");
                }

                // 多重でUpdateが呼び出せない例外を吐く
                throw new InvalidOperationException("現在のステートマシンは、既に更新処理を実行しています");
            }

            // Updateの起動スレッドIDを覚える
            LastUpdateThreadId = Thread.CurrentThread.ManagedThreadId;

            // まだ未起動なら
            if (!IsRunning)
            {
                // 次に処理するべきステート（つまり起動開始ステート）が未設定なら
                if (_nextState == null)
                {
                    // 起動が出来ない例外を吐く
                    throw new InvalidOperationException("開始ステートが設定されていないため、ステートマシンの起動が出来ません");
                }

                // 現在処理中ステートとして設定する
                _currentState = _nextState;
                _nextState = null;

                try
                {
                    // Enter処理中であることを設定してEnterを呼ぶ
                    _updateState = UpdateState.Enter;
                    _currentState.Enter();
                }
                catch (Exception exception)
                {
                    // 起動時の復帰は現在のステートにnullが入っていないとまずいので遷移前の状態に戻す
                    _nextState = _currentState;
                    _currentState = null;


                    // 更新状態をアイドリングにして、例外発生時のエラーハンドリングを行い終了する
                    _updateState = UpdateState.Idle;
                    DoHandleException(exception);
                    return;
                }


                // 次に遷移するステートが無いなら
                if (_nextState == null)
                {
                    // 起動処理は終わったので一旦終わる
                    _updateState = UpdateState.Idle;
                    return;
                }
            }

            try
            {
                // 次に遷移するステートが存在していないなら
                if (_nextState == null)
                {
                    // Update処理中であることを設定してUpdateを呼ぶ
                    _updateState = UpdateState.Update;
                    _currentState.Update();
                }

                // 次に遷移するステートが存在している間ループ
                while (_nextState != null)
                {
                    // Exit処理中であることを設定してExit処理を呼ぶ
                    _updateState = UpdateState.Exit;
                    _currentState.Exit();

                    // 次のステートに切り替える
                    _currentState = _nextState;
                    _nextState = null;

                    // Enter処理中であることを設定してEnterを呼ぶ
                    _updateState = UpdateState.Enter;
                    _currentState.Enter();
                }

                // 更新処理が終わったらアイドリングに戻る
                _updateState = UpdateState.Idle;
            }
            catch (Exception exception)
            {
                // 更新状態をアイドリングにして、例外発生時のエラーハンドリングを行い終了する
                _updateState = UpdateState.Idle;
                DoHandleException(exception);
                return;
            }
        }

        #endregion

        #region 内部ロジック系

        /// <summary>
        /// 発生した未処理の例外をハンドリングします
        /// </summary>
        /// <param name="exception">発生した未処理の例外</param>
        /// <exception cref="ArgumentNullException">exception が null です</exception>
        private void DoHandleException(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            // UnhandledExceptionMode == ThrowException
            // 例外をキャプチャして発生させる
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        /// <summary>
        /// ステートマシンが未起動の場合に例外を送出します
        /// </summary>
        /// <exception cref="InvalidOperationException">ステートマシンは、まだ起動していません</exception>
        protected void IfNotRunningThrowException()
        {
            // そもそもまだ現在実行中のステートが存在していないなら
            if (!IsRunning)
            {
                // まだ起動すらしていないので例外を吐く
                throw new InvalidOperationException("ステートマシンは、まだ起動していません");
            }
        }

        /// <summary>
        /// 指定されたステートの型のインスタンスを取得しますが、存在しない場合は生成してから取得します。
        /// 生成されたインスタンスは、次回から取得されるようになります。
        /// </summary>
        /// <typeparam name="TState">取得、または生成するステートの型</typeparam>
        /// <returns>取得、または生成されたステートのインスタンスを返します</returns>
        /// <exception cref="InvalidOperationException">ステートクラスのインスタンスの生成に失敗しました</exception>
        private TState GetOrCreateState<TState>() where TState : State, new()
        {
            var stateType = typeof(TState);
            foreach (var state in _stateList)
            {
                // もし該当のステートの型と一致するインスタンスなら
                if (state.GetType() == stateType)
                {
                    // そのインスタンスを返す
                    return (TState)state;
                }
            }

            // ループから抜けたのなら、型一致するインスタンスが無いという事なのでインスタンスを生成してキャッシュする
            var newState = new TState();
            _stateList.Add(newState);

            // 新しいステートに、自身の参照と遷移テーブルのインスタンスの初期化も行って返す
            newState.stateMachine = this;
            newState.transitionTable = new Dictionary<TEvent, State>();
            return newState;
        }

        #endregion
    }

    #endregion
}