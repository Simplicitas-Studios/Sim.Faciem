using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Sim.Faciem
{
    /// <summary>
    /// A captured screen-activation lifetime, independent of the cached ViewModel's final Dispose.
    /// Publication and ownership operations must run synchronously on its owning Unity thread.
    /// </summary>
    public sealed class NavigationActivation
    {
        private readonly CancellationTokenSource _cancellation = new();
        private readonly CancellationToken _token;
        private readonly Func<bool> _isCurrent;
        private readonly int _threadId;
        private readonly List<IDisposable> _resources = new();
        private readonly List<Exception> _errors = new();
        private readonly UniTaskCompletionSource _endCompletion = new();
        private UniTaskCompletionSource _notificationCompletion;
        private UniTask _endRunner;
        private bool _valid = true;
        private bool _ending;
        private bool _ended;

        internal NavigationActivation(Func<bool> isCurrent)
        {
            _isCurrent = isCurrent;
            _threadId = Thread.CurrentThread.ManagedThreadId;
            _token = _cancellation.Token;
        }

        /// <summary>Signals invalidation; cancellation does not forcibly stop user code.</summary>
        public CancellationToken CancellationToken => _token;

        /// <summary>True only while this captured lifetime is valid and still current for its model.</summary>
        public bool IsValid => _valid && _isCurrent();

        /// <summary>
        /// Runs a bounded synchronous publication only if this captured lifetime is still valid.
        /// Do not await or initiate navigation inside the action, or write UI from worker threads.
        /// </summary>
        public bool TryPublish(Action publication)
        {
            CheckThread();
            if (publication == null)
            {
                throw new ArgumentNullException(nameof(publication));
            }
            if (!IsValid)
            {
                return false;
            }
            publication();
            return true;
        }

        /// <summary>
        /// Owns a distinct resource until activation cleanup. Late ownership after cleanup disposes immediately.
        /// Invalidated but not yet drained activations defer cleanup until the owning operation can finish safely.
        /// </summary>
        public void Own(IDisposable resource)
        {
            CheckThread();
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }
            if (_ended)
            {
                resource.Dispose();
            }
            else
            {
                _resources.Add(resource);
            }
        }

        internal void Invalidate()
        {
            CheckThread();
            if (!_valid)
            {
                return;
            }
            _valid = false;
            _notificationCompletion = new UniTaskCompletionSource();
            try
            {
                _cancellation.Cancel();
            }
            catch (Exception error)
            {
                // Submission cannot lose a reserved operation because a cancellation observer throws.
                _errors.Add(error);
            }
            finally
            {
                _notificationCompletion.TrySetResult();
            }
        }

        internal UniTask EndAsync()
        {
            CheckThread();
            if (!_ending)
            {
                _ending = true;
                _endRunner = CompleteEndAsync();
            }
            return _endCompletion.Task;
        }

        private async UniTask CompleteEndAsync()
        {
            try
            {
                Invalidate();
                if (_notificationCompletion != null)
                {
                    await _notificationCompletion.Task;
                }
                _ended = true;
                IDisposable[] resources = _resources.ToArray();
                _resources.Clear();
                foreach (IDisposable resource in resources)
                {
                    try
                    {
                        resource.Dispose();
                    }
                    catch (Exception error)
                    {
                        _errors.Add(error);
                    }
                }
                _cancellation.Dispose();
                if (_errors.Count != 0)
                {
                    throw new AggregateException("Activation cancellation or cleanup failed.", _errors);
                }
                _endCompletion.TrySetResult();
            }
            catch (Exception error)
            {
                _endCompletion.TrySetException(error);
            }
        }

        private void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _threadId)
            {
                throw new InvalidOperationException("Activation ownership and publication require the owning Unity thread.");
            }
        }
    }
}
