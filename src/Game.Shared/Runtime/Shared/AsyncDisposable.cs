using System;
using System.Threading.Tasks;

namespace Game.Library.Shared
{
    public sealed class AsyncDisposable : IAsyncDisposable
    {
        private readonly Func<ValueTask> _cleanup;

        public AsyncDisposable(Func<ValueTask> cleanup)
        {
            _cleanup = cleanup;
        }

        public ValueTask DisposeAsync() => _cleanup();

        public static IAsyncDisposable Create(Func<ValueTask> action) => new AsyncDisposable(action);
    }
    public sealed class EmptyAsyncDisposable : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => new();
    }
}
