"""
测试用例模块初始化
"""
from .base_test import BaseTest
from .insertion_loss_test import InsertionLossTest
from .return_loss_test import ReturnLossTest
from .spectrum_test import SpectrumTest

__all__ = [
    'BaseTest',
    'InsertionLossTest',
    'ReturnLossTest',
    'SpectrumTest'
]
