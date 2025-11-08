# -*- coding: utf-8 -*-
"""
翻译界面
"""
from PyQt6.QtWidgets import QWidget, QVBoxLayout, QHBoxLayout, QGridLayout
from PyQt6.QtCore import Qt

from qfluentwidgets import (
    CardWidget, PrimaryPushButton, PushButton,
    ComboBox, ProgressBar, TextEdit,
    InfoBar, InfoBarPosition, TitleLabel, BodyLabel,
    FluentIcon
)

from ..translation.engine import TranslationEngine, TranslationWorker
from ..services.bailian_client import BailianClient, BailianAPIError
from ..translation.cache import TranslationCache
from ..dwg.entities import DWGDocument
from ..utils.logger import logger


class TranslationInterface(QWidget):
    """翻译界面"""

    # 支持的语言
    LANGUAGES = {
        '中文': 'Chinese',
        '英文': 'English',
        '日文': 'Japanese',
        '韩文': 'Korean',
        '德文': 'German',
        '法文': 'French',
        '西班牙文': 'Spanish',
        '俄文': 'Russian'
    }

    def __init__(self, parent=None):
        super().__init__(parent=parent)
        self.document: DWGDocument = None
        self.translation_worker: TranslationWorker = None
        self.setupUI()

    def setupUI(self):
        """设置UI"""
        layout = QVBoxLayout(self)
        layout.setContentsMargins(20, 20, 20, 20)
        layout.setSpacing(20)

        # 标题
        title = TitleLabel("图纸翻译", self)
        layout.addWidget(title)

        # 语言选择卡片
        lang_card = self._createLanguageCard()
        layout.addWidget(lang_card)

        # 翻译控制卡片
        control_card = self._createControlCard()
        layout.addWidget(control_card)

        # 统计信息卡片
        stats_card = self._createStatsCard()
        layout.addWidget(stats_card)

        layout.addStretch(1)

    def _createLanguageCard(self):
        """创建语言选择卡片"""
        card = CardWidget(self)
        layout = QGridLayout(card)
        layout.setContentsMargins(20, 20, 20, 20)
        layout.setSpacing(15)

        # 源语言
        from_label = BodyLabel("源语言:", self)
        layout.addWidget(from_label, 0, 0)

        self.fromLangCombo = ComboBox(self)
        self.fromLangCombo.addItems(list(self.LANGUAGES.keys()))
        self.fromLangCombo.setCurrentText('中文')
        layout.addWidget(self.fromLangCombo, 0, 1)

        # 目标语言
        to_label = BodyLabel("目标语言:", self)
        layout.addWidget(to_label, 1, 0)

        self.toLangCombo = ComboBox(self)
        self.toLangCombo.addItems(list(self.LANGUAGES.keys()))
        self.toLangCombo.setCurrentText('英文')
        layout.addWidget(self.toLangCombo, 1, 1)

        return card

    def _createControlCard(self):
        """创建翻译控制卡片"""
        card = CardWidget(self)
        layout = QVBoxLayout(card)
        layout.setContentsMargins(20, 20, 20, 20)
        layout.setSpacing(15)

        # 按钮
        btn_layout = QHBoxLayout()

        self.translateBtn = PrimaryPushButton("开始翻译", self)
        self.translateBtn.setIcon(FluentIcon.SYNC)
        self.stopBtn = PushButton("停止", self)
        self.stopBtn.setIcon(FluentIcon.CANCEL)

        self.translateBtn.clicked.connect(self.onTranslate)
        self.stopBtn.clicked.connect(self.onStop)
        self.stopBtn.setEnabled(False)

        btn_layout.addWidget(self.translateBtn)
        btn_layout.addWidget(self.stopBtn)
        btn_layout.addStretch(1)

        layout.addLayout(btn_layout)

        # 进度条
        self.progressBar = ProgressBar(self)
        self.progressBar.setValue(0)
        layout.addWidget(self.progressBar)

        # 状态标签
        self.statusLabel = BodyLabel("就绪", self)
        layout.addWidget(self.statusLabel)

        return card

    def _createStatsCard(self):
        """创建统计信息卡片"""
        card = CardWidget(self)
        layout = QVBoxLayout(card)
        layout.setContentsMargins(20, 20, 20, 20)
        layout.setSpacing(10)

        self.statsText = TextEdit(self)
        self.statsText.setReadOnly(True)
        self.statsText.setMaximumHeight(200)
        self.statsText.setPlainText("等待翻译...")

        layout.addWidget(self.statsText)

        return card

    def setDocument(self, document: DWGDocument):
        """设置要翻译的文档"""
        self.document = document
        self.translateBtn.setEnabled(True)

        # 统计文本实体
        text_count = sum(1 for e in document.entities if hasattr(e, 'text') and e.text)
        self.statusLabel.setText(f"文档已加载，找到 {text_count} 个文本实体")
        logger.info(f"翻译界面加载文档: {text_count}个文本实体")

    def onTranslate(self):
        """开始翻译"""
        if not self.document:
            self._showError("请先打开一个DWG文件")
            return

        # 检查API密钥
        import os
        if not os.getenv('DASHSCOPE_API_KEY'):
            self._showError(
                "未配置API密钥\n\n"
                "请设置环境变量 DASHSCOPE_API_KEY\n"
                "或在设置中配置API密钥"
            )
            return

        # 获取语言选择
        from_lang = self.LANGUAGES[self.fromLangCombo.currentText()]
        to_lang = self.LANGUAGES[self.toLangCombo.currentText()]

        if from_lang == to_lang:
            self._showError("源语言和目标语言不能相同")
            return

        # 禁用翻译按钮
        self.translateBtn.setEnabled(False)
        self.stopBtn.setEnabled(True)
        self.progressBar.setValue(0)
        self.statusLabel.setText("正在初始化...")

        try:
            # 创建翻译引擎
            client = BailianClient()
            cache = TranslationCache()
            engine = TranslationEngine(client, cache)

            # 创建工作线程
            self.translation_worker = TranslationWorker(
                engine,
                self.document,
                from_lang,
                to_lang
            )

            # 连接信号
            self.translation_worker.progress.connect(self._onProgress)
            self.translation_worker.finished.connect(self._onFinished)
            self.translation_worker.error.connect(self._onError)

            # 启动翻译
            self.translation_worker.start()

            logger.info(f"开始翻译: {from_lang} -> {to_lang}")

        except BailianAPIError as e:
            self._showError(f"API初始化失败: {e}")
            self.translateBtn.setEnabled(True)
            self.stopBtn.setEnabled(False)
        except Exception as e:
            self._showError(f"未知错误: {e}")
            self.translateBtn.setEnabled(True)
            self.stopBtn.setEnabled(False)

    def onStop(self):
        """停止翻译"""
        if self.translation_worker and self.translation_worker.isRunning():
            self.translation_worker.terminate()
            self.translation_worker.wait()
            self.statusLabel.setText("已停止")
            self.translateBtn.setEnabled(True)
            self.stopBtn.setEnabled(False)
            logger.info("翻译已停止")

    def _onProgress(self, current: int, total: int, message: str):
        """进度更新"""
        if total > 0:
            progress = int(current / total * 100)
            self.progressBar.setValue(progress)
        self.statusLabel.setText(f"{message} ({current}/{total})")

    def _onFinished(self, stats):
        """翻译完成"""
        self.translateBtn.setEnabled(True)
        self.stopBtn.setEnabled(False)
        self.progressBar.setValue(100)
        self.statusLabel.setText("翻译完成！")

        # 显示统计信息
        stats_text = f"""翻译完成！

总实体数: {stats.total_entities}
唯一文本: {stats.unique_texts}
缓存命中: {stats.cached_count}
API翻译: {stats.translated_count}
跳过: {stats.skipped_count}

Token消耗: {stats.total_tokens}
成本: ¥{stats.total_cost:.4f}
耗时: {stats.duration_seconds:.2f}秒

缓存命中率: {stats.cached_count / stats.unique_texts * 100 if stats.unique_texts > 0 else 0:.1f}%
"""

        # 添加质量控制统计
        if stats.quality_checked > 0:
            stats_text += f"""
{'='*50}
质量控制统计 (99.9999%准确率目标)
{'='*50}

检查数量: {stats.quality_checked}
完美翻译: {stats.quality_perfect}
自动修正: {stats.quality_corrected}
警告: {stats.quality_warnings}
错误: {stats.quality_errors}

平均质量分: {stats.average_quality_score:.2f}%
完美率: {stats.quality_perfect / stats.quality_checked * 100:.2f}%
修正率: {stats.quality_corrected / stats.quality_checked * 100:.2f}%
"""

        self.statsText.setPlainText(stats_text)

        # 显示成功提示
        self._showSuccess(
            f"翻译完成！\n"
            f"翻译了 {stats.translated_count} 条新文本\n"
            f"成本: ¥{stats.total_cost:.4f}"
        )

        # 通知父窗口刷新画布
        parent = self.parent()
        if parent and hasattr(parent, 'refreshCanvas'):
            parent.refreshCanvas()

        # 更新AI助手上下文
        if hasattr(self, 'parent_window') and hasattr(self.parent_window, 'context_manager'):
            try:
                from datetime import datetime
                from_lang = self.LANGUAGES[self.fromLangCombo.currentText()]
                to_lang = self.LANGUAGES[self.toLangCombo.currentText()]
                self.parent_window.context_manager.set_translation_results(
                    stats,
                    from_lang,
                    to_lang,
                    datetime.now().strftime("%Y-%m-%d %H:%M:%S")
                )
                logger.info("翻译结果已更新到AI助手上下文")
            except Exception as e:
                logger.warning(f"更新翻译上下文失败: {e}")

        logger.info(f"翻译完成: {stats.to_dict()}")

    def _onError(self, error_message: str):
        """翻译错误"""
        self.translateBtn.setEnabled(True)
        self.stopBtn.setEnabled(False)
        self.statusLabel.setText("翻译失败")

        self._showError(f"翻译失败: {error_message}")
        logger.error(f"翻译失败: {error_message}")

    def _showError(self, message: str):
        """显示错误消息"""
        InfoBar.error(
            title='错误',
            content=message,
            orient=Qt.Orientation.Horizontal,
            isClosable=True,
            position=InfoBarPosition.TOP,
            duration=3000,
            parent=self
        )

    def _showSuccess(self, message: str):
        """显示成功消息"""
        InfoBar.success(
            title='成功',
            content=message,
            orient=Qt.Orientation.Horizontal,
            isClosable=True,
            position=InfoBarPosition.TOP,
            duration=3000,
            parent=self
        )
