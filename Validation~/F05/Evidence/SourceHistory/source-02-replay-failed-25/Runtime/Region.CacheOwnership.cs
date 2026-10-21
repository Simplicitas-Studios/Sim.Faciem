using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine.UIElements;

namespace Sim.Faciem
{
    public partial class Region
    {
        internal sealed class CacheEntry
        {
            internal readonly ViewId Id;
            internal readonly VisualElement View;
            internal readonly BaseViewModel Model;
            internal readonly Sim.Faciem.View TypedView;

            internal CacheEntry(ViewId id, VisualElement view, BaseViewModel model, Sim.Faciem.View typedView)
            {
                Id = id;
                View = view;
                Model = model;
                TypedView = typedView;
            }
        }

        private readonly List<CacheEntry> _ownedEntries = new();
        private readonly List<BaseViewModel> _ownedModels = new();
        internal readonly int OwnerThreadId = Thread.CurrentThread.ManagedThreadId;
        internal bool IsReleased;
        internal NavigationResult ReleaseResult;
        internal CacheEntry[] OwnedEntries => _ownedEntries.ToArray();
        internal BaseViewModel[] OwnedModels => _ownedModels.ToArray();
        internal KeyValuePair<ViewId, VisualElement>[] CachedViews => _views.ToArray();

        internal void RecordCache(ViewId id, VisualElement view, BaseViewModel model, Sim.Faciem.View typedView = null)
        {
            if (IsReleased)
            {
                throw new ObjectDisposedException(nameof(Region));
            }
            if (!_ownedEntries.Any(entry => entry.Id.Equals(id) && ReferenceEquals(entry.View, view) && ReferenceEquals(entry.Model, model)))
            {
                _ownedEntries.Add(new CacheEntry(id, view, model, typedView));
            }
            if (!_ownedModels.Any(previous => ReferenceEquals(previous, model)))
            {
                _ownedModels.Add(model);
            }
        }

        internal bool OwnsModel(BaseViewModel model)
        {
            return _ownedModels.Any(previous => ReferenceEquals(previous, model));
        }

        internal void ClearCacheReferences()
        {
            _views.Clear();
            _currentActiveViews.Clear();
            _ownedEntries.Clear();
            _ownedModels.Clear();
        }
    }
}
