namespace ExtEvents
{
    using System;
    using System.Runtime.CompilerServices;
    using JetBrains.Annotations;

    [Serializable]
    public abstract class ExtEventBase<TDelegate> : BaseExtEvent where TDelegate : Delegate
    {
        private Type[] _eventParamTypes;
        protected override Type[] EventParamTypes => _eventParamTypes ??= GetEventParamTypes();

        /// <summary>
        /// The dynamic listeners list that you can add your listener to.
        /// </summary>
        [PublicAPI]
        public TDelegate DynamicListeners;
        
        internal override Delegate _dynamicListeners => DynamicListeners;

        /// <summary>
        /// Invokes all listeners of the event.
        /// </summary>
        [PublicAPI]
        public void Invoke()
        {
            unsafe
            {
                // ReSharper disable once ForCanBeConvertedToForeach
                for (int index = 0; index < _persistentListeners.Length; index++)
                {
                    _persistentListeners[index].Invoke(null);
                }
            }

            InvokeDynamicListeners();
        }

        [PublicAPI]
        public void AddListener(TDelegate listener) => DynamicListeners = (TDelegate)Delegate.Combine(DynamicListeners, listener);

        [PublicAPI]
        public void RemoveListener(TDelegate listener) => DynamicListeners = (TDelegate)Delegate.Remove(DynamicListeners, listener);

        [PublicAPI]
        public void RemoveAllListeners() => DynamicListeners = null;

        protected abstract Type[] GetEventParamTypes();
        protected abstract void InvokeDynamicListeners();
        protected abstract unsafe void PrepareArguments(void*[] arguments);
    }

    [Serializable]
    public abstract class ExtEventBase<TDelegate, T> : ExtEventBase<TDelegate> where TDelegate : Delegate
    {
        private readonly unsafe void*[] _arguments = new void*[1];

        protected override unsafe void PrepareArguments(void*[] arguments)
        {
            _arguments[0] = arguments[0];
        }

        protected void InvokeWithArgument<TArg>(TArg arg)
        {
            unsafe
            {
                _arguments[0] = Unsafe.AsPointer(ref arg);

                // ReSharper disable once ForCanBeConvertedToForeach
                for (int index = 0; index < _persistentListeners.Length; index++)
                {
                    _persistentListeners[index].Invoke(_arguments);
                }
            }

            InvokeDynamicListenersWithArgument(arg);
        }

        protected abstract void InvokeDynamicListenersWithArgument<TArg>(TArg arg);
    }

    [Serializable]
    public abstract class ExtEventBase<TDelegate, T1, T2> : ExtEventBase<TDelegate> where TDelegate : Delegate
    {
        private readonly unsafe void*[] _arguments = new void*[2];

        protected override unsafe void PrepareArguments(void*[] arguments)
        {
            _arguments[0] = arguments[0];
            _arguments[1] = arguments[1];
        }

        protected void InvokeWithArguments<TArg1, TArg2>(TArg1 arg1, TArg2 arg2)
        {
            unsafe
            {
                _arguments[0] = Unsafe.AsPointer(ref arg1);
                _arguments[1] = Unsafe.AsPointer(ref arg2);

                // ReSharper disable once ForCanBeConvertedToForeach
                for (int index = 0; index < _persistentListeners.Length; index++)
                {
                    _persistentListeners[index].Invoke(_arguments);
                }
            }

            InvokeDynamicListenersWithArguments(arg1, arg2);
        }

        protected abstract void InvokeDynamicListenersWithArguments<TArg1, TArg2>(TArg1 arg1, TArg2 arg2);
    }

    [Serializable]
    public abstract class ExtEventBase<TDelegate, T1, T2, T3> : ExtEventBase<TDelegate> where TDelegate : Delegate
    {
        private readonly unsafe void*[] _arguments = new void*[3];

        protected override unsafe void PrepareArguments(void*[] arguments)
        {
            _arguments[0] = arguments[0];
            _arguments[1] = arguments[1];
            _arguments[2] = arguments[2];
        }

        protected void InvokeWithArguments<TArg1, TArg2, TArg3>(TArg1 arg1, TArg2 arg2, TArg3 arg3)
        {
            unsafe
            {
                _arguments[0] = Unsafe.AsPointer(ref arg1);
                _arguments[1] = Unsafe.AsPointer(ref arg2);
                _arguments[2] = Unsafe.AsPointer(ref arg3);

                // ReSharper disable once ForCanBeConvertedToForeach
                for (int index = 0; index < _persistentListeners.Length; index++)
                {
                    _persistentListeners[index].Invoke(_arguments);
                }
            }

            InvokeDynamicListenersWithArguments(arg1, arg2, arg3);
        }

        protected abstract void InvokeDynamicListenersWithArguments<TArg1, TArg2, TArg3>(TArg1 arg1, TArg2 arg2, TArg3 arg3);
    }
}