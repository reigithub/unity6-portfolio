using System.Threading.Tasks;
using UnityEngine;

namespace Game.Shared.Extensions
{
    /// <summary>
    /// Task用の拡張メソッド
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>
        /// タスクをawaitせずに実行する（Fire-and-forget）
        /// 例外が発生した場合はDebug.LogExceptionで出力
        /// </summary>
        /// <param name="task">実行するタスク</param>
        public static void Forget(this Task task)
        {
            task.ContinueWith(e => Debug.LogException(e.Exception), TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}