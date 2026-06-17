#!/usr/bin/env python3
"""
TCP Bridge Server — Gateway between Unity and ROS2.

Listens on 0.0.0.0:10000. Each Unity connection gets its own thread;
messages are queued and drained to ROS at 50 Hz.

Protocol:
    4-byte big-endian length prefix → JSON payload (UTF-8, max 65535 bytes)

Message routing:
    {"x", "y", "z"}                       → /unity_clicked_point (Point)
    {"header": {"identity": {"api_id"}}}  → /api/sport_request   (Request)

Threading:
    Main thread   — accepts connections, spawns client threads
    Client thread — reads, parses, enqueues (queue_lock)
    Timer (20 ms) — drains queues, publishes to ROS

Usage:
    ros2 run ros_tcp_bridge bridge
"""

import socket
import threading
import struct
import json
import re
import rclpy
from rclpy.node import Node
from geometry_msgs.msg import Point
from std_msgs.msg import Float32
from unitree_api.msg import Request


class TCPBridgeServer(Node):
    def __init__(self):
        super().__init__('tcp_bridge_server')

        # Publishers for waypoints, speeds, and gestures
        self.waypoint_pub = self.create_publisher(Point, '/unity_clicked_point', 10)
        self.speed_pub = self.create_publisher(Float32, '/nav_waypoint_speed', 10)
        self.gesture_pub = self.create_publisher(Request, '/api/sport_request', 10)

        self.host = '0.0.0.0'
        self.port = 10000
        self._server_sock = None

        self._point_queue = []
        self._speed_queue = []
        self._gesture_queue = []
        self._queue_lock = threading.Lock()
        self.create_timer(0.02, self._drain_queue)

    def start_server(self):
        self._server_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self._server_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self._server_sock.bind((self.host, self.port))
        self._server_sock.listen(5)
        self.get_logger().info(f'TCP Bridge listening on {self.host}:{self.port}')
        t = threading.Thread(target=self._accept_loop, daemon=True)
        t.start()

    def _drain_queue(self):
        with self._queue_lock:
            points = list(self._point_queue)
            speeds = list(self._speed_queue)
            gestures = list(self._gesture_queue)
            self._point_queue.clear()
            self._speed_queue.clear()
            self._gesture_queue.clear()
        
        for point in points:
            self.waypoint_pub.publish(point)
        
        for speed in speeds:
            self.speed_pub.publish(speed)
        
        for gesture in gestures:
            self.gesture_pub.publish(gesture)

    def _enqueue_point(self, x, y, z):
        point = Point()
        point.x, point.y, point.z = x, y, z
        with self._queue_lock:
            self._point_queue.append(point)
    
    def _enqueue_speed(self, speed: float):
        """Enqueue speed message to be published to /nav_waypoint_speed"""
        msg = Float32()
        msg.data = float(speed)
        with self._queue_lock:
            self._speed_queue.append(msg)
    
    def _enqueue_gesture(self, api_id: int, parameter: str):
        msg = Request()
        msg.header.identity.api_id = api_id
        msg.parameter = parameter
        with self._queue_lock:
            self._gesture_queue.append(msg)

    def _accept_loop(self):
        while rclpy.ok():
            try:
                conn, addr = self._server_sock.accept()
                conn.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
                self.get_logger().info(f'Unity connected from {addr}')
                t = threading.Thread(
                    target=self._client_loop,
                    args=(conn, addr),
                    daemon=True
                )
                t.start()
            except Exception as e:
                if rclpy.ok():
                    self.get_logger().error(f'Accept error: {e}')

    def _recv_exact(self, conn, n):
        buf = b''
        while len(buf) < n:
            chunk = conn.recv(n - len(buf))
            if not chunk:
                return None
            buf += chunk
        return buf

    def _client_loop(self, conn, addr):
        try:
            while rclpy.ok():
                header = self._recv_exact(conn, 4)
                if not header:
                    break
                msg_len = struct.unpack('>I', header)[0]

                if msg_len == 0 or msg_len > 65535:
                    self.get_logger().warn(f'Suspicious msg_len={msg_len}, dropping')
                    break

                payload = self._recv_exact(conn, msg_len)
                if not payload:
                    break

                self._parse_json(payload)

        except Exception as e:
            self.get_logger().error(f'Client {addr} error: {e}')
        finally:
            conn.close()
            self.get_logger().info(f'Unity {addr} disconnected')

    def _parse_json(self, payload: bytes):
        try:
            text = payload.decode('utf-8', errors='ignore')
            data = json.loads(text)
            
            # Check if this is a gesture command (has api_id)
            if 'header' in data and 'identity' in data['header'] and 'api_id' in data['header']['identity']:
                self._handle_gesture_request(data)
                return
            
            # Otherwise handle as waypoint
            # Use regex to extract x, y, and speed — ignore z (corrupted bytes from Unity)
            x_match = re.search(r'"x"\s*:\s*([-\d.]+)', text)
            y_match = re.search(r'"y"\s*:\s*([-\d.]+)', text)
            speed_match = re.search(r'"speed"\s*:\s*([-\d.]+)', text)
            if x_match and y_match:
                x = float(x_match.group(1))
                y = float(y_match.group(1))
                speed = float(speed_match.group(1)) if speed_match else 0.35
                self._enqueue_point(x, y, 0.0)
                self._enqueue_speed(speed)
                self.get_logger().info(f'Goal queued: ({x:.3f}, {y:.3f}), speed: {speed:.2f} m/s')
            else:
                self.get_logger().warn(f'Could not parse x/y from: {text[:80]}')
        except Exception as e:
            self.get_logger().warn(f'Parse error: {e}')
    
    def _handle_gesture_request(self, data: dict):
        """Handle gesture commands from Unity."""
        try:
            api_id = data['header']['identity']['api_id']
            parameter = data.get('parameter', '{}')
            
            # If parameter is dict, convert to JSON string
            if isinstance(parameter, dict):
                parameter = json.dumps(parameter)
            elif not isinstance(parameter, str):
                parameter = '{}'
            
            self._enqueue_gesture(api_id, parameter)
            self.get_logger().info(f'Gesture queued: api_id={api_id}, parameter={parameter}')
        except Exception as e:
            self.get_logger().error(f'Error handling gesture request: {e}')


def main(args=None):
    rclpy.init(args=args)
    node = TCPBridgeServer()
    node.start_server()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        if node._server_sock:
            node._server_sock.close()
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()