#!/usr/bin/env python3
import sys
import rclpy
from unitree_api.msg import Request
import json
import time

def main():
    rclpy.init()
    node = rclpy.create_node('wakeup_node')
    pub = node.create_publisher(Request, '/api/sport/request', 10)
    
    time.sleep(0.5)
    
    # Send WAKEUP command (API ID 1002)
    msg = Request()
    msg.header.identity.api_id = 1002
    msg.parameter = json.dumps({})
    pub.publish(msg)
    
    print("✓ WAKEUP command sent (API ID 1002)")
    print("Robot should now be responsive to movement commands")
    
    time.sleep(0.5)
    node.destroy_node()
    rclpy.shutdown()

if __name__ == '__main__':
    main()
