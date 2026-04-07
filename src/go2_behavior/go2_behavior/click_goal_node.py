#!/usr/bin/env python3

import math

import rclpy
from rclpy.node import Node
from geometry_msgs.msg import Point, Twist


class ClickGoalNode(Node):

    def __init__(self):
        super().__init__('click_goal_node')

        self.goal = None

        self.sub = self.create_subscription(
            Point,
            '/unity_clicked_point',
            self.goal_callback,
            10
        )

        self.cmd_pub = self.create_publisher(
            Twist,
            '/cmd_vel',
            10
        )

        self.timer = self.create_timer(0.1, self.control_loop)

        self.current_x = 0.0
        self.current_y = 0.0

        self.max_linear_speed = 0.1
        self.max_angular_speed = 0.3
        self.goal_tolerance = 0.15

        self.get_logger().info('Click Goal Node started')

    def goal_callback(self, msg: Point):
        goal_x = msg.z
        goal_y = -msg.x

        self.goal = (goal_x, goal_y)
        self.get_logger().info(f'New goal received: {self.goal}')

    def control_loop(self):
        if self.goal is None:
            return

        goal_x, goal_y = self.goal

        dx = goal_x - self.current_x
        dy = goal_y - self.current_y

        distance = math.sqrt(dx * dx + dy * dy)

        cmd = Twist()

        if distance < self.goal_tolerance:
            self.get_logger().info('Goal reached')
            self.goal = None
            self.cmd_pub.publish(cmd)
            return

        angle = math.atan2(dy, dx)

        linear_speed = min(self.max_linear_speed, 0.1 * distance)
        angular_speed = max(-self.max_angular_speed, min(self.max_angular_speed, 0.3 * angle))

        cmd.linear.x = linear_speed
        cmd.angular.z = angular_speed

        self.cmd_pub.publish(cmd)


def main(args=None):
    rclpy.init(args=args)
    node = ClickGoalNode()
    rclpy.spin(node)
    node.destroy_node()
    rclpy.shutdown()


if __name__ == '__main__':
    main()