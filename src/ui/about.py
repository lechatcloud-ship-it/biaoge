# -*- coding: utf-8 -*-
"""
关于对话框
"""
from PyQt6.QtWidgets import (
    QDialog, QVBoxLayout, QHBoxLayout, QLabel,
    QPushButton, QTextBrowser
)
from PyQt6.QtCore import Qt
from PyQt6.QtGui import QPixmap, QFont
from pathlib import Path


class AboutDialog(QDialog):
    """关于对话框"""

    def __init__(self, parent=None):
        super().__init__(parent)

        self.setWindowTitle("关于 表哥")
        self.setMinimumSize(500, 600)
        self.setModal(True)

        self._init_ui()

    def _init_ui(self):
        """初始化UI"""
        layout = QVBoxLayout(self)
        layout.setSpacing(20)

        # Logo和标题
        title_layout = QVBoxLayout()
        title_layout.setAlignment(Qt.AlignmentFlag.AlignCenter)

        # Logo
        logo_label = QLabel()
        logo_path = Path(__file__).parent.parent.parent / "resources" / "logo.png"
        if logo_path.exists():
            pixmap = QPixmap(str(logo_path)).scaled(
                128, 128,
                Qt.AspectRatioMode.KeepAspectRatio,
                Qt.TransformationMode.SmoothTransformation
            )
            logo_label.setPixmap(pixmap)
        else:
            logo_label.setText("📊")
            logo_label.setStyleSheet("font-size: 64px;")

        title_layout.addWidget(logo_label)

        # 应用名称
        name_label = QLabel("表哥")
        name_font = QFont("Microsoft YaHei UI", 24, QFont.Weight.Bold)
        name_label.setFont(name_font)
        name_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        title_layout.addWidget(name_label)

        # 副标题
        subtitle_label = QLabel("DWG翻译计算软件")
        subtitle_font = QFont("Microsoft YaHei UI", 12)
        subtitle_label.setFont(subtitle_font)
        subtitle_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        subtitle_label.setStyleSheet("color: #666;")
        title_layout.addWidget(subtitle_label)

        layout.addLayout(title_layout)

        # 版本信息
        version_label = QLabel("版本 1.0.0")
        version_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        version_label.setStyleSheet("color: #888; font-size: 11px;")
        layout.addWidget(version_label)

        # 信息文本
        info_text = QTextBrowser()
        info_text.setOpenExternalLinks(True)
        info_text.setMaximumHeight(300)
        info_text.setHtml("""
            <h3>📌 产品简介</h3>
            <p>表哥是一款专业的DWG图纸翻译和计算软件，专为建筑工程行业打造。</p>

            <h3>✨ 核心功能</h3>
            <ul>
                <li><b>DWG预览</b>: 支持DWG/DXF文件预览，流畅的CAD级交互体验</li>
                <li><b>AI翻译</b>: 基于阿里云百炼大模型的人工级翻译质量</li>
                <li><b>智能算量</b>: 高级构件识别算法，支持材料和规格自动提取</li>
                <li><b>多格式导出</b>: 支持DWG、PDF、Excel多种格式导出</li>
            </ul>

            <h3>🚀 性能特点</h3>
            <ul>
                <li>支持50K+实体流畅渲染（空间索引优化）</li>
                <li>内存占用 < 500MB</li>
                <li>翻译成本 ¥0.05/图纸（缓存优化）</li>
                <li>商业级性能标准</li>
            </ul>

            <h3>🎯 技术栈</h3>
            <p><b>界面框架</b>: PyQt6 6.6+ | <b>渲染引擎</b>: QPainter<br/>
            <b>DWG解析</b>: ezdxf 1.1+ | <b>AI模型</b>: 阿里云百炼 Qwen系列<br/>
            <b>性能优化</b>: R-tree空间索引, Numba JIT加速</p>

            <h3>📄 许可证</h3>
            <p>商业软件 - 版权所有 © 2025</p>

            <h3>🔗 链接</h3>
            <p>
                <a href="https://github.com">GitHub</a> |
                <a href="https://dashscope.aliyun.com">阿里云百炼</a> |
                <a href="mailto:support@biaoge.com">技术支持</a>
            </p>

            <hr/>
            <p style="color: #888; font-size: 11px; text-align: center;">
                Powered by Claude AI | Made with ❤️ for Engineers
            </p>
        """)

        layout.addWidget(info_text)

        # 按钮
        button_layout = QHBoxLayout()
        button_layout.addStretch()

        ok_button = QPushButton("确定")
        ok_button.clicked.connect(self.accept)
        ok_button.setDefault(True)

        button_layout.addWidget(ok_button)

        layout.addLayout(button_layout)
