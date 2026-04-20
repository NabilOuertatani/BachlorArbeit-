from setuptools import find_packages, setup
from glob import glob
import os

package_name = 'go2_behavior'

setup(
    name=package_name,
    version='0.0.0',
    packages=find_packages(exclude=['test']),
    data_files=[
        (
            'share/ament_index/resource_index/packages',
            ['resource/' + package_name]
        ),
        (
            'share/' + package_name,
            ['package.xml']
        ),
        (
            os.path.join('share', package_name, 'launch'),
            glob('launch/*.launch.py')
        ),
    ],
    install_requires=['setuptools'],
    zip_safe=True,
    maintainer='haii',
    maintainer_email='haii@todo.todo',
    description='TODO: Package description',
    license='TODO: License declaration',
    extras_require={
        'test': ['pytest'],
    },
    entry_points={
    'console_scripts': [
        'tcp_bridge_server     = your_package.tcp_bridge_server:main',
        'goal_navigation_node  = your_package.goal_navigation_node:main',
        'cmd_vel_bridge        = your_package.cmd_vel_bridge:main',
        'pose_forwarder        = your_package.pose_forwarder:main',   # add
        'cloud_forwarder       = your_package.cloud_forwarder:main',  # add
    ],
},
)