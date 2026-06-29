using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.Playables;

namespace Game.Shared.Extensions
{
    public static class PlayableDirectorExtensions
    {
        public static UniTask PlayAsync(this PlayableDirector director, CancellationToken cancellationToken = default)
        {
            director.Play();
            return director.OnStoppedAsObservable().FirstAsync(cancellationToken).AsUniTask();
        }

        public static Observable<PlayableDirector> OnStoppedAsObservable(this PlayableDirector director)
        {
            return Observable.FromEvent<PlayableDirector>(
                h => director.stopped += h,
                h => director.stopped -= h
            );
        }
    }
}
