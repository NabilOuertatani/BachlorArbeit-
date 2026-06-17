#!/bin/bash
set -e

WORKSPACE_DIR="/home/haii/BachlorArbeit-"
cd "$WORKSPACE_DIR"

# Source ROS setup
source /opt/ros/humble/setup.bash
source install/setup.bash

echo "=========================================="
echo "Complete Robot Movement Diagnostic"
echo "=========================================="
echo ""

# Step 1: Verify robot connectivity
echo "[1/6] Checking robot connectivity..."
if ping -c 1 -W 2 192.168.1.7 &>/dev/null; then
    echo "  ✓ Robot reachable at 192.168.1.7"
    ROBOT_REACHABLE=true
else
    echo "  ✗ Robot NOT reachable - will use mock pose"
    ROBOT_REACHABLE=false
fi
echo ""

# Step 2: Kill any existing processes
echo "[2/6] Cleaning up old processes..."
pkill -f "odom_to_pose_bridge|cmd_vel_bridge|tcp_bridge|goal_navigation|robot_api_bridge|robot_odom" || true
sleep 2
echo "  ✓ Old processes killed"
echo ""

# Step 3: Start appropriate pose provider
echo "[3/6] Starting pose provider..."
if [ "$ROBOT_REACHABLE" = true ]; then
    echo "  Starting robot_odom_bridge (reading from real robot)..."
    ros2 run go2_robot_interface robot_odom_bridge > /tmp/odom_test.log 2>&1 &
    ODOM_PID=$!
    sleep 2
    if ps -p $ODOM_PID > /dev/null; then
        echo "  ✓ robot_odom_bridge started"
    else
        echo "  ✗ robot_odom_bridge failed"
        cat /tmp/odom_test.log
        exit 1
    fi
else
    echo "  ⚠ Robot unreachable - will try to start anyway (may fail silently)"
    ros2 run go2_robot_interface robot_odom_bridge > /tmp/odom_test.log 2>&1 &
    ODOM_PID=$!
    sleep 2
fi

# Start odom_to_pose bridge (converts /odom to /utlidar/robot_pose)
echo "  Starting odom_to_pose_bridge..."
python3 odom_to_pose_bridge.py > /tmp/pose_bridge_test.log 2>&1 &
BRIDGE_PID=$!
sleep 2
if ps -p $BRIDGE_PID > /dev/null; then
    echo "  ✓ odom_to_pose_bridge started"
else
    echo "  ✗ odom_to_pose_bridge failed"
    cat /tmp/pose_bridge_test.log
    kill $ODOM_PID 2>/dev/null || true
    exit 1
fi
echo ""

# Step 4: Start TCP Bridge
echo "[4/6] Starting TCP Bridge..."
python3 ros_tcp_bridge_server.py > /tmp/tcp_test2.log 2>&1 &
TCP_PID=$!
sleep 2
if ps -p $TCP_PID > /dev/null; then
    echo "  ✓ TCP Bridge started"
else
    echo "  ✗ TCP Bridge crashed"
    cat /tmp/tcp_test2.log
    kill $ODOM_PID $BRIDGE_PID 2>/dev/null || true
    exit 1
fi
echo ""

# Step 5: Start Goal Navigation
echo "[5/6] Starting Goal Navigation Node..."
ros2 run go2_behavior goal_navigation_node > /tmp/nav_test2.log 2>&1 &
NAV_PID=$!
sleep 3
if ps -p $NAV_PID > /dev/null; then
    echo "  ✓ Goal Navigation started"
else
    echo "  ✗ Goal Navigation crashed"
    cat /tmp/nav_test2.log
    kill $ODOM_PID $BRIDGE_PID $TCP_PID 2>/dev/null || true
    exit 1
fi
echo ""

# Step 6: Start cmd_vel Bridge
echo "[6/6] Starting cmd_vel Bridge..."
ros2 run go2_robot_interface cmd_vel_bridge > /tmp/vel_test2.log 2>&1 &
VEL_PID=$!
sleep 2
if ps -p $VEL_PID > /dev/null; then
    echo "  ✓ cmd_vel Bridge started"
else
    echo "  ✗ cmd_vel Bridge crashed"
    cat /tmp/vel_test2.log
    kill $ODOM_PID $BRIDGE_PID $TCP_PID $NAV_PID 2>/dev/null || true
    exit 1
fi
echo ""

# Start Robot API Bridge
echo "Starting Robot API Bridge..."
python3 robot_api_bridge.py > /tmp/api_test2.log 2>&1 &
API_PID=$!
sleep 2
if ps -p $API_PID > /dev/null; then
    echo "  ✓ Robot API Bridge started"
else
    echo "  ✗ Robot API Bridge crashed"
    cat /tmp/api_test2.log
    kill $ODOM_PID $BRIDGE_PID $TCP_PID $NAV_PID $VEL_PID 2>/dev/null || true
    exit 1
fi
echo ""

echo "=========================================="
echo "✅ All services started successfully!"
echo "=========================================="
echo ""

# Check if pose is being received
echo "Checking pose reception (5 second timeout)..."
timeout 5 ros2 topic echo /utlidar/robot_pose --no-arraylimit 2>/dev/null | head -20 || echo "  (No pose data received yet)"
echo ""

echo "Testing message flow..."
echo ""

# Test: Publish a test goal
echo "Publishing test movement goal: x=1.0, y=0.5"
timeout 5 ros2 topic pub -1 /unity_clicked_point geometry_msgs/Point "x: 1.0
y: 0.5
z: 0.0" > /dev/null 2>&1 &
sleep 1

# Monitor cmd_vel for 10 seconds
echo ""
echo "Monitoring /cmd_vel for 10 seconds..."
echo "(Should see velocity commands if pose is available)"
echo ""
timeout 10 ros2 topic echo /cmd_vel --no-arraylimit 2>/dev/null || echo "  (No cmd_vel data)"
echo ""

# Cleanup
echo ""
echo "Shutting down test services..."
kill $ODOM_PID $BRIDGE_PID $TCP_PID $NAV_PID $VEL_PID $API_PID 2>/dev/null || true
sleep 1

echo ""
echo "=========================================="
echo "Test complete!"
echo "=========================================="
echo ""
echo "Debug info:"
if [ "$ROBOT_REACHABLE" = true ]; then
    echo "  Robot Status: ✓ Connected"
    echo "  (Check /odom topic for robot pose data)"
else
    echo "  Robot Status: ✗ Not Reachable"
    echo "  (robot_odom_bridge cannot receive data without real robot)"
fi
echo ""
echo "Check logs if issues:"
echo "  Odom Bridge:  tail -20 /tmp/odom_test.log"
echo "  Pose Bridge:  tail -20 /tmp/pose_bridge_test.log"
echo "  Nav Node:     tail -50 /tmp/nav_test2.log"
echo "  cmd_vel Bridge: tail -20 /tmp/vel_test2.log"
