import socket, json, time, rclpy
from rclpy.node import Node
from unitree_go.msg import SportModeState

class RobotKeepalive(Node):
    def __init__(self):
        super().__init__('robot_keepalive')
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.target = ('192.168.123.161', 29999)
        self.mode = 0
        self.create_subscription(SportModeState, '/lf/sportmodestate', self.state_cb, 10)
        self.create_timer(5.0, self.keepalive)
        self.get_logger().info('Keepalive started — will recover mode every 5s')

    def state_cb(self, msg):
        self.mode = msg.mode

    def keepalive(self):
        if self.mode == 0:
            self.get_logger().info('Mode 0 detected — sending RecoveryStand')
            self.sock.sendto(json.dumps({'api_id': 1004, 'parameter': '{}'}).encode(), self.target)
            time.sleep(2)
            self.sock.sendto(json.dumps({'api_id': 1002, 'parameter': '{}'}).encode(), self.target)
            self.get_logger().info('Sent RecoveryStand + StandUp')
        else:
            self.get_logger().info(f'Mode OK: {self.mode}')

def main():
    rclpy.init()
    rclpy.spin(RobotKeepalive())

if __name__ == '__main__':
    main()
