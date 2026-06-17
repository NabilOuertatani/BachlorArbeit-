using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Manages waypoint collection and delivery to the Go2 via ROS.
///
/// Coordinate scale: 1 real metre = 2 Unity units (NavMesh_Ground scale 2,1,2).
///
/// Threads:
///   Main — raycasting, marker visuals, UI
///   TCP  — sends Point goals to /unity_clicked_point (port 10000)
///   UDP  — receives goal-reached signals from robot (port 10004)
///
/// Used by GestureSequenceUI for programmatic waypoint loading.
/// </summary>
public class MultiGoalManager : MonoBehaviour
{
    [Header("Scene")]
    public Camera        sceneCamera;
    public Transform     goalMarker;
    public GameObject    waypointPrefab;
    public Transform     waypointParent;
    public Canvas        mainUICanvas;
    public SpeedSelector speedSelector;  // NEW: UI for speed selection

    [Header("UI")]
    public Button          walkButton;
    public Button          clearButton;
    public Button          addWaypointsButton;
    public TextMeshProUGUI statusText;

    [Header("TCP Bridge")]
    public string serverIP   = "127.0.0.1";
    public int    serverPort = 10000;

    [Header("UDP (receive goal_reached)")]
    public int reachedPort = 10004;

    [Header("Scale (Unity units per real meter)")]
    public float scaleX = 2.0f;   // NavMesh_Ground scale X
    public float scaleZ = 2.0f;   // NavMesh_Ground scale Z

    [Header("Waypoint visuals")]
    public Color pendingColor = new Color(1f, 0.8f, 0f);
    public Color activeColor  = new Color(0f, 1f, 0.4f);
    public Color doneColor    = new Color(0.4f, 0.4f, 0.4f);

    // ── Internal ───────────────────────────────────────────────────
    private List<WaypointWithSpeed> _waypoints = new List<WaypointWithSpeed>();  // NEW: stores waypoints with speeds
    private List<GameObject> _markers  = new List<GameObject>();

    private int  _currentIndex = -1;
    private bool _isWalking    = false;
    private bool _inputEnabled = true;  // Disabled during watch-only mode

    // TCP
    private TcpClient     _tcp;
    private NetworkStream _stream;
    private Thread        _sendThread;
    private bool          _tcpRunning;
    private Queue<byte[]> _sendQueue = new Queue<byte[]>();
    private readonly object _qLock   = new object();

    // UDP
    private System.Net.Sockets.UdpClient _udp;
    private Thread                        _udpThread;
    private bool                          _udpRunning;
    private bool                          _goalReached;
    private readonly object               _grLock = new object();

    // ── Lifecycle ──────────────────────────────────────────────────

    void Start()
    {
        // Register with bridge so it can control navigation
        if (RobotBridge.Instance != null)
        {
            RobotBridge.Instance.RegisterMultiGoalManager(this);
        }
        if (sceneCamera == null) sceneCamera = Camera.main;

        // Register with the persistent RobotBridge for cross-scene communication
        if (RobotBridge.Instance != null)
            RobotBridge.Instance.RegisterMultiGoalManager(this);
        else
            Debug.LogWarning("[MultiGoalManager] RobotBridge not found — was it created in MainUI.unity?");

        // Auto-find SpeedSelector if not assigned
        if (speedSelector == null)
        {
            speedSelector = FindObjectOfType<SpeedSelector>();
            if (speedSelector != null)
                Debug.Log("[MultiGoalManager] Auto-found SpeedSelector");
            else
                Debug.LogWarning("[MultiGoalManager] SpeedSelector not found in scene! Assign it manually in Inspector.");
        }

        // Subscribe to speed confirmation event to auto-save to JSON
        if (speedSelector != null)
        {
            speedSelector.OnSpeedConfirmed += OnSpeedConfirmedByUser;
            Debug.Log("[MultiGoalManager] Subscribed to OnSpeedConfirmed event");
        }

        walkButton.onClick.AddListener(OnWalkPressed);
        clearButton.onClick.AddListener(OnClearPressed);
        
        // Add Waypoints button - save to GestureSequenceUI
        if (addWaypointsButton != null)
        {
            addWaypointsButton.onClick.AddListener(OnAddWaypointsPressed);
        }

        walkButton.interactable  = false;
        clearButton.interactable = true;
        SetStatus("Click on floor to add waypoints");

        _tcpRunning = true;
        _sendThread = new Thread(TcpSendLoop) { IsBackground = true };
        _sendThread.Start();

        _udpRunning = true;
        _udp        = new System.Net.Sockets.UdpClient(reachedPort);
        _udpThread  = new Thread(UdpReceiveLoop) { IsBackground = true };
        _udpThread.Start();

        Debug.Log("[MultiGoalManager] Ready | scale X=" + scaleX + " Z=" + scaleZ);
    }

    void Update()
    {
        // Only accept floor clicks if input is enabled AND not currently walking
        if (_inputEnabled && !_isWalking && Input.GetMouseButtonDown(0))
            TryAddWaypoint();

        bool reached = false;
        lock (_grLock) { reached = _goalReached; _goalReached = false; }
        
        if (reached)
            Debug.Log("[MultiGoalManager] Goal reached detected! _isWalking=" + _isWalking);
            
        if (reached && _isWalking)
            AdvanceToNextGoal();
    }

    void OnDestroy()
    {
        _tcpRunning = false;
        _udpRunning = false;
        _sendThread?.Join(300);
        _udpThread?.Join(300);
        _tcp?.Close();
        _udp?.Close();
    }

    // ── Input control (called by RobotBridge) ───────────────────

    /// <summary>
    /// Disables floor click input so waypoints can't be added during watch-only mode.
    /// Called by RobotBridge when entering simulation.
    /// </summary>
    public void DisableInput()
    {
        _inputEnabled = false;
        Debug.Log("[MultiGoalManager] Input disabled — watch-only mode active");
    }

    /// <summary>
    /// Re-enables floor click input after watch-only mode ends.
    /// Called by RobotBridge when exiting simulation.
    /// </summary>
    public void EnableInput()
    {
        _inputEnabled = true;
        Debug.Log("[MultiGoalManager] Input enabled — waypoint collection active");
    }

    // ── Waypoint collection ────────────────────────────────────────

    void TryAddWaypoint()
    {
        if (sceneCamera == null) return;

        // Ignore UI clicks
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        Vector3 unityPos = hit.point;

        // Unity → ROS: divide by scale to get real metres
        float rosX =  unityPos.z / scaleZ;
        float rosY = -unityPos.x / scaleX;

        // Create placeholder waypoint (speed will be set after user selection)
        WaypointWithSpeed wp = new WaypointWithSpeed(
            unityPos,
            new Vector3(rosX, rosY, 0),
            0.4f  // Default to normal speed
        );

        _waypoints.Add(wp);

        // Spawn marker with default color
        GameObject m = waypointParent != null
            ? Instantiate(waypointPrefab, waypointParent)
            : Instantiate(waypointPrefab);

        m.transform.position = new Vector3(unityPos.x, 0.15f, unityPos.z);
        SetMarkerColor(m, pendingColor);

        // Number label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(m.transform);
        labelObj.transform.localPosition = Vector3.up * 0.4f;
        var tmp       = labelObj.AddComponent<TextMeshPro>();
        tmp.text      = _markers.Count.ToString();
        tmp.fontSize  = 3;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        // Speed label (initially empty, will update when speed is selected)
        GameObject speedLabelObj = new GameObject("SpeedLabel");
        speedLabelObj.transform.SetParent(m.transform);
        speedLabelObj.transform.localPosition = Vector3.up * 0.6f;
        var speedTmp = speedLabelObj.AddComponent<TextMeshPro>();
        speedTmp.text = "0.4 m/s";
        speedTmp.fontSize = 2;
        speedTmp.alignment = TextAlignmentOptions.Center;
        speedTmp.color = Color.white;
        speedLabelObj.name = "SpeedLabel";

        _markers.Add(m);
        walkButton.interactable = true;
        SetStatus(_markers.Count + " waypoint(s) — select speed and press ADD POINTS");

        Debug.Log("[MultiGoalManager] Waypoint " + _markers.Count +
                  " placed at Unity(" + unityPos.x.ToString("F2") + ", " + unityPos.z.ToString("F2") + ")" +
                  " → ROS(" + rosX.ToString("F2") + ", " + rosY.ToString("F2") + ")");

        // Show speed selector for this waypoint
        if (speedSelector != null)
        {
            speedSelector.Show(_waypoints.Count - 1);
        }
        else
        {
            Debug.LogWarning("[MultiGoalManager] SpeedSelector not assigned!");
        }
    }

    // ── Walk ───────────────────────────────────────────────────────

    void OnWalkPressed()
    {
        if (_waypoints.Count == 0) return;
        _isWalking    = true;
        _currentIndex = -1;
        walkButton.interactable  = false;
        clearButton.interactable = false;
        Debug.Log("[MultiGoalManager] Starting walk — " + _waypoints.Count + " waypoints");
        AdvanceToNextGoal();
    }

    void AdvanceToNextGoal()
    {
        if (_currentIndex >= 0 && _currentIndex < _markers.Count)
            SetMarkerColor(_markers[_currentIndex], doneColor);

        _currentIndex++;

        if (_currentIndex >= _waypoints.Count)
        {
            _isWalking               = false;
            clearButton.interactable = true;
            SetStatus("All waypoints reached!");
            Debug.Log("[MultiGoalManager] All goals completed");
            return;
        }

        SetMarkerColor(_markers[_currentIndex], activeColor);

        // Update goal marker position
        WaypointWithSpeed wp = _waypoints[_currentIndex];
        if (goalMarker != null)
            goalMarker.position = new Vector3(
                wp.unityPosition.x,
                goalMarker.position.y,
                wp.unityPosition.z
            );

        // Build packet with speed — big-endian length prefix
        string json = "{\"x\":"  + wp.rosGoal.x.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) +
                      ",\"y\":" + wp.rosGoal.y.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) +
                      ",\"z\":0" +
                      ",\"speed\":" + wp.speed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                      "}";

        byte[] payload = Encoding.UTF8.GetBytes(json);
        int    len     = payload.Length;
        byte[] prefix  = new byte[4]
        {
            (byte)(len >> 24), (byte)(len >> 16),
            (byte)(len >>  8), (byte)(len)
        };
        byte[] packet = new byte[4 + payload.Length];
        Buffer.BlockCopy(prefix,  0, packet, 0,              4);
        Buffer.BlockCopy(payload, 0, packet, 4, payload.Length);

        lock (_qLock) { _sendQueue.Enqueue(packet); }

        Debug.Log($"[MultiGoalManager] Sent waypoint {_currentIndex + 1}: " + json);
    }

    // ── Clear ──────────────────────────────────────────────────────

    void OnClearPressed()
    {
        _isWalking    = false;
        _currentIndex = -1;
        _waypoints.Clear();
        foreach (var m in _markers) Destroy(m);
        _markers.Clear();
        walkButton.interactable  = false;
        clearButton.interactable = true;
        SetStatus("Cleared — click floor to add waypoints");
    }

    /// <summary>Called when user confirms speed selection (button click or Enter)</summary>
    void OnSpeedConfirmedByUser(int waypointIndex, float speed)
    {
        if (waypointIndex < 0 || waypointIndex >= _waypoints.Count)
        {
            Debug.LogWarning($"[MultiGoalManager] Invalid waypoint index {waypointIndex}");
            return;
        }

        // Update waypoint speed
        _waypoints[waypointIndex].speed = Mathf.Clamp(speed, 0.1f, 1.0f);
        Debug.Log($"[MultiGoalManager] Speed confirmed for waypoint {waypointIndex + 1}: {speed:F2} m/s");

        // Update marker speed label and color
        if (waypointIndex < _markers.Count)
        {
            GameObject marker = _markers[waypointIndex];
            Transform speedLabelTransform = marker.transform.Find("SpeedLabel");
            if (speedLabelTransform != null)
            {
                TextMeshPro speedText = speedLabelTransform.GetComponent<TextMeshPro>();
                if (speedText != null)
                {
                    speedText.text = _waypoints[waypointIndex].speed.ToString("F2") + " m/s";
                }
            }

            // Update marker color based on new speed
            SetMarkerColor(marker, SpeedSelector.GetSpeedColor(_waypoints[waypointIndex].speed));
        }

        // Note: Actual save to JSON happens when ADD WAYPOINTS or WALK button is clicked
        // via the existing OnAddWaypointsPressed() flow, not here during speed selection
    }

    void OnAddWaypointsPressed()
    {
        // Apply speed selection to the last waypoint
        if (_waypoints.Count > 0 && speedSelector != null)
        {
            float selectedSpeed = speedSelector.GetSelectedSpeed();
            _waypoints[_waypoints.Count - 1].speed = selectedSpeed;

            // Update the marker's speed label and color
            GameObject lastMarker = _markers[_markers.Count - 1];
            Transform speedLabelTransform = lastMarker.transform.Find("SpeedLabel");
            if (speedLabelTransform != null)
            {
                TextMeshPro speedText = speedLabelTransform.GetComponent<TextMeshPro>();
                if (speedText != null)
                {
                    speedText.text = selectedSpeed.ToString("F2") + " m/s";
                }
            }

            // Update marker color based on speed
            SetMarkerColor(lastMarker, SpeedSelector.GetSpeedColor(selectedSpeed));

            Debug.Log($"[MultiGoalManager] Waypoint {_waypoints.Count} speed set to {selectedSpeed:F2} m/s");
        }

        // Hide speed panel
        if (speedSelector != null)
            speedSelector.Hide();

        Debug.Log("[MultiGoalManager] Add Waypoints button pressed!");
        
        GestureSequenceUI gestureUI = FindObjectOfType<GestureSequenceUI>();
        if (gestureUI == null)
        {
            Debug.LogError("[MultiGoalManager] GestureSequenceUI not found!");
            return;
        }

        gestureUI.AddWaypoints();
        Debug.Log("[MultiGoalManager] Called GestureSequenceUI.AddWaypoints()");
    }

    // ── TCP send loop ──────────────────────────────────────────────

    void TcpSendLoop()
    {
        while (_tcpRunning)
        {
            if (_tcp == null || !_tcp.Connected)
            {
                try
                {
                    _tcp    = new TcpClient(serverIP, serverPort);
                    _stream = _tcp.GetStream();
                    Debug.Log("[MultiGoalManager] TCP connected");
                }
                catch { Thread.Sleep(2000); continue; }
            }

            byte[] packet = null;
            lock (_qLock) { if (_sendQueue.Count > 0) packet = _sendQueue.Dequeue(); }

            if (packet != null)
            {
                try { _stream.Write(packet, 0, packet.Length); }
                catch
                {
                    Debug.LogWarning("[MultiGoalManager] Send failed — reconnecting");
                    _tcp?.Close(); _tcp = null;
                }
            }
            Thread.Sleep(10);
        }
    }

    // ── UDP receive ────────────────────────────────────────────────

    void UdpReceiveLoop()
    {
        try
        {
            var ep = new System.Net.IPEndPoint(System.Net.IPAddress.Any, reachedPort);
            Debug.Log("[MultiGoalManager] UDP listening on port " + reachedPort);
            
            while (_udpRunning)
            {
                try
                {
                    byte[] data = _udp.Receive(ref ep);
                    string msg  = Encoding.UTF8.GetString(data).Trim();
                    Debug.Log("[MultiGoalManager] UDP received from " + ep + ": " + msg);
                    
                    if (msg.Contains("true") || msg.Contains("1"))
                    {
                        Debug.Log("[MultiGoalManager] Goal reached signal received!");
                        lock (_grLock) { _goalReached = true; }
                    }
                }
                catch (System.Net.Sockets.SocketException e)
                {
                    if (_udpRunning) Debug.LogWarning("[MultiGoalManager] UDP socket error: " + e.Message);
                }
                catch (Exception e)
                {
                    if (_udpRunning) Debug.LogWarning("[MultiGoalManager] UDP error: " + e.Message);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[MultiGoalManager] Failed to create UDP socket on port " + reachedPort + ": " + e.Message);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────

    void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    static void SetMarkerColor(GameObject m, Color c)
    {
        var r = m.GetComponent<Renderer>();
        if (r != null) r.material.color = c;
    }

    // ── Public API for GestureSequenceUI ───────────────────────────

    /// <summary>Get list of waypoints added to current gesture step</summary>
    public List<Vector3> GetCurrentWaypoints()
    {
        List<Vector3> positions = new List<Vector3>();
        foreach (WaypointWithSpeed wp in _waypoints)
            positions.Add(wp.unityPosition);
        return positions;
    }

    /// <summary>Get current waypoints WITH speeds (for saving sequences)</summary>
    public List<WaypointWithSpeed> GetCurrentWaypointsWithSpeed()
    {
        return new List<WaypointWithSpeed>(_waypoints);
    }

    /// <summary>Clear waypoints after saving to gesture</summary>
    public void ClearWaypoints()
    {
        _waypoints.Clear();
        
        foreach (GameObject m in _markers)
            Destroy(m);
        _markers.Clear();

        walkButton.interactable = false;
        SetStatus("Waypoints cleared. Click on floor to add new ones.");
        Debug.Log("[MultiGoalManager] Cleared all waypoints");
    }

    /// <summary>Load waypoints from gesture step (Unity coordinates, with default speed)</summary>
    public void LoadWaypoints(List<Vector3> unityWaypoints)
    {
        ClearWaypoints();
        
        foreach (Vector3 unityPos in unityWaypoints)
        {
            // Unity → ROS: divide by scale
            float rosX = unityPos.z / scaleZ;
            float rosY = -unityPos.x / scaleX;
            
            // Create waypoint with default speed
            WaypointWithSpeed wp = new WaypointWithSpeed(
                unityPos,
                new Vector3(rosX, rosY, 0),
                0.4f  // Default to normal speed
            );
            
            _waypoints.Add(wp);
            
            // Spawn marker
            GameObject m = waypointParent != null
                ? Instantiate(waypointPrefab, waypointParent)
                : Instantiate(waypointPrefab);
            
            m.transform.position = new Vector3(unityPos.x, 0.15f, unityPos.z);
            SetMarkerColor(m, pendingColor);
            
            // Add speed label
            GameObject speedLabelObj = new GameObject("SpeedLabel");
            speedLabelObj.transform.SetParent(m.transform);
            speedLabelObj.transform.localPosition = Vector3.up * 0.6f;
            var speedTmp = speedLabelObj.AddComponent<TextMeshPro>();
            speedTmp.text = "0.4 m/s";
            speedTmp.fontSize = 2;
            speedTmp.alignment = TextAlignmentOptions.Center;
            speedTmp.color = Color.white;
            
            _markers.Add(m);
        }
        
        walkButton.interactable = _waypoints.Count > 0;
        SetStatus(_markers.Count + " waypoint(s) loaded");
        Debug.Log("[MultiGoalManager] Loaded " + _waypoints.Count + " waypoints with default speed 0.4 m/s");
    }

    /// <summary>Load waypoints with individual speeds (preserves speeds from loaded sequences)</summary>
    public void LoadWaypoints(List<WaypointWithSpeed> waypointsWithSpeeds)
    {
        ClearWaypoints();
        
        foreach (WaypointWithSpeed wp in waypointsWithSpeeds)
        {
            // Use the waypoint as-is (already has both positions and speed)
            _waypoints.Add(wp);
            
            // Spawn marker
            GameObject m = waypointParent != null
                ? Instantiate(waypointPrefab, waypointParent)
                : Instantiate(waypointPrefab);
            
            Vector3 unityPos = wp.unityPosition;
            m.transform.position = new Vector3(unityPos.x, 0.15f, unityPos.z);
            SetMarkerColor(m, SpeedSelector.GetSpeedColor(wp.speed));
            
            // Add speed label with actual speed
            GameObject speedLabelObj = new GameObject("SpeedLabel");
            speedLabelObj.transform.SetParent(m.transform);
            speedLabelObj.transform.localPosition = Vector3.up * 0.6f;
            var speedTmp = speedLabelObj.AddComponent<TextMeshPro>();
            speedTmp.text = wp.speed.ToString("F2") + " m/s";
            speedTmp.fontSize = 2;
            speedTmp.alignment = TextAlignmentOptions.Center;
            speedTmp.color = Color.white;
            
            _markers.Add(m);
        }
        
        walkButton.interactable = _waypoints.Count > 0;
        SetStatus(_markers.Count + " waypoint(s) loaded with per-waypoint speeds");
        Debug.Log("[MultiGoalManager] Loaded " + _waypoints.Count + " waypoints with individual speeds");
    }

    /// <summary>Start navigation through loaded waypoints</summary>
    public void StartNavigation()
    {
        if (_waypoints.Count == 0)
        {
            Debug.LogWarning("[MultiGoalManager] No waypoints loaded!");
            return;
        }
        
        _isWalking = true;
        _currentIndex = -1;
        walkButton.interactable = false;
        clearButton.interactable = false;
        Debug.Log("[MultiGoalManager] Starting navigation — " + _waypoints.Count + " waypoints");
        AdvanceToNextGoal();
    }

    /// <summary>Check if navigation is complete</summary>
    public bool IsNavigationComplete()
    {
        return !_isWalking;
    }

    /// <summary>Add a single waypoint (Unity coordinates)</summary>
    public void AddWaypoint(Vector3 unityPos)
    {
        // Unity → ROS: divide by scale
        float rosX = unityPos.z / scaleZ;
        float rosY = -unityPos.x / scaleX;
        
        // Create waypoint with default speed
        WaypointWithSpeed wp = new WaypointWithSpeed(
            unityPos,
            new Vector3(rosX, rosY, 0),
            0.4f  // Default to normal speed
        );
        
        _waypoints.Add(wp);
        
        // Spawn marker
        GameObject m = waypointParent != null
            ? Instantiate(waypointPrefab, waypointParent)
            : Instantiate(waypointPrefab);
        
        m.transform.position = new Vector3(unityPos.x, 0.15f, unityPos.z);
        SetMarkerColor(m, pendingColor);
        
        _markers.Add(m);
        walkButton.interactable = true;
        SetStatus(_markers.Count + " waypoint(s)");
        Debug.Log("[MultiGoalManager] Added waypoint " + _markers.Count);
    }
}