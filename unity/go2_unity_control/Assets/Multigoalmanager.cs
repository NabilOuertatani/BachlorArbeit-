using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Multi-goal manager with correct scale (NavMesh_Ground scale 2,1,2)
/// 1 real meter = 2 Unity units
/// </summary>
public class MultiGoalManager : MonoBehaviour
{
    [Header("Scene")]
    public Camera        sceneCamera;
    public Transform     goalMarker;
    public GameObject    waypointPrefab;
    public Transform     waypointParent;

    [Header("UI")]
    public Button          walkButton;
    public Button          clearButton;
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
    private List<Vector3>    _rosGoals = new List<Vector3>();
    private List<Vector3>    _unityPos = new List<Vector3>();
    private List<GameObject> _markers  = new List<GameObject>();

    private int  _currentIndex = -1;
    private bool _isWalking    = false;

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
        // Disable single-goal script
        var singleGoal = FindObjectOfType<UnityClickToRosGoal>();
        if (singleGoal != null)
        {
            singleGoal.enabled = false;
            Debug.Log("[MultiGoalManager] Disabled UnityClickToRosGoal");
        }

        if (sceneCamera == null) sceneCamera = Camera.main;

        walkButton.onClick.AddListener(OnWalkPressed);
        clearButton.onClick.AddListener(OnClearPressed);
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
        if (!_isWalking && Input.GetMouseButtonDown(0))
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

        var singleGoal = FindObjectOfType<UnityClickToRosGoal>();
        if (singleGoal != null) singleGoal.enabled = true;
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

        _rosGoals.Add(new Vector3(rosX, rosY, 0));
        _unityPos.Add(unityPos);

        // Spawn marker
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

        _markers.Add(m);
        walkButton.interactable = true;
        SetStatus(_markers.Count + " waypoint(s) — press WALK to start");

        Debug.Log("[MultiGoalManager] Waypoint " + _markers.Count +
                  " Unity(" + unityPos.x.ToString("F2") + ", " + unityPos.z.ToString("F2") + ")" +
                  " → ROS(" + rosX.ToString("F2") + ", " + rosY.ToString("F2") + ")");
    }

    // ── Walk ───────────────────────────────────────────────────────

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
        if (_currentIndex >= 0 && _currentIndex < _markers.Count)
            SetMarkerColor(_markers[_currentIndex], doneColor);

        _currentIndex++;

        if (_currentIndex >= _rosGoals.Count)
        {
            _isWalking               = false;
            clearButton.interactable = true;
            SetStatus("All waypoints reached!");
            Debug.Log("[MultiGoalManager] All goals completed");
            return;
        }

        SetMarkerColor(_markers[_currentIndex], activeColor);

        if (goalMarker != null)
            goalMarker.position = new Vector3(
                _unityPos[_currentIndex].x,
                goalMarker.position.y,
                _unityPos[_currentIndex].z
            );

        // Build packet — big-endian length prefix
        Vector3 g    = _rosGoals[_currentIndex];
        string  json = "{\"x\":"  + g.x.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) +
                       ",\"y\":" + g.y.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) +
                       ",\"z\":0}";

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

        SetStatus("Going to waypoint " + (_currentIndex + 1) + " / " + _rosGoals.Count);
        Debug.Log("[MultiGoalManager] Sending goal " + (_currentIndex + 1) + ": " + json);
    }

    // ── Clear ──────────────────────────────────────────────────────

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
        SetStatus("Cleared — click floor to add waypoints");
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
}