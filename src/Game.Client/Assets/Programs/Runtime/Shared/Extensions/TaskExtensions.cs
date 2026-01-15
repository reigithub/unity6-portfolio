using System.Threading.Tasks;
using UnityEngine;

namespace Game.Shared.Extensions
{
    public static class TaskExtensions
    {
        public static void Forget(this Task task)
        {
            task.ContinueWith(e => Debug.LogException(e.Exception), TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}