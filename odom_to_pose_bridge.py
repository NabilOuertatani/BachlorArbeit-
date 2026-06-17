#!/usr/bin/env python3
"""
odom_to_pose_bridge.py — Converts /odom to /utlidar/robot_pose

The robot_odom_bridge publishes odometry on /odom.
The goal_navigation_node needs pose on /utlidar/robot_pose.
This bridge converts between them.
"""

import math
import rclpy
from rclpy.node import Node
from nav_msgs.msg import Odometry
from geometry_msgs.msg import PoseStamped


class OdomToPoseBridge(Node):
    def __init__(self):
        super().__init__('odom_to_pose_bridge')
        
        self.pose_pub = self.create_publisher(PoseStamped, '/utlidar/robot_pose', 10)
        
        self.odom_sub = self.create_subscription(
            Odometry,
            '/odom',
            self._on_odom,
            10
        )
        
        self.get_logger().info('OdomToPoseBridge: /odom → /utlidar/robot_pose')
    
    def _on_odom(self, msg: Odometry):
        """Convert Odometry to PoseStamped and republish as /utlidar/robot_pose."""
        pose = PoseStamped()
        pose.header = msg.header
        pose.header.frame_id = 'odom'
        pose.pose = msg.pose.pose
        
        self.pose_pub.publish(pose)


def main(args=None):
    rclpy.init(args=args)
    node = OdomToPoseBridge()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
