using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using R3;
using UnityEngine.UIElements;

namespace Sim.Faciem
{
    /// <summary>
    /// Framework-owned behavior for an existing cached UXML tree, not a VisualElement or ViewModel owner.
    /// Constructors query structure only; subscriptions belong in OnBind using Bind/OwnBinding.
    /// </summary>
    public abstract class View
    {
        private BaseViewModel _model;
        private VisualElement _root;
        private NavigationActivation _activation;
        private Func<bool> _ownsRoot;
        internal bool IsDisposed { get; private set; }
        internal BaseViewModel Model => _model;
        internal VisualElement VisualRoot => _root;

        protected View(BaseViewModel viewModel, VisualElement root)
        {
            _model = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        protected VisualElement Root => !IsDisposed ? _root : throw new ObjectDisposedException(GetType().Name);
        protected NavigationActivation Activation => _activation;

        /// <summary>Runs once per native activation. Use a replaying state source for immediate initial visuals.</summary>
        protected abstract void OnBind();

        /// <summary>
        /// Subscribes through the native activation, applying synchronous changes only while the captured
        /// activation and its original cached/mounted root remain owned. Replaying state applies immediately.
        /// </summary>
        protected void Bind<T>(Observable<T> state, Action<T> apply)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            NavigationActivation activation = _activation ?? throw new InvalidOperationException("Bindings require an active OnBind.");
            if (apply == null)
            {
                throw new ArgumentNullException(nameof(apply));
            }
            bool subscribing = true;
            Exception replayError = null;
            Action<T> guarded = Guard<T>(value =>
            {
                if (!subscribing)
                {
                    apply(value);
                    return;
                }
                if (replayError != null)
                {
                    return;
                }
                try
                {
                    apply(value);
                }
                catch (Exception error)
                {
                    // R3 normally reports observer errors without throwing from Subscribe. Entry must not falsely succeed.
                    replayError = error;
                }
            });
            activation.Own(state.Subscribe(guarded));
            subscribing = false;
            if (replayError != null)
            {
                ExceptionDispatchInfo.Capture(replayError).Throw();
            }
        }

        /// <summary>
        /// Capture during OnBind before async work or registering a custom event. The returned callback
        /// retains that activation, never a later re-entry. Invoke on the owning Unity thread.
        /// Direct unguarded writes/async-void callbacks are not managed publication.
        /// </summary>
        protected Action<T> Guard<T>(Action<T> apply)
        {
            if (apply == null)
            {
                throw new ArgumentNullException(nameof(apply));
            }
            NavigationActivation activation = _activation ?? throw new InvalidOperationException("Bindings require an active OnBind.");
            Func<bool> ownsRoot = _ownsRoot;
            return value => activation.TryPublish(() =>
            {
                if (!IsDisposed && ownsRoot())
                {
                    apply(value);
                }
            });
        }

        /// <summary>Own a custom event unsubscriber/resource during synchronous OnBind; cleanup is native activation-scoped.</summary>
        protected void OwnBinding(IDisposable subscription)
        {
            if (_activation == null)
            {
                throw new InvalidOperationException("Bindings require an active OnBind.");
            }
            _activation.Own(subscription);
        }

        internal void BindActivation(NavigationActivation activation, Func<bool> ownsRoot)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
            if (ReferenceEquals(_activation, activation))
            {
                return;
            }
            _activation = activation;
            _ownsRoot = ownsRoot;
            activation.Own(Disposable.Create(() =>
            {
                if (ReferenceEquals(_activation, activation))
                {
                    _activation = null;
                    _ownsRoot = null;
                }
            }));
            OnBind();
        }

        // Called only for abandoned preparation or after native activation drain/final release.
        internal void ReleaseReferences()
        {
            IsDisposed = true;
            _activation = null;
            _ownsRoot = null;
            _model = null;
            _root = null;
        }

        internal static Type ResolveType(ViewIdAsset asset)
        {
            if (string.IsNullOrWhiteSpace(asset.TypedView?.TypeName))
            {
                return null;
            }
            Type type = asset.TypedView.GetReferencedType();
            if (type == null || type.IsAbstract || type.ContainsGenericParameters || !typeof(View).IsAssignableFrom(type))
            {
                throw new InvalidOperationException("TypedView must resolve to a concrete View<TViewModel> type.");
            }
            return type;
        }

        internal static View Create(Type type, BaseViewModel model, VisualElement root)
        {
            if (type == null)
            {
                return null;
            }
            Type generic = type;
            while (generic != null && (!generic.IsGenericType || generic.GetGenericTypeDefinition() != typeof(View<>)))
            {
                generic = generic.BaseType;
            }
            if (generic == null || !generic.GetGenericArguments()[0].IsInstanceOfType(model))
            {
                throw new InvalidOperationException("TypedView's ViewModel type must accept the existing cached model.");
            }
            ConstructorInfo constructor = type.GetConstructor(new[] { generic.GetGenericArguments()[0], typeof(VisualElement) });
            if (constructor == null)
            {
                throw new InvalidOperationException("TypedView requires a public (TViewModel, VisualElement) constructor.");
            }
            View behavior;
            try
            {
                behavior = (View)constructor.Invoke(new object[] { model, root });
            }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
            if (!ReferenceEquals(behavior.Model, model) || !ReferenceEquals(behavior.VisualRoot, root))
            {
                behavior.ReleaseReferences();
                throw new InvalidOperationException("TypedView must retain exactly the supplied ViewModel and root.");
            }
            return behavior;
        }
    }

    /// <summary>Optional strongly typed imperative behavior; UXML/USS still own structure/appearance.</summary>
    public abstract class View<TViewModel> : View where TViewModel : BaseViewModel
    {
        protected View(TViewModel viewModel, VisualElement root) : base(viewModel, root)
        {
        }

        protected TViewModel ViewModel => !IsDisposed ? (TViewModel)Model : throw new ObjectDisposedException(GetType().Name);
    }
}
