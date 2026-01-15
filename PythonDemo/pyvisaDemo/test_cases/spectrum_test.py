"""
光谱测试
分析光源或器件的光谱特性
"""
import time
from typing import Dict, List, Any, Tuple
import numpy as np

from .base_test import BaseTest


class SpectrumTest(BaseTest):
    """光谱分析测试"""
    
    def __init__(self, config: Dict, instruments: Dict, result):
        super().__init__(config, instruments, result)
        
        # 获取仪器
        self.laser = self.get_instrument('laser_source')
        self.osa = self.get_instrument('osa')
        
        # 测试参数
        self.center_wavelength = self.parameters.get('center_wavelength', 1550)
        self.span = self.parameters.get('span', 20)
        self.resolution = self.parameters.get('resolution', 0.02)
        self.sensitivity = self.parameters.get('sensitivity', 'HIGH1')
        
        # 测试数据
        self.wavelength_data: np.ndarray = None
        self.power_data: np.ndarray = None
        self.peak_info: Dict = {}
        self.spectrum_analysis: Dict = {}
    
    def configure_osa(self, **kwargs) -> bool:
        """
        配置光谱分析仪
        
        Returns:
            bool: 配置是否成功
        """
        self.logger.info("配置OSA...")
        
        try:
            # 设置波长范围
            self.osa.set_center_span(self.center_wavelength, self.span)
            
            # 设置分辨率
            self.osa.set_resolution(self.resolution)
            
            # 设置灵敏度
            self.osa.set_sensitivity(self.sensitivity)
            
            self.logger.info(
                f"OSA配置完成: 中心波长={self.center_wavelength}nm, "
                f"跨度={self.span}nm, 分辨率={self.resolution}nm"
            )
            return True
            
        except Exception as e:
            self.logger.error(f"OSA配置失败: {e}")
            return False
    
    def calibrate_reference(self, **kwargs) -> float:
        """
        校准参考（对于光谱测试，这里返回峰值功率）
        
        Returns:
            float: 峰值功率
        """
        self.scan_spectrum()
        wavelength, power = self.osa.find_peak()
        return power
    
    def scan_spectrum(self, **kwargs) -> Tuple[np.ndarray, np.ndarray]:
        """
        扫描光谱
        
        Returns:
            Tuple: (波长数组, 功率数组)
        """
        self.logger.info("执行光谱扫描...")
        
        # 确保激光器输出
        if self.laser:
            self.laser.set_wavelength(self.center_wavelength)
            self.laser.output_on()
            time.sleep(0.5)
        
        # 执行扫描
        self.osa.single_sweep()
        
        # 获取数据
        self.wavelength_data, self.power_data = self.osa.get_trace_data()
        
        self.logger.info(f"光谱扫描完成，数据点数: {len(self.wavelength_data)}")
        
        return self.wavelength_data, self.power_data
    
    def analyze_peaks(self, **kwargs) -> Dict:
        """
        分析光谱峰值
        
        Returns:
            Dict: 峰值分析结果
        """
        self.logger.info("分析光谱峰值...")
        
        if self.wavelength_data is None:
            self.scan_spectrum()
        
        # 查找主峰
        peak_wavelength, peak_power = self.osa.find_peak()
        
        self.peak_info = {
            'wavelength': peak_wavelength,
            'power': peak_power
        }
        
        # 测量SMSR
        try:
            smsr = self.osa.measure_smsr()
            self.peak_info['smsr'] = smsr
            self.add_measurement('SMSR', smsr, 'dB')
            
            # 检查限值
            min_smsr = self.pass_criteria.get('min_smsr', 0)
            self.check_limit('SMSR', smsr, min_val=min_smsr)
        except:
            self.logger.warning("无法测量SMSR")
        
        # 测量3dB带宽
        try:
            bandwidth = self.osa.measure_3db_bandwidth()
            self.peak_info['bandwidth_3db'] = bandwidth
            self.add_measurement('3dB_bandwidth', bandwidth, 'nm')
            
            # 检查限值
            max_linewidth = self.pass_criteria.get('max_linewidth', float('inf'))
            self.check_limit('3dB_bandwidth', bandwidth, max_val=max_linewidth)
        except:
            self.logger.warning("无法测量3dB带宽")
        
        # 添加峰值测量
        self.add_measurement('peak_wavelength', peak_wavelength, 'nm')
        self.add_measurement('peak_power', peak_power, 'dBm')
        
        self.logger.info(
            f"峰值分析完成: λ={peak_wavelength:.3f}nm, P={peak_power:.2f}dBm"
        )
        
        return self.peak_info
    
    def analyze_spectrum_shape(self) -> Dict:
        """
        分析光谱形状
        
        Returns:
            Dict: 光谱形状分析结果
        """
        if self.wavelength_data is None or self.power_data is None:
            self.scan_spectrum()
        
        analysis = {}
        
        # 计算积分功率
        # 转换dBm到mW进行积分
        power_mw = 10 ** (self.power_data / 10)
        wavelength_m = self.wavelength_data * 1e-9
        total_power = np.trapz(power_mw, wavelength_m)
        analysis['total_power_mW'] = total_power
        
        # 计算光谱宽度 (RMS)
        peak_idx = np.argmax(self.power_data)
        peak_wl = self.wavelength_data[peak_idx]
        
        # 权重平均波长
        weights = power_mw / np.sum(power_mw)
        mean_wl = np.sum(self.wavelength_data * weights)
        
        # RMS光谱宽度
        rms_width = np.sqrt(np.sum(weights * (self.wavelength_data - mean_wl)**2))
        analysis['rms_width'] = rms_width
        analysis['center_wavelength'] = mean_wl
        
        self.spectrum_analysis = analysis
        return analysis
    
    def find_channels(self, threshold: float = -40) -> List[Dict]:
        """
        查找多个通道/峰值
        
        Args:
            threshold: 检测阈值(dBm)
            
        Returns:
            List[Dict]: 通道列表
        """
        if self.power_data is None:
            self.scan_spectrum()
        
        channels = []
        
        # 简单的峰值检测
        for i in range(1, len(self.power_data) - 1):
            if (self.power_data[i] > threshold and
                self.power_data[i] > self.power_data[i-1] and
                self.power_data[i] > self.power_data[i+1]):
                channels.append({
                    'wavelength': float(self.wavelength_data[i]),
                    'power': float(self.power_data[i])
                })
        
        self.logger.info(f"检测到 {len(channels)} 个通道")
        return channels
    
    def generate_report(self) -> Dict:
        """
        生成测试报告数据
        
        Returns:
            Dict: 报告数据
        """
        report = {
            'test_name': '光谱分析测试',
            'test_class': self.__class__.__name__,
            'parameters': {
                'center_wavelength': self.center_wavelength,
                'span': self.span,
                'resolution': self.resolution,
                'sensitivity': self.sensitivity
            },
            'peak_info': self.peak_info,
            'spectrum_analysis': self.spectrum_analysis,
            'measurements': self.measurements,
            'pass_criteria': self.pass_criteria,
            'spectrum_data': {
                'wavelengths': self.wavelength_data.tolist() if self.wavelength_data is not None else [],
                'powers': self.power_data.tolist() if self.power_data is not None else []
            }
        }
        
        # 添加汇总信息
        report['summary'] = {
            'peak_wavelength': self.peak_info.get('wavelength'),
            'peak_power': self.peak_info.get('power'),
            'smsr': self.peak_info.get('smsr'),
            'bandwidth_3db': self.peak_info.get('bandwidth_3db'),
            'all_passed': self._check_all_passed()
        }
        
        return report
    
    def _check_all_passed(self) -> bool:
        """检查所有标准是否通过"""
        if 'smsr' in self.peak_info and 'min_smsr' in self.pass_criteria:
            if self.peak_info['smsr'] < self.pass_criteria['min_smsr']:
                return False
        
        if 'bandwidth_3db' in self.peak_info and 'max_linewidth' in self.pass_criteria:
            if self.peak_info['bandwidth_3db'] > self.pass_criteria['max_linewidth']:
                return False
        
        return True
    
    def cleanup(self):
        """测试清理"""
        if self.laser:
            self.laser.output_off()
        self.logger.info("测试清理完成")
