# Dynamic Per-Waypoint Speed Selection Setup Guide

## Overview
This guide shows how to implement per-waypoint speed control for the Go2 robot navigation system.

---

## **PART 1: Unity Side (Already Complete)**

### Files Created:
1. **WaypointWithSpeed.cs** — Data structure storing position + speed
2. **SpeedSelector.cs** — UI controller for speed selection  
3. **Updated MultiGoalManager.cs** — Uses WaypointWithSpeed, sends speed in JSON

### Key Changes in MultiGoalManager.cs:
- Replaced `List<Vector3> _rosGoals` + `List<Vector3> _unityPos` with `List<WaypointWithSpeed> _waypoints`
- TryAddWaypoint() now calls `speedSelector.Show()` after placing waypoint
- OnAddPointsPressed() reads speed from SpeedSelector and applies it to waypoint
- AdvanceToNextGoal() now sends speed in TCP JSON: `{"x": 1.23, "y": 0.45, "z": 0, "speed": 0.4}`
- Waypoint markers now show speed as color: Blue (Slow 0.2), Yellow (Normal 0.4), Red (Fast 0.8)

---

## **PART 2: Unity UI Setup**

### Step 1: Create SpeedPanel in Canvas

In your **MainUI.unity** scene, add this hierarchy inside Canvas:

```
Canvas
└── SpeedPanel (NEW)
    ├── LayoutGroup (add Vertical Layout Group)
    ├── SpeedLabel (Text - "Set speed for waypoint X:")
    ├── SpeedButtonsRow
    │   ├── SlowButton ("SLOW 0.2 m/s")
    │   ├── NormalButton ("NORMAL 0.4 m/s")
    │   └── FastButton ("FAST 0.8 m/s")
    ├── CustomSpeedInput (TMP_InputField, optional)
    └── SelectedSpeedText (Text - "Selected: 0.4 m/s")
```

### Step 2: Assign References in MultiGoalManager

In the **UnityInterface.unity** scene:

1. Find **MultiGoalManager** GameObject
2. In Inspector, assign:
   - **SpeedSelector** → Drag the **SpeedSelector** script instance (attached to SpeedPanel)

### Step 3: Assign References in SpeedSelector

Attach **SpeedSelector.cs** to the **SpeedPanel** GameObject, then assign in Inspector:

- **Speed Panel** → SpeedPanel (self)
- **Speed Label** → SpeedLabel text component
- **Slow Button** → SlowButton
- **Normal Button** → NormalButton
- **Fast Button** → FastButton
- **Selected Speed Text** → SelectedSpeedText
- **Custom Speed Input** → CustomSpeedInput (optional)

Set preset speeds:
- **Slow Speed** = 0.2
- **Normal Speed** = 0.4
- **Fast Speed** = 0.8

---

## **PART 3: ROS Side Updates**

### Update: goal_navigation_node.py

**Location:** `/src/go2_behavior/go2_behavior/goal_navigation_node.py`

**Changes to make:**

1. **Add instance variable** to store current waypoint speed:
```python
self.current_waypoint_speed = 0.35  # Default to max_speed
self._default_max_speed = self.max_speed  # Store default
```

2. **Update `_on_goal()` method** to extract speed from Point message:
```python
def _on_goal(self, msg: Point):
    """Receive goal from Unity, extract speed if provided."""
    if not self.pose_ready:
        self.get_logger().warn('No pose yet — waiting for /utlidar/robot_pose')
        return
    
    # Check if speed info is available (z field can hold encoded data or just use 0)
    # For now, speed will come via separate topic or encoded in point
    # See ros_tcp_bridge_server.py changes below
    
    self.goal = (msg.x, msg.y)
    self.phase = self.TURNING
    self.get_logger().info(f'New goal: ({msg.x:.2f}, {msg.y:.2f})')
    self._pub_status('NAVIGATING')
```

3. **Add speed update in `_loop()`** - after getting the goal, use current_waypoint_speed:
```python
# In _loop() method, replace max_speed with current_waypoint_speed:
vx = max(0.10, self.current_waypoint_speed * proximity)  # Use waypoint-specific speed
```

4. **Subscribe to speed topic**:
```python
# In __init__, add subscriber:
self.create_subscription(Float32, '/nav_waypoint_speed', self._on_speed, 10)

# Add handler:
def _on_speed(self, msg):
    """Update speed for current waypoint."""
    self.current_waypoint_speed = msg.data
    self.get_logger().info(f'Waypoint speed set to {self.current_waypoint_speed:.2f} m/s')
```

---

### Update: ros_tcp_bridge_server.py

**Location:** `/src/ROS-TCP-Endpoint/src/ros_tcp_bridge_server.py` (or similar)

**Changes to make:**

1. **Parse speed from incoming JSON**:
```python
import json
from std_msgs.msg import Float32

# In message handler:
def handle_unity_goal(data):
    """Parse goal JSON and extract x, y, speed."""
    try:
        msg_data = json.loads(data.decode('utf-8'))
        x = msg_data.get('x', 0.0)
        y = msg_data.get('y', 0.0)
        speed = msg_data.get('speed', 0.35)  # Default if not provided
        
        # Publish Point (x, y)
        goal_msg = Point(x=float(x), y=float(y), z=0)
        publisher_goal.publish(goal_msg)
        
        # Publish speed as separate topic
        speed_msg = Float32(data=float(speed))
        publisher_speed.publish(speed_msg)
        
        print(f"[TCP Bridge] Goal: ({x}, {y}), Speed: {speed} m/s")
        
    except json.JSONDecodeError as e:
        print(f"[TCP Bridge] JSON parse error: {e}")
```

2. **Register publishers** in __init__:
```python
# Publisher for goal point
publisher_goal = node.create_publisher(Point, '/unity_clicked_point', 10)

# NEW: Publisher for waypoint speed
publisher_speed = node.create_publisher(Float32, '/nav_waypoint_speed', 10)
```

---

## **PART 4: Testing Workflow**

### Test Case: Multi-speed Navigation

1. **Start ROS2 system:**
   ```bash
   ros2 launch go2_bringup base.launch.py
   ```

2. **In Unity:**
   - Click floor to place first waypoint
   - SpeedPanel appears with "Set speed for waypoint 1:"
   - Click "FAST" (0.8 m/s) — text turns green
   - Click "ADD POINTS"
   - Waypoint marker turns RED (fast indicator)

3. **Repeat for next waypoints with different speeds**

4. **Click WALK** to start navigation
   - Debug output shows: `Sent waypoint 1: {"x": 1.23, "y": 0.45, "z": 0, "speed": 0.8}`
   - ROS bridge receives JSON, extracts speed
   - goal_navigation_node.py reads speed: "Waypoint speed set to 0.80 m/s"
   - Robot accelerates to 0.8 m/s for this segment
   - After reaching goal, next waypoint speed is used

5. **Verify in ROS**:
   ```bash
   ros2 topic echo /nav_waypoint_speed
   ```
   Should show speed updates as robot navigates waypoints

---

## **PART 5: Gesture Sequence Integration**

When saving/loading gestures with sequences:

1. **GestureSequenceUI** calls `RobotBridge.LoadWaypoints(waypointPositions)`
2. **MultiGoalManager.LoadWaypoints()** creates WaypointWithSpeed objects with DEFAULT speed 0.4 m/s
3. **For full speed control in saved sequences**, modify gesture data structure to save speeds:

   **In GestureDataManager.cs**, update SavedStep:
   ```csharp
   public class SavedStep
   {
       public string stepName;
       public List<SerializableWaypoint> waypoints;  // NEW
   }
   
   [System.Serializable]
   public class SerializableWaypoint
   {
       public float x, y, z;
       public float speed;  // NEW
   }
   ```

4. **Update GestureSequenceUI.PlaySequence()** to preserve speeds when saving/loading

---

## **PART 6: Troubleshooting**

### SpeedPanel not appearing
- Check SpeedSelector reference in MultiGoalManager inspector
- Verify SpeedPanel is in correct scene (MainUI.unity)
- Check Console for: `[SpeedSelector] Showing panel for waypoint X`

### Speed not changing robot speed
- Verify goal_navigation_node.py has the `/nav_waypoint_speed` subscriber
- Check ROS output: `ros2 topic echo /nav_waypoint_speed`
- Ensure robot is listening to /nav_waypoint_speed topic

### Waypoint markers not showing colors
- Verify SpeedSelector.GetSpeedColor() returns correct Color objects
- Check SetMarkerColor() is called with correct color after speed selection

### Speed not saved with gestures
- Implement the gesture data structure updates in Part 5
- Test with: Play → Move with speeds → Stop → Play again → speeds should persist

---

## **Summary of Implementation**

```
User Interaction Flow:
┌─────────────────────────────────────────────────────────────────┐
│  1. Click floor → waypoint placed + SpeedPanel shows            │
│  2. Select speed (Slow/Normal/Fast) → highlighted green         │
│  3. Click ADD POINTS → speed applied, panel hides               │
│  4. Repeat for each waypoint                                    │
│  5. Click WALK → Sends {"x", "y", "z", "speed"} via TCP        │
│  6. ROS bridge parses JSON, publishes speed to /nav_waypoint_speed
│  7. goal_navigation_node.py uses speed for this segment         │
│  8. Robot navigates at specified speed to each waypoint         │
└─────────────────────────────────────────────────────────────────┘

Data Flow:
  Unity Waypoint (0.8 m/s)
           ↓
  JSON: {"x": 1.23, "y": 0.45, "z": 0, "speed": 0.8}
           ↓
  TCP → ros_tcp_bridge_server.py
           ↓
  Publish /unity_clicked_point (Point)
  Publish /nav_waypoint_speed (Float32 = 0.8)
           ↓
  goal_navigation_node.py subscribes & uses:
    current_waypoint_speed = 0.8
    vx = max(0.10, 0.8 * proximity)  # Robot accelerates to 0.8 m/s
           ↓
  Robot navigates at correct speed
```

---

## **Files Reference**

| File | Location | Status |
|------|----------|--------|
| WaypointWithSpeed.cs | `/Assets/` | ✅ Created |
| SpeedSelector.cs | `/Assets/` | ✅ Created |
| Multigoalmanager.cs | `/Assets/` | ✅ Updated |
| goal_navigation_node.py | `/src/go2_behavior/` | 📝 Needs updates in Part 3 |
| ros_tcp_bridge_server.py | `/src/ROS-TCP-Endpoint/` | 📝 Needs updates in Part 4 |

---

## **Next Steps**

1. ✅ **Unity side is complete** — WaypointWithSpeed + SpeedSelector + MultiGoalManager updated
2. 📝 **Add SpeedPanel UI** in MainUI.unity (Part 2)
3. 📝 **Update ROS scripts** with speed parsing (Parts 3 & 4)
4. 🧪 **Test with manual waypoints** (Part 6)
5. 🧪 **Test with gesture sequences** (Part 5)

Good luck! 🚀
