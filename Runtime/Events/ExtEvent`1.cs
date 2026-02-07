namespace ExtEvents
{
    using System;
    using JetBrains.Annotations;

    [Serializable]
    public class ExtEvent<T> : ExtEventBase<Action<T>>
    {
        private readonly unsafe void*[] _arguments = new void*[1];
        protected override unsafe void*[] Arguments => _arguments;

        private Type[] _eventParamTypes;
        protected override Type[] EventParamTypes => _eventParamTypes ??= new Type[] { typeof(T) };

        [PublicAPI]
        public event Action<T> DynamicListeners;
        
        protected override Action<T> DynamicListenersField
        {
            get => DynamicListeners;
            set => DynamicListeners = value;
        }

        protected override void PrepareArguments(params object[] args)
        {

            if (args.Length != 1 || args[0] is not T)
                throw new ArgumentException($"Expected 1 argument of type {typeof(T).Name}");
            
            unsafe
            {
                T typedArg = (T)args[0];
                _arguments[0] = UnsafeHelper.AsPointer(ref typedArg);
            }
        }

        protected override void InvokeDynamicListeners(params object[] args)
        {
            if (args.Length != 1 || args[0] is not T arg)
                throw new ArgumentException($"Expected 1 argument of type {typeof(T).Name}");
            
            DynamicListeners?.Invoke(arg);
        }

        [PublicAPI]
        public void Invoke(T arg) => InvokeInternal(arg);
    }
}
