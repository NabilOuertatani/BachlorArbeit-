using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UnityClickToRosGoal : MonoBehaviour
{
    [Header("TCP Bridge")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 10000;
    public float reconnectDelay = 2.0f;

    [Header("Coordinate Mapping (Unity → ROS)")]
    [Tooltip("Which Unity axis maps to ROS X (forward)")]
    public AxisMapping rosX = AxisMapping.Z;
    [Tooltip("Which Unity axis maps to ROS Y (left)")]
    public AxisMapping rosY = AxisMapping.NegativeX;

    [Header("Scene")]
    public Camera sceneCamera;
    public Transform goalMarker;

    // ── Internal state ─────────────────────────────────────────────
    private TcpClient _client;
    private NetworkStream _stream;
    private Thread _sendThread;
    private ConcurrentQueue<byte[]> _sendQueue = new ConcurrentQueue<byte[]>();
    private bool _running = false;
    private bool _connected = false;

    public enum AxisMapping { X, Y, Z, NegativeX, NegativeY, NegativeZ }

    // ── Lifecycle ──────────────────────────────────────────────────

    void Start()
    {
        if (sceneCamera == null)
            sceneCamera = Camera.main;

        _running = true;
        _sendThread = new Thread(SendLoop) { IsBackground = true };
        _sendThread.Start();

        Debug.Log("UnityClickToRosGoal ready");
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryEnqueueClick();
    }

    void OnDestroy()
    {
        _running = false;
        _sendThread?.Join(500);
        CloseConnection();
    }

    // ── Click handling (main thread) ───────────────────────────────

    private void TryEnqueueClick()
    {
        if (sceneCamera == null) { Debug.LogError("No camera assigned"); return; }

        Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        Vector3 u = hit.point;

        if (goalMarker != null)
            goalMarker.position = u;

        // Configurable Unity → ROS coordinate mapping
        float rx = MapAxis(u, rosX);
        float ry = MapAxis(u, rosY);
        float rz = 0f; // ground plane navigation

        string json = $"{{\"x\":{rx:F4},\"y\":{ry:F4},\"z\":{rz:F4}}}";
        byte[] payload = Encoding.UTF8.GetBytes(json);

        // Big-endian length prefix (4 bytes)
        int len = payload.Length;
        byte[] prefix = new byte[4]
        {
            (byte)(len >> 24), (byte)(len >> 16),
            (byte)(len >>  8), (byte)(len)
        };

        byte[] packet = new byte[4 + payload.Length];
        Buffer.BlockCopy(prefix,  0, packet, 0,              4);
        Buffer.BlockCopy(payload, 0, packet, 4, payload.Length);

        _sendQueue.Enqueue(packet);
        Debug.Log($"Queued ROS goal: ({rx:F3}, {ry:F3})");
    }

    // ── Background send thread ─────────────────────────────────────

    private void SendLoop()
    {
        while (_running)
        {
            // Ensure connection
            if (!_connected)
            {
                TryConnect();
                if (!_connected)
                {
                    Thread.Sleep((int)(reconnectDelay * 1000));
                    continue;
                }
            }

            // Drain the queue
            while (_sendQueue.TryDequeue(out byte[] packet))
            {
                try
                {
                    _stream.Write(packet, 0, packet.Length);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Send failed, reconnecting: " + e.Message);
                    CloseConnection();
                    break;
                }
            }

            Thread.Sleep(10); // avoid busy loop
        }
    }

    private void TryConnect()
    {
        try
        {
            _client = new TcpClient();
            _client.Connect(serverIP, serverPort);
            _stream = _client.GetStream();
            _connected = true;
            Debug.Log($"Connected to ROS bridge at {serverIP}:{serverPort}");
        }
        catch (Exception e)
        {
            _connected = false;
            Debug.LogWarning($"ROS bridge not reachable ({serverIP}:{serverPort}): {e.Message}");
        }
    }

    private void CloseConnection()
    {
        _connected = false;
        try { _stream?.Close(); } catch { }
        try { _client?.Close(); } catch { }
        _stream = null;
        _client = null;
    }

    // ── Axis mapping helper ────────────────────────────────────────

    private static float MapAxis(Vector3 v, AxisMapping m) => m switch
    {
        AxisMapping.X         =>  v.x,
        AxisMapping.Y         =>  v.y,
        AxisMapping.Z         =>  v.z,
        AxisMapping.NegativeX => -v.x,
        AxisMapping.NegativeY => -v.y,
        AxisMapping.NegativeZ => -v.z,
        _                     =>  0f
    };
}