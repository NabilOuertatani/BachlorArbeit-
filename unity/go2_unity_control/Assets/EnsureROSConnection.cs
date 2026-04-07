using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

/// <summary>
/// Automatically ensures ROSConnection is properly set up.
/// This script runs first to guarantee ROS connectivity before other components.
/// </summary>
public class EnsureROSConnection : MonoBehaviour
{
    private void Awake()
    {
        // Get or create ROSConnection instance
        ROSConnection ros = ROSConnection.GetOrCreateInstance();
        
        if (ros == null)
        {
            Debug.LogError("[CRITICAL] Failed to get or create ROSConnection!");
            return;
        }
        
        // Log connection details
        Debug.Log("[ROS] ROSConnection instance ready (127.0.0.1:10000)");
        Debug.Log("[ROS] Make sure the ROS TCP bridge is running:");
        Debug.Log("[ROS]   source setup_robot.sh && python3 ros_tcp_bridge_server.py");
    }
}
