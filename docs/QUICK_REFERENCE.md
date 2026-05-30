# Quick Reference Guide

## Files Overview

### Unity (C#)

| File | Purpose | Key Methods |
|------|---------|---|
| `GestureSequenceUI.cs` | Main UI controller | `PlaySequence()`, `AddStep()`, `SaveSequence()` |
| `Multigoalmanager.cs` | Waypoint navigation | `LoadWaypoints()`, `StartNavigation()`, `IsNavigationComplete()` |
| `GestureStepData.cs` | Data structure | Simple serializable class |
| `GestureDataManager.cs` | Persistence (JSON) | `SaveSequences()`, `LoadSequences()` |

### ROS (Python)

| File | Purpose | Pub/Sub |
|------|---------|--------|
| `ros_tcp_bridge_server.py` | TCP gateway | Sub: TCP:10000 → Pub: `/unity_clicked_point`, `/api/sport_request` |
| `goal_navigation_node.py` | Navigation controller | Sub: `/unity_clicked_point`, `/utlidar/robot_pose` → Pub: `/cmd_vel` |
| `raise_leg_highlevel.py` | Dynamic gesture handler | Sub: `/api/sport_request` → Pub: `/api/sport_request` |
| `robot_api_bridge.py` | UDP to robot | Sub: `/api/sport_request` → Send: UDP:29999 |
| `cmd_vel_bridge.py` | Twist to Sport API | Sub: `/cmd_vel` → Pub: `/api/sport_request` |

---

## Message Types

### TCP Bridge (Port 10000)

**Waypoint (To Movement)**
```json
{"x": float, "y": float, "z": 0}
```

**Gesture (To Gestures)**
```json
{
  "header": {"identity": {"api_id": int}},
  "parameter": {}
}
```

### ROS Topics

| Topic | Type | Source | Destination |
|-------|------|--------|---|
| `/unity_clicked_point` | `geometry_msgs/Point` | TCP bridge | `goal_navigation_node` |
| `/api/sport_request` | `unitree_api/Request` | TCP bridge | `DynamicGestureHandler` → `robot_api_bridge` |
| `/cmd_vel` | `geometry_msgs/Twist` | `goal_navigation_node` | `cmd_vel_bridge` |
| `/goal_reached` | `std_msgs/Bool` | `goal_navigation_node` | UDP to Unity |

---

## API ID Reference

```python
1001 = Damp (disable)
1002 = StandUp
1003 = StandDown
1004 = RecoveryStand
1006 = Move (simple)
1008 = Move (continuous) ← used by cmd_vel_bridge
1016 = Hello (Raise Hand) ← gesture
1017 = Stretch
1019 = Wallow
1022 = Dance1
1023 = Dance2
```

---

## Execution Sequences

### 1. Create Sequence
```
1. Select gesture from dropdown
2. Click "+ Add Gesture"
   - Move → button appears, can add waypoints
   - Other → button hidden
3. (Optional) Click ground to add waypoints
4. (Optional) Click "+ Add Waypoints" to save them
5. Repeat steps 1-4 for more steps
6. Click "Save"
```

### 2. Play Sequence
```
1. Click on saved sequence card
2. Click "Play"
3. For each step:
   - Move: Navigate through waypoints
   - Gesture: Execute via ROS
4. Wait for sequence to complete
```

### 3. Data Flow When Playing
```
Move Step:
  GestureSequenceUI
  → MultiGoalManager.LoadWaypoints()
  → MultiGoalManager.StartNavigation()
  → Sends Point via TCP:10000
  → ros_tcp_bridge_server publishes /unity_clicked_point
  → goal_navigation_node navigates
  → Sends /cmd_vel with movement commands
  → Robot walks

Gesture Step:
  GestureSequenceUI
  → SendGestureCommand(apiId)
  → TCP:10000 with Request JSON
  → ros_tcp_bridge_server publishes /api/sport_request
  → DynamicGestureHandler forwards /api/sport_request
  → robot_api_bridge sends UDP:29999
  → Robot executes gesture
```

---

## Debugging

### Check ROS Topics
```bash
ros2 topic list
ros2 topic echo /unity_clicked_point
ros2 topic echo /api/sport_request
ros2 topic echo /cmd_vel
```

### Check Connections
```bash
# TCP bridge listening?
netstat -an | grep 10000

# Robot reachable?
ping 192.168.123.161

# Robot service?
ros2 node list | grep robot
```

### Common Issues

| Symptom | Check |
|---------|-------|
| No movement | Is `/cmd_vel` being published? Is robot at correct IP? |
| Gesture not executing | Is `DynamicGestureHandler` running? Check `/api/sport_request` |
| Sequence not saving | File write permissions in data folder? |
| Button doesn't appear | Is button component assigned in Inspector? |
| Waypoints not loading | Is MultiGoalManager in same scene? |

---

## Configuration

### Unity Scale
```csharp
scaleX = 2.0f;  // NavMesh_Ground X scale
scaleZ = 2.0f;  // NavMesh_Ground Z scale
// 1 real meter = 2 Unity units
```

### Navigation Parameters
```python
max_speed = 0.35 m/s
turn_speed = 0.6 rad/s
goal_tolerance = 0.35 m
heading_tolerance = 0.08 rad (5°)
```

### Network
```
TCP Bridge: 127.0.0.1:10000 (Unity → ROS)
Robot UDP: 192.168.123.161:29999 (API commands)
Unity UDP: 127.0.0.1:10004 (Goal reached feedback)
```

---

## Performance

| Operation | Time |
|-----------|------|
| Create sequence | Instant |
| Save sequence | < 100ms (file write) |
| Load sequence | < 50ms (file read) |
| Add waypoint | 1-2ms |
| Start navigation | Immediate |
| Navigation (per waypoint) | 5-10s (variable) |
| Gesture execution | 1-3s (variable by gesture) |
| TCP latency | < 1ms |
| ROS publish latency | < 2ms |

---

## Testing Checklist

- [ ] ROS bridge running: `ros2 run ros_tcp_bridge bridge`
- [ ] Navigation node running: `ros2 run go2_behavior goal_navigation_node`
- [ ] Gesture handler running: `ros2 run go2_behavior raise_leg_highlevel`
- [ ] Robot API bridge running: `ros2 run go2_robot_interface robot_api_bridge`
- [ ] TCP connection works: Unity connects to localhost:10000
- [ ] Robot at 192.168.123.161:29999
- [ ] LIDAR providing pose: `/utlidar/robot_pose` topic active
- [ ] No error messages in console

---

## File Locations

```
/Users/nabilouertatani/Documents/BachlorArbeit-/
├── unity/
│   └── go2_unity_control/
│       └── Assets/
│           ├── UIScripts/
│           │   ├── GestureSequenceUI.cs
│           │   └── GestureDataManager.cs
│           ├── Multigoalmanager.cs
│           └── GestureStepData.cs
├── src/
│   └── go2_behavior/
│       └── go2_behavior/
│           ├── goal_navigation_node.py
│           └── raise_leg_highlevel.py
├── ros_tcp_bridge_server.py
├── robot_api_bridge.py
└── docs/
    └── API_DOCUMENTATION.md (THIS FILE)
```

---

## Key Insights

1. **Separation of Concerns**
   - Movement: `goal_navigation_node.py` (closed-loop control)
   - Gestures: `DynamicGestureHandler` (pass-through)
   - UI: `GestureSequenceUI.cs` (user interface)

2. **Thread Safety**
   - TCP bridge uses lock-based queues
   - ROS nodes are thread-safe by default
   - Unity is single-threaded

3. **Coordinate Conversion**
   - Always happens in MultiGoalManager
   - ROS uses world frame
   - Unity uses local scene frame

4. **Error Resilience**
   - Graceful degradation on component failure
   - No hardcoded sequences (extensible)
   - Persistent storage for sequences (recoverable)

5. **Real-time Requirements**
   - Navigation: 50ms loop (20 Hz) - achievable
   - Gesture: < 1s latency - easily met
   - TCP: < 10ms - comfortable margin

---

## Contact & Support

For detailed API documentation, see `API_DOCUMENTATION.md`
For architecture diagrams, see project diagrams
For ROS setup, see ROS README
