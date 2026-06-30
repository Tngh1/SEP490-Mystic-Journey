using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MysticJourney.Screen.GameSetting
{
    public class ControlRebindManager : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private List<ControlBinding> bindings = new();

        private InputActionRebindingExtensions.RebindingOperation currentRebindOperation;
        private const string BindingKey = "MJ_KEY_BINDINGS";

        public bool HasUnsavedChanges { get; private set; }

        public event Action<string> OnConflictDetected;

        // ─── Lifecycle ───────────────────────────────────────────────────────────

        private void Start()
        {
            // LoadBindings() được gọi bởi GameSettingUIManager để tránh gọi 2 lần.
            RegisterButtons();
        }

        private void OnDestroy()
        {
            currentRebindOperation?.Dispose();
            currentRebindOperation = null;
        }

        // ─── UI Text ─────────────────────────────────────────────────────────────

        private void UpdateAllTexts()
        {
            foreach (var b in bindings)
                UpdateBindingText(b);
        }

        private void UpdateBindingText(ControlBinding binding)
        {
            if (binding?.action == null || binding.keyText == null) return;

            InputAction action = binding.action.action;
            if (action == null || binding.bindingIndex >= action.bindings.Count) return;

            binding.keyText.text = action.GetBindingDisplayString(binding.bindingIndex);
        }

        // ─── Button Registration ──────────────────────────────────────────────────

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

        // ─── Rebind ───────────────────────────────────────────────────────────────

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
                    
                    // LẤY RA CONTROL VỪA NHẤN TRƯỚC KHI DISPOSE
                    InputControl newControl = op.selectedControl; 
                    
                    op.Dispose();
                    currentRebindOperation = null;

                    // TRUYỀN CONTROL VÀO HÀM KIỂM TRA
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

        // ─── Conflict Detection ───────────────────────────────────────────────────
        private static string GetCurrentPath(InputBinding b)
        {
            // overridePath có khi đã rebind (kể cả đã load từ save)
            if (!string.IsNullOrEmpty(b.overridePath)) return b.overridePath;
            // path là giá trị mặc định trong Input Action Asset
            return b.path;
        }

        // ─── Conflict Detection ───────────────────────────────────────────────────

// ─── Conflict Detection ───────────────────────────────────────────────────

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

        // ─── Save / Load / Reset ──────────────────────────────────────────────────

        private void CancelCurrentRebind()
        {
            if (currentRebindOperation == null) return;

            // Lưu local ref và null field TRƯỚC khi Cancel()
            // vì Cancel() → OnCancel callback → set currentRebindOperation = null
            // → nếu không làm vậy thì dòng Dispose() sau sẽ NullReference
            var op = currentRebindOperation;
            currentRebindOperation = null;
            op.Cancel();
            op.Dispose();
        }

        public void SaveBindings()
        {
            if (bindings.Count == 0 || bindings[0].action == null) return;

            PlayerPrefs.SetString(BindingKey, bindings[0].action.asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
            HasUnsavedChanges = false;
        }

        public void LoadBindings()
        {
            CancelCurrentRebind(); // Hủy rebind đang chờ nếu có

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

        public void ResetBindings()
        {
            CancelCurrentRebind(); // Hủy rebind đang chờ nếu có

            if (bindings.Count == 0 || bindings[0].action == null) return;

            bindings[0].action.asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(BindingKey);
            PlayerPrefs.Save();
            UpdateAllTexts();
            HasUnsavedChanges = false;
        }
    }
}
