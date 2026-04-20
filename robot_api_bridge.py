#!/usr/bin/env python3
"""
Bridge between ROS2 /api/sport/request topic and actual Go2 robot hardware.
Converts ROS2 messages to Unitree API calls sent to the robot.
"""
import socket
import json
import time
import struct
import rclpy
from rclpy.node import Node
from unitree_api.msg import Request


class RobotAPIBridge(Node):
    def __init__(self):
        super().__init__('robot_api_bridge')
        
        self.robot_ip = '192.168.1.7'
        self.robot_port = 29999  # Unitree sport API port
        
        self.socket = None
        self.connect_to_robot()
        
        # Subscribe to ROS2 API requests
        self.subscription = self.create_subscription(
            Request,
            '/api/sport/request',
            self.api_request_callback,
            10
        )
        
        self.get_logger().info(f'Robot API Bridge started. Connecting to {self.robot_ip}:{self.robot_port}')
    
    def connect_to_robot(self):
        """Establish connection to the robot"""
        try:
            if self.socket:
                self.socket.close()
            
            self.socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            self.socket.connect((self.robot_ip, self.robot_port))
            self.get_logger().info(f'✓ Connected to robot at {self.robot_ip}:{self.robot_port}')
        except Exception as e:
            self.get_logger().error(f'Failed to connect to robot: {e}')
            self.socket = None
    
    def api_request_callback(self, msg: Request):
        """Handle ROS2 API request and send to robot"""
        try:
            # Build the request payload
            api_id = msg.header.identity.api_id
            parameter = msg.parameter
            
            # Parse parameter JSON
            try:
                param_dict = json.loads(parameter) if parameter else {}
            except:
                param_dict = {}
            
            # Create request dict for robot
            request_data = {
                "api_id": api_id,
                "parameter": parameter if parameter else "{}"
            }
            
            # Send to robot
            if self.socket:
                payload = json.dumps(request_data).encode('utf-8')
                self.socket.sendto(payload, (self.robot_ip, self.robot_port))
                
                self.get_logger().info(
                    f'Sent to robot - API ID: {api_id}, param: {parameter[:50]}'
                )
            else:
                self.get_logger().warn('Socket not connected, reconnecting...')
                self.connect_to_robot()
                if self.socket:
                    payload = json.dumps(request_data).encode('utf-8')
                    self.socket.sendto(payload, (self.robot_ip, self.robot_port))
        
        except Exception as e:
            self.get_logger().error(f'Error sending command to robot: {e}')
    
    def destroy_node(self):
        if self.socket:
            self.socket.close()
        super().destroy_node()


def main(args=None):
    rclpy.init(args=args)
    node = RobotAPIBridge()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        try:
            node.destroy_node()
        except:
            pass
        try:
            rclpy.shutdown()
        except:
            pass


if __name__ == '__main__':
    main()
