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
        
        # ROS2 publisher for clicked points
        self.publisher = self.create_publisher(Point, '/unity_clicked_point', 10)
        
        # TCP Server configuration
        self.host = '0.0.0.0'
        self.port = 10000
        self.server_socket = None
        self.get_logger().info(f'TCP Bridge Server initialized, listening on {self.host}:{self.port}')
        
    def start_server(self):
        """Start TCP server"""
        self.server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.server_socket.bind((self.host, self.port))
        self.server_socket.listen(5)
        self.get_logger().info(f'TCP Server listening on {self.host}:{self.port}')
        
        # Accept connections in a thread
        server_thread = threading.Thread(target=self.accept_connections, daemon=True)
        server_thread.start()
        
    def accept_connections(self):
        """Accept incoming TCP connections"""
        while rclpy.ok():
            try:
                client_socket, client_address = self.server_socket.accept()
                self.get_logger().info(f'Client connected from {client_address}')
                
                # Handle client in a separate thread
                client_thread = threading.Thread(
                    target=self.handle_client, 
                    args=(client_socket, client_address),
                    daemon=True
                )
                client_thread.start()
            except Exception as e:
                self.get_logger().error(f'Error accepting connection: {e}')
                
    def handle_client(self, client_socket, client_address):
        """Handle individual client connection"""
        try:
            while rclpy.ok():
                data = client_socket.recv(1024)
                
                if not data:
                    self.get_logger().info(f'Client {client_address} disconnected')
                    break
                    
                # Try to parse the message
                self.parse_message(data)
                
        except Exception as e:
            self.get_logger().error(f'Error handling client {client_address}: {e}')
        finally:
            client_socket.close()
            
    def parse_message(self, data):
        """Parse incoming TCP message and publish Point"""
        try:
            # ROS-TCP-Connector format: [4-byte length LE][data]
            if len(data) < 4:
                return
                
            payload = data[4:]  # Skip length prefix
            
            # Try JSON parsing first
            try:
                json_str = payload.decode('utf-8', errors='ignore').strip()
                if '{' in json_str:
                    json_start = json_str.find('{')
                    json_end = json_str.rfind('}') + 1
                    if json_start >= 0 and json_end > json_start:
                        json_data = json.loads(json_str[json_start:json_end])
                        
                        # Extract point coordinates
                        if 'x' in json_data and 'y' in json_data and 'z' in json_data:
                            point = Point()
                            point.x = float(json_data['x'])
                            point.y = float(json_data['y'])
                            point.z = float(json_data['z'])
                            self.publisher.publish(point)
                            self.get_logger().info(f'Published point: x={point.x:.2f}, y={point.y:.2f}, z={point.z:.2f}')
                            return
            except:
                pass
            
            # Try to extract floats from binary data
            if len(payload) >= 12:
                try:
                    # Little-endian float extraction
                    x = struct.unpack('<f', payload[0:4])[0]
                    y = struct.unpack('<f', payload[4:8])[0]
                    z = struct.unpack('<f', payload[8:12])[0]
                    
                    # Sanity check
                    if abs(x) < 1000 and abs(y) < 1000 and abs(z) < 1000:
                        point = Point()
                        point.x = x
                        point.y = y
                        point.z = z
                        self.publisher.publish(point)
                        self.get_logger().info(f'Published point: x={x:.2f}, y={y:.2f}, z={z:.2f}')
                        return
                except:
                    pass
                    
        except Exception as e:
            self.get_logger().debug(f'Error parsing message: {e}')


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
