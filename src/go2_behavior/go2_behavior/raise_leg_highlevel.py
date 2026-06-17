#!/usr/bin/env python3
"""
raise_leg_highlevel.py — raises the Front Right leg using the Unitree
high-level sport API via /api/sport/request.

Uses the Go2 sport API commands:
  - StandUp (1004)     : make robot stand
  - Hello   (1016)     : built-in wave gesture (raises FR leg)
  - StandUp (1004)     : return to stand after gesture

This is the safe high-level approach — no joint-level control needed.
The robot handles all balance and safety internally.

API IDs for Go2 sport:
  1001 = Damp
  1002 = StandUp
  1003 = StandDown
  1004 = RecoveryStand
  1006 = Move (vx, vy, vyaw)
  1016 = Hello (wave front right leg)
  1017 = Stretch
  1019 = Wallow
  1022 = Dance1
  1023 = Dance2
"""

import rclpy
import json
from rclpy.node import Node
from unitree_api.msg import Request


class RaiseLegHighLevel(Node):

    # Sport API IDs
    STAND_UP       = 1002
    RECOVERY_STAND = 1004
    HELLO          = 1016   # waves FR leg — exactly what we need

    def __init__(self):
        super().__init__('raise_leg_highlevel')

        self.pub = self.create_publisher(
            Request, '/api/sport/request', 10)

        # Sequence state
        self._sequence = [
            (2.0,  self.RECOVERY_STAND, {}),   # ensure standing
            (3.0,  self.HELLO,          {}),   # raise FR leg (wave)
            (5.0,  self.RECOVERY_STAND, {}),   # return to stand
        ]
        self._index     = 0
        self._next_time = None

        # Start after 1s
        self.create_timer(1.0, self._start)
        self.get_logger().info('RaiseLegHighLevel ready')
        self.get_logger().info('Sequence: StandUp → Hello (raise FR) → StandUp')

    def _start(self):
        """Called once after 1s delay to begin sequence."""
        self._next_time = self.get_clock().now()
        self.destroy_timer(self._timers[0] if hasattr(self, '_timers') else None)
        self.create_timer(0.1, self._run)

    def _run(self):
        if self._index >= len(self._sequence):
            self.get_logger().info('Sequence complete!')
            return

        wait_time, api_id, params = self._sequence[self._index]
        now = self.get_clock().now()

        if self._next_time is None:
            self._next_time = now

        elapsed = (now - self._next_time).nanoseconds * 1e-9

        if elapsed >= wait_time:
            self._send_command(api_id, params)
            self._next_time = now
            self._index    += 1

            names = {
                self.RECOVERY_STAND: 'RecoveryStand',
                self.HELLO:          'Hello (raise FR leg)',
            }
            self.get_logger().info(
                f'Step {self._index}/{len(self._sequence)}: '
                f'{names.get(api_id, str(api_id))}'
            )

    def _send_command(self, api_id: int, params: dict):
        msg = Request()
        msg.header.identity.api_id = api_id
        msg.parameter = json.dumps(params) if params else '{}'
        self.pub.publish(msg)
        self.get_logger().info(f'Sent api_id={api_id} params={msg.parameter}')


def main(args=None):
    rclpy.init(args=args)
    node = RaiseLegHighLevel()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()