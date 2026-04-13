using System;
using Bebop.Monads;
using Cysharp.Threading.Tasks;

namespace Plugins.Sim.Faciem.Shared
{
    public static class MaybeExtensions
    {
        public static void Do<T>(this Maybe<T> maybe, Action<T> action)
        {
            if (maybe is IMaybe<T> { HasValue: true } maybeInterface)
            {
                action(maybeInterface.Value);
            }
        }

        public static UniTask DoAsync<T>(this Maybe<T> maybe, Func<T, UniTask> action)
        {
            if (maybe is IMaybe<T> { HasValue: true } maybeInterface)
            {
                return action(maybeInterface.Value);
            }

            return UniTask.CompletedTask;
        }
    }
}