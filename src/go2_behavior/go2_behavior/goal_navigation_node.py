#!/usr/bin/env python3
"""
GoalNavigationNode — closed-loop + obstacle avoidance + multi-goal support.
Notifies Unity via UDP when each goal is reached so MultiGoalManager
can advance to the next waypoint.
"""

import math
import struct
import socket as socket_module
import rclpy
from rclpy.node import Node
from rclpy.qos import QoSProfile, ReliabilityPolicy, DurabilityPolicy
from geometry_msgs.msg import Twist, Point, PoseStamped
from sensor_msgs.msg import PointCloud2
from std_msgs.msg import Bool, String


class GoalNavigationNode(Node):

    IDLE     = 'idle'
    TURNING  = 'turning'
    DRIVING  = 'driving'
    AVOIDING = 'avoiding'

    def __init__(self):
        super().__init__('goal_navigation_node')

        # ── Parameters ─────────────────────────────────────────────
        self.declare_parameter('max_speed',           0.35)
        self.declare_parameter('turn_speed',           0.6)
        self.declare_parameter('goal_tolerance',       0.20)
        self.declare_parameter('heading_tolerance',    0.08)
        self.declare_parameter('pose_timeout',         1.0)
        self.declare_parameter('avoid_distance',       0.50)
        self.declare_parameter('stop_distance',        0.25)
        self.declare_parameter('forward_cone_deg',     40.0)
        self.declare_parameter('scan_sectors',         36)
        self.declare_parameter('avoid_speed',          0.20)
        self.declare_parameter('unity_ip',             '127.0.0.1')
        self.declare_parameter('unity_reached_port',   10004)

        self.max_speed    = self.get_parameter('max_speed').value
        self.turn_speed   = self.get_parameter('turn_speed').value
        self.goal_tol     = self.get_parameter('goal_tolerance').value
        self.heading_tol  = self.get_parameter('heading_tolerance').value
        self.pose_timeout = self.get_parameter('pose_timeout').value
        self.avoid_dist   = self.get_parameter('avoid_distance').value
        self.stop_dist    = self.get_parameter('stop_distance').value
        self.fwd_cone     = math.radians(self.get_parameter('forward_cone_deg').value)
        self.scan_sectors = self.get_parameter('scan_sectors').value
        self.avoid_speed  = self.get_parameter('avoid_speed').value

        unity_ip          = self.get_parameter('unity_ip').value
        unity_port        = self.get_parameter('unity_reached_port').value
        self._unity_addr  = (unity_ip, unity_port)
        self._udp_sock    = socket_module.socket(
            socket_module.AF_INET, socket_module.SOCK_DGRAM)

        # ── State ──────────────────────────────────────────────────
        self.robot_x        = 0.0
        self.robot_y        = 0.0
        self.robot_yaw      = 0.0
        self.last_pose_time = None
        self.pose_ready     = False
        self.goal           = None
        self.phase          = self.IDLE
        self.sector_dist    = [float('inf')] * self.scan_sectors

        # ── QoS ────────────────────────────────────────────────────
        sensor_qos = QoSProfile(
            depth=5,
            reliability=ReliabilityPolicy.BEST_EFFORT,
            durability=DurabilityPolicy.VOLATILE
        )

        # ── Publishers ─────────────────────────────────────────────
        self.cmd_pub          = self.create_publisher(Twist,       '/cmd_vel',        10)
        self.goal_reached_pub = self.create_publisher(Bool,        '/goal_reached',   10)
        self.status_pub       = self.create_publisher(String,      '/nav_status',     10)
        self.est_pose_pub     = self.create_publisher(PoseStamped, '/estimated_pose', 10)

        # ── Subscribers ────────────────────────────────────────────
        self.create_subscription(Point,       '/unity_clicked_point', self._on_goal,  10)
        self.create_subscription(PoseStamped, '/utlidar/robot_pose',  self._on_pose,  sensor_qos)
        self.create_subscription(PointCloud2, '/utlidar/cloud',       self._on_cloud, sensor_qos)

        self.create_timer(0.05, self._loop)
        self.get_logger().info(
            'GoalNavigationNode ready — multi-goal + obstacle avoidance')
        self.get_logger().info(
            f'Notifying Unity at {unity_ip}:{unity_port} on goal reached')

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

    def _on_cloud(self, msg: PointCloud2):
        sectors = [float('inf')] * self.scan_sectors
        step    = msg.point_step
        data    = msg.data
        ox = oy = oz = None
        for f in msg.fields:
            if f.name == 'x': ox = f.offset
            if f.name == 'y': oy = f.offset
            if f.name == 'z': oz = f.offset
        if ox is None: return

        for i in range(msg.width * msg.height):
            base = i * step
            try:
                x = struct.unpack_from('<f', data, base + ox)[0]
                y = struct.unpack_from('<f', data, base + oy)[0]
                z = struct.unpack_from('<f', data, base + oz)[0]
            except Exception:
                continue
            if not (math.isfinite(x) and math.isfinite(y) and math.isfinite(z)):
                continue
            if z < -0.1 or z > 1.2:
                continue
            dist = math.hypot(x, y)
            if dist < 0.05 or dist > 6.0:
                continue
            angle  = math.atan2(y, x)
            sector = int((angle + math.pi) / (2 * math.pi) * self.scan_sectors) % self.scan_sectors
            if dist < sectors[sector]:
                sectors[sector] = dist
        self.sector_dist = sectors

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
            # Notify ROS
            msg = Bool(); msg.data = True
            self.goal_reached_pub.publish(msg)
            # Notify Unity
            try:
                self._udp_sock.sendto(b'{"reached":true}', self._unity_addr)
            except Exception as e:
                self.get_logger().warn(f'UDP notify failed: {e}')
            self._pub_status('GOAL_REACHED')
            self.get_logger().info(f'Goal reached! dist={dist:.3f}m')
            return

        # ── Obstacle check ────────────────────────────────────────
        obstacle_ahead, min_dist = self._obstacle_in_cone(yaw_err)

        if min_dist < self.stop_dist:
            self._send(0.0, 0.0)
            self.get_logger().warn(
                f'Emergency stop — obstacle at {min_dist:.2f}m',
                throttle_duration_sec=1.0)
            self.phase = self.AVOIDING
            return

        if obstacle_ahead and self.phase == self.DRIVING:
            self.phase = self.AVOIDING
            self.get_logger().info(f'Obstacle at {min_dist:.2f}m — avoiding')

        # ── TURNING ───────────────────────────────────────────────
        if self.phase == self.TURNING:
            if abs(yaw_err) < self.heading_tol:
                self.phase = self.DRIVING
                self.get_logger().info('Aligned — driving')
                self._send(0.0, 0.0)
                return
            scale = min(1.0, abs(yaw_err) / math.radians(10))
            wz    = math.copysign(max(0.2, self.turn_speed * scale), yaw_err)
            self._send(0.0, wz)

        # ── DRIVING ───────────────────────────────────────────────
        elif self.phase == self.DRIVING:
            if abs(yaw_err) > math.radians(15):
                self.phase = self.TURNING
                return
            proximity = min(1.0, dist / 0.5)
            vx = max(0.10, self.max_speed * proximity)
            self._send(vx, 0.0)

        # ── AVOIDING ──────────────────────────────────────────────
        elif self.phase == self.AVOIDING:
            best_dir = self._find_clear_direction(desired)
            if best_dir is None:
                self._send(0.0, 0.0)
                self.get_logger().warn('No clear path', throttle_duration_sec=2.0)
                return
            steer_err     = self._norm(best_dir - self.robot_yaw)
            wz            = self._clamp(steer_err * 1.5, -self.turn_speed, self.turn_speed)
            forward_clear = self._min_dist_in_cone(0.0, math.radians(20))
            vx            = self.avoid_speed if forward_clear > self.avoid_dist else 0.0
            self._send(vx, wz)
            if not obstacle_ahead:
                self.get_logger().info('Obstacle cleared — resuming')
                self.phase = self.TURNING

    # ── Obstacle helpers ───────────────────────────────────────────

    def _obstacle_in_cone(self, yaw_err):
        min_d = self._min_dist_in_cone(yaw_err, self.fwd_cone)
        return min_d < self.avoid_dist, min_d

    def _min_dist_in_cone(self, center, half_width):
        min_d = float('inf')
        for i, d in enumerate(self.sector_dist):
            angle = (i / self.scan_sectors) * 2 * math.pi - math.pi
            if abs(self._norm(angle - center)) <= half_width:
                if d < min_d:
                    min_d = d
        return min_d

    def _find_clear_direction(self, goal_dir):
        best_angle = None
        best_score = float('inf')
        for i in range(self.scan_sectors):
            world_angle = -math.pi + (i / self.scan_sectors) * 2 * math.pi
            robot_angle = self._norm(world_angle - self.robot_yaw)
            if self._min_dist_in_cone(robot_angle, math.radians(15)) < self.avoid_dist:
                continue
            score = abs(self._norm(world_angle - goal_dir))
            if score < best_score:
                best_score = score
                best_angle = world_angle
        return best_angle

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