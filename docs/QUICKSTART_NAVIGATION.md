# Quick Start: Unity Click-to-Goal Navigation

## What's New
✨ A new ROS2 node (`goal_navigation_node.py`) that connects Unity clicks to robot movement!

## The Flow
```
Click in Unity → Goal point sent to ROS2 → Robot navigates to goal
```

## Getting Started (3 Steps)

### Step 1: Rebuild the Package
```bash
cd /home/haii/BachlorArbeit-
source install/setup.bash
colcon build --packages-select go2_behavior
source install/setup.bash  # Refresh environment
```

### Step 2: Start the Full System
Open 3 terminals:

**Terminal 1 - Goal Navigation Node:**
```bash
source /home/haii/BachlorArbeit-/install/setup.bash
ros2 run go2_behavior goal_navigation_node
```

**Terminal 2 - Command Bridge:**
```bash
source /home/haii/BachlorArbeit-/install/setup.bash
ros2 run go2_robot_interface cmd_vel_bridge
```

**Terminal 3 - Monitor Commands (optional):**
```bash
source /home/haii/BachlorArbeit-/install/setup.bash
ros2 topic echo /cmd_vel
```

### Step 3: Use in Unity
1. Open the project in Unity: `/home/haii/BachlorArbeit-/unity/go2_unity_control/`
2. Open scene: `Assets/UnityInterface.unity`
3. Press Play
4. **Click on the ground plane** to set a goal point
5. Watch the virtual dog navigate to it!

## What Happens Behind the Scenes

1. **ClickToGoal.cs** (Unity script)
   - Detects mouse clicks on ground plane
   - Calculates click position
   - Publishes to `/unity_clicked_point` topic

2. **goal_navigation_node** (new Python node)
   - Receives goal point from `/unity_clicked_point`
   - Calculates heading towards goal
   - Publishes velocity commands to `/cmd_vel`

3. **cmd_vel_bridge** (existing Python node)
   - Converts `/cmd_vel` messages to Unitree robot API
   - Sends commands to real or simulated robot

## Verify It's Working

Check each level:

**Level 1: Are clicks published?**
```bash
ros2 topic echo /unity_clicked_point
# Click in Unity, should see coordinates printed
```

**Level 2: Are commands generated?**
```bash
ros2 topic echo /cmd_vel
# Should see velocity messages after clicks
```

**Level 3: Is robot responding?**
- Check real robot or sim responds to movement
- Monitor `/api/sport/request` on robot side

## Common Issues & Fixes

| Issue | Solution |
|-------|----------|
| "No module named 'geomet...'" | `colcon build --packages-select go2_behavior` and restart |
| Robot not moving | Check cmd_vel_bridge is running in Terminal 2 |
| No click detection in Unity | Make sure a Collider is on ground plane |
| Goal never reached | Check goal_tolerance parameter (default 0.1m) |

## Parameters You Can Adjust

```bash
# Run with custom speed
ros2 run go2_behavior goal_navigation_node \
  --ros-args -p max_speed:=1.0 -p goal_tolerance:=0.05
```

| Parameter | Effect |
|-----------|--------|
| `max_speed` | How fast the robot moves (default 0.5 m/s) |
| `max_rotation_speed` | How fast it rotates (default 1.0 rad/s) |
| `goal_tolerance` | How close to get to goal (default 0.1 m) |

## Next Steps

- ✅ Test with virtual dog in Unity
- 🔜 Connect to real Go2 robot
- 🔜 Add obstacle avoidance
- 🔜 Implement zig-zag pattern

## Documentation
For detailed info, see: [GOAL_NAVIGATION_GUIDE.md](GOAL_NAVIGATION_GUIDE.md)

For full project overview: [PROJECT_SUMMARY.md](PROJECT_SUMMARY.md)

---

**Questions?** Check the log output in terminals for detailed error messages!
