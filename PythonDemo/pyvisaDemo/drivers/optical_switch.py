"""
光开关驱动
支持多种光开关设备
"""
import time
from typing import Optional, List, Tuple
from .base_driver import BaseDriver, SimulatedDriver


class OpticalSwitch(BaseDriver):
    """光开关驱动"""
    
    def __init__(self, resource_string: str, **kwargs):
        super().__init__(resource_string, **kwargs)
        self.channels = kwargs.get('parameters', {}).get('channels', 8)
        self.switch_time = kwargs.get('parameters', {}).get('switch_time', 0.1)
        self.current_channel = 1
        
    def _initialize(self):
        """初始化光开关"""
        self.clear_status()
        self.logger.info(f"光开关初始化完成，通道数: {self.channels}")
    
    def self_test(self) -> bool:
        """自检"""
        try:
            result = self.query("*TST?")
            return result == "0"
        except Exception as e:
            self.logger.error(f"自检失败: {e}")
            return False
    
    def switch_channel(self, channel: int):
        """
        切换到指定通道
        
        Args:
            channel: 目标通道号(1-N)
        """
        if channel < 1 or channel > self.channels:
            raise ValueError(f"通道号必须在1到{self.channels}之间")
        
        self.write(f"ROUT:CHAN {channel}")
        time.sleep(self.switch_time)  # 等待开关动作完成
        self.current_channel = channel
        self.logger.info(f"切换到通道 {channel}")
    
    def get_current_channel(self) -> int:
        """获取当前通道"""
        response = self.query("ROUT:CHAN?")
        self.current_channel = int(response)
        return self.current_channel
    
    def get_channel_count(self) -> int:
        """获取通道总数"""
        response = self.query("ROUT:CHAN:COUN?")
        return int(response)
    
    def switch_to_next(self) -> int:
        """
        切换到下一个通道
        
        Returns:
            int: 新的通道号
        """
        next_channel = (self.current_channel % self.channels) + 1
        self.switch_channel(next_channel)
        return next_channel
    
    def switch_to_previous(self) -> int:
        """
        切换到上一个通道
        
        Returns:
            int: 新的通道号
        """
        prev_channel = ((self.current_channel - 2) % self.channels) + 1
        self.switch_channel(prev_channel)
        return prev_channel
    
    def scan_all_channels(self, dwell_time: float = 1.0):
        """
        扫描所有通道
        
        Args:
            dwell_time: 每个通道停留时间(秒)
            
        Yields:
            int: 当前通道号
        """
        for ch in range(1, self.channels + 1):
            self.switch_channel(ch)
            yield ch
            time.sleep(dwell_time)
    
    def set_switch_speed(self, speed: str):
        """
        设置切换速度
        
        Args:
            speed: 速度模式 ('FAST', 'NORMAL', 'SLOW')
        """
        self.write(f"ROUT:SWIT:SPEED {speed}")
    
    def configure_route(self, input_port: int, output_port: int):
        """
        配置路由（用于矩阵开关）
        
        Args:
            input_port: 输入端口
            output_port: 输出端口
        """
        self.write(f"ROUT:CLOS (@{input_port},{output_port})")
        self.logger.info(f"配置路由: 输入{input_port} -> 输出{output_port}")
    
    def disconnect_route(self, input_port: int, output_port: int):
        """
        断开路由
        
        Args:
            input_port: 输入端口
            output_port: 输出端口
        """
        self.write(f"ROUT:OPEN (@{input_port},{output_port})")
    
    def get_all_routes(self) -> List[Tuple[int, int]]:
        """
        获取所有已配置的路由
        
        Returns:
            List[Tuple[int, int]]: 路由列表 [(input, output), ...]
        """
        response = self.query("ROUT:CLOS:STAT?")
        # 解析响应并返回路由列表
        routes = []
        # 具体解析逻辑取决于仪器型号
        return routes
    
    def reset_all_routes(self):
        """断开所有路由"""
        self.write("ROUT:OPEN:ALL")
        self.logger.info("已断开所有路由")


class SimulatedOpticalSwitch(SimulatedDriver):
    """仿真光开关"""
    
    def __init__(self, resource_string: str, **kwargs):
        super().__init__(resource_string, **kwargs)
        self._idn = "Simulated Optical Switch, OSW-100, SN SIM002"
        self.channels = kwargs.get('parameters', {}).get('channels', 8)
        self.current_channel = 1
        self._routes = []
        
    def _initialize(self):
        self.logger.info(f"仿真光开关初始化完成，通道数: {self.channels}")
    
    def self_test(self) -> bool:
        return True
    
    def switch_channel(self, channel: int):
        if channel < 1 or channel > self.channels:
            raise ValueError(f"通道号必须在1到{self.channels}之间")
        self.current_channel = channel
        self.logger.debug(f"仿真: 切换到通道 {channel}")
    
    def get_current_channel(self) -> int:
        return self.current_channel
    
    def get_channel_count(self) -> int:
        return self.channels
    
    def scan_all_channels(self, dwell_time: float = 1.0):
        for ch in range(1, self.channels + 1):
            self.switch_channel(ch)
            yield ch
            time.sleep(dwell_time * 0.1)  # 仿真时缩短等待时间
    
    def configure_route(self, input_port: int, output_port: int):
        self._routes.append((input_port, output_port))
        self.logger.debug(f"仿真: 配置路由 {input_port} -> {output_port}")
    
    def reset_all_routes(self):
        self._routes.clear()
        self.logger.debug("仿真: 清除所有路由")
