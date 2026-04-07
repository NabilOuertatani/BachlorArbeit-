#!/bin/bash
# Complete system startup script for Go2 Robot with Unity Interface
# Starts all required components in sequence

set -e  # Exit on error

WORKSPACE_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$WORKSPACE_DIR"

echo ""
echo "=========================================="
echo "Go2 Robot + Unity Navigation System"
echo "=========================================="
echo ""

# Check if robot is reachable
echo "[1/5] Checking robot connection..."
if ping -c 1 192.168.123.161 > /dev/null 2>&1; then
    echo "✓ Robot is reachable at 192.168.123.161"
else
    echo "✗ Robot NOT reachable at 192.168.123.161"
    echo "  Make sure robot is powered on and Ethernet cable is connected"
    exit 1
fi
echo ""

# Source environment
echo "[2/5] Setting up ROS2 environment..."
bash setup_robot.sh > /dev/null
echo "✓ ROS2 environment ready"
echo ""

# Check workspace is built
echo "[3/5] Verifying workspace build..."
if [ ! -d install/go2_behavior ]; then
    echo "✗ Workspace not built. Run: colcon build --packages-ignore com"
    exit 1
fi
echo "✓ Workspace is built"
echo ""

echo "[4/5] Ready to start services..."
echo ""
echo "========== STARTUP INSTRUCTIONS =========="
echo ""
echo "Open 4 NEW terminals and run these commands:"
echo ""
echo "--- Terminal 1: TCP Bridge (connects Unity) ---"
echo "cd $WORKSPACE_DIR"
echo "bash setup_robot.sh"
echo "python3 ros_tcp_bridge_server.py"
echo ""
echo "--- Terminal 2: Goal Navigation ---"
echo "cd $WORKSPACE_DIR"
echo "bash setup_robot.sh"
echo "ros2 run go2_behavior goal_navigation_node"
echo ""
echo "--- Terminal 3: Command Bridge ---"
echo "cd $WORKSPACE_DIR"
echo "bash setup_robot.sh"
echo "ros2 run go2_robot_interface cmd_vel_bridge"
echo ""
echo "--- Terminal 4: Monitor (optional) ---"
echo "cd $WORKSPACE_DIR"
echo "bash setup_robot.sh"
echo "ros2 topic echo /cmd_vel"
echo ""
echo "========== UNITY SETUP =========="
echo ""
echo "In Unity Editor:"
echo "1. Open UnityInterface.unity scene"
echo "2. Hit Play"
echo "3. Click on ground plane to send goal"
echo "4. Real robot should move!"
echo ""
echo "=========================================="
echo ""
