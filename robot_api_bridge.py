#!/usr/bin/env python3
import json, time
import rclpy
from rclpy.node import Node
from unitree_api.msg import Request

class RobotApiBridge(Node):
    def __init__(self):
        super().__init__('robot_api_bridge')
        self.req_pub = self.create_publisher(Request, '/api/sport/request', 10)
        self.create_subscription(Request, '/api/gesture/request', self._on_gesture_request, 10)
        self.get_logger().info('Robot API Bridge — publishing to /api/sport/request')

    def _on_gesture_request(self, msg: Request):
        api_id = msg.header.identity.api_id
        self.get_logger().info(f'[GESTURE] Received API {api_id}')

        wake = Request()
        wake.header.identity.api_id = 1004
        wake.parameter = '{}'
        self.req_pub.publish(wake)
        self.get_logger().info('[GESTURE] Published RecoveryStand (1004)')
        time.sleep(2.0)

        standup = Request()
        standup.header.identity.api_id = 1002
        standup.parameter = '{}'
        self.req_pub.publish(standup)
        self.get_logger().info('[GESTURE] Published StandUp (1002)')
        time.sleep(1.5)

        gesture = Request()
        gesture.header.identity.api_id = api_id
        gesture.parameter = msg.parameter if msg.parameter else '{}'
        self.req_pub.publish(gesture)
        self.get_logger().info(f'[GESTURE] Published {api_id} to /api/sport/request')

def main(args=None):
    rclpy.init(args=args)
    node = RobotApiBridge()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()

if __name__ == '__main__':
    main()
