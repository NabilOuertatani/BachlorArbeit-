#!/usr/bin/env python3
"""
TCP Bridge Server for Unity ROS-TCP-Connector
Listens on port 10000 and bridges ROS messages between TCP and ROS2
"""

import socket
import rclpy
from rclpy.node import Node
from geometry_msgs.msg import Point
import threading
import struct
import json


class TCPBridgeServer(Node):
    def __init__(self):
        super().__init__('tcp_bridge_server')

        self.publisher = self.create_publisher(Point, '/unity_clicked_point', 10)

        self.host = '0.0.0.0'
        self.port = 10000
        self.server_socket = None

        self.get_logger().info(
            f'TCP Bridge Server initialized, listening on {self.host}:{self.port}'
        )

    def start_server(self):
        self.server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.server_socket.bind((self.host, self.port))
        self.server_socket.listen(5)

        self.get_logger().info(f'TCP Server listening on {self.host}:{self.port}')

        server_thread = threading.Thread(target=self.accept_connections, daemon=True)
        server_thread.start()

    def accept_connections(self):
        while rclpy.ok():
            try:
                client_socket, client_address = self.server_socket.accept()
                self.get_logger().info(f'Client connected from {client_address}')

                client_thread = threading.Thread(
                    target=self.handle_client,
                    args=(client_socket, client_address),
                    daemon=True
                )
                client_thread.start()
            except Exception as e:
                self.get_logger().error(f'Error accepting connection: {e}')

    def recv_exact(self, client_socket, size: int) -> bytes | None:
        data = b''
        while len(data) < size:
            chunk = client_socket.recv(size - len(data))
            if not chunk:
                return None
            data += chunk
        return data

    def handle_client(self, client_socket, client_address):
        try:
            while rclpy.ok():
                # Read 4-byte little-endian payload length
                length_bytes = self.recv_exact(client_socket, 4)
                if not length_bytes:
                    self.get_logger().info(f'Client {client_address} disconnected')
                    break

                msg_len = struct.unpack('<I', length_bytes)[0]

                payload = self.recv_exact(client_socket, msg_len)
                if payload is None:
                    self.get_logger().info(f'Client {client_address} disconnected')
                    break

                self.parse_message(payload)

        except Exception as e:
            self.get_logger().error(f'Error handling client {client_address}: {e}')
        finally:
            client_socket.close()

    def parse_message(self, payload: bytes):
        try:
            # Try JSON first
            try:
                json_str = payload.decode('utf-8', errors='ignore').strip()
                if '{' in json_str and '}' in json_str:
                    json_start = json_str.find('{')
                    json_end = json_str.rfind('}') + 1
                    if json_start >= 0 and json_end > json_start:
                        json_data = json.loads(json_str[json_start:json_end])

                        if 'x' in json_data and 'y' in json_data and 'z' in json_data:
                            point = Point()
                            point.x = float(json_data['x'])
                            point.y = float(json_data['y'])
                            point.z = float(json_data['z'])
                            self.publisher.publish(point)
                            self.get_logger().info(
                                f'Published JSON point: x={point.x:.3f}, y={point.y:.3f}, z={point.z:.3f}'
                            )
                            return
            except Exception:
                pass

            # geometry_msgs/Point = 3 float64 values = 24 bytes
            if len(payload) >= 24:
                try:
                    x, y, z = struct.unpack('<ddd', payload[:24])

                    if abs(x) < 1000 and abs(y) < 1000 and abs(z) < 1000:
                        point = Point()
                        point.x = x
                        point.y = y
                        point.z = z
                        self.publisher.publish(point)
                        self.get_logger().info(
                            f'Published binary point: x={x:.3f}, y={y:.3f}, z={z:.3f}'
                        )
                        return
                except Exception:
                    pass

            self.get_logger().warning(
                f'Could not parse payload, len={len(payload)} bytes'
            )

        except Exception as e:
            self.get_logger().warning(f'Error parsing message: {e}')


def main(args=None):
    rclpy.init(args=args)
    node = TCPBridgeServer()
    node.start_server()

    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        if node.server_socket:
            node.server_socket.close()
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()