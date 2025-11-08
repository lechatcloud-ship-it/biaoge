#!/usr/bin/env python3
"""
大规模测试运行脚本

运行所有测试并生成详细报告
"""
import subprocess
import sys
import time
from pathlib import Path
from datetime import datetime
import json


class TestRunner:
    """测试运行器"""

    def __init__(self):
        self.project_root = Path(__file__).parent
        self.test_dir = self.project_root / "tests"
        self.results = {}
        self.start_time = None
        self.end_time = None

    def run_test_suite(self, test_path, suite_name):
        """运行单个测试套件"""
        print(f"\n{'='*60}")
        print(f"运行测试套件: {suite_name}")
        print(f"{'='*60}")

        start = time.time()

        try:
            # 运行pytest
            result = subprocess.run(
                [sys.executable, "-m", "pytest", str(test_path), "-v", "-s", "--tb=short"],
                cwd=str(self.project_root),
                capture_output=True,
                text=True,
                timeout=300  # 5分钟超时
            )

            elapsed = time.time() - start

            # 解析结果
            output = result.stdout + result.stderr
            passed = output.count(" PASSED")
            failed = output.count(" FAILED")
            errors = output.count(" ERROR")
            skipped = output.count(" SKIPPED")

            self.results[suite_name] = {
                'passed': passed,
                'failed': failed,
                'errors': errors,
                'skipped': skipped,
                'time': elapsed,
                'returncode': result.returncode,
                'output': output
            }

            # 打印摘要
            print(f"\n结果:")
            print(f"  ✅ 通过: {passed}")
            print(f"  ❌ 失败: {failed}")
            print(f"  ⚠️  错误: {errors}")
            print(f"  ⏭️  跳过: {skipped}")
            print(f"  ⏱️  耗时: {elapsed:.2f}s")

            return result.returncode == 0

        except subprocess.TimeoutExpired:
            print(f"❌ 测试超时（>5分钟）")
            self.results[suite_name] = {
                'passed': 0,
                'failed': 0,
                'errors': 1,
                'skipped': 0,
                'time': 300,
                'returncode': -1,
                'output': "测试超时"
            }
            return False

        except Exception as e:
            print(f"❌ 运行测试时出错: {e}")
            self.results[suite_name] = {
                'passed': 0,
                'failed': 0,
                'errors': 1,
                'skipped': 0,
                'time': 0,
                'returncode': -1,
                'output': str(e)
            }
            return False

    def run_all_tests(self):
        """运行所有测试"""
        self.start_time = datetime.now()

        print("🚀 开始大规模测试...")
        print(f"时间: {self.start_time.strftime('%Y-%m-%d %H:%M:%S')}")

        test_suites = [
            ("tests/unit/test_dimension_extraction.py", "尺寸提取功能测试"),
            ("tests/unit/test_dimension_supplementation.py", "尺寸补充系统测试"),
            ("tests/unit/test_result_validation.py", "结果验证系统测试"),
            ("tests/performance/test_large_scale_calculation.py", "大规模性能测试"),
        ]

        success_count = 0

        for test_path, suite_name in test_suites:
            full_path = self.project_root / test_path

            if not full_path.exists():
                print(f"⚠️  跳过: {suite_name} (文件不存在)")
                continue

            success = self.run_test_suite(full_path, suite_name)
            if success:
                success_count += 1

        self.end_time = datetime.now()
        total_time = (self.end_time - self.start_time).total_seconds()

        print(f"\n{'='*60}")
        print("测试完成!")
        print(f"{'='*60}")
        print(f"总耗时: {total_time:.2f}s")
        print(f"成功套件: {success_count}/{len(test_suites)}")

        return success_count == len(test_suites)

    def generate_report(self):
        """生成测试报告"""
        report_path = self.project_root / "TEST_REPORT.md"

        with open(report_path, "w", encoding="utf-8") as f:
            f.write("# 表哥DWG软件 - 大规模测试报告\n\n")
            f.write(f"**测试时间**: {self.start_time.strftime('%Y-%m-%d %H:%M:%S')}\n\n")
            f.write(f"**测试版本**: v2.0\n\n")

            # 总览
            f.write("## 📊 测试总览\n\n")

            total_passed = sum(r['passed'] for r in self.results.values())
            total_failed = sum(r['failed'] for r in self.results.values())
            total_errors = sum(r['errors'] for r in self.results.values())
            total_tests = total_passed + total_failed + total_errors

            if total_tests > 0:
                pass_rate = total_passed / total_tests * 100
            else:
                pass_rate = 0

            f.write(f"| 指标 | 数值 |\n")
            f.write(f"|------|------|\n")
            f.write(f"| 总测试数 | {total_tests} |\n")
            f.write(f"| ✅ 通过 | {total_passed} |\n")
            f.write(f"| ❌ 失败 | {total_failed} |\n")
            f.write(f"| ⚠️  错误 | {total_errors} |\n")
            f.write(f"| 通过率 | **{pass_rate:.1f}%** |\n")

            total_time = (self.end_time - self.start_time).total_seconds()
            f.write(f"| 总耗时 | {total_time:.2f}s |\n\n")

            # 各测试套件详情
            f.write("## 📋 测试套件详情\n\n")

            for suite_name, result in self.results.items():
                f.write(f"### {suite_name}\n\n")

                suite_total = result['passed'] + result['failed'] + result['errors']
                if suite_total > 0:
                    suite_pass_rate = result['passed'] / suite_total * 100
                else:
                    suite_pass_rate = 0

                f.write(f"| 指标 | 数值 |\n")
                f.write(f"|------|------|\n")
                f.write(f"| 通过 | {result['passed']} |\n")
                f.write(f"| 失败 | {result['failed']} |\n")
                f.write(f"| 错误 | {result['errors']} |\n")
                f.write(f"| 通过率 | {suite_pass_rate:.1f}% |\n")
                f.write(f"| 耗时 | {result['time']:.2f}s |\n\n")

                # 显示关键输出（性能数据等）
                output_lines = result['output'].split('\n')
                key_lines = [line for line in output_lines if any(
                    keyword in line for keyword in
                    ['【', '性能', '准确率', '通过率', '捕获率', '误报率', 'PASSED', 'FAILED']
                )]

                if key_lines:
                    f.write("<details>\n")
                    f.write("<summary>关键输出</summary>\n\n")
                    f.write("```\n")
                    f.write('\n'.join(key_lines[:20]))  # 最多20行
                    f.write("\n```\n\n")
                    f.write("</details>\n\n")

            # 结论
            f.write("## ✅ 测试结论\n\n")

            if pass_rate >= 90:
                f.write(f"✅ **测试通过** - 通过率{pass_rate:.1f}%，达到企业级标准（≥90%）\n\n")
            elif pass_rate >= 80:
                f.write(f"⚠️ **基本通过** - 通过率{pass_rate:.1f}%，接近标准（目标≥90%）\n\n")
            else:
                f.write(f"❌ **测试不通过** - 通过率{pass_rate:.1f}%，需要改进\n\n")

            # 推荐行动
            f.write("## 🎯 推荐行动\n\n")

            if total_failed > 0:
                f.write(f"1. 修复 {total_failed} 个失败的测试用例\n")

            if total_errors > 0:
                f.write(f"2. 处理 {total_errors} 个测试错误\n")

            if pass_rate >= 90:
                f.write("3. ✅ 系统已准备好投入生产环境\n")
            else:
                f.write("3. 继续完善系统，目标通过率≥90%\n")

            f.write(f"\n---\n\n")
            f.write(f"**报告生成时间**: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")

        print(f"\n📄 测试报告已生成: {report_path}")

        # 同时生成JSON格式
        json_path = self.project_root / "test_results.json"
        with open(json_path, "w", encoding="utf-8") as f:
            json.dump({
                'start_time': self.start_time.isoformat(),
                'end_time': self.end_time.isoformat(),
                'results': self.results,
                'summary': {
                    'total_tests': total_tests,
                    'passed': total_passed,
                    'failed': total_failed,
                    'errors': total_errors,
                    'pass_rate': pass_rate,
                    'total_time': total_time
                }
            }, f, indent=2, ensure_ascii=False)

        print(f"📊 JSON结果已保存: {json_path}")


def main():
    """主函数"""
    runner = TestRunner()

    # 运行所有测试
    success = runner.run_all_tests()

    # 生成报告
    runner.generate_report()

    # 返回退出码
    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()
