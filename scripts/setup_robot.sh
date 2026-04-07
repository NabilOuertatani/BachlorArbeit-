#!/bin/bash
# Setup for real Unitree Go2 robot connection via Ethernet
# IMPORTANT: Use "source setup_robot.sh" NOT "bash setup_robot.sh"

source /opt/ros/humble/setup.bash

# Source local workspace
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
source "${SCRIPT_DIR}/install/setup.bash"

# Configure DDS middleware for real robot on ethernet
export RMW_IMPLEMENTATION=rmw_cyclonedds_cpp
export CYCLONEDDS_URI='<CycloneDDS><Domain><General><Interfaces>
                            <NetworkInterface name="eno1" priority="default" multicast="default" />
                        </Interfaces></General></Domain></CycloneDDS>'

echo "✓ Environment setup complete!"
echo "✓ RMW_IMPLEMENTATION: $RMW_IMPLEMENTATION"
echo "✓ Network Interface: eno1 (192.168.123.99)"
echo "✓ Robot IP: 192.168.123.161"
echo ""
echo "Ready to run ROS2 commands!"
