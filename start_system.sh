#!/bin/bash
set -e

cd /home/haii/BachlorArbeit-
source /opt/ros/humble/setup.bash
source install/setup.bash

echo "🚀 Starting Go2 Navigation System..."
echo ""

# Kill any existing processes
pkill -f "ros_tcp_bridge|goal_navigation|cmd_vel_bridge|robot_api" 2>/dev/null || true
sleep 1

# Terminal 1: TCP Bridge
echo "[1/4] Starting TCP Bridge on 0.0.0.0:10000..."
python3 ros_tcp_bridge_server.py > /tmp/tcp_bridge.log 2>&1 &
TCP_PID=$!
echo "      PID: $TCP_PID"

# Terminal 2: Goal Navigation  
echo "[2/4] Starting Goal Navigation Node..."
ros2 run go2_behavior goal_navigation_node > /tmp/nav.log 2>&1 &
NAV_PID=$!
echo "      PID: $NAV_PID"

# Terminal 3: cmd_vel Bridge
echo "[3/4] Starting cmd_vel Bridge..."
ros2 run go2_robot_interface cmd_vel_bridge > /tmp/vel.log 2>&1 &
VEL_PID=$!
echo "      PID: $VEL_PID"

# Terminal 4: Robot API Bridge
echo "[4/4] Starting Robot API Bridge (→ 192.168.1.7:29999)..."
python3 robot_api_bridge.py > /tmp/api_bridge.log 2>&1 &
API_PID=$!
echo "      PID: $API_PID"

sleep 3

echo ""
echo "📊 Status:"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if ps -p $TCP_PID > /dev/null; then
    echo "✅ TCP Bridge: Running"
else
    echo "❌ TCP Bridge: Failed"
    echo "   Log: $(tail -1 /tmp/tcp_bridge.log)"
fi

if ps -p $NAV_PID > /dev/null; then
    echo "✅ Navigation: Running"
else
    echo "❌ Navigation: Failed"
    echo "   Log: $(tail -1 /tmp/nav.log)"
fi

if ps -p $VEL_PID > /dev/null; then
    echo "✅ cmd_vel Bridge: Running"
else
    echo "❌ cmd_vel Bridge: Failed"
    echo "   Log: $(tail -1 /tmp/vel.log)"
fi

if ps -p $API_PID > /dev/null; then
    echo "✅ Robot API Bridge: Running"
else
    echo "❌ Robot API Bridge: Failed"
    echo "   Log: $(tail -1 /tmp/api_bridge.log)"
fi

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "💡 Next steps:"
echo "   1. Power on Go2 robot (press power button ~3 sec)"
echo "   2. Wait 60 seconds for boot"
echo "   3. Click in Unity → Dog moves when online!"
echo ""
echo "📖 Logs:"
echo "   TCP Bridge: tail -f /tmp/tcp_bridge.log"
echo "   Navigation: tail -f /tmp/nav.log"
echo "   cmd_vel:    tail -f /tmp/vel.log"
echo "   API Bridge: tail -f /tmp/api_bridge.log"
echo ""

# Keep script running
wait
