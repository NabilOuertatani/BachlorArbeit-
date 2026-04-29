using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Receives robot pose and LiDAR cloud from ROS2.
/// Applies NavMesh_Ground scale (2,1,2) so Unity dog matches real world.
/// </summary>
public class RobotSync : MonoBehaviour
{
    [Header("Network")]
    public int posePort  = 10001;
    public int cloudPort = 10002;

    [Header("Scene references")]
    public Transform  robotMarker;
    public Transform  obstacleParent;
    public GameObject obstaclePrefab;

    [Header("Scale (Unity units per real meter)")]
    public float scaleX = 2.0f;   // NavMesh_Ground scale X
    public float scaleZ = 2.0f;   // NavMesh_Ground scale Z

    [Header("Movement speed")]
    public float speedMultiplier = 1.0f;  // Increase to make dog move faster in simulation

    [Header("Obstacle visualisation")]
    public int   maxObstacles   = 300;
    public float obstacleHeight = 0.05f;

    private UdpClient _poseUdp;
    private UdpClient _cloudUdp;
    private Thread    _poseThread;
    private Thread    _cloudThread;
    private bool      _running;

    private readonly object _poseLock  = new object();
    private readonly object _cloudLock = new object();

    private float         _pendingX, _pendingY, _pendingYaw;
    private bool          _hasPose;
    private List<Vector2> _pendingCloud = new List<Vector2>();
    private bool          _hasCloud;

    private List<GameObject> _dots = new List<GameObject>();

    void Start()
    {
        _running  = true;
        _poseUdp  = new UdpClient(posePort);
        _cloudUdp = new UdpClient(cloudPort);

        _poseThread  = new Thread(PoseLoop)  { IsBackground = true };
        _cloudThread = new Thread(CloudLoop) { IsBackground = true };
        _poseThread.Start();
        _cloudThread.Start();

        Debug.Log("[RobotSync] Started — pose:" + posePort +
                  " cloud:" + cloudPort +
                  " scale(" + scaleX + ", " + scaleZ + ")");
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

    // ── Apply pose ─────────────────────────────────────────────────

    void ApplyPose()
    {
        if (robotMarker == null) return;
        lock (_poseLock)
        {
            if (!_hasPose) return;
            // ROS (x fwd, y left) → Unity (x right, z fwd) × scale × speedMultiplier
            Vector3 targetPos = new Vector3(
                -_pendingY * scaleX * speedMultiplier,
                robotMarker.position.y,
                 _pendingX * scaleZ * speedMultiplier
            );
            robotMarker.position = targetPos;
            // ROS yaw CCW → Unity Y rotation CW
            robotMarker.rotation = Quaternion.Euler(
                -90f, -_pendingYaw * Mathf.Rad2Deg * speedMultiplier, 0f);
            _hasPose = false;
        }
    }

    // ── Apply cloud ────────────────────────────────────────────────

    void ApplyCloud()
    {
        lock (_cloudLock)
        {
            if (!_hasCloud) return;
            List<Vector2> pts = new List<Vector2>(_pendingCloud);
            _hasCloud = false;

            while (_dots.Count < pts.Count && _dots.Count < maxObstacles)
            {
                GameObject dot = obstacleParent != null
                    ? Instantiate(obstaclePrefab, obstacleParent)
                    : Instantiate(obstaclePrefab);
                _dots.Add(dot);
            }

            int visible = pts.Count < maxObstacles ? pts.Count : maxObstacles;
            for (int i = 0; i < _dots.Count; i++)
            {
                if (i < visible)
                {
                    // ROS robot frame (x fwd, y left) → Unity × scale
                    // Cloud is relative to robot so add robot position
                    float rx = robotMarker != null ? robotMarker.position.x : 0f;
                    float rz = robotMarker != null ? robotMarker.position.z : 0f;
                    _dots[i].transform.position = new Vector3(
                        rx + (-pts[i].y * scaleX),
                        obstacleHeight,
                        rz + ( pts[i].x * scaleZ)
                    );
                    _dots[i].SetActive(true);
                }
                else
                {
                    _dots[i].SetActive(false);
                }
            }
        }
    }

    // ── Pose receive thread ────────────────────────────────────────

    void PoseLoop()
    {
        var ep = new IPEndPoint(IPAddress.Any, posePort);
        while (_running)
        {
            try
            {
                byte[] data = _poseUdp.Receive(ref ep);
                string json = Encoding.UTF8.GetString(data);
                float x   = ParseFloat(json, "x");
                float y   = ParseFloat(json, "y");
                float yaw = ParseFloat(json, "yaw");
                lock (_poseLock)
                {
                    _pendingX = x; _pendingY = y; _pendingYaw = yaw;
                    _hasPose  = true;
                }
            }
            catch (Exception e)
            {
                if (_running) Debug.LogWarning("[RobotSync] Pose: " + e.Message);
            }
        }
    }

    // ── Cloud receive thread ───────────────────────────────────────

    void CloudLoop()
    {
        var ep = new IPEndPoint(IPAddress.Any, cloudPort);
        while (_running)
        {
            try
            {
                byte[]        data = _cloudUdp.Receive(ref ep);
                string        json = Encoding.UTF8.GetString(data);
                List<Vector2> pts  = ParseCloud(json);
                if (pts.Count > 0)
                    lock (_cloudLock) { _pendingCloud = pts; _hasCloud = true; }
            }
            catch (Exception e)
            {
                if (_running) Debug.LogWarning("[RobotSync] Cloud: " + e.Message);
            }
        }
    }

    // ── JSON parsers ───────────────────────────────────────────────

    static List<Vector2> ParseCloud(string json)
    {
        var result = new List<Vector2>();
        try
        {
            int start = json.IndexOf("[[");
            int end   = json.LastIndexOf("]]");
            if (start < 0 || end < 0) return result;

            string   inner = json.Substring(start + 1, end - start);
            string[] pairs = inner.Split(
                new string[] { "],[", "], [" },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (string pair in pairs)
            {
                string   clean = pair.Replace("[", "").Replace("]", "").Trim();
                string[] parts = clean.Split(',');
                if (parts.Length >= 2)
                {
                    float x, y;
                    bool okX = float.TryParse(parts[0].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out x);
                    bool okY = float.TryParse(parts[1].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out y);
                    if (okX && okY)
                        result.Add(new Vector2(x, y));
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[RobotSync] ParseCloud: " + e.Message);
        }
        return result;
    }

    static float ParseFloat(string json, string key)
    {
        string search = "\"" + key + "\":";
        int i = json.IndexOf(search);
        if (i < 0) return 0f;
        i += search.Length;
        int end = json.IndexOfAny(new char[] { ',', '}' }, i);
        if (end < 0) end = json.Length;
        float val;
        float.TryParse(json.Substring(i, end - i).Trim(),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out val);
        return val;
    }
}