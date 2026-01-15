"""
光谱分析仪(OSA)驱动
支持多种型号的光谱分析仪
"""
import time
import numpy as np
from typing import Optional, List, Dict, Tuple
from .base_driver import BaseDriver, SimulatedDriver


class OpticalSpectrumAnalyzer(BaseDriver):
    """光谱分析仪驱动"""
    
    def __init__(self, resource_string: str, **kwargs):
        super().__init__(resource_string, **kwargs)
        params = kwargs.get('parameters', {})
        self.start_wavelength = params.get('start_wavelength', 1520)
        self.stop_wavelength = params.get('stop_wavelength', 1580)
        self.resolution = params.get('resolution', 0.02)
        self.sensitivity = params.get('sensitivity', 'HIGH1')
        
    def _initialize(self):
        """初始化OSA"""
        self.clear_status()
        self.set_wavelength_range(self.start_wavelength, self.stop_wavelength)
        self.set_resolution(self.resolution)
        self.set_sensitivity(self.sensitivity)
        self.logger.info("光谱分析仪初始化完成")
    
    def self_test(self) -> bool:
        """自检"""
        try:
            result = self.query("*TST?")
            return result == "0"
        except Exception as e:
            self.logger.error(f"自检失败: {e}")
            return False
    
    def set_wavelength_range(self, start: float, stop: float):
        """
        设置波长范围
        
        Args:
            start: 起始波长(nm)
            stop: 终止波长(nm)
        """
        self.write(f":SENS:WAV:STAR {start}NM")
        self.write(f":SENS:WAV:STOP {stop}NM")
        self.start_wavelength = start
        self.stop_wavelength = stop
        self.logger.info(f"设置波长范围: {start} - {stop} nm")
    
    def set_center_span(self, center: float, span: float):
        """
        设置中心波长和扫描跨度
        
        Args:
            center: 中心波长(nm)
            span: 扫描跨度(nm)
        """
        self.write(f":SENS:WAV:CENT {center}NM")
        self.write(f":SENS:WAV:SPAN {span}NM")
        self.start_wavelength = center - span/2
        self.stop_wavelength = center + span/2
        self.logger.info(f"设置中心波长: {center} nm, 跨度: {span} nm")
    
    def set_resolution(self, resolution: float):
        """
        设置分辨率
        
        Args:
            resolution: 分辨率(nm)
        """
        self.write(f":SENS:BAND:RES {resolution}NM")
        self.resolution = resolution
        self.logger.info(f"设置分辨率: {resolution} nm")
    
    def set_sensitivity(self, sensitivity: str):
        """
        设置灵敏度
        
        Args:
            sensitivity: 灵敏度模式 ('NORM', 'MID', 'HIGH1', 'HIGH2', 'HIGH3')
        """
        self.write(f":SENS:SENS {sensitivity}")
        self.sensitivity = sensitivity
        self.logger.info(f"设置灵敏度: {sensitivity}")
    
    def set_reference_level(self, level: float):
        """
        设置参考电平
        
        Args:
            level: 参考电平(dBm)
        """
        self.write(f":DISP:TRAC:Y:RLEV {level}")
    
    def set_scale(self, scale: float):
        """
        设置Y轴刻度
        
        Args:
            scale: 刻度(dB/div)
        """
        self.write(f":DISP:TRAC:Y:PDIV {scale}")
    
    def single_sweep(self) -> bool:
        """
        单次扫描
        
        Returns:
            bool: 扫描是否完成
        """
        self.write(":INIT:SMOD SING")
        self.write(":INIT")
        self.wait_operation_complete(timeout=60)
        self.logger.info("单次扫描完成")
        return True
    
    def continuous_sweep(self, enable: bool = True):
        """
        连续扫描模式
        
        Args:
            enable: 是否启用连续扫描
        """
        mode = "REP" if enable else "SING"
        self.write(f":INIT:SMOD {mode}")
        if enable:
            self.write(":INIT")
    
    def stop_sweep(self):
        """停止扫描"""
        self.write(":ABOR")
    
    def get_trace_data(self, trace: str = 'A') -> Tuple[np.ndarray, np.ndarray]:
        """
        获取轨迹数据
        
        Args:
            trace: 轨迹名称 ('A', 'B', 'C', etc.)
            
        Returns:
            Tuple[np.ndarray, np.ndarray]: (波长数组, 功率数组)
        """
        # 获取波长数据
        self.write(f":TRAC:DATA:X? TR{trace}")
        wavelength_str = self.read()
        wavelengths = np.array([float(x) for x in wavelength_str.split(',')])
        
        # 获取功率数据
        self.write(f":TRAC:DATA:Y? TR{trace}")
        power_str = self.read()
        powers = np.array([float(x) for x in power_str.split(',')])
        
        return wavelengths * 1e9, powers  # 转换为nm
    
    def find_peak(self, trace: str = 'A') -> Tuple[float, float]:
        """
        查找峰值
        
        Args:
            trace: 轨迹名称
            
        Returns:
            Tuple[float, float]: (峰值波长nm, 峰值功率dBm)
        """
        self.write(f":CALC:MARK1:MAX")
        wavelength = float(self.query(":CALC:MARK1:X?")) * 1e9
        power = float(self.query(":CALC:MARK1:Y?"))
        self.logger.info(f"峰值: {wavelength:.3f} nm, {power:.2f} dBm")
        return wavelength, power
    
    def measure_smsr(self) -> float:
        """
        测量边模抑制比(SMSR)
        
        Returns:
            float: SMSR值(dB)
        """
        result = self.query(":CALC:PAR:SMSR?")
        smsr = float(result)
        self.logger.info(f"SMSR: {smsr:.2f} dB")
        return smsr
    
    def measure_3db_bandwidth(self) -> float:
        """
        测量3dB带宽
        
        Returns:
            float: 3dB带宽(nm)
        """
        result = self.query(":CALC:PAR:BWD:3DB?")
        bandwidth = float(result) * 1e9
        self.logger.info(f"3dB带宽: {bandwidth:.4f} nm")
        return bandwidth
    
    def measure_osnr(self, signal_bw: float = 0.1, noise_bw: float = 0.1) -> float:
        """
        测量光信噪比(OSNR)
        
        Args:
            signal_bw: 信号带宽(nm)
            noise_bw: 噪声带宽(nm)
            
        Returns:
            float: OSNR值(dB)
        """
        self.write(f":CALC:PAR:OSNR:SBW {signal_bw}NM")
        self.write(f":CALC:PAR:OSNR:NBW {noise_bw}NM")
        result = self.query(":CALC:PAR:OSNR?")
        osnr = float(result)
        self.logger.info(f"OSNR: {osnr:.2f} dB")
        return osnr
    
    def find_channels(self, threshold: float = -50) -> List[Dict]:
        """
        自动查找通道
        
        Args:
            threshold: 检测阈值(dBm)
            
        Returns:
            List[Dict]: 通道列表，每个通道包含波长和功率
        """
        self.write(f":CALC:PAR:ANA:THR {threshold}")
        result = self.query(":CALC:DATA:ANA?")
        
        channels = []
        # 解析响应（具体格式取决于仪器）
        # 这里提供一个通用的解析框架
        return channels
    
    def save_trace(self, filename: str, trace: str = 'A'):
        """
        保存轨迹到仪器内存
        
        Args:
            filename: 文件名
            trace: 轨迹名称
        """
        self.write(f":MMEM:STOR:TRAC TR{trace},'{filename}'")
    
    def export_csv(self, filename: str):
        """
        导出数据为CSV
        
        Args:
            filename: 本地文件路径
        """
        wavelengths, powers = self.get_trace_data()
        data = np.column_stack((wavelengths, powers))
        np.savetxt(filename, data, delimiter=',', header='Wavelength(nm),Power(dBm)', comments='')
        self.logger.info(f"数据已导出到 {filename}")


class SimulatedOSA(SimulatedDriver):
    """仿真光谱分析仪"""
    
    def __init__(self, resource_string: str, **kwargs):
        super().__init__(resource_string, **kwargs)
        self._idn = "Simulated OSA, OSA-2000, SN SIM003"
        params = kwargs.get('parameters', {})
        self.start_wavelength = params.get('start_wavelength', 1520)
        self.stop_wavelength = params.get('stop_wavelength', 1580)
        self.resolution = params.get('resolution', 0.02)
        self._peak_wavelength = 1550.0
        self._peak_power = 0.0
        
    def _initialize(self):
        self.logger.info("仿真OSA初始化完成")
    
    def self_test(self) -> bool:
        return True
    
    def set_wavelength_range(self, start: float, stop: float):
        self.start_wavelength = start
        self.stop_wavelength = stop
        self.logger.debug(f"仿真: 设置波长范围 {start}-{stop} nm")
    
    def set_center_span(self, center: float, span: float):
        self.start_wavelength = center - span/2
        self.stop_wavelength = center + span/2
    
    def set_resolution(self, resolution: float):
        self.resolution = resolution
    
    def single_sweep(self) -> bool:
        time.sleep(0.5)  # 模拟扫描时间
        self.logger.debug("仿真: 扫描完成")
        return True
    
    def get_trace_data(self, trace: str = 'A') -> Tuple[np.ndarray, np.ndarray]:
        """生成仿真光谱数据"""
        num_points = int((self.stop_wavelength - self.start_wavelength) / 0.01)
        wavelengths = np.linspace(self.start_wavelength, self.stop_wavelength, num_points)
        
        # 生成高斯峰形状的光谱
        sigma = 0.1  # 线宽
        powers = self._peak_power * np.exp(-((wavelengths - self._peak_wavelength)**2) / (2*sigma**2))
        
        # 添加噪声基底
        noise_floor = -60
        noise = np.random.normal(0, 1, num_points)
        powers = 10 * np.log10(10**(powers/10) + 10**(noise_floor/10)) + noise * 0.5
        
        return wavelengths, powers
    
    def find_peak(self, trace: str = 'A') -> Tuple[float, float]:
        return self._peak_wavelength, self._peak_power
    
    def measure_smsr(self) -> float:
        return 45.0 + np.random.normal(0, 0.5)
    
    def measure_3db_bandwidth(self) -> float:
        return 0.08 + np.random.normal(0, 0.01)
    
    def set_peak(self, wavelength: float, power: float):
        """设置仿真峰值参数"""
        self._peak_wavelength = wavelength
        self._peak_power = power
