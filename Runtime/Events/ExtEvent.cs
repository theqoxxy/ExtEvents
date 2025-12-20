namespace ExtEvents
{
    using System;
    using JetBrains.Annotations;

    [Serializable]
    public class ExtEvent : BaseExtEvent
    {
        protected override Type[] EventParamTypes => Type.EmptyTypes;

        /// <summary>
        /// The dynamic listeners list that you can add your listener to.
        /// </summary>
        [PublicAPI]
        public event Action DynamicListeners;
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

            DynamicListeners?.Invoke();
        }

        [PublicAPI]
        public void AddListener(Action listener) => DynamicListeners += listener;

        [PublicAPI]
        public void RemoveListener(Action listener) => DynamicListeners -= listener;

        public static ExtEvent operator +(ExtEvent extEvent, Action listener)
        {
            if (extEvent == null)
                return null;

            extEvent.AddListener(listener);
            return extEvent;
        }

        public static ExtEvent operator -(ExtEvent extEvent, Action listener)
        {
            if (extEvent == null)
                return null;

            extEvent.RemoveListener(listener);
            return extEvent;
        }
    }
}
