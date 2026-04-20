#!/usr/bin/env python3
"""
CloudForwarder — forwards /utlidar/cloud to Unity as compact JSON over UDP.

The full point cloud is too large to send raw at 10Hz.
This node downsamples it to a manageable set of 2D obstacle points
(ignoring height — floor plane only) and sends them to Unity.

Unity uses these points to draw obstacles on the floor map.

Downsampling strategy:
  - Keep only points within max_range metres of the robot
  - Ignore points below min_height (floor) and above max_height (ceiling)
  - Voxel-grid downsample to grid_size resolution
  - Send max_points points per frame
"""

import math
import json
import socket
import rclpy
from rclpy.node import Node
from rclpy.qos import QoSProfile, ReliabilityPolicy, DurabilityPolicy
from sensor_msgs.msg import PointCloud2
import struct


class CloudForwarder(Node):

    def __init__(self):
        super().__init__('cloud_forwarder')

        self.declare_parameter('unity_ip',    '127.0.0.1')
        self.declare_parameter('unity_port',  10002)
        self.declare_parameter('max_range',   5.0)    # metres
        self.declare_parameter('min_height', -0.05)   # metres — ignore floor
        self.declare_parameter('max_height',  1.50)   # metres — ignore ceiling
        self.declare_parameter('grid_size',   0.10)   # metres — voxel resolution
        self.declare_parameter('max_points',  300)    # max points per UDP packet

        unity_ip    = self.get_parameter('unity_ip').value
        unity_port  = self.get_parameter('unity_port').value
        self.max_r  = self.get_parameter('max_range').value
        self.min_h  = self.get_parameter('min_height').value
        self.max_h  = self.get_parameter('max_height').value
        self.grid   = self.get_parameter('grid_size').value
        self.max_pt = self.get_parameter('max_points').value

        self._addr = (unity_ip, unity_port)
        self._sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

        sensor_qos = QoSProfile(
            depth=5,
            reliability=ReliabilityPolicy.BEST_EFFORT,
            durability=DurabilityPolicy.VOLATILE
        )

        self.create_subscription(
            PointCloud2,
            '/utlidar/cloud',
            self._on_cloud,
            sensor_qos
        )

        self.get_logger().info(
            f'CloudForwarder → Unity {unity_ip}:{unity_port} '
            f'| grid={self.grid}m | max_pts={self.max_pt}'
        )

    def _on_cloud(self, msg: PointCloud2):
        points = self._parse_cloud(msg)
        if not points:
            return

        # Downsample via voxel grid (keep one point per grid cell)
        voxels = {}
        for x, y in points:
            key = (int(x / self.grid), int(y / self.grid))
            if key not in voxels:
                voxels[key] = (round(x, 3), round(y, 3))

        downsampled = list(voxels.values())[:self.max_pt]

        payload = json.dumps({'pts': downsampled}).encode('utf-8')

        # Split into chunks if too large for UDP (~60KB safe limit)
        if len(payload) > 60000:
            chunk_size = self.max_pt // 2
            for i in range(0, len(downsampled), chunk_size):
                chunk = json.dumps(
                    {'pts': downsampled[i:i+chunk_size]}
                ).encode('utf-8')
                self._send(chunk)
        else:
            self._send(payload)

    def _send(self, payload: bytes):
        try:
            self._sock.sendto(payload, self._addr)
        except Exception as e:
            self.get_logger().warn(f'UDP send failed: {e}', throttle_duration_sec=5.0)

    def _parse_cloud(self, msg: PointCloud2):
        """Extract (x, y) pairs from a PointCloud2 message."""
        # Find x, y, z field offsets
        offsets = {}
        for field in msg.fields:
            if field.name in ('x', 'y', 'z'):
                offsets[field.name] = field.offset

        if 'x' not in offsets or 'y' not in offsets or 'z' not in offsets:
            return []

        ox = offsets['x']
        oy = offsets['y']
        oz = offsets['z']
        ps = msg.point_step
        data = msg.data
        points = []

        for i in range(msg.width * msg.height):
            base = i * ps
            try:
                x = struct.unpack_from('<f', data, base + ox)[0]
                y = struct.unpack_from('<f', data, base + oy)[0]
                z = struct.unpack_from('<f', data, base + oz)[0]
            except struct.error:
                continue

            # Filter NaN / Inf
            if not (math.isfinite(x) and math.isfinite(y) and math.isfinite(z)):
                continue

            # Height filter (remove floor and ceiling)
            if z < self.min_h or z > self.max_h:
                continue

            # Range filter
            if math.hypot(x, y) > self.max_r:
                continue

            points.append((x, y))

        return points

    def destroy_node(self):
        self._sock.close()
        super().destroy_node()


def main(args=None):
    rclpy.init(args=args)
    node = CloudForwarder()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()