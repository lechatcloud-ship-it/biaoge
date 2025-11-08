# -*- coding: utf-8 -*-
"""
AI对话助手窗口
基于业界最佳实践（微软、IBM对话设计指南）
"""
from PyQt6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QTextEdit,
    QLineEdit, QPushButton, QLabel, QScrollArea,
    QFrame, QMessageBox, QSplitter
)
from PyQt6.QtCore import Qt, QThread, pyqtSignal, QTimer
from PyQt6.QtGui import QTextCursor, QFont, QColor
from datetime import datetime
from typing import Optional, List, Dict, Any
import json

from ..services.bailian_client import BailianClient
from ..dwg.entities import DWGDocument
from ..utils.logger import logger


class AIMessageWidget(QFrame):
    """单条AI消息组件"""

    def __init__(self, role: str, content: str, timestamp: str, parent=None):
        super().__init__(parent)
        self.role = role
        self.content = content
        self.timestamp = timestamp

        self.setFrameShape(QFrame.Shape.StyledPanel)
        self.setup_ui()

    def setup_ui(self):
        """设置UI"""
        layout = QVBoxLayout(self)
        layout.setContentsMargins(10, 8, 10, 8)
        layout.setSpacing(5)

        # 头部：角色 + 时间
        header_layout = QHBoxLayout()

        role_label = QLabel(f"{'👤 您' if self.role == 'user' else '🤖 AI助手'}")
        role_label.setStyleSheet(f"""
            font-weight: bold;
            color: {'#0078D4' if self.role == 'user' else '#107C10'};
        """)
        header_layout.addWidget(role_label)

        header_layout.addStretch()

        time_label = QLabel(self.timestamp)
        time_label.setStyleSheet("color: #666; font-size: 11px;")
        header_layout.addWidget(time_label)

        layout.addLayout(header_layout)

        # 消息内容
        content_label = QLabel(self.content)
        content_label.setWordWrap(True)
        content_label.setTextInteractionFlags(
            Qt.TextInteractionFlag.TextSelectableByMouse |
            Qt.TextInteractionFlag.LinksClickable
        )
        content_label.setStyleSheet("""
            padding: 8px;
            background-color: #F5F5F5;
            border-radius: 6px;
            line-height: 1.5;
        """)
        layout.addWidget(content_label)

        # 不同角色的背景色
        if self.role == 'user':
            self.setStyleSheet("""
                AIMessageWidget {
                    background-color: #E3F2FD;
                    border-left: 3px solid #0078D4;
                    margin-bottom: 10px;
                }
            """)
        else:
            self.setStyleSheet("""
                AIMessageWidget {
                    background-color: #F0F9FF;
                    border-left: 3px solid #107C10;
                    margin-bottom: 10px;
                }
            """)


class AIResponseThread(QThread):
    """AI响应线程（异步处理）"""

    response_ready = pyqtSignal(str)  # AI回复内容
    error_occurred = pyqtSignal(str)  # 错误信息

    def __init__(self, client: BailianClient, messages: List[Dict], context_prompt: str):
        super().__init__()
        self.client = client
        self.messages = messages
        self.context_prompt = context_prompt

    def run(self):
        """运行AI请求"""
        try:
            # 构建完整消息
            full_messages = [
                {'role': 'system', 'content': self.context_prompt},
                *self.messages
            ]

            # 调用AI（使用qwen-max获得最佳推理能力）
            model = self.client.get_model_for_task('calculation')
            response = self.client._call_api(full_messages, model)

            ai_reply = response['translated_text'].strip()
            self.response_ready.emit(ai_reply)

        except Exception as e:
            logger.error(f"AI对话失败: {e}", exc_info=True)
            self.error_occurred.emit(str(e))


class AIChatWidget(QWidget):
    """
    AI对话助手窗口

    功能：
    - 上下文感知（知道当前文档、翻译、算量结果）
    - 智能问答（解释结果、修正错误、优化参数）
    - 操作建议（主动提供可行方案）
    - 历史记录（可回溯对话）

    设计原则（参考微软/IBM最佳实践）：
    1. 清晰透明 - 明确AI能力边界
    2. 场景感知 - 自动识别用户意图
    3. 简洁可操作 - 优先提供可执行建议
    """

    def __init__(self, parent=None):
        super().__init__(parent)

        # 当前上下文
        self.current_document: Optional[DWGDocument] = None
        self.current_context: Dict[str, Any] = {}
        self.chat_history: List[Dict[str, str]] = []

        # AI客户端
        try:
            self.ai_client = BailianClient()
        except Exception as e:
            logger.warning(f"AI客户端初始化失败: {e}")
            self.ai_client = None

        # AI响应线程
        self.ai_thread: Optional[AIResponseThread] = None

        self.setup_ui()
        self.send_welcome_message()

    def setup_ui(self):
        """设置UI"""
        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(0)

        # 标题栏
        title_bar = QFrame()
        title_bar.setStyleSheet("""
            QFrame {
                background-color: #0078D4;
                padding: 12px;
            }
        """)
        title_layout = QHBoxLayout(title_bar)
        title_layout.setContentsMargins(10, 5, 10, 5)

        title_label = QLabel("💬 AI智能助手")
        title_label.setStyleSheet("""
            font-size: 14px;
            font-weight: bold;
            color: white;
        """)
        title_layout.addWidget(title_label)

        subtitle_label = QLabel("由 qwen-max 驱动 · 专业建筑工程助手")
        subtitle_label.setStyleSheet("""
            font-size: 11px;
            color: rgba(255,255,255,0.8);
        """)
        title_layout.addWidget(subtitle_label)
        title_layout.addStretch()

        layout.addWidget(title_bar)

        # 消息显示区域（滚动）
        scroll_area = QScrollArea()
        scroll_area.setWidgetResizable(True)
        scroll_area.setStyleSheet("""
            QScrollArea {
                border: none;
                background-color: white;
            }
        """)

        # 消息容器
        self.messages_container = QWidget()
        self.messages_layout = QVBoxLayout(self.messages_container)
        self.messages_layout.setContentsMargins(10, 10, 10, 10)
        self.messages_layout.setSpacing(10)
        self.messages_layout.addStretch()  # 在底部添加弹簧

        scroll_area.setWidget(self.messages_container)
        layout.addWidget(scroll_area, 1)  # stretch=1，占据主要空间

        # 输入区域
        input_frame = QFrame()
        input_frame.setStyleSheet("""
            QFrame {
                background-color: #F5F5F5;
                border-top: 1px solid #DDD;
                padding: 10px;
            }
        """)
        input_layout = QVBoxLayout(input_frame)
        input_layout.setContentsMargins(10, 10, 10, 10)

        # 快捷命令提示
        hint_label = QLabel("💡 提示：直接输入问题，或使用 /help 查看帮助")
        hint_label.setStyleSheet("color: #666; font-size: 11px;")
        input_layout.addWidget(hint_label)

        # 输入框和发送按钮
        input_row = QHBoxLayout()

        self.user_input = QLineEdit()
        self.user_input.setPlaceholderText("输入您的问题，例如：这个梁的长度为什么是0？")
        self.user_input.setStyleSheet("""
            QLineEdit {
                padding: 10px;
                border: 1px solid #CCC;
                border-radius: 6px;
                font-size: 13px;
                background-color: white;
            }
            QLineEdit:focus {
                border: 2px solid #0078D4;
            }
        """)
        self.user_input.returnPressed.connect(self.on_send_message)
        input_row.addWidget(self.user_input, 1)

        self.send_button = QPushButton("📤 发送")
        self.send_button.setStyleSheet("""
            QPushButton {
                background-color: #0078D4;
                color: white;
                border: none;
                padding: 10px 20px;
                border-radius: 6px;
                font-weight: bold;
            }
            QPushButton:hover {
                background-color: #106EBE;
            }
            QPushButton:pressed {
                background-color: #005A9E;
            }
            QPushButton:disabled {
                background-color: #CCCCCC;
            }
        """)
        self.send_button.clicked.connect(self.on_send_message)
        input_row.addWidget(self.send_button)

        input_layout.addLayout(input_row)

        layout.addWidget(input_frame)

    def send_welcome_message(self):
        """发送欢迎消息"""
        welcome = """您好！我是表哥软件的AI助手 🤖

我可以帮您：
✅ 解释翻译结果
✅ 修正算量错误
✅ 分析尺寸异常
✅ 提供专业建议

常用命令：
/help - 查看完整帮助
/check - 检查当前结果
/clear - 清空对话历史

请告诉我您遇到了什么问题，我会尽力帮助您！"""

        self.add_message('assistant', welcome)

    def add_message(self, role: str, content: str):
        """添加消息到显示区域"""
        timestamp = datetime.now().strftime("%H:%M:%S")

        # 创建消息组件
        message_widget = AIMessageWidget(role, content, timestamp)

        # 添加到布局（在stretch之前）
        count = self.messages_layout.count()
        self.messages_layout.insertWidget(count - 1, message_widget)

        # 自动滚动到底部
        QTimer.singleShot(100, self.scroll_to_bottom)

    def scroll_to_bottom(self):
        """滚动到底部"""
        scroll_area = self.messages_container.parentWidget()
        if scroll_area:
            scrollbar = scroll_area.verticalScrollBar()
            scrollbar.setValue(scrollbar.maximum())

    def on_send_message(self):
        """发送消息"""
        user_message = self.user_input.text().strip()
        if not user_message:
            return

        # 清空输入框
        self.user_input.clear()

        # 添加用户消息
        self.add_message('user', user_message)

        # 添加到历史
        self.chat_history.append({
            'role': 'user',
            'content': user_message
        })

        # 处理命令
        if user_message.startswith('/'):
            self.handle_command(user_message)
            return

        # 禁用输入
        self.user_input.setEnabled(False)
        self.send_button.setEnabled(False)

        # 显示"思考中..."
        self.add_message('assistant', '🤔 思考中...')

        # 异步调用AI
        if self.ai_client:
            context_prompt = self.build_context_prompt()
            self.ai_thread = AIResponseThread(
                self.ai_client,
                self.chat_history.copy(),
                context_prompt
            )
            self.ai_thread.response_ready.connect(self.on_ai_response)
            self.ai_thread.error_occurred.connect(self.on_ai_error)
            self.ai_thread.start()
        else:
            self.on_ai_error("AI客户端未初始化，请检查API密钥配置")

    def on_ai_response(self, response: str):
        """收到AI回复"""
        # 移除"思考中..."
        count = self.messages_layout.count()
        if count > 1:
            item = self.messages_layout.itemAt(count - 2)
            if item and item.widget():
                item.widget().deleteLater()

        # 添加AI回复
        self.add_message('assistant', response)

        # 添加到历史
        self.chat_history.append({
            'role': 'assistant',
            'content': response
        })

        # 恢复输入
        self.user_input.setEnabled(True)
        self.send_button.setEnabled(True)
        self.user_input.setFocus()

    def on_ai_error(self, error_message: str):
        """AI错误处理"""
        # 移除"思考中..."
        count = self.messages_layout.count()
        if count > 1:
            item = self.messages_layout.itemAt(count - 2)
            if item and item.widget():
                item.widget().deleteLater()

        # 显示错误
        error_msg = f"❌ 抱歉，出现了错误：\n\n{error_message}\n\n请稍后重试或检查网络连接。"
        self.add_message('assistant', error_msg)

        # 恢复输入
        self.user_input.setEnabled(True)
        self.send_button.setEnabled(True)
        self.user_input.setFocus()

    def handle_command(self, command: str):
        """处理命令"""
        cmd = command.lower().strip()

        if cmd == '/help':
            help_text = """📖 AI助手帮助文档

【可用命令】
/help - 显示此帮助
/check - 检查当前文档的翻译和算量结果
/clear - 清空对话历史
/context - 查看当前上下文信息

【提问示例】
"为什么这个梁的体积是0？"
"KL1应该翻译成什么？"
"检查一下有没有明显错误"
"帮我优化识别参数"

【功能说明】
- 我可以访问当前文档的所有信息
- 我使用qwen-max模型，具备强大的推理能力
- 我会根据上下文给出具体建议
- 对于复杂问题，我会提供多个解决方案"""

            self.add_message('assistant', help_text)

        elif cmd == '/check':
            self.check_current_results()

        elif cmd == '/clear':
            reply = QMessageBox.question(
                self,
                "确认清空",
                "确定要清空对话历史吗？此操作不可恢复。",
                QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No
            )
            if reply == QMessageBox.StandardButton.Yes:
                self.clear_chat()

        elif cmd == '/context':
            self.show_context_info()

        else:
            self.add_message('assistant', f"❓ 未知命令：{command}\n\n请输入 /help 查看可用命令。")

    def check_current_results(self):
        """检查当前结果"""
        if not self.current_document:
            self.add_message('assistant', "⚠️ 当前没有打开的文档。请先打开一个DWG文件。")
            return

        # 构建检查消息
        check_msg = f"正在检查文档：{self.current_document.metadata.get('filename', '未知')}\n\n"
        check_msg += f"实体数量：{len(self.current_document.entities)}\n"
        check_msg += f"图层数量：{len(self.current_document.layers)}\n\n"

        if 'components' in self.current_context:
            components = self.current_context['components']
            check_msg += f"已识别构件：{len(components)}个\n\n"
            check_msg += "正在分析可能存在的问题..."

        self.add_message('assistant', check_msg)

        # TODO: 实际的智能检查逻辑

    def clear_chat(self):
        """清空对话"""
        # 清空历史
        self.chat_history.clear()

        # 清空UI（保留stretch）
        while self.messages_layout.count() > 1:
            item = self.messages_layout.takeAt(0)
            if item and item.widget():
                item.widget().deleteLater()

        # 重新发送欢迎消息
        self.send_welcome_message()

    def show_context_info(self):
        """显示上下文信息"""
        if self.current_document:
            info = f"""📋 当前上下文信息

【文档】
- 文件名：{self.current_document.metadata.get('filename', '未知')}
- 版本：{self.current_document.version}
- 实体：{len(self.current_document.entities)} 个
- 图层：{len(self.current_document.layers)} 个

【状态】
- 翻译状态：{'已完成' if self.current_context.get('translated') else '未翻译'}
- 算量状态：{'已完成' if self.current_context.get('calculated') else '未算量'}
"""
        else:
            info = "当前没有打开的文档"

        self.add_message('assistant', info)

    def build_context_prompt(self) -> str:
        """构建上下文Prompt"""
        prompt = """【角色】
你是表哥DWG智能翻译算量软件的专业AI助手，精通：
- CAD图纸翻译（建筑/结构/机电）
- 工程量计算（构件识别、尺寸提取）
- 建筑规范和标准做法
- 用户问题诊断和解决

【当前上下文】
"""

        # 文档信息
        if self.current_document:
            prompt += f"- 文件：{self.current_document.metadata.get('filename', '未知')}\n"
            prompt += f"- 实体数：{len(self.current_document.entities)}\n"
            prompt += f"- 图层数：{len(self.current_document.layers)}\n"

        # 状态信息
        if self.current_context:
            if 'components' in self.current_context:
                prompt += f"- 已识别构件：{len(self.current_context['components'])}个\n"
            if 'translated' in self.current_context:
                prompt += f"- 翻译状态：已完成\n"

        prompt += """
【对话原则】
1. 简洁专业 - 直接给出解决方案，避免冗长解释
2. 场景感知 - 结合当前文档信息给建议
3. 主动提示 - 发现问题主动告知
4. 可操作性 - 提供具体的操作步骤

【输出格式】
- 使用Markdown格式
- 问题描述简洁（1-2句）
- 解决方案分点列出
- 必要时提供示例

请协助用户解决问题。"""

        return prompt

    def set_document(self, document: DWGDocument):
        """设置当前文档"""
        self.current_document = document
        self.current_context = {
            'document': document,
            'translated': False,
            'calculated': False,
        }
        logger.info(f"AI助手：已加载文档 {document.metadata.get('filename')}")

    def update_context(self, key: str, value: Any):
        """更新上下文"""
        self.current_context[key] = value
