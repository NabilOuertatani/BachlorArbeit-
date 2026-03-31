import json

import rclpy
from rclpy.node import Node
from geometry_msgs.msg import Twist
from unitree_api.msg import Request


ROBOT_SPORT_API_ID_STOPMOVE = 1003
ROBOT_SPORT_API_ID_MOVE = 1008


class CmdVelBridge(Node):
    def __init__(self):
        super().__init__('cmd_vel_bridge')

        self.req_pub = self.create_publisher(Request, '/api/sport/request', 10)

        self.subscription = self.create_subscription(
            Twist,
            '/cmd_vel',
            self.cmd_vel_callback,
            10
        )

        self.last_was_zero = True
        self.last_msg_time = self.get_clock().now()

        self.timer = self.create_timer(0.1, self.watchdog_callback)

        self.get_logger().info('cmd_vel_bridge started.')
        self.get_logger().info('Listening on /cmd_vel')
        self.get_logger().info('Publishing Unitree requests to /api/sport/request')

    def publish_move(self, vx: float, vy: float, yaw: float):
        msg = Request()
        msg.header.identity.api_id = ROBOT_SPORT_API_ID_MOVE
        msg.parameter = json.dumps({
            "x": float(vx),
            "y": float(vy),
            "z": float(yaw)
        })
        self.req_pub.publish(msg)

    def publish_stop(self):
        msg = Request()
        msg.header.identity.api_id = ROBOT_SPORT_API_ID_STOPMOVE
        self.req_pub.publish(msg)

    def watchdog_callback(self):
        now = self.get_clock().now()
        dt = (now - self.last_msg_time).nanoseconds / 1e9

        if dt > 0.5:
            if not self.last_was_zero:
                self.publish_stop()
                self.get_logger().warn('Watchdog: No cmd_vel -> StopMove sent')
                self.last_was_zero = True

    def cmd_vel_callback(self, msg: Twist):
        self.last_msg_time = self.get_clock().now()

        vx = msg.linear.x
        vy = msg.linear.y
        yaw = msg.angular.z

        is_zero = (
            abs(vx) < 1e-4 and
            abs(vy) < 1e-4 and
            abs(yaw) < 1e-4
        )

        if is_zero:
            if not self.last_was_zero:
                self.publish_stop()
                self.get_logger().info('Sent StopMove')
            self.last_was_zero = True
            return

        self.publish_move(vx, vy, yaw)
        self.last_was_zero = False

        self.get_logger().info(
            f'Sent Move -> vx: {vx:.3f}, vy: {vy:.3f}, yaw: {yaw:.3f}'
        )


def main(args=None):
    rclpy.init(args=args)
    node = CmdVelBridge()
    rclpy.spin(node)
    node.destroy_node()
    rclpy.shutdown()