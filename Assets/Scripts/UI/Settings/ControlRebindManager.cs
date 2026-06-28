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

        /// <summary>True nếu người dùng đã rebind nhưng chưa nhấn Save.</summary>
        public bool HasUnsavedChanges { get; private set; }

        /// <summary>Phát khi phím mới bị trùng. Tham số: thông báo lỗi.</summary>
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

            // Đang rebind → chặn, không mở operation mới
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
                    op.Dispose();
                    currentRebindOperation = null;

                    string conflict = FindConflict(binding);
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

        /// <summary>
        /// Trả về path hiện tại của một binding:
        /// ưu tiên overridePath, nếu không có thì lấy path gốc (default).
        /// So sánh cả hai để phát hiện trùng với binding đã lưu lẫn binding mặc định.
        /// </summary>
        private static string GetCurrentPath(InputBinding b)
        {
            // overridePath có khi đã rebind (kể cả đã load từ save)
            if (!string.IsNullOrEmpty(b.overridePath)) return b.overridePath;
            // path là giá trị mặc định trong Input Action Asset
            return b.path;
        }

        private string FindConflict(ControlBinding target)
        {
            InputAction targetAction = target.action?.action;
            if (targetAction == null || target.bindingIndex >= targetAction.bindings.Count) return null;

            // Lấy path của phím vừa gán — normalize về lowercase để tránh mọi vấn đề format/case
            string newPath = GetCurrentPath(targetAction.bindings[target.bindingIndex])
                             ?.ToLowerInvariant();
            if (string.IsNullOrEmpty(newPath)) return null;

            foreach (var other in bindings)
            {
                if (other == target || other.action == null) continue;

                InputAction otherAction = other.action.action;
                if (otherAction == null) continue;

                // Scan TẤT CẢ binding trong action của other (bỏ qua composite meta).
                // Lý do: nếu bindingIndex trỏ vào composite meta (isComposite=true, path rỗng)
                // thì chỉ kiểm tra đúng 1 index sẽ miss. Scan toàn bộ đảm bảo không bỏ sót
                // binding mặc định (chỉ có .path) lẫn binding đã save/load (có .overridePath).
                foreach (var b in otherAction.bindings)
                {
                    if (b.isComposite) continue; // composite meta không có path thực

                    string otherPath = GetCurrentPath(b)?.ToLowerInvariant();
                    if (string.IsNullOrEmpty(otherPath)) continue;
                    if (otherPath != newPath) continue;

                    // Trùng → trả về thông báo
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

        /// <summary>
        /// Hủy operation đang chờ nhấn phím (nếu có).
        /// Gọi trước Reset/Load để tránh lỗi khi interrupt rebind đang chạy.
        /// </summary>
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
