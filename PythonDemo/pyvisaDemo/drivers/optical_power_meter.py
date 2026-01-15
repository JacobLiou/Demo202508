"""
光功率计驱动
支持多种型号的光功率计仪器
"""
import time
import random
from typing import Optional, List, Dict
from .base_driver import BaseDriver, SimulatedDriver


class OpticalPowerMeter(BaseDriver):
    """光功率计驱动"""
    
    def __init__(self, resource_string: str, **kwargs):
        super().__init__(resource_string, **kwargs)
        self.wavelength = kwargs.get('parameters', {}).get('wavelength', 1550)
        self.unit = kwargs.get('parameters', {}).get('unit', 'dBm')
        self.averaging_time = kwargs.get('parameters', {}).get('averaging_time', 0.1)
        self.channel = 1
        
    def _initialize(self):
        """初始化光功率计"""
        self.clear_status()
        self.set_wavelength(self.wavelength)
        self.set_unit(self.unit)
        self.set_averaging_time(self.averaging_time)
        self.logger.info("光功率计初始化完成")
    
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
        设置测量波长
        
        Args:
            wavelength: 波长(nm)
        """
        self.write(f"SENS{self.channel}:POW:WAV {wavelength}NM")
        self.wavelength = wavelength
        self.logger.info(f"设置波长: {wavelength} nm")
    
    def get_wavelength(self) -> float:
        """获取当前波长"""
        response = self.query(f"SENS{self.channel}:POW:WAV?")
        return float(response) * 1e9  # 转换为nm
    
    def set_unit(self, unit: str):
        """
        设置功率单位
        
        Args:
            unit: 单位 ('dBm' or 'W')
        """
        unit_code = 0 if unit.upper() == 'DBM' else 1
        self.write(f"SENS{self.channel}:POW:UNIT {unit_code}")
        self.unit = unit
        self.logger.info(f"设置单位: {unit}")
    
    def set_averaging_time(self, avg_time: float):
        """
        设置平均时间
        
        Args:
            avg_time: 平均时间(秒)
        """
        self.write(f"SENS{self.channel}:POW:ATIME {avg_time}")
        self.averaging_time = avg_time
        self.logger.info(f"设置平均时间: {avg_time} s")
    
    def set_range(self, range_dbm: float):
        """
        设置测量范围
        
        Args:
            range_dbm: 范围(dBm)
        """
        self.write(f"SENS{self.channel}:POW:RANG {range_dbm}")
    
    def set_auto_range(self, enable: bool = True):
        """
        设置自动量程
        
        Args:
            enable: 是否启用自动量程
        """
        state = 1 if enable else 0
        self.write(f"SENS{self.channel}:POW:RANG:AUTO {state}")
    
    def measure_power(self) -> float:
        """
        测量光功率
        
        Returns:
            float: 光功率值(dBm或W，取决于当前单位设置)
        """
        response = self.query(f"READ{self.channel}:POW?")
        power = float(response)
        self.logger.debug(f"测量功率: {power} {self.unit}")
        return power
    
    def measure_power_with_wavelength(self, wavelength: float) -> float:
        """
        在指定波长测量光功率
        
        Args:
            wavelength: 波长(nm)
            
        Returns:
            float: 光功率值
        """
        self.set_wavelength(wavelength)
        time.sleep(0.1)  # 等待设置生效
        return self.measure_power()
    
    def measure_multiple(self, count: int = 10, interval: float = 0.1) -> List[float]:
        """
        多次测量取平均
        
        Args:
            count: 测量次数
            interval: 测量间隔(秒)
            
        Returns:
            List[float]: 测量值列表
        """
        measurements = []
        for i in range(count):
            power = self.measure_power()
            measurements.append(power)
            if i < count - 1:
                time.sleep(interval)
        return measurements
    
    def zero_calibration(self):
        """零点校准"""
        self.write(f"SENS{self.channel}:CORR:COLL:ZERO")
        self.wait_operation_complete(timeout=30)
        self.logger.info("零点校准完成")
    
    def reference_calibration(self) -> float:
        """
        参考校准（存储当前功率作为参考）
        
        Returns:
            float: 参考功率值
        """
        ref_power = self.measure_power()
        self.write(f"SENS{self.channel}:CORR:COLL:REF:VAL {ref_power}")
        self.logger.info(f"参考功率: {ref_power} {self.unit}")
        return ref_power
    
    def get_relative_power(self, reference: float) -> float:
        """
        获取相对功率（相对于参考值）
        
        Args:
            reference: 参考功率值(dBm)
            
        Returns:
            float: 相对功率(dB)
        """
        current_power = self.measure_power()
        relative = current_power - reference
        return relative


class SimulatedOpticalPowerMeter(SimulatedDriver):
    """仿真光功率计"""
    
    def __init__(self, resource_string: str, **kwargs):
        super().__init__(resource_string, **kwargs)
        self._idn = "Simulated Optical Power Meter, OPM-1000, SN SIM001"
        self.wavelength = kwargs.get('parameters', {}).get('wavelength', 1550)
        self.unit = 'dBm'
        self._base_power = -10.0
        
    def _initialize(self):
        self.logger.info("仿真光功率计初始化完成")
    
    def self_test(self) -> bool:
        return True
    
    def set_wavelength(self, wavelength: float):
        self.wavelength = wavelength
        self.logger.debug(f"仿真: 设置波长 {wavelength} nm")
    
    def measure_power(self) -> float:
        # 模拟测量值，加入随机噪声
        noise = random.gauss(0, 0.05)
        power = self._base_power + noise
        self.logger.debug(f"仿真: 测量功率 {power:.3f} dBm")
        return round(power, 3)
    
    def measure_power_with_wavelength(self, wavelength: float) -> float:
        self.set_wavelength(wavelength)
        return self.measure_power()
    
    def measure_multiple(self, count: int = 10, interval: float = 0.1) -> List[float]:
        return [self.measure_power() for _ in range(count)]
    
    def zero_calibration(self):
        self.logger.info("仿真: 零点校准完成")
    
    def reference_calibration(self) -> float:
        return self._base_power
    
    def set_base_power(self, power: float):
        """设置仿真基准功率"""
        self._base_power = power
