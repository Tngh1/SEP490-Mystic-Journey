using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MysticJourney.Screen.GameSetting
{
    [System.Serializable]
    public class ControlBinding
    {
        [Header("Input")]

        public InputActionReference action;

        [Tooltip("Binding index trong Input Action")]
        public int bindingIndex;

        [Header("UI")]

        public Button button;

        public TMP_Text keyText;

        public string displayName;

        [Header("Options")]

        public bool excludeMouse = true;
    }
}