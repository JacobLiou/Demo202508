"""
激光光源驱动
支持多种型号的激光器和可调谐激光器
"""
import time
from typing import Optional, List, Tuple
from .base_driver import BaseDriver, SimulatedDriver


class LaserSource(BaseDriver):
    """激光光源驱动"""
    
    def __init__(self, resource_string: str, **kwargs):
        super().__init__(resource_string, **kwargs)
        params = kwargs.get('parameters', {})
        self.wavelength_range = params.get('wavelength_range', [1500, 1630])
        self.power_range = params.get('power_range', [-10, 10])
        self.default_power = params.get('default_power', 0)
        self.default_wavelength = params.get('default_wavelength', 1550)
        self.current_wavelength = self.default_wavelength
        self.current_power = self.default_power
        self.output_enabled = False
        self.channel = 1
        
    def _initialize(self):
        """初始化激光器"""
        self.clear_status()
        self.set_wavelength(self.default_wavelength)
        self.set_power(self.default_power)
        self.logger.info("激光光源初始化完成")
    
    def self_test(self) -> bool:
        """自检"""
        try:
            result = self.query("*TST?")
            return result == "0"
        except Exception as e:
            self.logger.error(f"自检失败: {e}")
            return False
    
    def set_wavelength(self, wavelength: float):
        """
        设置波长
        
        Args:
            wavelength: 波长(nm)
        """
        if not self.wavelength_range[0] <= wavelength <= self.wavelength_range[1]:
            raise ValueError(f"波长必须在 {self.wavelength_range[0]}-{self.wavelength_range[1]} nm 范围内")
        
        self.write(f":SOUR{self.channel}:WAV {wavelength}NM")
        self.current_wavelength = wavelength
        time.sleep(0.1)  # 等待波长稳定
        self.logger.info(f"设置波长: {wavelength} nm")
    
    def get_wavelength(self) -> float:
        """获取当前波长"""
        response = self.query(f":SOUR{self.channel}:WAV?")
        self.current_wavelength = float(response) * 1e9
        return self.current_wavelength
    
    def set_power(self, power: float):
        """
        设置输出功率
        
        Args:
            power: 功率(dBm)
        """
        if not self.power_range[0] <= power <= self.power_range[1]:
            raise ValueError(f"功率必须在 {self.power_range[0]}-{self.power_range[1]} dBm 范围内")
        
        self.write(f":SOUR{self.channel}:POW {power}DBM")
        self.current_power = power
        self.logger.info(f"设置功率: {power} dBm")
    
    def get_power(self) -> float:
        """获取当前功率设置"""
        response = self.query(f":SOUR{self.channel}:POW?")
        self.current_power = float(response)
        return self.current_power
    
    def output_on(self):
        """打开激光输出"""
        self.write(f":SOUR{self.channel}:POW:STAT 1")
        self.output_enabled = True
        time.sleep(0.5)  # 等待激光稳定
        self.logger.info("激光输出已打开")
    
    def output_off(self):
        """关闭激光输出"""
        self.write(f":SOUR{self.channel}:POW:STAT 0")
        self.output_enabled = False
        self.logger.info("激光输出已关闭")
    
    def get_output_state(self) -> bool:
        """获取输出状态"""
        response = self.query(f":SOUR{self.channel}:POW:STAT?")
        self.output_enabled = response == "1"
        return self.output_enabled
    
    def set_coherence_control(self, enable: bool):
        """
        设置相干控制（减少干涉）
        
        Args:
            enable: 是否启用相干控制
        """
        state = 1 if enable else 0
        self.write(f":SOUR{self.channel}:AM:STAT {state}")
    
    def wavelength_sweep(self, start: float, stop: float, step: float = 0.1, 
                         dwell_time: float = 0.1):
        """
        波长扫描
        
        Args:
            start: 起始波长(nm)
            stop: 终止波长(nm)
            step: 步进(nm)
            dwell_time: 每步停留时间(秒)
            
        Yields:
            float: 当前波长
        """
        current = start
        direction = 1 if stop > start else -1
        step = abs(step) * direction
        
        while (direction > 0 and current <= stop) or (direction < 0 and current >= stop):
            self.set_wavelength(current)
            time.sleep(dwell_time)
            yield current
            current += step
    
    def power_sweep(self, start: float, stop: float, step: float = 0.5,
                    dwell_time: float = 0.1):
        """
        功率扫描
        
        Args:
            start: 起始功率(dBm)
            stop: 终止功率(dBm)
            step: 步进(dB)
            dwell_time: 每步停留时间(秒)
            
        Yields:
            float: 当前功率
        """
        current = start
        direction = 1 if stop > start else -1
        step = abs(step) * direction
        
        while (direction > 0 and current <= stop) or (direction < 0 and current >= stop):
            self.set_power(current)
            time.sleep(dwell_time)
            yield current
            current += step
    
    def configure_for_test(self, wavelength: float, power: float):
        """
        配置测试参数
        
        Args:
            wavelength: 波长(nm)
            power: 功率(dBm)
        """
        self.set_wavelength(wavelength)
        self.set_power(power)
        self.output_on()
    
    def get_actual_power(self) -> float:
        """
        获取实际输出功率（通过内置功率计）
        
        Returns:
            float: 实际功率(dBm)
        """
        response = self.query(f":SOUR{self.channel}:POW:ACT?")
        return float(response)
    
    def enable_auto_power_control(self, enable: bool = True):
        """
        启用自动功率控制(APC)
        
        Args:
            enable: 是否启用
        """
        state = 1 if enable else 0
        self.write(f":SOUR{self.channel}:POW:MODE {'APC' if enable else 'MAN'}")
        self.logger.info(f"自动功率控制: {'启用' if enable else '禁用'}")


class SimulatedLaserSource(SimulatedDriver):
    """仿真激光光源"""
    
    def __init__(self, resource_string: str, **kwargs):
        super().__init__(resource_string, **kwargs)
        self._idn = "Simulated Laser Source, TLS-1000, SN SIM004"
        params = kwargs.get('parameters', {})
        self.wavelength_range = params.get('wavelength_range', [1500, 1630])
        self.power_range = params.get('power_range', [-10, 10])
        self.current_wavelength = params.get('default_wavelength', 1550)
        self.current_power = params.get('default_power', 0)
        self.output_enabled = False
        
    def _initialize(self):
        self.logger.info("仿真激光光源初始化完成")
    
    def self_test(self) -> bool:
        return True
    
    def set_wavelength(self, wavelength: float):
        if not self.wavelength_range[0] <= wavelength <= self.wavelength_range[1]:
            raise ValueError(f"波长超出范围: {wavelength}")
        self.current_wavelength = wavelength
        self.logger.debug(f"仿真: 设置波长 {wavelength} nm")
    
    def get_wavelength(self) -> float:
        return self.current_wavelength
    
    def set_power(self, power: float):
        if not self.power_range[0] <= power <= self.power_range[1]:
            raise ValueError(f"功率超出范围: {power}")
        self.current_power = power
        self.logger.debug(f"仿真: 设置功率 {power} dBm")
    
    def get_power(self) -> float:
        return self.current_power
    
    def output_on(self):
        self.output_enabled = True
        self.logger.debug("仿真: 激光输出打开")
    
    def output_off(self):
        self.output_enabled = False
        self.logger.debug("仿真: 激光输出关闭")
    
    def get_output_state(self) -> bool:
        return self.output_enabled
    
    def wavelength_sweep(self, start: float, stop: float, step: float = 0.1,
                         dwell_time: float = 0.1):
        current = start
        direction = 1 if stop > start else -1
        step = abs(step) * direction
        
        while (direction > 0 and current <= stop) or (direction < 0 and current >= stop):
            self.set_wavelength(current)
            time.sleep(dwell_time * 0.1)  # 仿真时缩短等待
            yield current
            current += step
    
    def configure_for_test(self, wavelength: float, power: float):
        self.set_wavelength(wavelength)
        self.set_power(power)
        self.output_on()
