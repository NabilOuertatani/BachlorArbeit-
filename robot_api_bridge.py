#!/usr/bin/env python3
"""
robot_api_bridge.py — Forward /api/sport_request to the Go2 over UDP.

Subscribes to /api/sport_request (ROS2), strips the ROS header, and fires
the payload to 192.168.123.161:29999 as a UDP datagram. Fire-and-forget;
no ACK, no retry, ~2–5 ms latency.

API IDs:
    1001 Damp  1002 StandUp   1003 StandDown  1004 RecoveryStand
    1006 Move  1008 MoveLoop  1016 Hello       1017 Stretch
    1019 Wallow  1022 Dance1  1023 Dance2

Related: cmd_vel_bridge.py · robot_odom_bridge.py · DynamicGestureHandler

Usage:
    ros2 run go2_robot_interface robot_api_bridge
"""

import rclpy
from rclpy.node import Node
import socket
import json
import logging

from unitree_api.msg import Request

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


class RobotApiBridge(Node):
    """Bridge ROS2 API requests to robot UDP port."""

    def __init__(self):
        super().__init__('robot_api_bridge')

        # Robot configuration
        self.robot_ip = '192.168.123.161'
        self.robot_port = 29999
        
        # UDP socket
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        
        self.get_logger().info(f'Robot API Bridge initialized (target: {self.robot_ip}:{self.robot_port})')

        # Subscribers for both movement and gestures
        self.create_subscription(
            Request,
            '/api/sport_request',
            self._on_sport_request,
            10
        )
        self.create_subscription(
            Request,
            '/api/gesture/request',
            self._on_gesture_request,
            10
        )

    def _on_sport_request(self, msg: Request):
        """Handle movement /api/sport_request messages."""
        self._send_to_robot(msg, "SPORT")

    def _on_gesture_request(self, msg: Request):
       """Handle gesture /api/gesture/request messages."""
       import time
       # Robot must be in sport mode to execute gestures
       # Send RecoveryStand first to ensure correct mode
       wake_cmd = {'api_id': 1004, 'parameter': '{}'}
       wake_payload = json.dumps(wake_cmd).encode('utf-8')
       self.sock.sendto(wake_payload, (self.robot_ip, self.robot_port))
       self.get_logger().info('[GESTURE] Pre-wake: Sent RecoveryStand (1004)')
       time.sleep(2.0)  # Wait for robot to enter sport mode
       # Now send the actual gesture
       self._send_to_robot(msg, "GESTURE")

    def _send_to_robot(self, msg: Request, msg_type: str):
        """Handle incoming /api/sport_request messages."""
        try:
            # Extract API ID and parameters
            api_id = msg.header.identity.api_id if hasattr(msg, 'header') else 1008
            param_str = msg.parameter if hasattr(msg, 'parameter') else '{}'
            
            # Log receipt
            self.get_logger().info(f'[{msg_type}] [RECEIVED] API ID: {api_id}, param: {param_str}')
            
            # Build command JSON
            command = {
                'api_id': api_id,
                'parameter': param_str
            }
            
            # Serialize and send
            payload = json.dumps(command).encode('utf-8')
            self.get_logger().info(f'[{msg_type}] [SENDING] UDP to {self.robot_ip}:{self.robot_port} - Payload: {payload.decode("utf-8")}')
            
            bytes_sent = self.sock.sendto(payload, (self.robot_ip, self.robot_port))
            self.get_logger().info(f'[{msg_type}] [SUCCESS] Sent {bytes_sent} bytes to robot - API ID: {api_id}')
            
        except Exception as e:
            self.get_logger().error(f'[{msg_type}] [ERROR] Failed to send: {e}', exc_info=True)


def main(args=None):
    rclpy.init(args=args)
    node = RobotApiBridge()
    
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        logger.info('Interrupted by user')
    except Exception as e:
        logger.error(f'Error in main: {e}')
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
