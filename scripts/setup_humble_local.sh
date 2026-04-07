#!/bin/bash
# Setup for local development on this laptop
# Use this instead of setup.sh if you don't have the unitree_ros2 cyclonedds workspace

echo "Setting up Unitree Go2 ROS2 environment (Local Development)"

# Source ROS2 Humble
source /opt/ros/humble/setup.bash

# Source local workspace
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
source "${SCRIPT_DIR}/install/setup.bash"

# Configure DDS middleware for local loopback (simulation mode)
export RMW_IMPLEMENTATION=rmw_cyclonedds_cpp
export CYCLONEDDS_URI='<CycloneDDS><Domain><General><Interfaces>
                            <NetworkInterface name="lo" priority="default" multicast="default" />
                        </Interfaces></General></Domain></CycloneDDS>'

echo "Environment setup complete!"
echo "RMW_IMPLEMENTATION: $RMW_IMPLEMENTATION"
echo "Workspace: ${SCRIPT_DIR}/install"
