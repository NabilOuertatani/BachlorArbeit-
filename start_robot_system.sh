#!/bin/bash
# Fixed system startup with pose provider fallback

WORKSPACE_DIR="/home/haii/BachlorArbeit-"
cd "$WORKSPACE_DIR"

# Source ROS setup
source /opt/ros/humble/setup.bash
source install/setup.bash

echo "=========================================="
echo "Starting Go2 Robot Navigation System"
echo "=========================================="
echo ""

# Kill any existing processes
pkill -f "odom_to_pose_bridge|cmd_vel_bridge|tcp_bridge|goal_navigation|robot_api_bridge|robot_odom|mock_pose" || true
sleep 1

# Function to create mock pose publisher
start_mock_pose() {
    python3 << 'MOCK_POSE_EOF' > /tmp/mock_pose.log 2>&1 &
import rclpy
from geometry_msgs.msg import PoseStamped, Quaternion
import math
import time

rclpy.init()
node = rclpy.create_node('mock_pose_publisher')
pub = node.create_publisher(PoseStamped, '/utlidar/robot_pose', 10)

pose = PoseStamped()
pose.header.frame_id = 'odom'
pose.pose.position.x = 0.0
pose.pose.position.y = 0.0
pose.pose.position.z = 0.0
pose.pose.orientation = Quaternion(x=0.0, y=0.0, z=0.0, w=1.0)

try:
    while rclpy.ok():
        pose.header.stamp = node.get_clock().now().to_msg()
        pub.publish(pose)
        time.sleep(0.05)
except KeyboardInterrupt:
    pass
finally:
    node.destroy_node()
    rclpy.shutdown()
MOCK_POSE_EOF
}

# Check if robot is providing odometry
echo "[1/6] Checking robot telemetry..."
timeout 2 ros2 topic list 2>/dev/null | grep -q "lf/sportmodestate"
if [ $? -eq 0 ]; then
    echo "  ✓ Robot publishing /lf/sportmodestate — starting odom bridge"
    ros2 run go2_robot_interface robot_odom_bridge > /tmp/odom_bridge.log 2>&1 &
    ODOM_PID=$!
    sleep 2
    
    # Check if odom data is flowing
    timeout 2 ros2 topic echo /odom --no-arraylimit 2>/dev/null | head -1 > /dev/null
    if [ $? -eq 0 ]; then
        echo "  ✓ /odom data flowing — starting pose bridge"
        python3 odom_to_pose_bridge.py > /tmp/pose_bridge.log 2>&1 &
        BRIDGE_PID=$!
        sleep 1
    else
        echo "  ⚠ Odom bridge started but no data — using mock pose"
        kill $ODOM_PID 2>/dev/null || true
        start_mock_pose
        MOCK_PID=$!
    fi
else
    echo "  ⚠ No robot telemetry detected — using mock pose publisher"
    start_mock_pose
    MOCK_PID=$!
fi
sleep 1
echo ""

# Start TCP Bridge
echo "[2/6] Starting TCP Bridge (Unity ↔ ROS2)..."
python3 ros_tcp_bridge_server.py > /tmp/tcp_bridge.log 2>&1 &
TCP_PID=$!
sleep 2
echo "  ✓ TCP Bridge started"
echo ""

# Start Goal Navigation
echo "[3/6] Starting Goal Navigation Node..."
ros2 run go2_behavior goal_navigation_node > /tmp/goal_nav.log 2>&1 &
NAV_PID=$!
sleep 2
echo "  ✓ Goal Navigation started"
echo ""

# Start cmd_vel Bridge
echo "[4/6] Starting cmd_vel Bridge..."
ros2 run go2_robot_interface cmd_vel_bridge > /tmp/cmd_vel.log 2>&1 &
VEL_PID=$!
sleep 1
echo "  ✓ cmd_vel Bridge started"
echo ""

# Start Robot API Bridge
echo "[5/6] Starting Robot API Bridge..."
python3 robot_api_bridge.py > /tmp/robot_api.log 2>&1 &
API_PID=$!
sleep 1
echo "  ✓ Robot API Bridge started"
echo ""

echo "=========================================="
echo "✅ System Ready!"
echo "=========================================="
echo ""
echo "Status:"
echo "  Robot API Bridge: 192.168.1.7:29999"
echo "  TCP Bridge:       localhost:10000"
echo "  Goal Navigation:  20 Hz control loop"
echo "  Pose Provider:    $([ -n "$MOCK_PID" ] && echo 'Mock (testing mode)' || echo 'Real robot odometry')"
echo ""
echo "Next steps:"
echo "  1. Start Unity client and connect to localhost:10000"
echo "  2. Power on Go2 robot if not already on"
echo "  3. Click on ground plane in Unity to send navigation goals"
echo "  4. Dog should move when goal is received"
echo ""
echo "Monitor topics in separate terminals:"
echo "  ros2 topic echo /cmd_vel              # See velocity commands"
echo "  ros2 topic echo /utlidar/robot_pose   # See robot position"
echo "  ros2 topic echo /goal_reached         # See when goals reached"
echo ""
echo "Logs:"
echo "  tail -f /tmp/goal_nav.log             # Navigation debug"
echo "  tail -f /tmp/robot_api.log            # Robot commands"
echo "  tail -f /tmp/tcp_bridge.log           # Unity bridge"
echo ""
echo "To stop: press Ctrl+C"
echo ""

# Keep running
wait
