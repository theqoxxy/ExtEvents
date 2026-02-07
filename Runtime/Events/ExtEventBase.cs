namespace ExtEvents
{
    using System;
    using System.Runtime.CompilerServices;
    using JetBrains.Annotations;

    [Serializable]
    public abstract class ExtEventBase<TDelegate> : BaseExtEvent where TDelegate : Delegate
    {
        protected abstract TDelegate DynamicListenersField { get; set; }
        protected abstract unsafe void*[] Arguments { get; }
        internal override Delegate _dynamicListeners => DynamicListenersField;

        protected abstract void PrepareArguments(params object[] args);
        protected abstract void InvokeDynamicListeners(params object[] args);

        [PublicAPI]
        public void AddListener(TDelegate listener) => DynamicListenersField = (TDelegate)Delegate.Combine(DynamicListenersField, listener);

        [PublicAPI]
        public void RemoveListener(TDelegate listener) => DynamicListenersField = (TDelegate)Delegate.Remove(DynamicListenersField, listener);

        [PublicAPI]
        public void RemoveAllListeners() => DynamicListenersField = null;

        protected void InvokeInternal(params object[] args)
        {
            unsafe
            {
                PrepareArguments(args);

                if (Arguments != null)
                {
                    // ReSharper disable once ForCanBeConvertedToForeach
                    for (int index = 0; index < _persistentListeners.Length; index++)
                    {
                        _persistentListeners[index].Invoke(Arguments);
                    }
                }
                else
                {
                    for (int index = 0; index < _persistentListeners.Length; index++)
                    {
                        _persistentListeners[index].Invoke(null);
                    }
                }
            }

            InvokeDynamicListeners(args);
        }

        public static ExtEventBase<TDelegate> operator +(ExtEventBase<TDelegate> extEvent, TDelegate listener)
        {
            if (extEvent == null)
                return null;

            extEvent.AddListener(listener);
            return extEvent;
        }

        public static ExtEventBase<TDelegate> operator -(ExtEventBase<TDelegate> extEvent, TDelegate listener)
        {
            if (extEvent == null)
                return null;

            extEvent.RemoveListener(listener);
            return extEvent;
        }

        internal static unsafe class UnsafeHelper
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void* AsPointer<T>(ref T value) => Unsafe.AsPointer(ref value);
        }
    }
}