from launch import LaunchDescription
from launch_ros.actions import Node


def generate_launch_description():
    return LaunchDescription([
        Node(
            package='go2_robot_interface',
            executable='cmd_vel_bridge',
            name='cmd_vel_bridge',
            output='screen'
        ),
        Node(
            package='go2_behavior',
            executable='behavior_node',
            name='behavior_node',
            output='screen'
        ),
    ])