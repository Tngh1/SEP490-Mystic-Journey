using System;
using System.Collections.Generic;
using UnityEngine;

namespace NavMeshPlus.Extensions
{
    // Executes i disposable operation.
    public class NavMeshBuilderState: IDisposable
    {
        public Matrix4x4 worldToLocal;
        public Bounds worldBounds;
        public IEnumerable<GameObject> roots;
        private CompositeDisposable disposable;
        private Dictionary<Type, System.Object> mExtraState;
        private bool _disposed;

        // Executes new operation.
        public T GetExtraState<T>(bool dispose = true) where T : class, new()
        {
            if (mExtraState == null)  // Entity not found — short-circuit with appropriate error result
            { 
                mExtraState = new Dictionary<Type, System.Object>();
                disposable = new CompositeDisposable();
            }
            if (!mExtraState.TryGetValue(typeof(T), out System.Object extra))
            {
                extra = mExtraState[typeof(T)] = new T();
                if (dispose)
                {
                    disposable.Add(extra);
                }
            }

            return extra as T;
        }

        // Executes dispose operation.
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
                disposable?.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            _disposed = true;
        }

        // Executes dispose operation.
        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }
    }
    partial class CompositeDisposable: IDisposable
    {
        private bool _disposed;
        private List<IDisposable> extraStates = new List<IDisposable>();

        // Executes add operation.
        public void Add(IDisposable dispose)
        {
            extraStates.Add(dispose);
        }
        // Executes add operation.
        public void Add(object dispose)
        {
            if(dispose is IDisposable)
            {
                extraStates.Add((IDisposable)dispose);
            }
        }
        // Executes dispose operation.
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
                foreach (var item in extraStates)
                {
                    item?.Dispose();
                }
                extraStates.Clear();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            _disposed = true;
        }

        // Executes dispose operation.
        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }
    }
}