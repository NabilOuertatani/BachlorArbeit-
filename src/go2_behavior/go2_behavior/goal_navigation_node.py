#!/usr/bin/env python3
"""
GoalNavigationNode — closed-loop mode using /utlidar/robot_pose at 18Hz.
Real pose feedback eliminates dead reckoning drift and circling.
"""

import math
import rclpy
from rclpy.node import Node
from rclpy.qos import QoSProfile, ReliabilityPolicy, DurabilityPolicy
from geometry_msgs.msg import Twist, Point, PoseStamped
from std_msgs.msg import Bool, String


class GoalNavigationNode(Node):

    IDLE    = 'idle'
    TURNING = 'turning'
    DRIVING = 'driving'

    def __init__(self):
        super().__init__('goal_navigation_node')

        self.declare_parameter('max_speed',         0.35)
        self.declare_parameter('turn_speed',         0.6)
        self.declare_parameter('goal_tolerance',     0.20)
        self.declare_parameter('heading_tolerance',  0.08)
        self.declare_parameter('kp_turn',            1.2)
        self.declare_parameter('pose_timeout',       1.0)

        self.max_speed    = self.get_parameter('max_speed').value
        self.turn_speed   = self.get_parameter('turn_speed').value
        self.goal_tol     = self.get_parameter('goal_tolerance').value
        self.heading_tol  = self.get_parameter('heading_tolerance').value
        self.kp_turn      = self.get_parameter('kp_turn').value
        self.pose_timeout = self.get_parameter('pose_timeout').value

        self.robot_x        = 0.0
        self.robot_y        = 0.0
        self.robot_yaw      = 0.0
        self.last_pose_time = None
        self.pose_ready     = False

        self.goal  = None
        self.phase = self.IDLE

        sensor_qos = QoSProfile(
            depth=10,
            reliability=ReliabilityPolicy.BEST_EFFORT,
            durability=DurabilityPolicy.VOLATILE
        )

        self.cmd_pub          = self.create_publisher(Twist,       '/cmd_vel',        10)
        self.goal_reached_pub = self.create_publisher(Bool,        '/goal_reached',   10)
        self.status_pub       = self.create_publisher(String,      '/nav_status',     10)
        self.est_pose_pub     = self.create_publisher(PoseStamped, '/estimated_pose', 10)

        self.create_subscription(Point, '/unity_clicked_point', self._on_goal, 10)
        self.create_subscription(PoseStamped, '/utlidar/robot_pose', self._on_pose, sensor_qos)

        self.create_timer(0.05, self._loop)
        self.get_logger().info('GoalNavigationNode ready — CLOSED-LOOP mode (real pose)')

    def _on_pose(self, msg: PoseStamped):
        self.robot_x        = msg.pose.position.x
        self.robot_y        = msg.pose.position.y
        self.robot_yaw      = self._quat_to_yaw(msg.pose.orientation)
        self.last_pose_time = self.get_clock().now()
        self.pose_ready     = True
        self._publish_estimated_pose()

    def _on_goal(self, msg: Point):
        if not self.pose_ready:
            self.get_logger().warn('No pose yet — waiting for /utlidar/robot_pose')
            return
        self.goal  = (msg.x, msg.y)
        self.phase = self.TURNING
        self.get_logger().info(f'New goal: ({msg.x:.2f}, {msg.y:.2f}) | robot at ({self.robot_x:.2f}, {self.robot_y:.2f})')
        self._pub_status('NAVIGATING')

    def _loop(self):
        if not self.pose_ready or self.goal is None:
            return

        age = (self.get_clock().now() - self.last_pose_time).nanoseconds * 1e-9
        if age > self.pose_timeout:
            self.get_logger().warn(f'Pose stale ({age:.1f}s) — stopping', throttle_duration_sec=2.0)
            self._send(0.0, 0.0)
            return

        gx, gy  = self.goal
        dist    = math.hypot(gx - self.robot_x, gy - self.robot_y)
        desired = math.atan2(gy - self.robot_y, gx - self.robot_x)
        yaw_err = self._norm(desired - self.robot_yaw)

        if dist < self.goal_tol:
            self._send(0.0, 0.0)
            self.goal  = None
            self.phase = self.IDLE
            msg = Bool(); msg.data = True
            self.goal_reached_pub.publish(msg)
            self._pub_status('GOAL_REACHED')
            self.get_logger().info(f'Goal reached! dist={dist:.3f}m')
            return

        if self.phase == self.TURNING:
            if abs(yaw_err) < self.heading_tol:
                self.phase = self.DRIVING
                self.get_logger().info('Aligned — driving')
                self._send(0.0, 0.0)
                return
            scale = min(1.0, abs(yaw_err) / math.radians(10))
            wz    = math.copysign(max(0.2, self.turn_speed * scale), yaw_err)
            self._send(0.0, wz)

        elif self.phase == self.DRIVING:
            if abs(yaw_err) > math.radians(15):
                self.phase = self.TURNING
                self.get_logger().info(f'Drift {math.degrees(yaw_err):.1f}° — re-aligning')
                return
            proximity = min(1.0, dist / 0.5)
            vx = max(0.10, self.max_speed * proximity)
            self._send(vx, 0.0)

    def _send(self, vx: float, wz: float):
        msg = Twist()
        msg.linear.x  = float(vx)
        msg.angular.z = float(wz)
        self.cmd_pub.publish(msg)

    def _pub_status(self, s: str):
        msg = String(); msg.data = s
        self.status_pub.publish(msg)

    def _publish_estimated_pose(self):
        msg = PoseStamped()
        msg.header.frame_id = 'map'
        msg.header.stamp    = self.get_clock().now().to_msg()
        msg.pose.position.x = self.robot_x
        msg.pose.position.y = self.robot_y
        msg.pose.position.z = 0.0
        msg.pose.orientation.z = math.sin(self.robot_yaw / 2.0)
        msg.pose.orientation.w = math.cos(self.robot_yaw / 2.0)
        self.est_pose_pub.publish(msg)

    @staticmethod
    def _quat_to_yaw(q) -> float:
        return math.atan2(
            2.0 * (q.w * q.z + q.x * q.y),
            1.0 - 2.0 * (q.y * q.y + q.z * q.z)
        )

    @staticmethod
    def _norm(a: float) -> float:
        return (a + math.pi) % (2 * math.pi) - math.pi


def main(args=None):
    rclpy.init(args=args)
    node = GoalNavigationNode()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()