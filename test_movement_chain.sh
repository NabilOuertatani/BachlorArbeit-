#!/bin/bash
set -e

WORKSPACE_DIR="/home/haii/BachlorArbeit-"
cd "$WORKSPACE_DIR"

# Source ROS setup
source /opt/ros/humble/setup.bash
source install/setup.bash

echo "=========================================="
echo "Robot Movement Diagnostic Test"
echo "=========================================="
echo ""

# Step 1: Verify robot connectivity
echo "[1/5] Checking robot connectivity..."
if ping -c 1 -W 2 192.168.1.7 &>/dev/null; then
    echo "  ✓ Robot reachable at 192.168.1.7"
else
    echo "  ✗ Robot NOT reachable at 192.168.1.7"
    echo "    Have you powered on the Go2 robot?"
    exit 1
fi
echo ""

# Step 2: Kill any existing processes
echo "[2/5] Cleaning up old processes..."
pkill -f "cmd_vel_bridge|tcp_bridge|goal_navigation|robot_api_bridge" || true
sleep 2
echo "  ✓ Old processes killed"
echo ""

# Step 3: Start TCP Bridge
echo "[3/5] Starting TCP Bridge..."
python3 ros_tcp_bridge_server.py > /tmp/tcp_test.log 2>&1 &
TCP_PID=$!
sleep 2
if ps -p $TCP_PID > /dev/null; then
    echo "  ✓ TCP Bridge started (PID: $TCP_PID)"
else
    echo "  ✗ TCP Bridge crashed"
    cat /tmp/tcp_test.log
    exit 1
fi
echo ""

# Step 4: Start Goal Navigation
echo "[4/5] Starting Goal Navigation Node..."
ros2 run go2_behavior goal_navigation_node > /tmp/nav_test.log 2>&1 &
NAV_PID=$!
sleep 3
if ps -p $NAV_PID > /dev/null; then
    echo "  ✓ Goal Navigation started (PID: $NAV_PID)"
else
    echo "  ✗ Goal Navigation crashed"
    cat /tmp/nav_test.log
    kill $TCP_PID 2>/dev/null || true
    exit 1
fi
echo ""

# Step 5: Start cmd_vel Bridge
echo "[5/5] Starting cmd_vel Bridge..."
ros2 run go2_robot_interface cmd_vel_bridge > /tmp/vel_test.log 2>&1 &
VEL_PID=$!
sleep 3
if ps -p $VEL_PID > /dev/null; then
    echo "  ✓ cmd_vel Bridge started (PID: $VEL_PID)"
else
    echo "  ✗ cmd_vel Bridge crashed"
    cat /tmp/vel_test.log
    kill $TCP_PID $NAV_PID 2>/dev/null || true
    exit 1
fi
echo ""

# Step 6: Start Robot API Bridge
echo "[6/6] Starting Robot API Bridge..."
python3 robot_api_bridge.py > /tmp/api_test.log 2>&1 &
API_PID=$!
sleep 2
if ps -p $API_PID > /dev/null; then
    echo "  ✓ Robot API Bridge started (PID: $API_PID)"
else
    echo "  ✗ Robot API Bridge crashed"
    cat /tmp/api_test.log
    kill $TCP_PID $NAV_PID $VEL_PID 2>/dev/null || true
    exit 1
fi
echo ""

echo "=========================================="
echo "✅ All services started successfully!"
echo "=========================================="
echo ""
echo "Testing message flow..."
echo ""

# Test 1: Check if topics exist
echo "Active ROS topics:"
ros2 topic list | grep -E "cmd_vel|unity_clicked|api_sport|goal_reached" || echo "  (waiting for first message)"
echo ""

# Test 2: Publish a test goal (using ns offset from origin to avoid collisions)
echo "Publishing test movement goal: x=1.0, y=0.5"
timeout 5 ros2 topic pub -1 /unity_clicked_point geometry_msgs/Point "x: 1.0
y: 0.5
z: 0.0" > /dev/null 2>&1 &
sleep 1

# Monitor cmd_vel for 10 seconds
echo ""
echo "Monitoring /cmd_vel for 10 seconds..."
echo "(Should see velocity commands if everything is working)"
echo ""
timeout 10 ros2 topic echo /cmd_vel | head -20 || true
echo ""

# Cleanup
echo ""
echo "Shutting down test services..."
kill $TCP_PID $NAV_PID $VEL_PID $API_PID 2>/dev/null || true
sleep 1

echo ""
echo "=========================================="
echo "Test complete!"
echo "=========================================="
echo ""
echo "Summary:"
echo "  TCP Bridge:    OK"
echo "  Goal Navigation: OK" 
echo "  cmd_vel Bridge: OK"
echo "  Robot API Bridge: OK"
echo ""
echo "If you saw velocity commands above, the system is working!"
echo "Otherwise, check the logs:"
echo "  cat /tmp/tcp_test.log"
echo "  cat /tmp/nav_test.log"
echo "  cat /tmp/vel_test.log"
echo "  cat /tmp/api_test.log"
