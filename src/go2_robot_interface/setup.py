from setuptools import find_packages, setup

package_name = 'go2_robot_interface'

setup(
    name=package_name,
    version='0.0.0',
    packages=find_packages(exclude=['test']),
    data_files=[
        ('share/ament_index/resource_index/packages',
            ['resource/' + package_name]),
        ('share/' + package_name, ['package.xml']),
    ],
    install_requires=['setuptools'],
    zip_safe=True,
    maintainer='haii',
    maintainer_email='haii@todo.todo',
    description='TODO: Package description',
    license='TODO: License declaration',
    extras_require={
        'test': [
            'pytest',
        ],
    },
    entry_points={
    'console_scripts': [
        'cmd_vel_bridge = go2_robot_interface.cmd_vel_bridge:main',
        'go2_move_bridge = go2_robot_interface.go2_move_bridge:main',
        'robot_odom_bridge = go2_robot_interface.robot_odom_bridge:main',
    ],
},
)
