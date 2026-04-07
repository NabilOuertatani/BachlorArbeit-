# Goal Navigation System - Integration Guide

## Overview
The Goal Navigation System connects Unity interface to the Go2 robot movement system. When you click a point in Unity, the robot receives that goal and navigates to it.

## System Architecture

```
Unity (ClickToGoal.cs)
    ↓ publishes to
/unity_clicked_point (geometry_msgs/Point)
    ↓ subscribes to
goal_navigation_node (NEW)
    ↓ publishes to
/cmd_vel (geometry_msgs/Twist)
    ↓ subscribes to
cmd_vel_bridge.py
    ↓ converts to
Unitree Robot API
    ↓
Robot Executes Movement
```

## Running the System

### Option 1: Launch Goal Navigation Alone
```bash
source /home/haii/BachlorArbeit-/install/setup.bash
ros2 launch go2_behavior goal_navigation.launch.py
```

### Option 2: Run as Standalone Node
```bash
source /home/haii/BachlorArbeit-/install/setup.bash
ros2 run go2_behavior goal_navigation_node
```

### Option 3: With Custom Parameters
```bash
ros2 run go2_behavior goal_navigation_node --ros-args \
  -p max_speed:=0.8 \
  -p max_rotation_speed:=1.5 \
  -p goal_tolerance:=0.05
```

## Required System Components

1. **cmd_vel_bridge.py** - Must be running to translate commands to robot
   ```bash
   ros2 run go2_robot_interface cmd_vel_bridge
   ```

2. **Unity Interface** - Must be running and connected to ROS2
   - Open UnityInterface.unity in Unity Editor
   - Press Play to start simulation

3. **ROS2 Robot Communication** - Ensure robot connection is configured
   ```bash
   # On robot or networked system
   ros2 topic list  # Should show /api/sport/request
   ```

## Testing the Integration

### Test 1: Verify Topic Publishing
```bash
# Terminal 1: Start goal navigation
ros2 run go2_behavior goal_navigation_node

# Terminal 2: Monitor goal topic
ros2 topic echo /unity_clicked_point

# Unity: Click on the ground plane
# Expected: See point coordinates printed
```

### Test 2: Test Complete Chain
```bash
# Terminal 1: Start bridge
ros2 run go2_robot_interface cmd_vel_bridge

# Terminal 2: Start goal navigation
ros2 run go2_behavior goal_navigation_node

# Terminal 3: Monitor commands
ros2 topic echo /cmd_vel

# Unity: Click on ground plane
# Expected: See Twist messages with velocity commands
```

### Test 3: Check Goal Reached Signal
```bash
# Terminal: Monitor goal_reached topic
ros2 topic echo /goal_reached

# Unity: Click and wait for robot to "reach" goal
# Expected: Boolean message with 'data: true' when goal is reached
```

## Node Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `max_speed` | float | 0.5 | Maximum forward velocity (m/s) |
| `max_rotation_speed` | float | 1.0 | Maximum angular velocity (rad/s) |
| `goal_tolerance` | float | 0.1 | Distance threshold to consider goal reached (m) |
| `use_zig_zag` | bool | true | Enable zig-zag pattern (future enhancement) |
| `zig_zag_width` | float | 0.3 | Zig-zag pattern width (m) |

## Topics

### Subscribed Topics
- `/unity_clicked_point` - Incoming goal point from Unity
  - Message Type: `geometry_msgs/Point`
  - Fields: `x`, `y`, `z`

### Published Topics
- `/cmd_vel` - Velocity commands to robot
  - Message Type: `geometry_msgs/Twist`
  - Fields: `linear (x, y, z)`, `angular (x, y, z)`

- `/goal_reached` - Signal when goal is reached
  - Message Type: `std_msgs/Bool`
  - Fields: `data`

## How It Works

1. **Goal Reception:** When user clicks in Unity, ClickToGoal.cs publishes a Point message to `/unity_clicked_point`

2. **Navigation:** goal_navigation_node receives the point and:
   - Calculates heading towards the goal
   - Implements proportional control for rotation
   - Adjusts forward speed based on rotation error
   - Updates estimated position and orientation

3. **Movement:** Publishes velocity commands to `/cmd_vel`
   - Linear.x: Forward speed
   - Linear.y: Side speed (0 for now)
   - Angular.z: Rotation speed

4. **Goal Checking:** Continuously checks distance to goal
   - If distance < goal_tolerance: Goal is reached
   - Publishes `true` on `/goal_reached`
   - Stops the robot

## Troubleshooting

### Robot not moving after clicking
- Check if cmd_vel_bridge is running
- Monitor `/cmd_vel` topic - should see velocity commands
- Verify robot connection and network interface

### Goal not being received
- Check Unity is publishing to `/unity_clicked_point`
- Verify ROS TCP Connector is configured correctly
- Check firewall/network connectivity between Unity and ROS2

### Robot overshoots goal
- Decrease `max_speed` parameter
- Increase `goal_tolerance` parameter
- Reduce proportional gain (modify yaw_error * 0.5 in code)

### Robot doesn't rotate towards goal
- Check `max_rotation_speed` is not too low
- Verify `/cmd_vel` messages are being received
- Check if cmd_vel_bridge is correctly converting Twist to robot API

## Future Enhancements

1. **Zig-Zag Navigation:** Implement the zig-zag pattern from Unity
2. **Obstacle Avoidance:** Add collision detection and path replanning
3. **Odometry Integration:** Use actual robot odometry instead of estimation
4. **Smooth Trajectory:** Implement cubic spline path following
5. **Multi-Goal Queue:** Support queuing multiple goals
6. **Path Visualization:** Send planned path back to Unity for visualization

## Code Structure

### goal_navigation_node.py
- `GoalNavigationNode` - Main ROS2 node class
  - `goal_callback()` - Receives new goals from Unity
  - `control_loop()` - Main 20Hz control loop
  - `publish_cmd()` - Sends velocity commands
  - Helper methods for math (distance, heading, angle normalization)

## Notes

- Position estimation is done via integration (dead reckoning)
- In production, connect to robot odometry topic for better accuracy
- Proportional control is simple; PID control could improve performance
- Rotation is prioritized over forward movement to achieve accurate heading
