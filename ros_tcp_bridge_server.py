#!/usr/bin/env python3
import socket
import threading
import struct
import json
import re
import rclpy
from rclpy.node import Node
from geometry_msgs.msg import Point


class TCPBridgeServer(Node):
    def __init__(self):
        super().__init__('tcp_bridge_server')

        self.publisher = self.create_publisher(Point, '/unity_clicked_point', 10)

        self.host = '0.0.0.0'
        self.port = 10000
        self._server_sock = None

        self._point_queue = []
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
            items = list(self._point_queue)
            self._point_queue.clear()
        for point in items:
            self.publisher.publish(point)

    def _enqueue_point(self, x, y, z):
        point = Point()
        point.x, point.y, point.z = x, y, z
        with self._queue_lock:
            self._point_queue.append(point)

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
            # Use regex to extract x and y — ignore z (corrupted bytes from Unity)
            x_match = re.search(r'"x"\s*:\s*([-\d.]+)', text)
            y_match = re.search(r'"y"\s*:\s*([-\d.]+)', text)
            if x_match and y_match:
                x = float(x_match.group(1))
                y = float(y_match.group(1))
                self._enqueue_point(x, y, 0.0)
                self.get_logger().info(f'Goal queued: ({x:.3f}, {y:.3f})')
            else:
                self.get_logger().warn(f'Could not parse x/y from: {text[:80]}')
        except Exception as e:
            self.get_logger().warn(f'Parse error: {e}')


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