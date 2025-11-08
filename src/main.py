# -*- coding: utf-8 -*-
"""
DWG智能翻译算量系统 - 主入口
"""
import sys
import os
from pathlib import Path

# Windows编码修复 - 确保中文路径和文件名正确处理
if sys.platform == 'win32':
    # 设置控制台编码为UTF-8
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8')
    if hasattr(sys.stderr, 'reconfigure'):
        sys.stderr.reconfigure(encoding='utf-8')

    # 设置环境变量
    os.environ['PYTHONIOENCODING'] = 'utf-8'

    # 设置文件系统编码
    if hasattr(sys, '_enablelegacywindowsfsencoding'):
        sys._enablelegacywindowsfsencoding()

# 添加src到路径
sys.path.insert(0, str(Path(__file__).parent.parent))

from PyQt6.QtWidgets import QApplication
from PyQt6.QtCore import Qt

from src.ui.main_window import MainWindow
from src.utils.logger import logger
from src.utils.config_manager import config


def main():
    """主函数"""
    # 设置高DPI支持
    QApplication.setHighDpiScaleFactorRoundingPolicy(
        Qt.HighDpiScaleFactorRoundingPolicy.PassThrough
    )
    QApplication.setAttribute(Qt.ApplicationAttribute.AA_EnableHighDpiScaling)
    QApplication.setAttribute(Qt.ApplicationAttribute.AA_UseHighDpiPixmaps)

    # 创建应用
    app = QApplication(sys.argv)
    app.setApplicationName(config.get('app.name', 'BiaoGe'))
    app.setApplicationVersion(config.get('app.version', '1.0.0'))

    logger.info("应用启动...")
    logger.info(f"Python版本: {sys.version}")
    logger.info(f"PyQt版本: {QApplication.instance().applicationVersion()}")

    # 创建主窗口
    window = MainWindow()
    window.show()

    logger.info("主窗口已显示")

    # 运行应用
    exit_code = app.exec()

    logger.info(f"应用退出，退出码: {exit_code}")
    sys.exit(exit_code)


if __name__ == '__main__':
    main()
