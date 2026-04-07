#  Unity Click-to-Goal Setup Guide

## Problem
Robot doesn't follow where you click in Unity. The reason: **ROSConnectionPrefab is not in the scene**.

## Solution - Add ROSConnection to Your Scene

### Option 1: Automatic (Recommended)
1. Open `UnityInterface.unity` in Unity Editor
2. The `EnsureROSConnection.cs` script will automatically initialize the connection when you press Play
3. Check the Console window - you should see:
   ```
   [ROS] ROSConnection instance ready
   [ROS] ✓ Connected to ROS at: 127.0.0.1:10000
   ```

### Option 2: Manual Setup
1. Open `UnityInterface.unity` in Unity Editor
2. In the Hierarchy, right-click and select **Instantiate Prefab**
3. Find and select `Assets/Resources/ROSConnectionPrefab.prefab`
4. Verify the settings in the Inspector:
   - **IP Address:** 127.0.0.1
   - **ROS Port:** 10000
   - **Connect On Start:** ✓ (enabled)
   - **Keepalive Time:** 1

## Before Testing

Make sure the ROS2 bridge is running:

```bash
cd ~/BachlorArbeit-
source setup_robot.sh
python3 ros_tcp_bridge_server.py
```

## Testing

1. Open Unity Console (Window → General → Console)
2. Start the game (Play button)
3. Watch Console for:
   - **Success:** `[ROS] ✓ Connected to ROS at: 127.0.0.1:10000`
   - **Failure:**  `[ROS] ⏳ Waiting for connection...` means bridge isn't running

4. Click on the ground plane in the game
5. Check Console for debug messages like:
   - `Clicked: (x, y, z)`
   - `Goal published to ROS`

## Troubleshooting

### Issue: "No connection" message
**Solution:** Start the TCP bridge first:
```bash
source ~/BachlorArbeit-/setup_robot.sh && python3 ros_tcp_bridge_server.py
```

### Issue: Click doesn't work
**Solution:** Make sure a camera is in the scene and is set as "Main Camera". Also enable the Plane collider.

### Issue: ROS messages not received
**Solution:** Check that `/unity_clicked_point` topic is being published:
```bash
source ~/BachlorArbeit-/setup_robot.sh
ros2 topic echo /unity_clicked_point
```

## Architecture

```
Unity Scene
  ├─ EnsureROSConnection (auto-initializes)
  ├─ ClickToGoal (handles mouse clicks)
  └─ MainCamera
          ↓
    TCP Bridge (port 10000)
        ↓
    ROS2 Topics
        ├─ /unity_clicked_point (receive goal)
        ├─ /cmd_vel (send velocity)
        └─ /goal_reached (receive done)
```
