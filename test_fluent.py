#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""测试Fluent Design是否正常工作"""
import sys
from PyQt6.QtWidgets import QApplication, QWidget, QVBoxLayout, QLabel
from qfluentwidgets import FluentWindow, NavigationItemPosition, FluentIcon, setTheme, Theme

class TestWindow(FluentWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Fluent Design 测试")
        self.resize(800, 600)

        # 创建测试页面
        self.home_page = QWidget()
        self.home_page.setObjectName("homePage")
        layout = QVBoxLayout(self.home_page)
        layout.addWidget(QLabel("✅ Fluent Design 工作正常！"))

        # 添加到导航栏
        self.addSubInterface(
            self.home_page,
            FluentIcon.HOME,
            "主页",
            NavigationItemPosition.TOP
        )

        # 设置主题
        setTheme(Theme.AUTO)
        print("✅ Fluent Design 初始化成功！")

if __name__ == '__main__':
    app = QApplication(sys.argv)
    window = TestWindow()
    window.show()
    print("✅ 窗口已显示，Fluent Design正常工作！")
    sys.exit(app.exec())
