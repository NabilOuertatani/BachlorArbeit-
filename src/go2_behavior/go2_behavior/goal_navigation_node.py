#!/usr/bin/env python3
"""
GoalNavigationNode — Wi-Fi optimised open-loop navigation.

Strategy:
  1. Rotate in place until aligned with goal (no forward motion during turn)
  2. Drive forward the estimated distance (slow ramp at start and end)
  3. Publish estimated pose after every command for Unity sync

Dead reckoning is used because /utlidar/robot_pose is unreliable over Wi-Fi.
Both the real dog and Unity dog start at origin (0, 0, yaw=0).
"""

import math
import rclpy
from rclpy.node import Node
from geometry_msgs.msg import Twist, Point, PoseStamped
from std_msgs.msg import Bool, String
from builtin_interfaces.msg import Time


class GoalNavigationNode(Node):

    # ── State machine phases ───────────────────────────────────────
    IDLE     = 'idle'
    TURNING  = 'turning'
    DRIVING  = 'driving'
    BRAKING  = 'braking'

    def __init__(self):
        super().__init__('goal_navigation_node')

        # ── Parameters ─────────────────────────────────────────────
        self.declare_parameter('max_speed',          0.35)   # m/s — conservative for Wi-Fi
        self.declare_parameter('turn_speed',         0.55)   # rad/s
        self.declare_parameter('goal_tolerance',     0.15)   # m
        self.declare_parameter('heading_tolerance',  0.06)   # rad (~3.5°) — tight turn lock
        self.declare_parameter('ramp_distance',      0.40)   # m — slow down within this dist
        self.declare_parameter('min_speed',          0.10)   # m/s — never go slower than this
        self.declare_parameter('loop_hz',            20.0)

        self.max_speed       = self.get_parameter('max_speed').value
        self.turn_speed      = self.get_parameter('turn_speed').value
        self.goal_tol        = self.get_parameter('goal_tolerance').value
        self.heading_tol     = self.get_parameter('heading_tolerance').value
        self.ramp_dist       = self.get_parameter('ramp_distance').value
        self.min_speed       = self.get_parameter('min_speed').value
        loop_hz              = self.get_parameter('loop_hz').value
        self.dt              = 1.0 / loop_hz

        # ── Dead-reckoning state (both dogs start at origin) ───────
        self.x   = 0.0
        self.y   = 0.0
        self.yaw = 0.0   # radians, 0 = robot's initial forward direction

        # ── Navigation state ───────────────────────────────────────
        self.goal        = None   # (gx, gy)
        self.phase       = self.IDLE
        self.goal_dist   = 0.0   # total distance to drive
        self.driven      = 0.0   # distance covered so far

        # ── Publishers ─────────────────────────────────────────────
        self.cmd_pub          = self.create_publisher(Twist,       '/cmd_vel',         10)
        self.goal_reached_pub = self.create_publisher(Bool,        '/goal_reached',    10)
        self.status_pub       = self.create_publisher(String,      '/nav_status',      10)
        self.est_pose_pub     = self.create_publisher(PoseStamped, '/estimated_pose',  10)

        # ── Subscribers ────────────────────────────────────────────
        self.create_subscription(Point, '/unity_clicked_point', self._on_goal, 10)

        # ── Control loop ───────────────────────────────────────────
        self.create_timer(self.dt, self._loop)

        self.get_logger().info('GoalNavigationNode ready (Wi-Fi open-loop mode)')

    # ── Goal callback ──────────────────────────────────────────────

    def _on_goal(self, msg: Point):
        gx, gy = msg.x, msg.y
        dist = math.hypot(gx - self.x, gy - self.y)

        if dist < self.goal_tol:
            self.get_logger().info('Goal is too close, ignoring.')
            return

        self.goal      = (gx, gy)
        self.goal_dist = dist
        self.driven    = 0.0
        self.phase     = self.TURNING

        self.get_logger().info(
            f'New goal: ({gx:.2f}, {gy:.2f}) | dist={dist:.2f}m | '
            f'from ({self.x:.2f}, {self.y:.2f})'
        )
        self._pub_status('NAVIGATING')

    # ── Main control loop ──────────────────────────────────────────

    def _loop(self):
        self._publish_estimated_pose()

        if self.phase == self.IDLE or self.goal is None:
            return

        if self.phase == self.TURNING:
            self._step_turn()
        elif self.phase == self.DRIVING:
            self._step_drive()
        elif self.phase == self.BRAKING:
            self._step_brake()

    # ── Phase: turn in place ───────────────────────────────────────

    def _step_turn(self):
        gx, gy = self.goal
        desired_yaw = math.atan2(gy - self.y, gx - self.x)
        err = self._norm(desired_yaw - self.yaw)

        if abs(err) < self.heading_tol:
            # Aligned — snap yaw and start driving
            self.yaw   = desired_yaw
            self.phase = self.DRIVING
            self.driven = 0.0
            self.get_logger().info('Aligned. Driving...')
            self._send(0.0, 0.0)
            return

        direction = 1.0 if err > 0 else -1.0

        # Slow down in last 15° to avoid overshoot
        scale = min(1.0, abs(err) / math.radians(15))
        wz = direction * max(0.25, self.turn_speed * scale)

        self._send(0.0, wz)

        # Integrate yaw estimate
        self.yaw = self._norm(self.yaw + wz * self.dt)

    # ── Phase: drive forward ───────────────────────────────────────

    def _step_drive(self):
        remaining = self.goal_dist - self.driven

        if remaining <= self.goal_tol:
            self.phase = self.BRAKING
            return

        # Speed ramp: slow at start (first 0.3m) and near end
        ramp_in  = min(1.0, self.driven / 0.30)
        ramp_out = min(1.0, remaining / self.ramp_dist)
        scale    = min(ramp_in, ramp_out)
        vx       = max(self.min_speed, self.max_speed * scale)

        self._send(vx, 0.0)

        # Integrate position estimate
        self.x      += vx * math.cos(self.yaw) * self.dt
        self.y      += vx * math.sin(self.yaw) * self.dt
        self.driven += vx * self.dt

    # ── Phase: brake / arrive ─────────────────────────────────────

    def _step_brake(self):
        self._send(0.0, 0.0)

        # Snap estimated position to goal
        self.x, self.y = self.goal
        self.goal      = None
        self.phase     = self.IDLE

        msg = Bool()
        msg.data = True
        self.goal_reached_pub.publish(msg)
        self._pub_status('GOAL_REACHED')
        self.get_logger().info(f'Goal reached. Position: ({self.x:.2f}, {self.y:.2f})')

    # ── Helpers ────────────────────────────────────────────────────

    def _send(self, vx: float, wz: float):
        msg = Twist()
        msg.linear.x  = float(vx)
        msg.angular.z = float(wz)
        self.cmd_pub.publish(msg)

    def _pub_status(self, s: str):
        msg = String()
        msg.data = s
        self.status_pub.publish(msg)

    def _publish_estimated_pose(self):
        msg = PoseStamped()
        msg.header.frame_id = 'map'
        now = self.get_clock().now().to_msg()
        msg.header.stamp = now
        msg.pose.position.x = self.x
        msg.pose.position.y = self.y
        msg.pose.position.z = 0.0
        # Yaw → quaternion (z-axis rotation only)
        msg.pose.orientation.z = math.sin(self.yaw / 2.0)
        msg.pose.orientation.w = math.cos(self.yaw / 2.0)
        self.est_pose_pub.publish(msg)

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