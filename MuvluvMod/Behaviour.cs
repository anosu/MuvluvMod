using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MuvluvMod
{
    public class PluginBehaviour : MonoBehaviour
    {
        void Update()
        {
            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                Config.Translation.Value = !Config.Translation.Value;
            }
        }

    }
}
