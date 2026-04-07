#!/bin/bash
# Quick test script - runs all components needed for goal navigation

cd /home/haii/BachlorArbeit-

# Setup environment
bash setup_humble_local.sh > /dev/null

echo "=========================================="
echo "Starting Components for Goal Navigation"
echo "=========================================="

# Function to run command in a separate terminal
run_in_new_terminal() {
    local name=$1
    local cmd=$2
    echo "Starting: $name"
    gnome-terminal --title="$name" -- bash -c "cd /home/haii/BachlorArbeit- && bash setup_humble_local.sh && $cmd; exec bash" &
}

# Start the components
run_in_new_terminal "Goal Navigation Node" "ros2 run go2_behavior goal_navigation_node"
sleep 1
run_in_new_terminal "Command Velocity Bridge" "ros2 run go2_robot_interface cmd_vel_bridge"
sleep 1
run_in_new_terminal "Topic Monitor" "ros2 topic echo /cmd_vel"

echo "=========================================="
echo "All nodes started!"
echo "Open Unity and click on ground plane to test"
echo "=========================================="
