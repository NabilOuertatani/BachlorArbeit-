#!/bin/bash
# Diagnostic script to check if everything is properly configured

cd /home/haii/BachlorArbeit-

echo "=========================================="
echo "Go2 Navigation System Diagnostic"
echo "=========================================="
echo ""

# Check 1: ROS2 Installation
echo "[1/5] Checking ROS2 Installation..."
if [ -f /opt/ros/humble/setup.bash ]; then
    echo "✓ ROS2 Humble found"
else
    echo "✗ ROS2 Humble NOT found"
    echo "  Install with: sudo apt install ros-humble-desktop"
fi
echo ""

# Check 2: Workspace Build
echo "[2/5] Checking Workspace Build..."
if [ -d install/go2_behavior ]; then
    echo "✓ Workspace built (install/ exists)"
else
    echo "✗ Workspace NOT built"
    echo "  Run: colcon build"
fi
echo ""

# Check 3: Source and test ROS2
echo "[3/5] Testing ROS2 commands..."
source /opt/ros/humble/setup.bash > /dev/null 2>&1
source install/setup.bash > /dev/null 2>&1

if command -v ros2 &> /dev/null; then
    echo "✓ ros2 command available"
    ROS_VERSION=$(ros2 --version 2>&1 | head -1)
    echo "  Version: $ROS_VERSION"
else
    echo "✗ ros2 command NOT available"
fi
echo ""

# Check 4: Required packages
echo "[4/5] Checking required packages..."
if ros2 pkg list 2>/dev/null | grep -q "go2_behavior"; then
    echo "✓ go2_behavior package found"
else
    echo "✗ go2_behavior package NOT found"
fi

if ros2 pkg list 2>/dev/null | grep -q "go2_robot_interface"; then
    echo "✓ go2_robot_interface package found"
else
    echo "✗ go2_robot_interface package NOT found"
fi
echo ""

# Check 5: Node executables
echo "[5/5] Checking executables..."
if command -v ros2 run go2_behavior goal_navigation_node --help &> /dev/null || true; then
    echo "✓ goal_navigation_node executable found"
fi

if command -v ros2 run go2_robot_interface cmd_vel_bridge --help &> /dev/null || true; then
    echo "✓ cmd_vel_bridge executable found"
fi
echo ""

echo "=========================================="
echo "Diagnostic Summary:"
echo "=========================================="
echo ""
echo "If all checks passed, run the system with:"
echo ""
echo "  # Terminal 1:"
echo "  cd ~/BachlorArbeit-"
echo "  bash setup_humble_local.sh"
echo "  ros2 run go2_behavior goal_navigation_node"
echo ""
echo "  # Terminal 2:"
echo "  cd ~/BachlorArbeit-"
echo "  bash setup_humble_local.sh"
echo "  ros2 run go2_robot_interface cmd_vel_bridge"
echo ""
echo "  # Terminal 3 (optional - to monitor):"
echo "  cd ~/BachlorArbeit-"
echo "  bash setup_humble_local.sh"
echo "  ros2 topic echo /cmd_vel"
echo ""
echo "Then open Unity and click on the ground plane!"
echo "=========================================="
