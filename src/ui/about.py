# -*- coding: utf-8 -*-
"""
关于对话框
"""
from qfluentwidgets import (
    Dialog, TitleLabel, BodyLabel, PrimaryPushButton,
    FluentIcon, CardWidget, SmoothScrollArea
)
from PyQt6.QtWidgets import QVBoxLayout, QHBoxLayout, QWidget, QLabel
from PyQt6.QtCore import Qt
from PyQt6.QtGui import QPixmap, QFont
from pathlib import Path


class AboutDialog(Dialog):
    """关于对话框"""

    def __init__(self, parent=None):
        super().__init__("关于 表哥", "", parent)

        self.setMinimumSize(550, 650)

        # 创建内容
        content_widget = QWidget()
        layout = QVBoxLayout(content_widget)
        layout.setSpacing(20)
        layout.setContentsMargins(20, 20, 20, 20)

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
            # 使用默认图标
            logo_label.setFixedSize(128, 128)
            logo_label.setStyleSheet("""
                QLabel {
                    background-color: #0078D4;
                    border-radius: 64px;
                }
            """)

        title_layout.addWidget(logo_label)

        # 应用名称
        name_label = TitleLabel("表哥")
        name_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        name_label.setStyleSheet("font-size: 28px; font-weight: bold;")
        title_layout.addWidget(name_label)

        # 副标题
        subtitle_label = BodyLabel("DWG翻译计算软件")
        subtitle_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        subtitle_label.setStyleSheet("color: #606060; font-size: 14px;")
        title_layout.addWidget(subtitle_label)

        layout.addLayout(title_layout)

        # 版本信息
        version_label = BodyLabel("版本 1.0.0")
        version_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        version_label.setStyleSheet("color: #888; font-size: 12px;")
        layout.addWidget(version_label)

        # 信息卡片
        info_card = CardWidget()
        info_layout = QVBoxLayout(info_card)
        info_layout.setSpacing(15)
        info_layout.setContentsMargins(20, 20, 20, 20)

        # 产品简介
        intro_title = BodyLabel("产品简介")
        intro_title.setStyleSheet("font-size: 14px; font-weight: bold;")
        info_layout.addWidget(intro_title)

        intro_text = BodyLabel("表哥是一款专业的DWG图纸翻译和计算软件，专为建筑工程行业打造。")
        intro_text.setWordWrap(True)
        info_layout.addWidget(intro_text)

        # 核心功能
        features_title = BodyLabel("核心功能")
        features_title.setStyleSheet("font-size: 14px; font-weight: bold; margin-top: 10px;")
        info_layout.addWidget(features_title)

        features = [
            "DWG预览: 支持DWG/DXF文件预览，流畅的CAD级交互体验",
            "AI翻译: 基于阿里云百炼大模型的人工级翻译质量",
            "智能算量: 高级构件识别算法，支持材料和规格自动提取",
            "多格式导出: 支持DWG、PDF、Excel多种格式导出"
        ]
        for feature in features:
            feature_label = BodyLabel(f"• {feature}")
            feature_label.setWordWrap(True)
            info_layout.addWidget(feature_label)

        # 性能特点
        perf_title = BodyLabel("性能特点")
        perf_title.setStyleSheet("font-size: 14px; font-weight: bold; margin-top: 10px;")
        info_layout.addWidget(perf_title)

        perf_features = [
            "支持50K+实体流畅渲染（空间索引优化）",
            "内存占用 < 500MB",
            "翻译成本 约0.05元/图纸（缓存优化）",
            "商业级性能标准"
        ]
        for perf in perf_features:
            perf_label = BodyLabel(f"• {perf}")
            info_layout.addWidget(perf_label)

        # 技术栈
        tech_title = BodyLabel("技术栈")
        tech_title.setStyleSheet("font-size: 14px; font-weight: bold; margin-top: 10px;")
        info_layout.addWidget(tech_title)

        tech_text = BodyLabel(
            "界面框架: PyQt6 6.6+ | UI组件: PyQt-Fluent-Widgets 1.9.2\n"
            "DWG解析: ezdxf 1.1+ / Aspose.CAD 25.4.0 | AI模型: 阿里云百炼 Qwen系列\n"
            "性能优化: R-tree空间索引, Numba JIT加速"
        )
        tech_text.setWordWrap(True)
        info_layout.addWidget(tech_text)

        # 许可证
        license_title = BodyLabel("许可证")
        license_title.setStyleSheet("font-size: 14px; font-weight: bold; margin-top: 10px;")
        info_layout.addWidget(license_title)

        license_text = BodyLabel("商业软件 - 版权所有 © 2025")
        info_layout.addWidget(license_text)

        # 链接
        links_title = BodyLabel("链接")
        links_title.setStyleSheet("font-size: 14px; font-weight: bold; margin-top: 10px;")
        info_layout.addWidget(links_title)

        links_text = BodyLabel("GitHub | 阿里云百炼 | support@biaoge.com")
        info_layout.addWidget(links_text)

        # 底部信息
        footer_label = BodyLabel("Powered by Claude AI | Made for Engineers")
        footer_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        footer_label.setStyleSheet("color: #888; font-size: 11px; margin-top: 10px;")
        info_layout.addWidget(footer_label)

        layout.addWidget(info_card)

        # 添加滚动区域
        scroll_area = SmoothScrollArea()
        scroll_area.setWidget(content_widget)
        scroll_area.setWidgetResizable(True)
        scroll_area.setStyleSheet("QScrollArea { border: none; }")

        # 设置内容
        main_layout = QVBoxLayout()
        main_layout.addWidget(scroll_area)

        # 确定按钮
        button_layout = QHBoxLayout()
        button_layout.addStretch()

        ok_button = PrimaryPushButton("确定", self)
        ok_button.clicked.connect(self.accept)
        ok_button.setFixedWidth(100)

        button_layout.addWidget(ok_button)
        main_layout.addLayout(button_layout)

        # 设置对话框内容
        self.textLayout.addLayout(main_layout)
