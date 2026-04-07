from launch import LaunchDescription
from launch_ros.actions import Node


def generate_launch_description():
    """Launch the goal navigation system from Unity interface."""
    
    goal_navigation_node = Node(
        package='go2_behavior',
        executable='goal_navigation_node',
        name='goal_navigation_node',
        output='screen',
        parameters=[
            {'max_speed': 0.5},
            {'max_rotation_speed': 1.0},
            {'goal_tolerance': 0.1},
            {'use_zig_zag': True},
            {'zig_zag_width': 0.3},
        ],
    )

    return LaunchDescription([
        goal_navigation_node,
    ])
