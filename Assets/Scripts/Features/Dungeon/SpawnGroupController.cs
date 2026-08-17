using System.Collections.Generic;
using UnityEngine;

// Executes mono behaviour operation.
public class SpawnGroupController : MonoBehaviour
{
    private readonly List<Transform> _allPoints = new();

    private readonly List<Transform> _freePoints = new();


    // Executes capacity operation.
    public int Capacity => _allPoints.Count;

    // Executes available operation.
    public int Available => _freePoints.Count;

    // Executes has available point operation.
    public bool HasAvailablePoint => _freePoints.Count > 0;


    // Initializes internal component caches and dependencies for SpawnGroupController upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        DiscoverSpawnPoints();
        ResetGroup();
    }


    // Executes claim random point operation.
    public Transform ClaimRandomPoint()
    {
        if (!HasAvailablePoint)
        {
            Debug.LogWarning($"[SpawnGroupController] '{name}' has no remaining SpawnPoints.");
            return null;
        }

        // Randomize the eligible candidates before selecting this gameplay result.
        int idx = Random.Range(0, _freePoints.Count);
        Transform point = _freePoints[idx];
        _freePoints.RemoveAt(idx);
        return point;
    }

    // Executes reset group operation.
    public void ResetGroup()
    {
        _freePoints.Clear();
        _freePoints.AddRange(_allPoints);
        ShuffleFreePoints();
    }


    // Executes discover spawn points operation.
    private void DiscoverSpawnPoints()
    {
        _allPoints.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
                _allPoints.Add(child);
        }

        if (_allPoints.Count == 0)
            Debug.LogWarning($"[SpawnGroupController] '{name}' has no child SpawnPoints. " +
                             "Add child Transforms as spawn locations.");
    }

    // Executes shuffle free points operation.
    private void ShuffleFreePoints()
    {
        for (int i = _freePoints.Count - 1; i > 0; i--)
        {
            // Randomize the eligible candidates before selecting this gameplay result.
            int j = Random.Range(0, i + 1);
            Transform temp = _freePoints[i];
            _freePoints[i] = _freePoints[j];
            _freePoints[j] = temp;
        }
    }
}
