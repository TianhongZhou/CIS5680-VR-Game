using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CIS5680VRGame.Balls
{
    /// <summary>
    /// Works around an XRIT 3.1.2 lifecycle bug where the socket coroutine can be null in OnEnable/OnDisable.
    /// </summary>
    public class SafeXRSocketInteractor : XRSocketInteractor
    {
        static readonly FieldInfo s_UpdateRoutineField =
            typeof(XRSocketInteractor).GetField("m_UpdateCollidersAfterTriggerStay", BindingFlags.Instance | BindingFlags.NonPublic);

        static readonly MethodInfo s_CreateRoutineMethod =
            typeof(XRSocketInteractor).GetMethod("UpdateCollidersAfterOnTriggerStay", BindingFlags.Instance | BindingFlags.NonPublic);

        void EnsureUpdateRoutine()
        {
            if (s_UpdateRoutineField == null || s_CreateRoutineMethod == null)
                return;

            if (s_UpdateRoutineField.GetValue(this) is IEnumerator)
                return;

            var routine = s_CreateRoutineMethod.Invoke(this, null) as IEnumerator;
            if (routine != null)
                s_UpdateRoutineField.SetValue(this, routine);
        }

        protected override void Awake()
        {
            base.Awake();
            showInteractableHoverMeshes = false;
            EnsureUpdateRoutine();
        }

        protected override void OnEnable()
        {
            showInteractableHoverMeshes = false;
            EnsureUpdateRoutine();
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            EnsureUpdateRoutine();
            base.OnDisable();
        }
    }
}
