namespace ExtEvents
{
    using System;
    using JetBrains.Annotations;

    [Serializable]
    public class ExtEvent<T1, T2> : ExtEventBase<Action<T1, T2>>
    {
        private readonly unsafe void*[] _arguments = new void*[2];
        protected override unsafe void*[] Arguments => _arguments;

        private Type[] _eventParamTypes;
        protected override Type[] EventParamTypes => _eventParamTypes ??= new Type[] { typeof(T1), typeof(T2) };

        [PublicAPI]
        public event Action<T1, T2> DynamicListeners;
        
        protected override Action<T1, T2> DynamicListenersField
        {
            get => DynamicListeners;
            set => DynamicListeners = value;
        }

        protected override void PrepareArguments(params object[] args)
        {
            if (args.Length != 2 || args[0] is not T1 || args[1] is not T2)
                throw new ArgumentException($"Expected 2 arguments of types {typeof(T1).Name}, {typeof(T2).Name}");
            
            unsafe
            {
                T1 arg1 = (T1)args[0];
                T2 arg2 = (T2)args[1];
                _arguments[0] = UnsafeHelper.AsPointer(ref arg1);
                _arguments[1] = UnsafeHelper.AsPointer(ref arg2);
            }
        }

        protected override void InvokeDynamicListeners(params object[] args)
        {
            if (args.Length != 2 || args[0] is not T1 || args[1] is not T2)
                throw new ArgumentException($"Expected 2 arguments of types {typeof(T1).Name}, {typeof(T2).Name}");
            
            DynamicListeners?.Invoke((T1)args[0], (T2)args[1]);
        }

        [PublicAPI]
        public void Invoke(T1 arg1, T2 arg2) => InvokeInternal(arg1, arg2);
    }
}
