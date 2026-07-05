#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal static class MontageEditorKeyboardInput
    {
        public static bool WasSpacePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                return true;
#endif
            Event evt = Event.current;
            return evt != null && evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Space;
        }
    }
}
