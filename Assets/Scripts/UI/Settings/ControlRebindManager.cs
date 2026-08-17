using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MysticJourney.Screen.GameSetting
{
    // Executes core business logic for mono behaviour.
    public class ControlRebindManager : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private List<ControlBinding> bindings = new();

        private InputActionRebindingExtensions.RebindingOperation currentRebindOperation;
        private const string BindingKey = "MJ_KEY_BINDINGS";

        // Executes core business logic for has unsaved changes.
        public bool HasUnsavedChanges { get; private set; }

        public event Action<string> OnConflictDetected;


        // Performs startup initialization for ControlRebindManager on the first active frame.
        // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
        private void Start()
        {
            RegisterButtons();
        }

        // Unsubscribe this component's event handlers and release its temporary runtime resources.
        private void OnDestroy()
        {
            currentRebindOperation?.Dispose();
            currentRebindOperation = null;
        }


        // Update all texts; it updates binding text and processes each matching entry.
        private void UpdateAllTexts()
        {
            foreach (var b in bindings)
                UpdateBindingText(b);
        }

        // Executes core business logic for update binding text.
        private void UpdateBindingText(ControlBinding binding)
        {
            if (binding?.action == null || binding.keyText == null) return;

            InputAction action = binding.action.action;
            if (action == null || binding.bindingIndex >= action.bindings.Count) return;

            binding.keyText.text = action.GetBindingDisplayString(binding.bindingIndex);
        }


        // Executes core business logic for register buttons.
        private void RegisterButtons()
        {
            foreach (var binding in bindings)
            {
                if (binding.button == null) continue;

                ControlBinding cache = binding;
                binding.button.onClick.RemoveAllListeners();
                binding.button.onClick.AddListener(() => StartRebind(cache));
            }
        }


// Executes core business logic for start rebind.
private void StartRebind(ControlBinding binding)
        {
            if (binding?.action == null) return;

            InputAction action = binding.action.action;
            if (action == null) return;

            if (currentRebindOperation != null) return;

            action.Disable();
            binding.keyText.text = "...";

            var rebindOp = action
                .PerformInteractiveRebinding(binding.bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(op =>
                {
                    action.Enable();
                    UpdateBindingText(binding);
                    op.Dispose();
                    currentRebindOperation = null;
                })
                .OnComplete(op =>
                {
                    action.Enable();

                    InputControl newControl = op.selectedControl;

                    op.Dispose();
                    currentRebindOperation = null;

                    string conflict = FindConflict(binding, newControl);
                    if (conflict != null)
                    {
                        action.RemoveBindingOverride(binding.bindingIndex);
                        UpdateBindingText(binding);
                        OnConflictDetected?.Invoke(conflict);
                        return;
                    }

                    UpdateAllTexts();
                    HasUnsavedChanges = true;
                });

            if (binding.excludeMouse)
                rebindOp = rebindOp.WithControlsExcluding("<Mouse>");

            currentRebindOperation = rebindOp;
            currentRebindOperation.Start();
        }

        // Executes core business logic for get current path.
        // Logic details: validates required non-empty string arguments.
        private static string GetCurrentPath(InputBinding b)
        {
            if (!string.IsNullOrEmpty(b.overridePath)) return b.overridePath;
            return b.path;
        }



        // Executes core business logic for find conflict.
        // Logic details: validates required non-empty string arguments.
        private string FindConflict(ControlBinding target, InputControl newControl)
        {
            if (newControl == null) return null;

            InputAction targetAction = target.action?.action;
            if (targetAction == null || target.bindingIndex >= targetAction.bindings.Count) return null;

            foreach (var other in bindings)
            {
                if (other == target || other.action == null) continue;

                InputAction otherAction = other.action.action;
                if (otherAction == null) continue;

                if (otherAction == targetAction && other.bindingIndex == target.bindingIndex) continue;

                if (other.bindingIndex >= otherAction.bindings.Count) continue;

                var otherBindingEntry = otherAction.bindings[other.bindingIndex];

                if (otherBindingEntry.isComposite) continue;

                string otherPath = otherBindingEntry.effectivePath;
                if (string.IsNullOrEmpty(otherPath)) continue;

                if (InputControlPath.Matches(otherPath, newControl))
                {
                    string label = string.IsNullOrEmpty(other.displayName)
                        ? otherAction.name
                        : other.displayName;

                    string key = targetAction.GetBindingDisplayString(target.bindingIndex);
                    return $"\"{key}\" is already assigned to {label}";
                }
            }

            return null;
        }


        // Executes core business logic for cancel current rebind.
        private void CancelCurrentRebind()
        {
            if (currentRebindOperation == null) return;

            var op = currentRebindOperation;
            currentRebindOperation = null;
            op.Cancel();
            op.Dispose();
        }

        // Executes core business logic for save bindings.
        public void SaveBindings()
        {
            if (bindings.Count == 0 || bindings[0].action == null) return;

            PlayerPrefs.SetString(BindingKey, bindings[0].action.asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
            HasUnsavedChanges = false;
        }

        // Executes core business logic for load bindings.
        public void LoadBindings()
        {
            CancelCurrentRebind();

            if (!PlayerPrefs.HasKey(BindingKey))
            {
                UpdateAllTexts();
                HasUnsavedChanges = false;
                return;
            }

            if (bindings.Count == 0 || bindings[0].action == null) return;

            bindings[0].action.asset.LoadBindingOverridesFromJson(PlayerPrefs.GetString(BindingKey));
            UpdateAllTexts();
            HasUnsavedChanges = false;
        }

        // Executes core business logic for reset bindings.
        public void ResetBindings()
        {
            CancelCurrentRebind();

            if (bindings.Count == 0 || bindings[0].action == null) return;

            bindings[0].action.asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(BindingKey);
            PlayerPrefs.Save();
            UpdateAllTexts();
            HasUnsavedChanges = false;
        }
    }
}
