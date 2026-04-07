import math
import rclpy
from rclpy.node import Node
from geometry_msgs.msg import Twist, Point
from std_msgs.msg import Bool


class GoalNavigationNode(Node):
    """
    Navigates the Go2 robot to clicked goal points from Unity.
    Subscribes to /unity_clicked_point and publishes velocity commands to /cmd_vel.
    """

    def __init__(self):
        super().__init__('goal_navigation_node')

        # Declare parameters
        self.declare_parameter('max_speed', 0.5)
        self.declare_parameter('max_rotation_speed', 1.0)
        self.declare_parameter('goal_tolerance', 0.1)
        self.declare_parameter('use_zig_zag', True)
        self.declare_parameter('zig_zag_width', 0.3)

        # Get parameters
        self.max_speed = self.get_parameter('max_speed').value
        self.max_rotation_speed = self.get_parameter('max_rotation_speed').value
        self.goal_tolerance = self.get_parameter('goal_tolerance').value
        self.use_zig_zag = self.get_parameter('use_zig_zag').value
        self.zig_zag_width = self.get_parameter('zig_zag_width').value

        # Publishers and Subscribers
        self.cmd_pub = self.create_publisher(Twist, '/cmd_vel', 10)
        self.goal_reached_pub = self.create_publisher(Bool, '/goal_reached', 10)

        self.goal_sub = self.create_subscription(
            Point,
            '/unity_clicked_point',
            self.goal_callback,
            10
        )

        # State variables
        self.current_goal = None
        self.robot_position = [0.0, 0.0]  # x, y coordinates
        self.robot_yaw = 0.0  # rotation in radians
        self.is_moving = False

        # Timer for control loop
        self.timer = self.create_timer(0.05, self.control_loop)  # 20 Hz

        self.get_logger().info('Goal Navigation Node started')
        self.get_logger().info(f'Max speed: {self.max_speed} m/s')
        self.get_logger().info(f'Goal tolerance: {self.goal_tolerance} m')
        self.get_logger().info(f'Zig-zag enabled: {self.use_zig_zag}')

    def goal_callback(self, msg: Point):
        """Handle incoming goal point from Unity."""
        self.current_goal = [msg.x, msg.y]
        self.is_moving = True
        self.get_logger().info(f'New goal received: ({msg.x:.2f}, {msg.y:.2f}, {msg.z:.2f})')

    def control_loop(self):
        """Main control loop that moves the robot towards the goal."""
        if self.current_goal is None:
            return

        # Calculate distance to goal
        dist_to_goal = self.calculate_distance(
            self.robot_position,
            self.current_goal
        )

        # Check if goal is reached
        if dist_to_goal < self.goal_tolerance:
            self.publish_cmd(0.0, 0.0, 0.0)
            self.is_moving = False
            msg = Bool()
            msg.data = True
            self.goal_reached_pub.publish(msg)
            self.get_logger().info('Goal reached!')
            self.current_goal = None
            return

        # Calculate desired heading to goal
        desired_yaw = self.calculate_heading(
            self.robot_position,
            self.current_goal
        )

        # Calculate yaw error
        yaw_error = self.normalize_angle(desired_yaw - self.robot_yaw)

        # Proportional control for rotation
        rotation_cmd = min(
            max(yaw_error * 0.5, -self.max_rotation_speed),
            self.max_rotation_speed
        )

        # Reduce forward speed when rotating significantly
        forward_speed = self.max_speed
        if abs(yaw_error) > 0.3:  # ~17 degrees
            forward_speed = self.max_speed * (1.0 - abs(yaw_error) / math.pi)

        # Publish velocity command
        self.publish_cmd(forward_speed, 0.0, rotation_cmd)

        # Update robot position estimate (simple integration)
        # In real scenario, this would come from odometry
        dt = 0.05
        self.robot_position[0] += forward_speed * math.cos(self.robot_yaw) * dt
        self.robot_position[1] += forward_speed * math.sin(self.robot_yaw) * dt
        self.robot_yaw += rotation_cmd * dt
        self.robot_yaw = self.normalize_angle(self.robot_yaw)

    def publish_cmd(self, vx: float, vy: float, wz: float):
        """Publish velocity command to /cmd_vel."""
        msg = Twist()
        msg.linear.x = vx
        msg.linear.y = vy
        msg.angular.z = wz
        self.cmd_pub.publish(msg)

    @staticmethod
    def calculate_distance(pos1: list, pos2: list) -> float:
        """Calculate Euclidean distance between two positions."""
        dx = pos2[0] - pos1[0]
        dy = pos2[1] - pos1[1]
        return math.sqrt(dx**2 + dy**2)

    @staticmethod
    def calculate_heading(from_pos: list, to_pos: list) -> float:
        """Calculate desired heading angle to reach target."""
        dx = to_pos[0] - from_pos[0]
        dy = to_pos[1] - from_pos[1]
        return math.atan2(dy, dx)

    @staticmethod
    def normalize_angle(angle: float) -> float:
        """Normalize angle to [-pi, pi]."""
        while angle > math.pi:
            angle -= 2 * math.pi
        while angle < -math.pi:
            angle += 2 * math.pi
        return angle


def main(args=None):
    rclpy.init(args=args)
    node = GoalNavigationNode()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        try:
            node.destroy_node()
        except:
            pass
        try:
            rclpy.shutdown()
        except:
            pass


if __name__ == '__main__':
    main()
