#!/usr/bin/env python3
"""
GoalNavigationNode — closed-loop, no obstacle avoidance.
Uses /utlidar/robot_pose for real position.
Notifies Unity via UDP when each goal is reached (multi-goal support).
"""

import math
import socket as socket_module
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

        # ── Parameters ─────────────────────────────────────────────
        self.declare_parameter('max_speed',          0.35)
        self.declare_parameter('turn_speed',          0.6)
        self.declare_parameter('goal_tolerance', 0.35)
        self.declare_parameter('heading_tolerance',   0.08)
        self.declare_parameter('pose_timeout', 3.0)
        self.declare_parameter('unity_ip',            '127.0.0.1')
        self.declare_parameter('unity_reached_port',  10004)

        self.max_speed    = self.get_parameter('max_speed').value
        self.turn_speed   = self.get_parameter('turn_speed').value
        self.goal_tol     = self.get_parameter('goal_tolerance').value
        self.heading_tol  = self.get_parameter('heading_tolerance').value
        self.pose_timeout = self.get_parameter('pose_timeout').value

        unity_ip         = self.get_parameter('unity_ip').value
        unity_port       = self.get_parameter('unity_reached_port').value
        self._unity_addr = (unity_ip, unity_port)
        self._udp_sock   = socket_module.socket(
            socket_module.AF_INET, socket_module.SOCK_DGRAM)

        # ── State ──────────────────────────────────────────────────
        self.robot_x        = 0.0
        self.robot_y        = 0.0
        self.robot_yaw      = 0.0
        self.last_pose_time = None
        self.pose_ready     = False
        self.goal           = None
        self.phase          = self.IDLE

        # ── QoS ────────────────────────────────────────────────────
        sensor_qos = QoSProfile(
            depth=10,
            reliability=ReliabilityPolicy.BEST_EFFORT,
            durability=DurabilityPolicy.VOLATILE
        )

        # ── Publishers ─────────────────────────────────────────────
        self.cmd_pub          = self.create_publisher(Twist,       '/cmd_vel',        10)
        self.goal_reached_pub = self.create_publisher(Bool,        '/goal_reached',   10)
        self.status_pub       = self.create_publisher(String,      '/nav_status',     10)
        self.est_pose_pub     = self.create_publisher(PoseStamped, '/estimated_pose', 10)

        # ── Subscribers ────────────────────────────────────────────
        self.create_subscription(Point,       '/unity_clicked_point', self._on_goal, 10)
        self.create_subscription(PoseStamped, '/utlidar/robot_pose',  self._on_pose, sensor_qos)

        self.create_timer(0.05, self._loop)

        self.get_logger().info('GoalNavigationNode ready — closed-loop, multi-goal')
        self.get_logger().info(f'Notifying Unity at {unity_ip}:{unity_port}')

    # ── Callbacks ──────────────────────────────────────────────────

    def _on_pose(self, msg: PoseStamped):
        self.robot_x        = msg.pose.position.x
        self.robot_y        = msg.pose.position.y
        self.robot_yaw      = self._quat_to_yaw(msg.pose.orientation)
        self.last_pose_time = self.get_clock().now()
        self.pose_ready     = True
        self._publish_pose()

    def _on_goal(self, msg: Point):
        if not self.pose_ready:
            self.get_logger().warn('No pose yet — waiting for /utlidar/robot_pose')
            return
        self.goal  = (msg.x, msg.y)
        self.phase = self.TURNING
        self.get_logger().info(
            f'New goal: ({msg.x:.2f}, {msg.y:.2f}) | '
            f'robot at ({self.robot_x:.2f}, {self.robot_y:.2f})'
        )
        self._pub_status('NAVIGATING')

    # ── Control loop ───────────────────────────────────────────────

    def _loop(self):
        if not self.pose_ready or self.goal is None:
            return

        age = (self.get_clock().now() - self.last_pose_time).nanoseconds * 1e-9
        if age > self.pose_timeout:
            self.get_logger().warn('Pose stale — stopping', throttle_duration_sec=2.0)
            self._send(0.0, 0.0)
            return

        gx, gy  = self.goal
        dist    = math.hypot(gx - self.robot_x, gy - self.robot_y)
        desired = math.atan2(gy - self.robot_y, gx - self.robot_x)
        yaw_err = self._norm(desired - self.robot_yaw)

        # ── Goal reached ──────────────────────────────────────────
        if dist < self.goal_tol:
            self._send(0.0, 0.0)
            self.goal  = None
            self.phase = self.IDLE
            msg = Bool(); msg.data = True
            self.goal_reached_pub.publish(msg)
            try:
                self._udp_sock.sendto(b'{"reached":true}', self._unity_addr)
            except Exception as e:
                self.get_logger().warn(f'UDP notify failed: {e}')
            self._pub_status('GOAL_REACHED')
            self.get_logger().info(f'Goal reached! dist={dist:.3f}m')
            return

        # ── TURNING ───────────────────────────────────────────────
        if self.phase == self.TURNING:
            if abs(yaw_err) < self.heading_tol:
                self.phase = self.DRIVING
                self.get_logger().info('Aligned — driving')
                # Fall through to DRIVING phase immediately
            else:
                scale = min(1.0, abs(yaw_err) / math.radians(10))
                wz    = math.copysign(max(0.2, self.turn_speed * scale), yaw_err)
                self._send(0.0, wz)
                return

        # ── DRIVING ───────────────────────────────────────────────
        if self.phase == self.DRIVING:
            if abs(yaw_err) > math.radians(15):
                self.phase = self.TURNING
                self.get_logger().info(f'Drift {math.degrees(yaw_err):.1f}° — re-aligning')
                return
            proximity = min(1.0, dist / 0.5)
            vx = max(0.10, self.max_speed * proximity)
            # Also steer to maintain heading while driving
            wz = self._clamp(yaw_err * 1.5, -self.turn_speed * 0.3, self.turn_speed * 0.3)
            self._send(vx, wz)
            self.get_logger().info(f'Driving: dist={dist:.3f}m, vx={vx:.3f}, wz={wz:.3f}, yaw_err={math.degrees(yaw_err):.1f}°', throttle_duration_sec=2.0)

    # ── Helpers ────────────────────────────────────────────────────

    def _send(self, vx, wz):
        msg = Twist()
        msg.linear.x  = float(vx)
        msg.angular.z = float(wz)
        self.cmd_pub.publish(msg)

    def _pub_status(self, s):
        msg = String(); msg.data = s
        self.status_pub.publish(msg)

    def _publish_pose(self):
        msg = PoseStamped()
        msg.header.frame_id    = 'map'
        msg.header.stamp       = self.get_clock().now().to_msg()
        msg.pose.position.x    = self.robot_x
        msg.pose.position.y    = self.robot_y
        msg.pose.position.z    = 0.0
        msg.pose.orientation.z = math.sin(self.robot_yaw / 2.0)
        msg.pose.orientation.w = math.cos(self.robot_yaw / 2.0)
        self.est_pose_pub.publish(msg)

    def destroy_node(self):
        self._udp_sock.close()
        super().destroy_node()

    @staticmethod
    def _quat_to_yaw(q):
        return math.atan2(
            2.0 * (q.w * q.z + q.x * q.y),
            1.0 - 2.0 * (q.y * q.y + q.z * q.z)
        )

    @staticmethod
    def _norm(a):
        return (a + math.pi) % (2 * math.pi) - math.pi
    
    @staticmethod
    def _clamp(v, lo, hi):
        return max(lo, min(hi, v))

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