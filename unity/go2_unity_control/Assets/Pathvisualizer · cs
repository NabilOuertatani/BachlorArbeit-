using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Draws two things in Unity:
///   1. A line from the robot's current position to the goal (planned path)
///   2. A trail of where the robot has been (actual path)
///
/// Attach to any GameObject in your scene.
/// Assign robotMarker and goalMarker in the Inspector.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class PathVisualizer : MonoBehaviour
{
    [Header("Scene references")]
    public Transform robotMarker;   // the dawg
    public Transform goalMarker;    // GoalMarker in your scene

    [Header("Planned path (robot → goal)")]
    public Color plannedColor    = new Color(0f, 0.8f, 1f, 0.8f);  // cyan
    public float plannedWidth    = 0.05f;

    [Header("Actual path (trail)")]
    public Color  trailColor     = new Color(1f, 0.4f, 0f, 0.8f);  // orange
    public float  trailWidth     = 0.03f;
    public float  trailInterval  = 0.10f;   // record a point every X metres
    public int    maxTrailPoints = 500;

    [Header("UDP path receiver (optional)")]
    public int    pathPort       = 10003;   // ROS sends waypoints here
    public bool   receiveFromROS = false;

    // ── Internal ───────────────────────────────────────────────────
    private LineRenderer _plannedLine;
    private LineRenderer _trailLine;
    private GameObject   _trailObj;

    private List<Vector3> _trail        = new List<Vector3>();
    private Vector3       _lastRecorded = Vector3.positiveInfinity;
    private bool          _hasGoal      = false;

    // ROS waypoints (optional)
    private UdpClient     _udp;
    private Thread        _udpThread;
    private bool          _running;
    private List<Vector3> _pendingWaypoints = null;
    private readonly object _wpLock = new object();

    // ── Lifecycle ──────────────────────────────────────────────────

    void Start()
    {
        // Planned path line renderer (on this GameObject)
        _plannedLine             = GetComponent<LineRenderer>();
        _plannedLine.startWidth  = plannedWidth;
        _plannedLine.endWidth    = plannedWidth;
        _plannedLine.material    = CreateMaterial(plannedColor);
        _plannedLine.positionCount = 0;
        _plannedLine.useWorldSpace = true;

        // Trail line renderer (separate GameObject)
        _trailObj  = new GameObject("PathTrail");
        _trailLine = _trailObj.AddComponent<LineRenderer>();
        _trailLine.startWidth    = trailWidth;
        _trailLine.endWidth      = trailWidth;
        _trailLine.material      = CreateMaterial(trailColor);
        _trailLine.positionCount = 0;
        _trailLine.useWorldSpace = true;

        // Optional UDP receiver for ROS waypoints
        if (receiveFromROS)
        {
            _running   = true;
            _udp       = new UdpClient(pathPort);
            _udpThread = new Thread(UdpLoop) { IsBackground = true };
            _udpThread.Start();
            Debug.Log("[PathVisualizer] UDP path receiver on port " + pathPort);
        }

        Debug.Log("[PathVisualizer] Ready");
    }

    void Update()
    {
        if (robotMarker == null) return;

        UpdateTrail();
        UpdatePlannedPath();
        ApplyWaypoints();
    }

    void OnDestroy()
    {
        _running = false;
        _udp?.Close();
        _udpThread?.Join(300);
        if (_trailObj != null) Destroy(_trailObj);
    }

    // ── Trail (actual path) ────────────────────────────────────────

    void UpdateTrail()
    {
        Vector3 pos = robotMarker.position;

        // Record point if moved far enough
        if (_lastRecorded == Vector3.positiveInfinity ||
            Vector3.Distance(pos, _lastRecorded) >= trailInterval)
        {
            _trail.Add(new Vector3(pos.x, 0.02f, pos.z));
            _lastRecorded = pos;

            // Trim old points
            if (_trail.Count > maxTrailPoints)
                _trail.RemoveAt(0);

            _trailLine.positionCount = _trail.Count;
            _trailLine.SetPositions(_trail.ToArray());
        }
    }

    // ── Planned path (robot → goal) ────────────────────────────────

    void UpdatePlannedPath()
    {
        if (goalMarker == null) return;

        // Show line only when goal marker is active and far enough
        float dist = Vector3.Distance(robotMarker.position, goalMarker.position);
        if (dist < 0.3f)
        {
            _plannedLine.positionCount = 0;
            return;
        }

        _plannedLine.positionCount = 2;
        _plannedLine.SetPosition(0, new Vector3(robotMarker.position.x, 0.03f, robotMarker.position.z));
        _plannedLine.SetPosition(1, new Vector3(goalMarker.position.x,  0.03f, goalMarker.position.z));
    }

    // ── ROS waypoints (optional) ───────────────────────────────────

    void ApplyWaypoints()
    {
        lock (_wpLock)
        {
            if (_pendingWaypoints == null) return;
            var wps = _pendingWaypoints;
            _pendingWaypoints = null;

            if (wps.Count > 1)
            {
                _plannedLine.positionCount = wps.Count;
                _plannedLine.SetPositions(wps.ToArray());
            }
        }
    }

    void UdpLoop()
    {
        var ep = new IPEndPoint(IPAddress.Any, pathPort);
        while (_running)
        {
            try
            {
                byte[]        data = _udp.Receive(ref ep);
                string        json = Encoding.UTF8.GetString(data);
                List<Vector3> wps  = ParseWaypoints(json);
                if (wps.Count > 0)
                    lock (_wpLock) { _pendingWaypoints = wps; }
            }
            catch (Exception e)
            {
                if (_running) Debug.LogWarning("[PathVisualizer] UDP: " + e.Message);
            }
        }
    }

    // ── Public API ─────────────────────────────────────────────────

    /// <summary>Call this to clear the trail (e.g. on new goal).</summary>
    public void ClearTrail()
    {
        _trail.Clear();
        _lastRecorded           = Vector3.positiveInfinity;
        _trailLine.positionCount = 0;
    }

    // ── Helpers ────────────────────────────────────────────────────

    static List<Vector3> ParseWaypoints(string json)
    {
        var result = new List<Vector3>();
        try
        {
            int start = json.IndexOf("[[");
            int end   = json.LastIndexOf("]]");
            if (start < 0 || end < 0) return result;

            string   inner = json.Substring(start + 1, end - start);
            string[] pairs = inner.Split(new string[] { "],[", "], [" },
                                         StringSplitOptions.RemoveEmptyEntries);

            foreach (string pair in pairs)
            {
                string   clean = pair.Replace("[", "").Replace("]", "").Trim();
                string[] parts = clean.Split(',');
                if (parts.Length >= 2)
                {
                    float rx, ry;
                    bool okX = float.TryParse(parts[0].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out rx);
                    bool okY = float.TryParse(parts[1].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out ry);
                    if (okX && okY)
                        // ROS (x fwd, y left) → Unity (x right, z fwd)
                        result.Add(new Vector3(-ry, 0.03f, rx));
                }
            }
        }
        catch (Exception e) { Debug.LogWarning("[PathVisualizer] Parse: " + e.Message); }
        return result;
    }

    static Material CreateMaterial(Color color)
    {
        var mat   = new Material(Shader.Find("Sprites/Default"));
        mat.color = color;
        return mat;
    }
}