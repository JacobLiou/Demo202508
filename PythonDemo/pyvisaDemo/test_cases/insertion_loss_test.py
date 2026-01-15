"""
插入损耗测试
测量光器件的插入损耗
"""
import time
from typing import Dict, List, Any, Optional
import numpy as np

from .base_test import BaseTest


class InsertionLossTest(BaseTest):
    """插入损耗测试"""
    
    def __init__(self, config: Dict, instruments: Dict, result):
        super().__init__(config, instruments, result)
        
        # 获取仪器
        self.laser = self.get_instrument('laser_source')
        self.power_meter = self.get_instrument('optical_power_meter')
        
        # 测试参数
        self.wavelengths = self.parameters.get('wavelengths', [1550])
        self.input_power = self.parameters.get('input_power', 0)
        self.measurement_count = self.parameters.get('measurement_count', 3)
        self.settling_time = self.parameters.get('settling_time', 0.5)
        
        # 测试数据
        self.reference_powers: Dict[float, float] = {}
        self.measured_powers: Dict[float, List[float]] = {}
        self.insertion_losses: Dict[float, float] = {}
    
    def calibrate_reference(self, wavelength: float = None, **kwargs) -> float:
        """
        校准参考功率
        直接连接激光器和功率计测量参考功率
        
        Args:
            wavelength: 波长(nm)，如果不指定则使用默认波长
            
        Returns:
            float: 参考功率值(dBm)
        """
        if wavelength is None:
            wavelength = self.parameters.get('default_wavelength', 1550)
        
        self.logger.info(f"校准参考功率 @ {wavelength} nm")
        
        # 设置激光器
        self.laser.set_wavelength(wavelength)
        self.laser.set_power(self.input_power)
        self.laser.output_on()
        
        # 设置功率计波长
        self.power_meter.set_wavelength(wavelength)
        
        # 等待稳定
        time.sleep(self.settling_time)
        
        # 测量多次取平均
        powers = self.power_meter.measure_multiple(count=self.measurement_count)
        ref_power = np.mean(powers)
        
        self.reference_powers[wavelength] = ref_power
        self.reference_values[f'reference_{wavelength}nm'] = ref_power
        
        self.logger.info(f"参考功率 @ {wavelength} nm: {ref_power:.3f} dBm")
        return ref_power
    
    def measure_insertion_loss(self, wavelength: float) -> Dict:
        """
        测量指定波长的插入损耗
        
        Args:
            wavelength: 波长(nm)
            
        Returns:
            Dict: 测量结果
        """
        self.logger.info(f"测量插损 @ {wavelength} nm")
        
        # 确保有参考功率
        if wavelength not in self.reference_powers:
            self.calibrate_reference(wavelength)
        
        ref_power = self.reference_powers[wavelength]
        
        # 设置激光器波长
        self.laser.set_wavelength(wavelength)
        self.laser.output_on()
        
        # 设置功率计波长
        self.power_meter.set_wavelength(wavelength)
        
        # 等待稳定
        time.sleep(self.settling_time)
        
        # 测量DUT后的功率
        powers = self.power_meter.measure_multiple(count=self.measurement_count)
        output_power = np.mean(powers)
        
        # 计算插入损耗
        insertion_loss = ref_power - output_power
        
        # 保存数据
        self.measured_powers[wavelength] = powers
        self.insertion_losses[wavelength] = insertion_loss
        
        # 添加测量结果
        self.add_measurement(f'IL_{wavelength}nm', insertion_loss, 'dB')
        self.add_measurement(f'output_power_{wavelength}nm', output_power, 'dBm')
        
        # 检查限值
        max_il = self.pass_criteria.get('max_insertion_loss', float('inf'))
        passed = self.check_limit(f'IL_{wavelength}nm', insertion_loss, max_val=max_il)
        
        result = {
            'wavelength': wavelength,
            'reference_power': ref_power,
            'output_power': output_power,
            'insertion_loss': insertion_loss,
            'measurements': powers,
            'std_dev': float(np.std(powers)),
            'passed': passed
        }
        
        self.logger.info(
            f"插损 @ {wavelength} nm: {insertion_loss:.3f} dB "
            f"({'PASS' if passed else 'FAIL'})"
        )
        
        return result
    
    def run_wavelength_sweep(self) -> List[Dict]:
        """
        执行波长扫描测试
        
        Returns:
            List[Dict]: 所有波长的测试结果
        """
        results = []
        
        for wavelength in self.wavelengths:
            result = self.measure_insertion_loss(wavelength)
            results.append(result)
        
        # 计算均匀性
        if len(self.insertion_losses) > 1:
            il_values = list(self.insertion_losses.values())
            uniformity = max(il_values) - min(il_values)
            self.add_measurement('uniformity', uniformity, 'dB')
            
            max_dev = self.pass_criteria.get('max_deviation', float('inf'))
            self.check_limit('uniformity', uniformity, max_val=max_dev)
        
        return results
    
    def generate_report(self) -> Dict:
        """
        生成测试报告数据
        
        Returns:
            Dict: 报告数据
        """
        report = {
            'test_name': '插入损耗测试',
            'test_class': self.__class__.__name__,
            'parameters': {
                'wavelengths': self.wavelengths,
                'input_power': self.input_power,
                'measurement_count': self.measurement_count
            },
            'reference_powers': self.reference_powers,
            'insertion_losses': self.insertion_losses,
            'measurements': self.measurements,
            'pass_criteria': self.pass_criteria,
            'summary': {}
        }
        
        if self.insertion_losses:
            il_values = list(self.insertion_losses.values())
            report['summary'] = {
                'min_il': min(il_values),
                'max_il': max(il_values),
                'avg_il': np.mean(il_values),
                'uniformity': max(il_values) - min(il_values),
                'all_passed': all(
                    il <= self.pass_criteria.get('max_insertion_loss', float('inf'))
                    for il in il_values
                )
            }
        
        return report
    
    def cleanup(self):
        """测试清理"""
        if self.laser:
            self.laser.output_off()
        self.logger.info("测试清理完成")
