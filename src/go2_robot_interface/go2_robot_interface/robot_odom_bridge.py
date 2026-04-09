#!/usr/bin/env python3

import math

import rclpy
from rclpy.node import Node
from nav_msgs.msg import Odometry
from geometry_msgs.msg import Quaternion
from unitree_go.msg import SportModeState


def quaternion_from_yaw(yaw: float) -> Quaternion:
    q = Quaternion()
    q.x = 0.0
    q.y = 0.0
    q.z = math.sin(yaw * 0.5)
    q.w = math.cos(yaw * 0.5)
    return q


class RobotOdomBridge(Node):
    def __init__(self):
        super().__init__('robot_odom_bridge')

        self.declare_parameter('state_topic', 'lf/sportmodestate')
        self.state_topic = self.get_parameter('state_topic').get_parameter_value().string_value

        self.odom_pub = self.create_publisher(Odometry, '/odom', 10)

        self.state_sub = self.create_subscription(
            SportModeState,
            self.state_topic,
            self.state_callback,
            10
        )

        self.last_msg_time = self.get_clock().now()
        self.timer = self.create_timer(2.0, self.watchdog_callback)

        self.get_logger().info(f'RobotOdomBridge started. Subscribing to: {self.state_topic}')
        self.get_logger().info('Publishing odometry on /odom')

    def state_callback(self, msg: SportModeState):
        odom = Odometry()

        now = self.get_clock().now().to_msg()
        odom.header.stamp = now
        odom.header.frame_id = 'odom'
        odom.child_frame_id = 'base_link'

        # Position from SportModeState
        odom.pose.pose.position.x = float(msg.position[0])
        odom.pose.pose.position.y = float(msg.position[1])
        odom.pose.pose.position.z = float(msg.position[2])

        # Orientation from IMU yaw
        yaw = float(msg.imu_state.rpy[2])
        odom.pose.pose.orientation = quaternion_from_yaw(yaw)

        # Velocity in odom frame
        # SportModeState in the examples indicates velocity is available
        try:
            odom.twist.twist.linear.x = float(msg.velocity[0])
            odom.twist.twist.linear.y = float(msg.velocity[1])
            odom.twist.twist.linear.z = float(msg.velocity[2])
        except Exception:
            pass

        # Angular velocity from IMU gyroscope if available
        try:
            odom.twist.twist.angular.x = float(msg.imu_state.gyroscope[0])
            odom.twist.twist.angular.y = float(msg.imu_state.gyroscope[1])
            odom.twist.twist.angular.z = float(msg.imu_state.gyroscope[2])
        except Exception:
            pass

        self.odom_pub.publish(odom)
        self.last_msg_time = self.get_clock().now()

    def watchdog_callback(self):
        dt = (self.get_clock().now() - self.last_msg_time).nanoseconds / 1e9
        if dt > 3.0:
            self.get_logger().warn(
                f'No SportModeState messages received for {dt:.1f}s on topic "{self.state_topic}"'
            )


def main(args=None):
    rclpy.init(args=args)
    node = RobotOdomBridge()
    rclpy.spin(node)
    node.destroy_node()
    rclpy.shutdown()


if __name__ == '__main__':
    main()