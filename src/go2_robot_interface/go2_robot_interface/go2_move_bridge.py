import rclpy
from rclpy.node import Node


class HelloGo2Node(Node):
    def __init__(self):
        super().__init__('hello_go2_node')
        self.get_logger().info('HelloGo2Node has started.')
        self.timer = self.create_timer(2.0, self.timer_callback)

    def timer_callback(self):
        self.get_logger().info('Hello from go2_robot_interface package!')


def main(args=None):
    rclpy.init(args=args)
    node = HelloGo2Node()
    rclpy.spin(node)
    node.destroy_node()
    rclpy.shutdown()