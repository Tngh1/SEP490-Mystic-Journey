using UnityEngine;
using UnityEditor;
using System.Linq;

public class FindPopupManager {
    [MenuItem("Tools/FindPopupManager")]
    public static void Find() {
        var popups = GameObject.FindObjectsByType<MysticJourney.UI.UIPopupManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var popup in popups) {
            Debug.Log("Found UIPopupManager on: " + popup.gameObject.name);
            var btnConfirm = popup.GetType().GetField("btnConfirm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(popup) as UnityEngine.UI.Button;
            if (btnConfirm != null) {
                Debug.Log("btnConfirm: " + btnConfirm.gameObject.name);
                for (int i = 0; i < btnConfirm.onClick.GetPersistentEventCount(); i++) {
                    Debug.Log("Listener " + i + ": " + btnConfirm.onClick.GetPersistentTarget(i)?.name + " -> " + btnConfirm.onClick.GetPersistentMethodName(i));
                }
            }
        }
    }
}
