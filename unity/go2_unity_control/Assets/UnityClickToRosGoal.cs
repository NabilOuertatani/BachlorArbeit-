using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class UnityClickToRosGoal : MonoBehaviour
{
    [Header("TCP Bridge")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 10000;

    [Header("Scene")]
    public Camera sceneCamera;
    public Transform goalMarker;

    void Start()
    {
        if (sceneCamera == null)
            sceneCamera = Camera.main;

        Debug.Log("UnityClickToRosGoal ready");
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PublishClickedPoint();
        }
    }

    private void PublishClickedPoint()
    {
        if (sceneCamera == null)
        {
            Debug.LogError("No camera assigned");
            return;
        }

        Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        Vector3 unityPoint = hit.point;

        if (goalMarker != null)
            goalMarker.position = unityPoint;

        // Unity -> ROS-style coordinates
        Vector3 rosPoint = new Vector3(
            unityPoint.z,
            -unityPoint.x,
            unityPoint.y
        );

        string json = $"{{\"x\":{rosPoint.x},\"y\":{rosPoint.y},\"z\":{rosPoint.z}}}";
        byte[] payload = Encoding.UTF8.GetBytes(json);
        byte[] lengthPrefix = System.BitConverter.GetBytes(payload.Length);

        try
        {
            using (TcpClient client = new TcpClient(serverIP, serverPort))
            using (NetworkStream stream = client.GetStream())
            {
                stream.Write(lengthPrefix, 0, lengthPrefix.Length);
                stream.Write(payload, 0, payload.Length);
            }

            Debug.Log($"Published ROS point: ({rosPoint.x:F3}, {rosPoint.y:F3}, {rosPoint.z:F3})");
        }
        catch (System.Exception e)
        {
            Debug.LogError("TCP send failed: " + e.Message);
        }
    }
}