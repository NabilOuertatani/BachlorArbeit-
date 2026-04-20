using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Receives robot estimated pose (UDP port 10001) and LiDAR obstacle
/// points (UDP port 10002) from ROS2 and visualises them in Unity.
///
/// Attach to a GameObject in your scene.
/// Assign:
///   - robotMarker:   the GameObject that represents the real dog in Unity
///   - obstacleParent: empty GameObject to hold obstacle dots
///   - obstaclePrefab: a small sphere/cube prefab for each obstacle point
/// </summary>
public class RobotSync : MonoBehaviour
{
    [Header("Network")]
    public int posePort     = 10001;
    public int cloudPort    = 10002;

    [Header("Scene references")]
    public Transform robotMarker;     // The Unity dog GameObject
    public Transform obstacleParent;  // Empty parent for obstacle dots
    public GameObject obstaclePrefab; // Small sphere prefab

    [Header("Obstacle visualisation")]
    public int   maxObstacles   = 300;
    public float obstacleHeight = 0.05f;  // Y position of dots in Unity

    // ── Internal ───────────────────────────────────────────────────
    private UdpClient _poseUdp;
    private UdpClient _cloudUdp;
    private Thread    _poseThread;
    private Thread    _cloudThread;
    private bool      _running;

    // Thread-safe buffers
    private readonly object _poseLock  = new object();
    private readonly object _cloudLock = new object();

    private float   _pendingX, _pendingY, _pendingYaw;
    private bool    _hasPose;

    private List<Vector2> _pendingCloud = new List<Vector2>();
    private bool          _hasCloud;

    // Pooled obstacle dots
    private List<GameObject> _obstacleDots = new List<GameObject>();

    // ── Lifecycle ──────────────────────────────────────────────────

    void Start()
    {
        _running = true;

        _poseUdp  = new UdpClient(posePort);
        _cloudUdp = new UdpClient(cloudPort);

        _poseThread  = new Thread(PoseReceiveLoop)  { IsBackground = true };
        _cloudThread = new Thread(CloudReceiveLoop) { IsBackground = true };

        _poseThread.Start();
        _cloudThread.Start();

        Debug.Log($"[RobotSync] Listening — pose:{posePort} cloud:{cloudPort}");
    }

    void Update()
    {
        ApplyPose();
        ApplyCloud();
    }

    void OnDestroy()
    {
        _running = false;
        _poseUdp?.Close();
        _cloudUdp?.Close();
        _poseThread?.Join(300);
        _cloudThread?.Join(300);
    }

    // ── Apply pose on main thread ──────────────────────────────────

    void ApplyPose()
    {
        if (robotMarker == null) return;
        lock (_poseLock)
        {
            if (!_hasPose) return;
            // ROS2 frame (x forward, y left) → Unity frame (x right, z forward)
            robotMarker.position = new Vector3(
                -_pendingY,
                robotMarker.position.y,
                 _pendingX
            );
            // ROS yaw (CCW from x-axis) → Unity Y rotation (CW from z-axis)
            robotMarker.rotation = Quaternion.Euler(0f, -_pendingYaw * Mathf.Rad2Deg, 0f);
            _hasPose = false;
        }
    }

    void ApplyCloud()
    {
        lock (_cloudLock)
        {
            if (!_hasCloud) return;

            List<Vector2> pts = new List<Vector2>(_pendingCloud);
            _hasCloud = false;

            // Grow pool if needed
            while (_obstacleDots.Count < pts.Count && _obstacleDots.Count < maxObstacles)
            {
                var dot = obstacleParent != null
                    ? Instantiate(obstaclePrefab, obstacleParent)
                    : Instantiate(obstaclePrefab);
                _obstacleDots.Add(dot);
            }

            // Position visible dots
            int visible = Mathf.Min(pts.Count, maxObstacles);
            for (int i = 0; i < _obstacleDots.Count; i++)
            {
                if (i < visible)
                {
                    // ROS (x, y) → Unity (x=-y, z=x)
                    _obstacleDots[i].transform.position = new Vector3(
                        -pts[i].y,
                        obstacleHeight,
                         pts[i].x
                    );
                    _obstacleDots[i].SetActive(true);
                }
                else
                {
                    _obstacleDots[i].SetActive(false);
                }
            }
        }
    }

    // ── Pose receive thread ────────────────────────────────────────

    void PoseReceiveLoop()
    {
        var ep = new IPEndPoint(IPAddress.Any, posePort);
        while (_running)
        {
            try
            {
                byte[]  data = _poseUdp.Receive(ref ep);
                string  json = Encoding.UTF8.GetString(data);
                float   x    = ParseFloat(json, "x");
                float   y    = ParseFloat(json, "y");
                float   yaw  = ParseFloat(json, "yaw");
                lock (_poseLock) { _pendingX = x; _pendingY = y; _pendingYaw = yaw; _hasPose = true; }
            }
            catch (Exception e)
            {
                if (_running) Debug.LogWarning("[RobotSync] Pose: " + e.Message);
            }
        }
    }

    // ── Cloud receive thread ───────────────────────────────────────

    void CloudReceiveLoop()
    {
        var ep = new IPEndPoint(IPAddress.Any, cloudPort);
        while (_running)
        {
            try
            {
                byte[] data = _cloudUdp.Receive(ref ep);
                string json = Encoding.UTF8.GetString(data);
                var    pts  = ParseCloud(json);
                if (pts.Count > 0)
                    lock (_cloudLock) { _pendingCloud = pts; _hasCloud = true; }
            }
            catch (Exception e)
            {
                if (_running) Debug.LogWarning("[RobotSync] Cloud: " + e.Message);
            }
        }
    }

    // ── JSON parsers (no external lib needed) ─────────────────────

    /// <summary>Parse {"pts":[[x1,y1],[x2,y2],...]} </summary>
    static List<Vector2> ParseCloud(string json)
    {
        var result = new List<Vector2>();
        // Find the array content after "pts":
        int start = json.IndexOf("[[");
        int end   = json.LastIndexOf("]]");
        if (start < 0 || end < 0) return result;

        string inner = json.Substring(start + 1, end - start);  // [[x,y],...]
        // Split on "]," to get individual pairs
        string[] pairs = inner.Split(new string[] { "],[" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string pair in pairs)
        {
            string clean = pair.Trim('[', ']', ' ');
            string[] parts = clean.Split(',');
            if (parts.Length >= 2 &&
                float.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float y))
            {
                result.Add(new Vector2(x, y));
            }
        }
        return result;
    }

    static float ParseFloat(string json, string key)
    {
        string search = $"\"{key}\":";
        int i = json.IndexOf(search);
        if (i < 0) return 0f;
        i += search.Length;
        int end = json.IndexOfAny(new[] { ',', '}' }, i);
        if (end < 0) end = json.Length;
        float.TryParse(
            json.Substring(i, end - i).Trim(),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out float val
        );
        return val;
    }
}