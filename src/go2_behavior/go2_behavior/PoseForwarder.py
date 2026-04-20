#!/usr/bin/env python3
"""
PoseForwarder — subscribes to /estimated_pose and forwards it to Unity via UDP.

The GoalNavigationNode publishes /estimated_pose (dead reckoning).
This node forwards it as compact JSON so Unity can move the virtual dog
in sync with the real one.

UDP is used (not TCP) because:
- Pose updates are high-frequency (~20 Hz)
- A missed frame is fine — next one arrives in 50ms
- No connection management needed
"""

import socket
import json
import math
import rclpy
from rclpy.node import Node
from geometry_msgs.msg import PoseStamped


class PoseForwarder(Node):

    def __init__(self):
        super().__init__('pose_forwarder')

        self.declare_parameter('unity_ip',   '127.0.0.1')
        self.declare_parameter('unity_port', 10001)

        unity_ip   = self.get_parameter('unity_ip').value
        unity_port = self.get_parameter('unity_port').value

        self._addr = (unity_ip, unity_port)
        self._sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

        self.create_subscription(
            PoseStamped,
            '/estimated_pose',
            self._on_pose,
            10
        )

        self.get_logger().info(
            f'PoseForwarder → Unity {unity_ip}:{unity_port}'
        )

    def _on_pose(self, msg: PoseStamped):
        q   = msg.pose.orientation
        yaw = math.atan2(
            2.0 * (q.w * q.z + q.x * q.y),
            1.0 - 2.0 * (q.y * q.y + q.z * q.z)
        )

        payload = json.dumps({
            'x':   round(msg.pose.position.x, 4),
            'y':   round(msg.pose.position.y, 4),
            'yaw': round(yaw, 4)
        }).encode('utf-8')

        try:
            self._sock.sendto(payload, self._addr)
        except Exception as e:
            self.get_logger().warn(f'UDP send failed: {e}', throttle_duration_sec=5.0)

    def destroy_node(self):
        self._sock.close()
        super().destroy_node()


def main(args=None):
    rclpy.init(args=args)
    node = PoseForwarder()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()