"""
报告生成器
生成测试报告（Excel、HTML、PDF等格式）
"""
import logging
from pathlib import Path
from datetime import datetime
from typing import Dict, List, Any, Optional
import json

import pandas as pd
import numpy as np
from jinja2 import Template


class ReportGenerator:
    """测试报告生成器"""
    
    def __init__(self, output_dir: str = None):
        """
        初始化报告生成器
        
        Args:
            output_dir: 报告输出目录
        """
        if output_dir is None:
            self.output_dir = Path(__file__).parent.parent / 'reports'
        else:
            self.output_dir = Path(output_dir)
        
        self.output_dir.mkdir(exist_ok=True)
        self.logger = logging.getLogger(self.__class__.__name__)
    
    def generate_excel_report(self, test_result, filename: str = None) -> str:
        """
        生成Excel报告
        
        Args:
            test_result: TestResult对象
            filename: 文件名
            
        Returns:
            str: 报告文件路径
        """
        if filename is None:
            timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
            filename = f"report_{test_result.flow_id}_{timestamp}.xlsx"
        
        filepath = self.output_dir / filename
        
        with pd.ExcelWriter(filepath, engine='openpyxl') as writer:
            # 测试概要
            summary_data = {
                '项目': ['测试流程', '状态', '开始时间', '结束时间', '持续时间', '产品序列号'],
                '值': [
                    test_result.flow_name,
                    test_result.status.value,
                    test_result.start_time.strftime('%Y-%m-%d %H:%M:%S'),
                    test_result.end_time.strftime('%Y-%m-%d %H:%M:%S') if test_result.end_time else 'N/A',
                    f"{test_result.duration:.2f} 秒",
                    test_result.product_info.get('serial_number', 'N/A')
                ]
            }
            pd.DataFrame(summary_data).to_excel(writer, sheet_name='测试概要', index=False)
            
            # 测量结果
            if test_result.measurements:
                measurements_data = []
                for name, value in test_result.measurements.items():
                    measurements_data.append({
                        '测量项': name,
                        '值': value,
                        '结果': 'PASS' if test_result.passed_criteria.get(name, True) else 'FAIL'
                    })
                pd.DataFrame(measurements_data).to_excel(writer, sheet_name='测量结果', index=False)
            
            # 步骤详情
            if test_result.step_results:
                steps_data = []
                for step in test_result.step_results:
                    steps_data.append({
                        '步骤ID': step.step_id,
                        '名称': step.name,
                        '状态': step.status.value,
                        '持续时间': f"{step.duration:.2f}s",
                        '错误信息': step.error_message
                    })
                pd.DataFrame(steps_data).to_excel(writer, sheet_name='步骤详情', index=False)
            
            # 通过标准
            if test_result.pass_criteria:
                criteria_data = []
                for name, limit in test_result.pass_criteria.items():
                    actual = test_result.measurements.get(name, 'N/A')
                    passed = test_result.passed_criteria.get(name, True)
                    criteria_data.append({
                        '项目': name,
                        '限值': str(limit),
                        '实际值': actual,
                        '结果': 'PASS' if passed else 'FAIL'
                    })
                pd.DataFrame(criteria_data).to_excel(writer, sheet_name='通过标准', index=False)
        
        self.logger.info(f"Excel报告已生成: {filepath}")
        return str(filepath)
    
    def generate_html_report(self, test_result, filename: str = None) -> str:
        """
        生成HTML报告
        
        Args:
            test_result: TestResult对象
            filename: 文件名
            
        Returns:
            str: 报告文件路径
        """
        if filename is None:
            timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
            filename = f"report_{test_result.flow_id}_{timestamp}.html"
        
        filepath = self.output_dir / filename
        
        html_template = """
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>测试报告 - {{ flow_name }}</title>
    <style>
        body {
            font-family: 'Microsoft YaHei', Arial, sans-serif;
            margin: 20px;
            background-color: #f5f5f5;
        }
        .container {
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        h1 {
            color: #333;
            border-bottom: 2px solid #007bff;
            padding-bottom: 10px;
        }
        h2 {
            color: #666;
            margin-top: 30px;
        }
        table {
            width: 100%;
            border-collapse: collapse;
            margin: 15px 0;
        }
        th, td {
            border: 1px solid #ddd;
            padding: 10px;
            text-align: left;
        }
        th {
            background-color: #007bff;
            color: white;
        }
        tr:nth-child(even) {
            background-color: #f9f9f9;
        }
        .status-passed {
            color: #28a745;
            font-weight: bold;
        }
        .status-failed {
            color: #dc3545;
            font-weight: bold;
        }
        .status-error {
            color: #ffc107;
            font-weight: bold;
        }
        .summary-box {
            display: flex;
            justify-content: space-around;
            margin: 20px 0;
        }
        .summary-item {
            text-align: center;
            padding: 20px;
            background: #f8f9fa;
            border-radius: 8px;
            min-width: 150px;
        }
        .summary-item .value {
            font-size: 24px;
            font-weight: bold;
            color: #007bff;
        }
        .summary-item .label {
            color: #666;
            margin-top: 5px;
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>🔬 测试报告</h1>
        
        <div class="summary-box">
            <div class="summary-item">
                <div class="value">{{ flow_name }}</div>
                <div class="label">测试流程</div>
            </div>
            <div class="summary-item">
                <div class="value status-{{ status }}">{{ status|upper }}</div>
                <div class="label">测试状态</div>
            </div>
            <div class="summary-item">
                <div class="value">{{ duration }}s</div>
                <div class="label">测试时长</div>
            </div>
        </div>
        
        <h2>📋 测试信息</h2>
        <table>
            <tr><th>项目</th><th>值</th></tr>
            <tr><td>流程ID</td><td>{{ flow_id }}</td></tr>
            <tr><td>开始时间</td><td>{{ start_time }}</td></tr>
            <tr><td>结束时间</td><td>{{ end_time }}</td></tr>
            <tr><td>产品序列号</td><td>{{ serial_number }}</td></tr>
        </table>
        
        {% if measurements %}
        <h2>📊 测量结果</h2>
        <table>
            <tr><th>测量项</th><th>值</th><th>结果</th></tr>
            {% for name, value in measurements.items() %}
            <tr>
                <td>{{ name }}</td>
                <td>{{ value }}</td>
                <td class="status-{{ 'passed' if passed_criteria.get(name, True) else 'failed' }}">
                    {{ 'PASS' if passed_criteria.get(name, True) else 'FAIL' }}
                </td>
            </tr>
            {% endfor %}
        </table>
        {% endif %}
        
        {% if steps %}
        <h2>📝 测试步骤</h2>
        <table>
            <tr><th>步骤</th><th>名称</th><th>状态</th><th>耗时</th></tr>
            {% for step in steps %}
            <tr>
                <td>{{ step.step_id }}</td>
                <td>{{ step.name }}</td>
                <td class="status-{{ step.status }}">{{ step.status|upper }}</td>
                <td>{{ step.duration }}s</td>
            </tr>
            {% endfor %}
        </table>
        {% endif %}
        
        <footer style="margin-top: 30px; text-align: center; color: #999;">
            <p>报告生成时间: {{ report_time }}</p>
            <p>光通信硬件测试自动化平台</p>
        </footer>
    </div>
</body>
</html>
        """
        
        template = Template(html_template)
        
        # 准备步骤数据
        steps = []
        for step in test_result.step_results:
            steps.append({
                'step_id': step.step_id,
                'name': step.name,
                'status': step.status.value,
                'duration': f"{step.duration:.2f}"
            })
        
        html_content = template.render(
            flow_name=test_result.flow_name,
            flow_id=test_result.flow_id,
            status=test_result.status.value,
            duration=f"{test_result.duration:.2f}",
            start_time=test_result.start_time.strftime('%Y-%m-%d %H:%M:%S'),
            end_time=test_result.end_time.strftime('%Y-%m-%d %H:%M:%S') if test_result.end_time else 'N/A',
            serial_number=test_result.product_info.get('serial_number', 'N/A'),
            measurements=test_result.measurements,
            passed_criteria=test_result.passed_criteria,
            steps=steps,
            report_time=datetime.now().strftime('%Y-%m-%d %H:%M:%S')
        )
        
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(html_content)
        
        self.logger.info(f"HTML报告已生成: {filepath}")
        return str(filepath)
    
    def generate_json_report(self, test_result, filename: str = None) -> str:
        """
        生成JSON报告
        
        Args:
            test_result: TestResult对象
            filename: 文件名
            
        Returns:
            str: 报告文件路径
        """
        if filename is None:
            timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
            filename = f"report_{test_result.flow_id}_{timestamp}.json"
        
        filepath = self.output_dir / filename
        
        report_data = {
            'flow_id': test_result.flow_id,
            'flow_name': test_result.flow_name,
            'status': test_result.status.value,
            'start_time': test_result.start_time.isoformat(),
            'end_time': test_result.end_time.isoformat() if test_result.end_time else None,
            'duration': test_result.duration,
            'product_info': test_result.product_info,
            'measurements': test_result.measurements,
            'pass_criteria': test_result.pass_criteria,
            'passed_criteria': test_result.passed_criteria,
            'error_message': test_result.error_message,
            'steps': []
        }
        
        for step in test_result.step_results:
            report_data['steps'].append({
                'step_id': step.step_id,
                'name': step.name,
                'status': step.status.value,
                'start_time': step.start_time.isoformat(),
                'end_time': step.end_time.isoformat(),
                'duration': step.duration,
                'data': step.data,
                'error_message': step.error_message
            })
        
        with open(filepath, 'w', encoding='utf-8') as f:
            json.dump(report_data, f, indent=2, ensure_ascii=False)
        
        self.logger.info(f"JSON报告已生成: {filepath}")
        return str(filepath)
    
    def generate_csv_report(self, test_result, filename: str = None) -> str:
        """
        生成CSV报告（仅测量数据）
        
        Args:
            test_result: TestResult对象
            filename: 文件名
            
        Returns:
            str: 报告文件路径
        """
        if filename is None:
            timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
            filename = f"report_{test_result.flow_id}_{timestamp}.csv"
        
        filepath = self.output_dir / filename
        
        data = []
        for name, value in test_result.measurements.items():
            data.append({
                'Test': test_result.flow_name,
                'Measurement': name,
                'Value': value,
                'Result': 'PASS' if test_result.passed_criteria.get(name, True) else 'FAIL',
                'Timestamp': test_result.start_time.isoformat()
            })
        
        df = pd.DataFrame(data)
        df.to_csv(filepath, index=False, encoding='utf-8-sig')
        
        self.logger.info(f"CSV报告已生成: {filepath}")
        return str(filepath)
    
    def generate_all_reports(self, test_result) -> Dict[str, str]:
        """
        生成所有格式的报告
        
        Args:
            test_result: TestResult对象
            
        Returns:
            Dict[str, str]: 各格式报告的文件路径
        """
        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
        base_name = f"report_{test_result.flow_id}_{timestamp}"
        
        paths = {
            'excel': self.generate_excel_report(test_result, f"{base_name}.xlsx"),
            'html': self.generate_html_report(test_result, f"{base_name}.html"),
            'json': self.generate_json_report(test_result, f"{base_name}.json"),
            'csv': self.generate_csv_report(test_result, f"{base_name}.csv")
        }
        
        return paths
    
    def generate_batch_summary(self, results: List, filename: str = None) -> str:
        """
        生成批量测试汇总报告
        
        Args:
            results: TestResult对象列表
            filename: 文件名
            
        Returns:
            str: 报告文件路径
        """
        if filename is None:
            timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
            filename = f"batch_summary_{timestamp}.xlsx"
        
        filepath = self.output_dir / filename
        
        summary_data = []
        for result in results:
            summary_data.append({
                '流程ID': result.flow_id,
                '流程名称': result.flow_name,
                '产品序列号': result.product_info.get('serial_number', 'N/A'),
                '状态': result.status.value,
                '开始时间': result.start_time.strftime('%Y-%m-%d %H:%M:%S'),
                '持续时间(s)': result.duration,
                '错误信息': result.error_message or ''
            })
        
        df = pd.DataFrame(summary_data)
        
        with pd.ExcelWriter(filepath, engine='openpyxl') as writer:
            df.to_excel(writer, sheet_name='测试汇总', index=False)
            
            # 统计信息
            stats = {
                '项目': ['总测试数', '通过数', '失败数', '通过率'],
                '值': [
                    len(results),
                    len([r for r in results if r.status.value == 'passed']),
                    len([r for r in results if r.status.value in ['failed', 'error']]),
                    f"{len([r for r in results if r.status.value == 'passed']) / len(results) * 100:.1f}%"
                ]
            }
            pd.DataFrame(stats).to_excel(writer, sheet_name='统计', index=False)
        
        self.logger.info(f"批量汇总报告已生成: {filepath}")
        return str(filepath)
