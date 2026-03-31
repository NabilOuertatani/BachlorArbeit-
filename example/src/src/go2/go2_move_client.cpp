#include <chrono>
#include <memory>
#include <thread>
#include <cstdlib>
#include <iostream>

#include "rclcpp/rclcpp.hpp"
#include "common/ros2_sport_client.h"

using namespace std::chrono_literals;

class Go2MoveClientNode : public rclcpp::Node {
public:
  Go2MoveClientNode(float vx, float vy, float yaw, double duration_sec)
      : Node("go2_move_client_node"), sport_client_(this),
        vx_(vx), vy_(vy), yaw_(yaw), duration_sec_(duration_sec) {}

  void Run() {
    unitree_api::msg::Request req;

    auto start = this->now();
    rclcpp::WallRate rate(10);

    while (rclcpp::ok() &&
           (this->now() - start).seconds() < duration_sec_) {
      sport_client_.Move(req, vx_, vy_, yaw_);
      rclcpp::spin_some(this->get_node_base_interface());
      rate.sleep();
    }

    sport_client_.StopMove(req);
    rclcpp::spin_some(this->get_node_base_interface());
    RCLCPP_INFO(this->get_logger(), "Movement finished, StopMove sent.");
  }

private:
  SportClient sport_client_;
  float vx_;
  float vy_;
  float yaw_;
  double duration_sec_;
};

int main(int argc, char **argv) {
  rclcpp::init(argc, argv);

  if (argc != 5) {
    std::cerr << "Usage: go2_move_client <vx> <vy> <yaw> <duration_sec>\n";
    std::cerr << "Example: go2_move_client 0.1 0.0 0.0 2.0\n";
    return 1;
  }

  float vx = std::stof(argv[1]);
  float vy = std::stof(argv[2]);
  float yaw = std::stof(argv[3]);
  double duration_sec = std::stod(argv[4]);

  auto node = std::make_shared<Go2MoveClientNode>(vx, vy, yaw, duration_sec);
  node->Run();

  rclcpp::shutdown();
  return 0;
}
