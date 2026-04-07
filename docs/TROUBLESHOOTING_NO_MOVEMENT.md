# Troubleshooting: Robot Not Moving

## The Problem
You set up goal_navigation_node and cmd_vel_bridge, but the real robot (or simulation) doesn't move when clicking in Unity.

## Symptoms
- Goal navigation node starts successfully
- No errors in console
- But robot doesn't move

## Root Causes & Solutions

### 1. **Missing ROS2 Setup** ⚠️ [LIKELY YOUR ISSUE]

**Symptom:** `ros2: command not found`

**Solution:** Use the new setup script:
```bash
cd ~/BachlorArbeit-
bash setup_humble_local.sh
source install/setup.bash
ros2 run go2_behavior goal_navigation_node
```

---

### 2. **Missing DDS Configuration** ⚠️ [LIKELY YOUR ISSUE]  

**Symptom:** ROS nodes hang or don't communicate

**Solution:** Set middleware environment variables:
```bash
export RMW_IMPLEMENTATION=rmw_cyclonedds_cpp
export CYCLONEDDS_URI='<CycloneDDS><Domain><General><Interfaces>
                        <NetworkInterface name="lo" priority="default" multicast="default" />
                    </Interfaces></General></Domain></CycloneDDS>'
```

**Easier:** Just use the setup script (includes this automatically):
```bash
bash setup_humble_local.sh
```

---

### 3. **Message Flow Not Connected**

Check each step:

#### Step 1: Goal received from Unity?
```bash
# Terminal: Monitor goal topic
ros2 topic echo /unity_clicked_point

# In Unity: Click on ground plane
# Expected: See point coordinates like:
# x: 5.0
# y: 0.0  
# z: 3.5
```

#### Step 2: Navigation node publishing commands?
```bash
# Terminal: Monitor velocity commands
ros2 topic echo /cmd_vel

# Should see (after clicking in Unity):
# linear:
#   x: 0.5
#   y: 0.0
#   z: 0.0
# angular:
#   x: 0.0
#   y: 0.0
#   z: 1.2
```

#### Step 3: Bridge receiving and converting?
```bash
# Terminal: Monitor robot requests
ros2 topic echo /api/sport/request

# Should see sport movement requests with parameters
```

---

## Step-by-Step Test

### Setup: 3 Terminals

**Terminal 1 - Goal Navigation:**
```bash
cd ~/BachlorArbeit-
bash setup_humble_local.sh
ros2 run go2_behavior goal_navigation_node
```

**Terminal 2 - Command Bridge:**
```bash
cd ~/BachlorArbeit-
bash setup_humble_local.sh
ros2 run go2_robot_interface cmd_vel_bridge
```

**Terminal 3 - Monitor (to debug):**
```bash
cd ~/BachlorArbeit-
bash setup_humble_local.sh
ros2 topic echo /cmd_vel
```

### Run Test:
1. All terminals ready?
2. Open Unity: `unity/go2_unity_control/`
3. Open scene: `Assets/UnityInterface.unity`
4. **Play** button in editor
5. **Click on ground plane**
6. Check Terminal 3 - do you see `/cmd_vel` messages?

---

## Common Error Messages

### "ros2: command not found"
```bash
# FIX: Source ROS2 first
source /opt/ros/humble/setup.bash
source ~/BachlorArbeit-/install/setup.bash
```

### "Cannot find module 'unitree_api'"
```bash
# FIX: Rebuild workspace
cd ~/BachlorArbeit-
colcon build
source install/setup.bash
```

### "DDS communication timeout"
```bash
# FIX: Set DDS middleware
bash setup_humble_local.sh  # Does this automatically
```

### "No publishers on /unity_clicked_point"
```bash
# Unity side issue:
# 1. Check ROS TCP Connector is connected
# 2. Check ClickToGoal.cs has Cube assigned
# 3. Check ground plane has collider
```

---

## Verify Each Component

### 1. Is goal_navigation_node running?
```bash
ros2 node list | grep goal_navigation
# Expected output: /goal_navigation_node
```

### 2. Is cmd_vel_bridge listening to /cmd_vel?
```bash
ros2 node info /cmd_vel_bridge
# Should show subscriber to /cmd_vel
```

### 3. Are topics active?
```bash
ros2 topic list
# Should include: /cmd_vel, /goal_reached, /unity_clicked_point
```

### 4. Is data flowing?
```bash
# Send test goal point
ros2 topic pub -1 /unity_clicked_point geometry_msgs/Point '{x: 5.0, y: 0.0, z: 0.0}'

# Check if /cmd_vel has messages
ros2 topic echo /cmd_vel
# Should see velocity commands
```

---

## For Real Robot Connection

If testing with actual Go2 robot:

1. **Configure network interface** in `setup_humble_local.sh`:
   ```bash
   # Change "lo" to your network interface (e.g., "eth0")
   # Get it with: ip link show
   ```

2. **Check robot is reachable:**
   ```bash
   ping 192.168.123.123  # Robot IP
   ```

3. **Verify topics from robot:**
   ```bash
   ros2 topic list | grep api
   # Should show /api/sport/request and other API topics
   ```

---

## Still Not Working?

Provide:
1. Output from `ros2 topic list`
2. Output from `ros2 node list`
3. Error messages from terminals
4. Screenshot of unity setup

Then I can help further!

---

## Quick Checklist

- [ ] Running `bash setup_humble_local.sh` before commands
- [ ] All 3 nodes/bridges running in separate terminals
- [ ] Unity scene is playing
- [ ] Ground plane has a Collider component
- [ ] ROS TCP Connector configured in Unity
- [ ] `/unity_clicked_point` publishes when clicking
- [ ] `/cmd_vel` messages appear when goal is sent
- [ ] Robot responds to movement commands
