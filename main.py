"""
DWG翻译计算软件 - 主程序入口
Biaoge - Professional DWG Translation & Calculation Software

Version: 1.0.0
Author: Claude AI
License: Commercial
"""
import sys
import os
from pathlib import Path

# 添加项目路径
sys.path.insert(0, str(Path(__file__).parent))

from PyQt6.QtWidgets import QApplication, QSplashScreen
from PyQt6.QtGui import QPixmap, QFont, QIcon
from PyQt6.QtCore import Qt, QTimer
from src.ui.main_window import MainWindow
from src.utils.logger import logger
from src.utils.config_manager import ConfigManager


class Application:
    """应用程序管理器"""

    def __init__(self):
        self.app = None
        self.main_window = None
        self.splash = None

    def run(self):
        """运行应用程序"""
        try:
            # 创建QApplication
            self.app = QApplication(sys.argv)
            self.app.setApplicationName("表哥 - DWG翻译计算软件")
            self.app.setApplicationVersion("1.0.0")
            self.app.setOrganizationName("Biaoge")

            # 设置应用图标
            self._set_app_icon()

            # 设置字体
            self._set_app_font()

            # 显示启动画面
            self._show_splash_screen()

            # 加载配置
            logger.info("正在加载配置...")
            config = ConfigManager()

            # 创建主窗口
            logger.info("正在初始化主窗口...")
            self.main_window = MainWindow()

            # 关闭启动画面
            if self.splash:
                self.splash.finish(self.main_window)

            # 显示主窗口
            self.main_window.show()

            logger.info("应用程序启动成功")

            # 运行应用
            return self.app.exec()

        except Exception as e:
            logger.error(f"应用程序启动失败: {e}", exc_info=True)
            return 1

    def _set_app_icon(self):
        """设置应用图标"""
        icon_path = Path(__file__).parent / "resources" / "icon.png"
        if icon_path.exists():
            self.app.setWindowIcon(QIcon(str(icon_path)))

    def _set_app_font(self):
        """设置应用字体"""
        font = QFont("Microsoft YaHei UI", 9)
        self.app.setFont(font)

    def _show_splash_screen(self):
        """显示启动画面"""
        try:
            splash_path = Path(__file__).parent / "resources" / "splash.png"

            if splash_path.exists():
                pixmap = QPixmap(str(splash_path))
            else:
                # 创建简单的启动画面
                pixmap = QPixmap(600, 400)
                pixmap.fill(Qt.GlobalColor.white)

            self.splash = QSplashScreen(pixmap, Qt.WindowType.WindowStaysOnTopHint)
            self.splash.show()

            # 显示加载信息
            self.splash.showMessage(
                "正在启动表哥 DWG翻译计算软件...\n版本 1.0.0",
                Qt.AlignmentFlag.AlignBottom | Qt.AlignmentFlag.AlignCenter,
                Qt.GlobalColor.black
            )

            self.app.processEvents()

        except Exception as e:
            logger.warning(f"无法显示启动画面: {e}")


def main():
    """主函数"""
    # 设置环境变量
    os.environ['QT_AUTO_SCREEN_SCALE_FACTOR'] = '1'

    # 创建并运行应用
    app = Application()
    sys.exit(app.run())


if __name__ == '__main__':
    main()
