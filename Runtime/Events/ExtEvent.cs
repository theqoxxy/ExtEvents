namespace ExtEvents
{
    using System;
    using JetBrains.Annotations;

    [Serializable]
    public class ExtEvent : ExtEventBase<Action>
    {
        protected override Type[] GetEventParamTypes() => Type.EmptyTypes;

        protected override void InvokeDynamicListeners()
        {
            DynamicListeners?.Invoke();
        }

        protected override unsafe void PrepareArguments(void*[] arguments)
        {
            // No arguments for parameterless event
        }

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