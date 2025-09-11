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
            if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                Config.EnableSkipButton.Value = !Config.EnableSkipButton.Value;
            }
            if (Keyboard.current.f4Key.wasPressedThisFrame)
            {
                Config.VoiceInterruption.Value = !Config.VoiceInterruption.Value;
            }
        }

    }
}
