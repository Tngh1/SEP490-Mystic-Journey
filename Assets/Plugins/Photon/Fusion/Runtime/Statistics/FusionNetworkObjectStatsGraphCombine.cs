namespace Fusion.Statistics {
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System;

  // Executes mono behaviour operation.
  public class FusionNetworkObjectStatsGraphCombine : MonoBehaviour {
    
    [SerializeField] private Text _titleText;
    [SerializeField] private Dropdown _statDropdown;
    [SerializeField] private NetworkObjectStat _statsToRender;
    [SerializeField] private RectTransform _rect;
    [SerializeField] private RectTransform _combinedGraphRender;
    [SerializeField] private Button _toggleButton;

    private float _headerHeight = 50;
    private float _graphHeight = 150;

    private Dictionary<NetworkObjectStat, FusionNetworkObjectStatsGraph> _statsGraphs;
    [SerializeField]
    private FusionNetworkObjectStatsGraph _statsGraphPrefab;

    private ContentSizeFitter _parentContentSizeFitter;

    /// <summary>
    /// Gets the unique identifier of the network object.
    /// </summary>
    /// <value>
    /// The network object identifier.
    /// </value>
    public NetworkId NetworkObjectID => _networkObject.Id;

    private NetworkObject _networkObject;
    private FusionStatistics _fusionStatistics;
    private FusionNetworkObjectStatistics _objectStatisticsInstance;

    // Executes setup network object operation.
    public void SetupNetworkObject(NetworkObject networkObject, FusionStatistics fusionStatistics, FusionNetworkObjectStatistics objectStatisticsInstance) {
      _networkObject = networkObject;
      _fusionStatistics = fusionStatistics;
      _objectStatisticsInstance = objectStatisticsInstance;
    }

    // Performs startup initialization for FusionNetworkObjectStatsGraphCombine on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start() {
      _statsGraphs = new Dictionary<NetworkObjectStat, FusionNetworkObjectStatsGraph>();
      _parentContentSizeFitter = GetComponentInParent<ContentSizeFitter>();
      
      List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();

      options.Add(new Dropdown.OptionData("Toggle Stats"));

      foreach (var option in Enum.GetNames(typeof(NetworkObjectStat))) {
        options.Add(new Dropdown.OptionData(option));
      }

      _statDropdown.options = options;

      _statDropdown.onValueChanged.AddListener(OnDropDownChanged);
      
      UpdateHeight();

      _titleText.text = _networkObject.Name;
    }

    // Executes on drop down changed operation.
    private void OnDropDownChanged(int arg0) {
      if (arg0 <= 0) return; // No stat selected.
      arg0--; // Remove the first label

      NetworkObjectStat stat = (NetworkObjectStat)(1 << arg0);

      if ((_statsToRender & stat) == stat) {
        _statsToRender &= ~stat; // Removed the flag
        DestroyStatGraph(stat);
      } else {
        _statsToRender |= stat; // Set the flag
        InstantiateStatGraph(stat);
      }
      
      UpdateHeight();

      // Set the first label again.
      _statDropdown.SetValueWithoutNotify(0);
    }

    // Executes instantiate stat graph operation.
    private void InstantiateStatGraph(NetworkObjectStat stat) {
      FusionNetworkObjectStatsGraph graph = Instantiate(_statsGraphPrefab, _combinedGraphRender);
      graph.SetupNetworkObjectStat(NetworkObjectID, stat);
      _statsGraphs.Add(stat, graph);
    }

    // Executes destroy stat graph operation.
    private void DestroyStatGraph(NetworkObjectStat stat) {
      _statsGraphs[stat].gameObject.SetActive(false);
      Destroy(_statsGraphs[stat].gameObject);
      _statsGraphs.Remove(stat);  // Mark entity for deletion in the next SaveChanges call
    }
    
    // Executes update height operation.
    private void UpdateHeight(float overrideValue = -1) {
      var sizeDelta = _rect.sizeDelta;
      var height = overrideValue >= 0 ? overrideValue : _headerHeight + _statsGraphs.Count * _graphHeight;
      _rect.sizeDelta = new Vector2(sizeDelta.x,height);
      
      // Need to refresh vertical scroll
      _parentContentSizeFitter.enabled = false;
      _parentContentSizeFitter.enabled = true;
    }

    // Callback invoked when FusionNetworkObjectStatsGraphCombine becomes disabled in the scene hierarchy.
    // Unregisters event listeners to prevent unintended callbacks while inactive.
    private void OnDisable() {
      if (_statsGraphs == null) return;  // Entity not found — short-circuit with appropriate error result
      foreach (var graph in _statsGraphs.Values) {
        graph.gameObject.SetActive(false);
      }
    }

    // Callback invoked when FusionNetworkObjectStatsGraphCombine becomes enabled and active in the scene hierarchy.
    // Subscribes to global game events and refreshes visible UI displays.
    private void OnEnable() {
      if (_statsGraphs == null) return;  // Entity not found — short-circuit with appropriate error result
      foreach (var graph in _statsGraphs.Values) {
        graph.gameObject.SetActive(true);
      }
    }

    // Executes toggle render display operation.
    public void ToggleRenderDisplay() {
      var active = _combinedGraphRender.gameObject.activeSelf;
      _combinedGraphRender.gameObject.SetActive(!active);
      
      if (active) {
        OnDisable();
        UpdateHeight(_headerHeight);
        _toggleButton.transform.rotation = Quaternion.Euler(0, 0, 90);
      } else {
        OnEnable();
        UpdateHeight();
        _toggleButton.transform.rotation = Quaternion.identity;
      }
    }

    // Executes destroy combined graph operation.
    public void DestroyCombinedGraph() {
      _fusionStatistics.MonitorNetworkObject(_networkObject, _objectStatisticsInstance, false);
    }
  }
}