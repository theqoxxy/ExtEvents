namespace ExtEvents
{
    using System;
    using JetBrains.Annotations;

    [Serializable]
    public class ExtEvent<T1, T2, T3> : ExtEventBase<Action<T1, T2, T3>, T1, T2, T3>
    {
        protected override Type[] GetEventParamTypes() => new Type[] { typeof(T1), typeof(T2), typeof(T3) };

        /// <summary>
        /// Invokes all listeners of the event.
        /// </summary>
        [PublicAPI]
        public void Invoke(T1 arg1, T2 arg2, T3 arg3) => InvokeWithArguments(arg1, arg2, arg3);

        protected override void InvokeDynamicListeners()
        {
            throw new InvalidOperationException("Parameterless Invoke() is not supported for ExtEvent<T1, T2, T3>");
        }

        protected override void InvokeDynamicListenersWithArguments<TArg1, TArg2, TArg3>(TArg1 arg1, TArg2 arg2, TArg3 arg3)
        {
            if (arg1 is T1 typedArg1 && arg2 is T2 typedArg2 && arg3 is T3 typedArg3)
            {
                DynamicListeners?.Invoke(typedArg1, typedArg2, typedArg3);
            }
        }

        public static ExtEvent<T1, T2, T3> operator +(ExtEvent<T1, T2, T3> extEvent, Action<T1, T2, T3> listener)
        {
            if (extEvent == null)
                return null;

            extEvent.AddListener(listener);
            return extEvent;
        }

        public static ExtEvent<T1, T2, T3> operator -(ExtEvent<T1, T2, T3> extEvent, Action<T1, T2, T3> listener)
        {
            if (extEvent == null)
                return null;

            extEvent.RemoveListener(listener);
            return extEvent;
        }
    }
}