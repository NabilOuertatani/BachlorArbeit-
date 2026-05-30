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

        # Subscriber
        self.create_subscription(
            Request,
            '/api/sport_request',
            self._on_request,
            10
        )

    def _on_request(self, msg: Request):
        """Handle incoming /api/sport_request messages."""
        try:
            # Extract API ID and parameters
            api_id = msg.header.identity.api_id if hasattr(msg, 'header') else 1008
            param_str = msg.parameter if hasattr(msg, 'parameter') else '{}'
            
            # Build command JSON
            command = {
                'api_id': api_id,
                'parameter': param_str
            }
            
            # Serialize and send
            payload = json.dumps(command).encode('utf-8')
            self.sock.sendto(payload, (self.robot_ip, self.robot_port))
            
            self.get_logger().info(f'Sent to robot - API ID: {api_id}, param: {param_str}')
            
        except Exception as e:
            self.get_logger().error(f'Error sending to robot: {e}')


def main(args=None):
    rclpy.init(args=args)
    node = RobotApiBridge()
    
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    except Exception:
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



def main(args=None):
    rclpy.init(args=args)
    node = RobotApiBridge()
    
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
