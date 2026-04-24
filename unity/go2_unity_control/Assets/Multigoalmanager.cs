using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Multi-goal manager:
///   - Click on floor to queue waypoints (shown as numbered spheres)
///   - Press Walk to send them one by one to ROS
///   - Press Clear to reset
///   - Listens to /goal_reached (UDP port 10004) to advance to next goal
/// </summary>
public class MultiGoalManager : MonoBehaviour
{
    [Header("Scene")]
    public Camera        sceneCamera;
    public Transform     goalMarker;        // existing GoalMarker
    public GameObject    waypointPrefab;    // small sphere prefab
    public Transform     waypointParent;    // empty parent for waypoint dots

    [Header("UI")]
    public Button        walkButton;
    public Button        clearButton;
    public TextMeshProUGUI statusText;

    [Header("TCP Bridge (send goals)")]
    public string serverIP   = "127.0.0.1";
    public int    serverPort = 10000;

    [Header("UDP (receive goal_reached)")]
    public int    reachedPort = 10004;

    [Header("Waypoint visuals")]
    public Color pendingColor  = new Color(1f, 0.8f, 0f);   // yellow
    public Color activeColor   = new Color(0f, 1f, 0.4f);   // green
    public Color doneColor     = new Color(0.4f, 0.4f, 0.4f); // gray

    // ── Internal ───────────────────────────────────────────────────
    private List<Vector3>     _rosGoals    = new List<Vector3>();   // ROS frame
    private List<Vector3>     _unityPos    = new List<Vector3>();   // Unity frame
    private List<GameObject>  _markers     = new List<GameObject>();

    private int  _currentIndex = -1;
    private bool _isWalking    = false;

    // TCP
    private TcpClient     _tcp;
    private NetworkStream _stream;
    private Thread        _sendThread;
    private bool          _tcpRunning;
    private Queue<byte[]> _sendQueue = new Queue<byte[]>();
    private readonly object _qLock   = new object();

    // UDP goal_reached receiver
    private System.Net.Sockets.UdpClient _udp;
    private Thread                        _udpThread;
    private bool                          _udpRunning;
    private bool                          _goalReached;
    private readonly object               _grLock = new object();

    // ── Lifecycle ──────────────────────────────────────────────────

    void Start()
    {
        if (sceneCamera == null) sceneCamera = Camera.main;

        walkButton.onClick.AddListener(OnWalkPressed);
        clearButton.onClick.AddListener(OnClearPressed);

        walkButton.interactable = false;
        SetStatus("Click on floor to add waypoints");

        // TCP send thread
        _tcpRunning = true;
        _sendThread = new Thread(TcpSendLoop) { IsBackground = true };
        _sendThread.Start();

        // UDP goal_reached listener
        _udpRunning = true;
        _udp        = new System.Net.Sockets.UdpClient(reachedPort);
        _udpThread  = new Thread(UdpReceiveLoop) { IsBackground = true };
        _udpThread.Start();

        Debug.Log("[MultiGoalManager] Ready");
    }

    void Update()
    {
        // Add waypoint on click (only when not walking)
        if (!_isWalking && Input.GetMouseButtonDown(0))
            TryAddWaypoint();

        // Check if current goal was reached
        bool reached = false;
        lock (_grLock) { reached = _goalReached; _goalReached = false; }
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

    // ── Waypoint collection ────────────────────────────────────────

    void TryAddWaypoint()
    {
        if (sceneCamera == null) return;
        Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        Vector3 unityPos = hit.point;

        // ROS coordinate mapping: Unity Z → ROS X, -Unity X → ROS Y
        float rosX = unityPos.z;
        float rosY = -unityPos.x;

        _rosGoals.Add(new Vector3(rosX, rosY, 0));
        _unityPos.Add(unityPos);

        // Spawn numbered marker
        GameObject m = waypointParent != null
            ? Instantiate(waypointPrefab, waypointParent)
            : Instantiate(waypointPrefab);

        m.transform.position = new Vector3(unityPos.x, 0.15f, unityPos.z);
        SetMarkerColor(m, pendingColor);

        // Add number label
        var canvas = new GameObject("Label");
        canvas.transform.SetParent(m.transform);
        canvas.transform.localPosition = Vector3.up * 0.3f;
        var tmp = canvas.AddComponent<TextMeshPro>();
        tmp.text      = _markers.Count.ToString();
        tmp.fontSize  = 3;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        _markers.Add(m);

        walkButton.interactable = true;
        SetStatus(_markers.Count + " waypoint(s) queued. Press Walk!");

        Debug.Log("[MultiGoalManager] Waypoint " + _markers.Count + 
                  " added: ROS(" + rosX.ToString("F2") + ", " + rosY.ToString("F2") + ")");
    }

    // ── Walk button ────────────────────────────────────────────────

    void OnWalkPressed()
    {
        if (_rosGoals.Count == 0) return;

        _isWalking    = true;
        _currentIndex = -1;

        walkButton.interactable  = false;
        clearButton.interactable = false;

        Debug.Log("[MultiGoalManager] Starting walk — " + _rosGoals.Count + " waypoints");
        AdvanceToNextGoal();
    }

    void AdvanceToNextGoal()
    {
        // Mark previous as done
        if (_currentIndex >= 0 && _currentIndex < _markers.Count)
            SetMarkerColor(_markers[_currentIndex], doneColor);

        _currentIndex++;

        if (_currentIndex >= _rosGoals.Count)
        {
            // All done
            _isWalking = false;
            clearButton.interactable = true;
            SetStatus("All waypoints reached!");
            Debug.Log("[MultiGoalManager] All goals completed");
            return;
        }

        // Highlight active marker
        SetMarkerColor(_markers[_currentIndex], activeColor);

        // Move GoalMarker in Unity
        if (goalMarker != null)
            goalMarker.position = new Vector3(
                _unityPos[_currentIndex].x,
                goalMarker.position.y,
                _unityPos[_currentIndex].z
            );

        // Send goal to ROS
        Vector3 g   = _rosGoals[_currentIndex];
        string  json = "{\"x\":" + g.x.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) +
                       ",\"y\":" + g.y.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) +
                       ",\"z\":0}";

        byte[] payload = Encoding.UTF8.GetBytes(json);
        byte[] prefix  = BitConverter.GetBytes(payload.Length);  // little-endian
        byte[] packet  = new byte[4 + payload.Length];
        Buffer.BlockCopy(prefix,  0, packet, 0,              4);
        Buffer.BlockCopy(payload, 0, packet, 4, payload.Length);

        lock (_qLock) { _sendQueue.Enqueue(packet); }

        SetStatus("Going to waypoint " + (_currentIndex + 1) + " / " + _rosGoals.Count);
        Debug.Log("[MultiGoalManager] Sending goal " + (_currentIndex + 1) + 
                  ": " + json);
    }

    // ── Clear button ───────────────────────────────────────────────

    void OnClearPressed()
    {
        _isWalking    = false;
        _currentIndex = -1;
        _rosGoals.Clear();
        _unityPos.Clear();

        foreach (var m in _markers) Destroy(m);
        _markers.Clear();

        walkButton.interactable  = false;
        clearButton.interactable = true;
        SetStatus("Cleared. Click to add waypoints.");
    }

    // ── TCP send loop ──────────────────────────────────────────────

    void TcpSendLoop()
    {
        while (_tcpRunning)
        {
            // Ensure connection
            if (_tcp == null || !_tcp.Connected)
            {
                try
                {
                    _tcp    = new TcpClient(serverIP, serverPort);
                    _stream = _tcp.GetStream();
                    Debug.Log("[MultiGoalManager] TCP connected");
                }
                catch
                {
                    Thread.Sleep(2000);
                    continue;
                }
            }

            // Send queued packets
            byte[] packet = null;
            lock (_qLock)
            {
                if (_sendQueue.Count > 0) packet = _sendQueue.Dequeue();
            }

            if (packet != null)
            {
                try { _stream.Write(packet, 0, packet.Length); }
                catch
                {
                    Debug.LogWarning("[MultiGoalManager] Send failed, reconnecting");
                    _tcp?.Close(); _tcp = null;
                }
            }

            Thread.Sleep(10);
        }
    }

    // ── UDP goal_reached receiver ──────────────────────────────────

    void UdpReceiveLoop()
    {
        var ep = new System.Net.IPEndPoint(System.Net.IPAddress.Any, reachedPort);
        while (_udpRunning)
        {
            try
            {
                byte[] data = _udp.Receive(ref ep);
                string msg  = Encoding.UTF8.GetString(data).Trim();
                if (msg.Contains("true") || msg.Contains("1"))
                    lock (_grLock) { _goalReached = true; }
            }
            catch (Exception e)
            {
                if (_udpRunning) Debug.LogWarning("[MultiGoalManager] UDP: " + e.Message);
            }
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
}