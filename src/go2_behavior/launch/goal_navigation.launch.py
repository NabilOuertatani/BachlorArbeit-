"""
Launch file — starts the full Unity + ROS2 + Go2 pipeline.

Usage:
  ros2 launch your_package go2_unity.launch.py

Optional overrides:
  ros2 launch your_package go2_unity.launch.py unity_ip:=192.168.1.100
  ros2 launch your_package go2_unity.launch.py max_speed:=0.25
"""

from launch import LaunchDescription
from launch.actions import DeclareLaunchArgument
from launch.substitutions import LaunchConfiguration
from launch_ros.actions import Node


def generate_launch_description():

    # ── Arguments (can be overridden on CLI) ──────────────────────
    args = [
        DeclareLaunchArgument('unity_ip',       default_value='127.0.0.1',
                              description='IP of the Unity machine'),
        DeclareLaunchArgument('max_speed',       default_value='0.35'),
        DeclareLaunchArgument('turn_speed',      default_value='0.55'),
        DeclareLaunchArgument('goal_tolerance',  default_value='0.15'),
        DeclareLaunchArgument('max_range',       default_value='5.0',
                              description='LiDAR range to forward (metres)'),
    ]

    unity_ip      = LaunchConfiguration('unity_ip')
    max_speed     = LaunchConfiguration('max_speed')
    turn_speed    = LaunchConfiguration('turn_speed')
    goal_tol      = LaunchConfiguration('goal_tolerance')
    max_range     = LaunchConfiguration('max_range')

    # ── Nodes ──────────────────────────────────────────────────────
    nodes = [

        # 1. TCP bridge — receives Unity click goals
        Node(
            package='your_package',
            executable='tcp_bridge_server',
            name='tcp_bridge_server',
            output='screen',
        ),

        # 2. Navigation — converts goals to /cmd_vel
        Node(
            package='your_package',
            executable='goal_navigation_node',
            name='goal_navigation_node',
            output='screen',
            parameters=[{
                'max_speed':      max_speed,
                'turn_speed':     turn_speed,
                'goal_tolerance': goal_tol,
            }],
        ),

        # 3. cmd_vel bridge — converts /cmd_vel to Unitree sport API
        Node(
            package='your_package',
            executable='cmd_vel_bridge',
            name='cmd_vel_bridge',
            output='screen',
        ),

        # 4. Pose forwarder — sends estimated pose to Unity
        Node(
            package='your_package',
            executable='pose_forwarder',
            name='pose_forwarder',
            output='screen',
            parameters=[{
                'unity_ip':   unity_ip,
                'unity_port': 10001,
            }],
        ),

        # 5. Cloud forwarder — sends LiDAR obstacles to Unity
        Node(
            package='your_package',
            executable='cloud_forwarder',
            name='cloud_forwarder',
            output='screen',
            parameters=[{
                'unity_ip':   unity_ip,
                'unity_port': 10002,
                'max_range':  max_range,
            }],
        ),

    ]

    return LaunchDescription(args + nodes)
