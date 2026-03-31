import rclpy
from rclpy.node import Node
from geometry_msgs.msg import Twist


class BehaviorNode(Node):
    def __init__(self):
        super().__init__('behavior_node')

        self.cmd_pub = self.create_publisher(Twist, '/cmd_vel', 10)

        self.timer = self.create_timer(0.1, self.timer_callback)  # 10 Hz
        self.start_time = self.get_clock().now()

        self.get_logger().info('behavior_node started')
        self.get_logger().info('Running behavior: small_steps_forward')

    def publish_cmd(self, vx=0.0, vy=0.0, wz=0.0):
        msg = Twist()
        msg.linear.x = vx
        msg.linear.y = vy
        msg.angular.z = wz
        self.cmd_pub.publish(msg)

    def timer_callback(self):
        now = self.get_clock().now()
        t = (now - self.start_time).nanoseconds / 1e9

        # Phase 1: pause
        if t < 1.0:
            self.publish_cmd(0.0, 0.0, 0.0)

        # Phase 2: walk 1 meter forward (0.3 m/s for ~3.33 seconds = 1 meter)
        elif t < 2.0:
            self.publish_cmd(0.3, 0.0, 0.0)

        elif t < 4.0:
            self.publish_cmd(-0.3, 0.0, 0.0)
        # Phase 3: stop
        else:
            self.publish_cmd(0.0, 0.0, 0.0)
            self.get_logger().info('Behavior finished - walked 1 meter')
            rclpy.shutdown()


def main(args=None):
    rclpy.init(args=args)
    node = BehaviorNode()
    rclpy.spin(node)
    node.destroy_node()
    rclpy.shutdown()