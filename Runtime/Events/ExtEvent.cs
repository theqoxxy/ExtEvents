namespace ExtEvents
{
    using System;
    using JetBrains.Annotations;

    [Serializable]
    public class ExtEvent : ExtEventBase<Action>
    {
        protected override Type[] EventParamTypes => Type.EmptyTypes;
        
        protected override unsafe void*[] Arguments => null;

        [PublicAPI]
        public event Action DynamicListeners;
        
        protected override Action DynamicListenersField
        {
            get => DynamicListeners;
            set => DynamicListeners = value;
        }

        protected override void PrepareArguments(params object[] args)
        {
            if (args.Length != 0)
                throw new ArgumentException("ExtEvent without parameters expects no arguments");
        }

        protected override void InvokeDynamicListeners(params object[] args)
        {
            if (args.Length != 0)
                throw new ArgumentException("ExtEvent without parameters expects no arguments");
            
            DynamicListeners?.Invoke();
        }

        /// <summary>
        /// Invokes all listeners of the event.
        /// </summary>
        [PublicAPI]
        public void Invoke() => InvokeInternal();
    }
}
