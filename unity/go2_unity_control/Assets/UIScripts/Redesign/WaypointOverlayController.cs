using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Draws the Configure-screen viewport overlays on top of the live 3D scene
/// (never replacing it): numbered billboard markers at each waypoint, a dashed
/// LineRenderer path at floor level, and the waypoint list in the side panel.
///
/// The waypoints themselves stay owned by MultiGoalManager — this component
/// only observes them (via RobotBridge) and mirrors them visually. While no
/// live waypoints exist it falls back to the waypoints of the sequence being
/// edited (so opening a saved gesture shows its path immediately).
/// </summary>
public class WaypointOverlayController : MonoBehaviour
{
    [Header("Wired by RedesignBuilder")]
    public WaypointMarker markerPrefab;
    public Material dashedLineMaterial;
    public Transform listParent;              // Content of the waypoint ScrollRect
    public WaypointListItem listItemPrefab;
    public TMP_Text waypointCountText;        // "WAYPOINTS · 3"
    public GestureSequenceUI gestureUI;       // fallback source while editing

    [Header("Path style")]
    public float lineWidth = 0.08f;
    public float floorY = 0.02f;              // slightly raised to avoid z-fighting
    public float markerY = 0.35f;

    private readonly List<WaypointMarker> _markers = new List<WaypointMarker>();
    private readonly List<WaypointListItem> _listItems = new List<WaypointListItem>();
    private GameObject _overlayRoot;
    private LineRenderer _line;
    private int _lastHash = -1;

    void OnEnable()
    {
        _lastHash = -1; // force refresh when the config screen comes back
        if (_overlayRoot != null) _overlayRoot.SetActive(true);
    }

    void OnDisable()
    {
        if (_overlayRoot != null) _overlayRoot.SetActive(false);
    }

    void Update()
    {
        List<Vector3> waypoints = CurrentWaypoints();

        int hash = 17;
        foreach (Vector3 p in waypoints)
            hash = hash * 31 + p.GetHashCode();
        hash = hash * 31 + waypoints.Count;

        if (hash == _lastHash) return;
        _lastHash = hash;

        Rebuild(waypoints);
    }

    private List<Vector3> CurrentWaypoints()
    {
        // Live waypoints being placed right now take priority
        if (RobotBridge.Instance != null)
        {
            List<Vector3> live = RobotBridge.Instance.GetCurrentWaypoints();
            if (live != null && live.Count > 0) return live;
        }

        // Otherwise show the waypoints of the sequence being edited
        if (gestureUI != null)
        {
            List<Vector3> editing = gestureUI.GetAllWaypointPositions();
            if (editing != null) return editing;
        }

        return new List<Vector3>();
    }

    private void Rebuild(List<Vector3> waypoints)
    {
        EnsureOverlayRoot();

        // ── Markers ────────────────────────────────────────────────
        while (_markers.Count > waypoints.Count)
        {
            Destroy(_markers[_markers.Count - 1].gameObject);
            _markers.RemoveAt(_markers.Count - 1);
        }
        while (_markers.Count < waypoints.Count && markerPrefab != null)
        {
            WaypointMarker m = Instantiate(markerPrefab, _overlayRoot.transform);
            _markers.Add(m);
        }
        for (int i = 0; i < _markers.Count; i++)
        {
            _markers[i].transform.position =
                new Vector3(waypoints[i].x, markerY, waypoints[i].z);
            _markers[i].SetNumber(i + 1);
        }

        // ── Dashed path ────────────────────────────────────────────
        if (_line != null)
        {
            if (waypoints.Count >= 2)
            {
                _line.positionCount = waypoints.Count;
                for (int i = 0; i < waypoints.Count; i++)
                    _line.SetPosition(i, new Vector3(waypoints[i].x, floorY, waypoints[i].z));
            }
            else
            {
                _line.positionCount = 0;
            }
        }

        // ── Side-panel list ────────────────────────────────────────
        float scaleX = 2f, scaleZ = 2f;
        MultiGoalManager mgm = FindFirstObjectByType<MultiGoalManager>();
        if (mgm != null) { scaleX = mgm.scaleX; scaleZ = mgm.scaleZ; }

        while (_listItems.Count > waypoints.Count)
        {
            Destroy(_listItems[_listItems.Count - 1].gameObject);
            _listItems.RemoveAt(_listItems.Count - 1);
        }
        while (_listItems.Count < waypoints.Count && listItemPrefab != null && listParent != null)
        {
            WaypointListItem item = Instantiate(listItemPrefab, listParent);
            item.gameObject.SetActive(true);
            _listItems.Add(item);
        }
        for (int i = 0; i < _listItems.Count; i++)
            _listItems[i].Bind(i + 1, waypoints[i], scaleX, scaleZ);

        if (waypointCountText != null)
            waypointCountText.text = "WAYPOINTS · " + waypoints.Count;
    }

    private void EnsureOverlayRoot()
    {
        if (_overlayRoot != null) return;

        _overlayRoot = new GameObject("WaypointOverlayRoot");

        var lineObj = new GameObject("DashedPath");
        lineObj.transform.SetParent(_overlayRoot.transform, false);
        _line = lineObj.AddComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _line.startWidth = lineWidth;
        _line.endWidth = lineWidth;
        _line.textureMode = LineTextureMode.Tile;
        _line.alignment = LineAlignment.TransformZ;          // flat on the floor
        lineObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        _line.numCornerVertices = 2;
        _line.positionCount = 0;
        if (dashedLineMaterial != null)
            _line.material = dashedLineMaterial;
        _line.startColor = UITheme.Accent;
        _line.endColor = UITheme.Accent;
    }

    void OnDestroy()
    {
        if (_overlayRoot != null) Destroy(_overlayRoot);
    }
}
