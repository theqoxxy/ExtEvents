namespace ExtEvents
{
    using System;
    using JetBrains.Annotations;

    [Serializable]
    public class ExtEvent<T> : ExtEventBase<Action<T>, T>
    {
        protected override Type[] GetEventParamTypes() => new Type[] { typeof(T) };

        /// <summary>
        /// Invokes all listeners of the event.
        /// </summary>
        [PublicAPI]
        public void Invoke(T arg) => InvokeWithArgument(arg);

        protected override void InvokeDynamicListeners()
        {
            throw new InvalidOperationException("Parameterless Invoke() is not supported for ExtEvent<T>");
        }

        protected override void InvokeDynamicListenersWithArgument<TArg>(TArg arg)
        {
            if (arg is T typedArg)
            {
                DynamicListeners?.Invoke(typedArg);
            }
        }

        public static ExtEvent<T> operator +(ExtEvent<T> extEvent, Action<T> listener)
        {
            if (extEvent == null)
                return null;

            extEvent.AddListener(listener);
            return extEvent;
        }

        public static ExtEvent<T> operator -(ExtEvent<T> extEvent, Action<T> listener)
        {
            if (extEvent == null)
                return null;

            extEvent.RemoveListener(listener);
            return extEvent;
        }
    }
}