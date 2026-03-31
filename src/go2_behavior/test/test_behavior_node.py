"""Unit tests for behavior_node."""

import unittest
from unittest.mock import MagicMock, patch, call
import rclpy
from geometry_msgs.msg import Twist
from go2_behavior.behavior_node import BehaviorNode


class TestBehaviorNode(unittest.TestCase):
    """Test cases for BehaviorNode class."""

    def setUp(self):
        """Set up test fixtures."""
        # Initialize rclpy if not already done
        if not rclpy.ok():
            rclpy.init()

    def tearDown(self):
        """Clean up after tests."""
        if rclpy.ok():
            rclpy.shutdown()

    def test_behavior_node_initialization(self):
        """Test that BehaviorNode initializes correctly."""
        node = BehaviorNode()
        self.assertEqual(node.get_name(), 'behavior_node')
        self.assertIsNotNone(node.cmd_pub)
        self.assertIsNotNone(node.timer)
        node.destroy_node()

    def test_publish_cmd_default_values(self):
        """Test publish_cmd with default values."""
        node = BehaviorNode()
        node.cmd_pub = MagicMock()

        # Call publish_cmd with defaults
        node.publish_cmd()

        # Verify publish was called
        node.cmd_pub.publish.assert_called_once()
        msg = node.cmd_pub.publish.call_args[0][0]
        self.assertEqual(msg.linear.x, 0.0)
        self.assertEqual(msg.linear.y, 0.0)
        self.assertEqual(msg.angular.z, 0.0)

        node.destroy_node()

    def test_publish_cmd_with_values(self):
        """Test publish_cmd with specific velocity values."""
        node = BehaviorNode()
        node.cmd_pub = MagicMock()

        # Call publish_cmd with specific values
        node.publish_cmd(vx=0.5, vy=0.1, wz=0.2)

        # Verify publish was called with correct values
        node.cmd_pub.publish.assert_called_once()
        msg = node.cmd_pub.publish.call_args[0][0]
        self.assertEqual(msg.linear.x, 0.5)
        self.assertEqual(msg.linear.y, 0.1)
        self.assertEqual(msg.angular.z, 0.2)

        node.destroy_node()

    def test_timer_callback_logic(self):
        """Test the timer callback motion logic."""
        node = BehaviorNode()
        node.cmd_pub = MagicMock()

        # Mock the clock to control time progression
        with patch.object(node, 'get_clock') as mock_clock:
            # Create mock time objects
            start_time = MagicMock()
            start_time.nanoseconds = 0

            # Test Phase 1: pause (t < 1.0)
            current_time_1 = MagicMock()
            current_time_1.nanoseconds = 0.5e9  # 0.5 seconds
            current_time_1.__sub__ = MagicMock(return_value=MagicMock(nanoseconds=0.5e9))

            mock_clock.return_value.now.return_value = current_time_1
            node.start_time = start_time

            node.timer_callback()
            call_args = node.cmd_pub.publish.call_args[0][0]
            self.assertEqual(call_args.linear.x, 0.0)
            self.assertEqual(call_args.linear.y, 0.0)
            self.assertEqual(call_args.angular.z, 0.0)

            node.cmd_pub.reset_mock()

            # Test Phase 2: forward (1.0 <= t < 2.0)
            current_time_2 = MagicMock()
            current_time_2.nanoseconds = 1.5e9  # 1.5 seconds
            current_time_2.__sub__ = MagicMock(return_value=MagicMock(nanoseconds=1.5e9))
            mock_clock.return_value.now.return_value = current_time_2

            node.timer_callback()
            call_args = node.cmd_pub.publish.call_args[0][0]
            self.assertEqual(call_args.linear.x, 0.3)
            self.assertEqual(call_args.linear.y, 0.0)

            node.cmd_pub.reset_mock()

            # Test Phase 3: backward (2.0 <= t < 4.0)
            current_time_3 = MagicMock()
            current_time_3.nanoseconds = 3.0e9  # 3.0 seconds
            current_time_3.__sub__ = MagicMock(return_value=MagicMock(nanoseconds=3.0e9))
            mock_clock.return_value.now.return_value = current_time_3

            node.timer_callback()
            call_args = node.cmd_pub.publish.call_args[0][0]
            self.assertEqual(call_args.linear.x, -0.3)

        node.destroy_node()


if __name__ == '__main__':
    unittest.main()
