namespace ExtEvents
{
    using System;
    using JetBrains.Annotations;

    [Serializable]
    public class ExtEvent<T1, T2> : ExtEventBase<Action<T1, T2>, T1, T2>
    {
        protected override Type[] GetEventParamTypes() => new Type[] { typeof(T1), typeof(T2) };

        /// <summary>
        /// Invokes all listeners of the event.
        /// </summary>
        [PublicAPI]
        public void Invoke(T1 arg1, T2 arg2) => InvokeWithArguments(arg1, arg2);

        protected override void InvokeDynamicListeners()
        {
            throw new InvalidOperationException("Parameterless Invoke() is not supported for ExtEvent<T1, T2>");
        }

        protected override void InvokeDynamicListenersWithArguments<TArg1, TArg2>(TArg1 arg1, TArg2 arg2)
        {
            if (arg1 is T1 typedArg1 && arg2 is T2 typedArg2)
            {
                DynamicListeners?.Invoke(typedArg1, typedArg2);
            }
        }

        public static ExtEvent<T1, T2> operator +(ExtEvent<T1, T2> extEvent, Action<T1, T2> listener)
        {
            if (extEvent == null)
                return null;

            extEvent.AddDynamicListener(listener);
            return extEvent;
        }

        public static ExtEvent<T1, T2> operator -(ExtEvent<T1, T2> extEvent, Action<T1, T2> listener)
        {
            if (extEvent == null)
                return null;

            extEvent.RemoveDynamicListener(listener);
            return extEvent;
        }
    }
}