#!/usr/bin/env python3
"""
dynamic_gesture_handler.py — Gesture logging/monitoring node.

Receives /api/sport_request from the TCP bridge and logs it for debugging.
Does NOT republish — robot_api_bridge.py directly handles forwarding to UDP.

Flow:
    ros_tcp_bridge_server → /api/sport_request → robot_api_bridge → UDP

This node just monitors and logs for debugging.

Gesture IDs (any unlisted ID also passes through automatically):
    1001 Damp  1002 StandUp  1003 StandDown  1004 RecoveryStand
    1016 Hello  1017 Stretch  1019 Wallow  1022 Dance1  1023 Dance2
"""

import rclpy
import json
from rclpy.node import Node
from unitree_api.msg import Request


class DynamicGestureHandler(Node):
    """
    Dynamically handle gesture commands from Unity.
    Subscribes to /api/sport_request and forwards to robot API bridge.
    """

    def __init__(self):
        super().__init__('dynamic_gesture_handler')

        # Subscriber to receive gesture commands from Unity (via TCP bridge)
        # NOTE: We do NOT republish — just log and let robot_api_bridge handle it
        self.create_subscription(
            Request,
            '/api/sport_request',
            self._on_gesture_request,
            10
        )

        self.get_logger().info('═══════════════════════════════════════')
        self.get_logger().info('Dynamic Gesture Handler Started')
        self.get_logger().info('─────────────────────────────────────')
        self.get_logger().info('✓ Subscribes to: /api/sport_request')
        self.get_logger().info('✓ Logs & monitors gestures only (NO republish)')
        self.get_logger().info('✓ robot_api_bridge handles actual forwarding')
        self.get_logger().info('✓ Accepts ANY gesture API ID dynamically')
        self.get_logger().info('✓ Does NOT handle movement (use goal_navigation_node)')
        self.get_logger().info('═══════════════════════════════════════')

    def _on_gesture_request(self, msg: Request):
        """
        Monitor gesture commands for logging/debugging.
        Do NOT republish — robot_api_bridge directly handles /api/sport_request.
        """
        try:
            api_id = msg.header.identity.api_id
            parameter = msg.parameter if msg.parameter else '{}'

            # Log the gesture for debugging
            self._log_gesture_name(api_id, parameter)

            self.get_logger().info(
                f'✓ Gesture received: api_id={api_id}, param={parameter}'
            )

        except Exception as e:
            self.get_logger().error(f'Error handling gesture: {e}')

    def _log_gesture_name(self, api_id: int, parameter: str):
        """Lookup and log gesture name for debugging."""
        gesture_names = {
            1001: 'Damp (disable)',
            1002: 'StandUp',
            1003: 'StandDown (sit)',
            1004: 'RecoveryStand',
            1006: 'Move (simple)',
            1008: 'Move (continuous)',
            1016: 'Hello (Raise Hand)',
            1017: 'Stretch',
            1019: 'Wallow',
            1022: 'Dance1',
            1023: 'Dance2',
        }
        name = gesture_names.get(api_id, f'Unknown({api_id})')
        self.get_logger().info(f'→ Received: {name} [api_id={api_id}]')


def main(args=None):
    rclpy.init(args=args)
    node = DynamicGestureHandler()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()