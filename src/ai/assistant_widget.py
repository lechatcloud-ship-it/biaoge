# -*- coding: utf-8 -*-
"""
AI助手UI组件
"""
from PyQt6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QTextEdit,
    QLineEdit, QPushButton, QLabel, QScrollArea,
    QFrame, QSizePolicy, QComboBox
)
from PyQt6.QtCore import Qt, pyqtSignal, QThread, pyqtSlot
from PyQt6.QtGui import QFont, QTextCursor

try:
    from qfluentwidgets import (
        PushButton, PrimaryPushButton, TextEdit,
        LineEdit, TitleLabel, BodyLabel, CardWidget
    )
    FLUENT = True
except:
    FLUENT = False

from ..utils.logger import logger
from datetime import datetime


class AIStreamWorker(QThread):
    """AI流式响应工作线程"""

    # 信号
    chunk_received = pyqtSignal(str)  # 收到文本块
    thinking_received = pyqtSignal(str)  # 收到思考内容
    finished = pyqtSignal()  # 完成
    error = pyqtSignal(str)  # 错误

    def __init__(self, ai_assistant, user_message, enable_thinking=False):
        super().__init__()
        self.ai_assistant = ai_assistant
        self.user_message = user_message
        self.enable_thinking = enable_thinking

    def run(self):
        """运行流式对话"""
        try:
            for chunk in self.ai_assistant.chat_stream(
                self.user_message,
                enable_thinking=self.enable_thinking
            ):
                if 'choices' in chunk and len(chunk['choices']) > 0:
                    delta = chunk['choices'][0].get('delta', {})

                    # 发送思考内容
                    if 'reasoning_content' in delta:
                        self.thinking_received.emit(delta['reasoning_content'])

                    # 发送回复内容
                    if 'content' in delta:
                        self.chunk_received.emit(delta['content'])

            self.finished.emit()

        except Exception as e:
            logger.error(f"流式对话错误: {e}", exc_info=True)
            self.error.emit(str(e))


class AIAssistantWidget(QWidget):
    """AI助手对话界面"""

    # 信号
    message_sent = pyqtSignal(str)  # 用户发送消息

    def __init__(self, parent=None, ai_assistant=None):
        super().__init__(parent)
        self.conversation_history = []
        self.ai_assistant = ai_assistant  # AI助手实例
        self.stream_worker = None  # 流式工作线程
        self.current_ai_message_id = None  # 当前正在接收的AI消息ID
        self.setupUI()
        logger.info("AI助手界面初始化完成")

    def setupUI(self):
        """设置UI"""
        layout = QVBoxLayout(self)
        layout.setContentsMargins(10, 10, 10, 10)
        layout.setSpacing(10)

        # 标题栏
        header = self._createHeader()
        layout.addWidget(header)

        # 对话历史区域
        self.chatHistory = self._createChatHistory()
        layout.addWidget(self.chatHistory, 1)  # 占据主要空间

        # 快捷操作区域
        shortcuts = self._createShortcuts()
        layout.addWidget(shortcuts)

        # 输入区域
        inputArea = self._createInputArea()
        layout.addWidget(inputArea)

        # 初始欢迎消息
        self.addAIMessage(
            "您好！我是DWG智能助手 🤖\n\n"
            "我可以帮您：\n"
            "• 分析图纸内容和结构\n"
            "• 解答翻译质量问题\n"
            "• 解释算量结果\n"
            "• 提供优化建议\n"
            "• 生成各类报表\n\n"
            "请随时向我提问！"
        )

    def _createHeader(self):
        """创建标题栏"""
        header = QFrame()
        header.setFrameShape(QFrame.Shape.StyledPanel)
        layout = QHBoxLayout(header)

        if FLUENT:
            title = TitleLabel("AI助手 - DWG智能分析师")
        else:
            title = QLabel("AI助手 - DWG智能分析师")
            title.setStyleSheet("font-size: 16px; font-weight: bold;")

        layout.addWidget(title)
        layout.addStretch()

        # 模型选择
        layout.addWidget(QLabel("模型:"))
        self.modelCombo = QComboBox()
        self.modelCombo.addItems(["qwen-max", "qwen-plus", "qwen3-max", "qwq-max-preview"])
        self.modelCombo.setCurrentText("qwen-max")
        self.modelCombo.currentTextChanged.connect(self.onModelChanged)
        layout.addWidget(self.modelCombo)

        # 深度思考开关
        layout.addWidget(QLabel("深度思考:"))
        self.thinkingCombo = QComboBox()
        self.thinkingCombo.addItems(["关闭", "开启"])
        self.thinkingCombo.currentTextChanged.connect(self.onThinkingModeChanged)
        layout.addWidget(self.thinkingCombo)

        # 状态指示器
        self.statusLabel = QLabel("● 在线")
        self.statusLabel.setStyleSheet("color: green;")
        layout.addWidget(self.statusLabel)

        return header

    def _createChatHistory(self):
        """创建对话历史区域"""
        if FLUENT:
            chatHistory = TextEdit()
        else:
            chatHistory = QTextEdit()

        chatHistory.setReadOnly(True)
        chatHistory.setMinimumHeight(400)

        # 设置字体
        font = QFont("Microsoft YaHei", 10)
        chatHistory.setFont(font)

        return chatHistory

    def _createShortcuts(self):
        """创建快捷操作按钮"""
        shortcuts = QFrame()
        shortcuts.setFrameShape(QFrame.Shape.StyledPanel)
        layout = QVBoxLayout(shortcuts)

        if FLUENT:
            label = BodyLabel("快捷操作")
        else:
            label = QLabel("快捷操作")
            label.setStyleSheet("font-weight: bold;")

        layout.addWidget(label)

        # 快捷按钮
        buttons_layout = QHBoxLayout()

        if FLUENT:
            btn1 = PushButton("生成工程量清单")
            btn2 = PushButton("生成材料汇总")
            btn3 = PushButton("成本估算")
        else:
            btn1 = QPushButton("生成工程量清单")
            btn2 = QPushButton("生成材料汇总")
            btn3 = QPushButton("成本估算")

        btn1.clicked.connect(lambda: self._sendQuickMessage("请生成完整的工程量清单"))
        btn2.clicked.connect(lambda: self._sendQuickMessage("请生成材料汇总表"))
        btn3.clicked.connect(lambda: self._sendQuickMessage("请估算工程成本"))

        buttons_layout.addWidget(btn1)
        buttons_layout.addWidget(btn2)
        buttons_layout.addWidget(btn3)

        layout.addLayout(buttons_layout)

        return shortcuts

    def _createInputArea(self):
        """创建输入区域"""
        inputArea = QFrame()
        inputArea.setFrameShape(QFrame.Shape.StyledPanel)
        layout = QVBoxLayout(inputArea)

        # 输入框
        if FLUENT:
            self.inputField = LineEdit()
        else:
            self.inputField = QLineEdit()

        self.inputField.setPlaceholderText("💬 请输入您的问题...")
        self.inputField.returnPressed.connect(self.onSendMessage)
        layout.addWidget(self.inputField)

        # 按钮行
        buttons = QHBoxLayout()

        if FLUENT:
            self.sendBtn = PrimaryPushButton("发送")
            self.clearBtn = PushButton("清空对话")
        else:
            self.sendBtn = QPushButton("发送")
            self.clearBtn = QPushButton("清空对话")

        self.sendBtn.clicked.connect(self.onSendMessage)
        self.clearBtn.clicked.connect(self.onClearHistory)

        buttons.addWidget(self.sendBtn)
        buttons.addWidget(self.clearBtn)
        buttons.addStretch()

        layout.addLayout(buttons)

        return inputArea

    def onSendMessage(self):
        """发送消息"""
        message = self.inputField.text().strip()
        if not message:
            return

        # 清空输入框
        self.inputField.clear()

        # 禁用发送按钮
        self.sendBtn.setEnabled(False)
        self.inputField.setEnabled(False)

        # 显示用户消息
        self.addUserMessage(message)

        # 如果有AI助手实例，使用流式对话
        if self.ai_assistant:
            self.startStreamingChat(message)
        else:
            # 否则发送信号（由外部处理）
            self.message_sent.emit(message)
            self.sendBtn.setEnabled(True)
            self.inputField.setEnabled(True)

        logger.debug(f"用户消息: {message}")

    def _sendQuickMessage(self, message: str):
        """发送快捷消息"""
        self.inputField.setText(message)
        self.onSendMessage()

    def addUserMessage(self, message: str):
        """添加用户消息到对话历史"""
        timestamp = datetime.now().strftime("%H:%M:%S")
        formatted_message = f"""
<div style='margin: 10px 0; text-align: right;'>
    <div style='display: inline-block; max-width: 70%; background-color: #DCF8C6;
                padding: 10px; border-radius: 10px; text-align: left;'>
        <b>👤 用户</b> <span style='color: gray; font-size: 10px;'>{timestamp}</span><br>
        {self._formatMessage(message)}
    </div>
</div>
"""
        self.chatHistory.append(formatted_message)
        self._scrollToBottom()

        # 保存到历史
        self.conversation_history.append({
            'role': 'user',
            'content': message,
            'timestamp': timestamp
        })

    def addAIMessage(self, message: str):
        """添加AI消息到对话历史"""
        timestamp = datetime.now().strftime("%H:%M:%S")
        formatted_message = f"""
<div style='margin: 10px 0;'>
    <div style='display: inline-block; max-width: 70%; background-color: #E8E8E8;
                padding: 10px; border-radius: 10px;'>
        <b>🤖 AI助手</b> <span style='color: gray; font-size: 10px;'>{timestamp}</span><br>
        {self._formatMessage(message)}
    </div>
</div>
"""
        self.chatHistory.append(formatted_message)
        self._scrollToBottom()

        # 保存到历史
        self.conversation_history.append({
            'role': 'assistant',
            'content': message,
            'timestamp': timestamp
        })

    def addSystemMessage(self, message: str):
        """添加系统消息"""
        timestamp = datetime.now().strftime("%H:%M:%S")
        formatted_message = f"""
<div style='margin: 10px 0; text-align: center;'>
    <span style='color: gray; font-size: 11px;'>
        {message} ({timestamp})
    </span>
</div>
"""
        self.chatHistory.append(formatted_message)
        self._scrollToBottom()

    def _formatMessage(self, message: str) -> str:
        """格式化消息（支持简单的markdown）"""
        # 换行
        message = message.replace('\n', '<br>')

        # 粗体
        message = message.replace('**', '<b>').replace('**', '</b>')

        # 代码块（简单处理）
        message = message.replace('`', '<code style="background-color: #f0f0f0; padding: 2px 4px;">')
        message = message.replace('`', '</code>')

        return message

    def _scrollToBottom(self):
        """滚动到底部"""
        cursor = self.chatHistory.textCursor()
        cursor.movePosition(QTextCursor.MoveOperation.End)
        self.chatHistory.setTextCursor(cursor)

    def onClearHistory(self):
        """清空对话历史"""
        self.chatHistory.clear()
        self.conversation_history.clear()
        logger.info("对话历史已清空")

        # 重新添加欢迎消息
        self.addAIMessage(
            "对话历史已清空。\n"
            "继续向我提问吧！"
        )

    def setStatus(self, online: bool):
        """设置在线状态"""
        if online:
            self.statusLabel.setText("● 在线")
            self.statusLabel.setStyleSheet("color: green;")
        else:
            self.statusLabel.setText("● 离线")
            self.statusLabel.setStyleSheet("color: gray;")

    def getConversationHistory(self):
        """获取对话历史"""
        return self.conversation_history

    # ========== 流式对话方法 ==========

    def startStreamingChat(self, message: str):
        """开始流式对话"""
        if self.stream_worker and self.stream_worker.isRunning():
            logger.warning("已有流式对话正在进行中")
            return

        # 创建AI消息占位符
        self.current_ai_message_id = self._addAIMessagePlaceholder()

        # 获取当前设置
        enable_thinking = (self.thinkingCombo.currentText() == "开启")

        # 创建工作线程
        self.stream_worker = AIStreamWorker(
            self.ai_assistant,
            message,
            enable_thinking=enable_thinking
        )

        # 连接信号
        self.stream_worker.chunk_received.connect(self.onChunkReceived)
        self.stream_worker.thinking_received.connect(self.onThinkingReceived)
        self.stream_worker.finished.connect(self.onStreamFinished)
        self.stream_worker.error.connect(self.onStreamError)

        # 启动线程
        self.stream_worker.start()

        logger.info("流式对话已启动")

    @pyqtSlot(str)
    def onChunkReceived(self, chunk: str):
        """接收到文本块"""
        self._appendToCurrentAIMessage(chunk)

    @pyqtSlot(str)
    def onThinkingReceived(self, thinking: str):
        """接收到思考内容"""
        # 可以选择显示或隐藏思考过程
        # 这里我们显示在灰色区域
        self._appendThinkingToCurrentAIMessage(thinking)

    @pyqtSlot()
    def onStreamFinished(self):
        """流式对话完成"""
        logger.info("流式对话已完成")
        self._finalizeCurrentAIMessage()

        # 重新启用发送按钮
        self.sendBtn.setEnabled(True)
        self.inputField.setEnabled(True)

    @pyqtSlot(str)
    def onStreamError(self, error_msg: str):
        """流式对话错误"""
        logger.error(f"流式对话错误: {error_msg}")
        self._appendToCurrentAIMessage(f"\n\n错误: {error_msg}")
        self._finalizeCurrentAIMessage()

        # 重新启用发送按钮
        self.sendBtn.setEnabled(True)
        self.inputField.setEnabled(True)

    def _addAIMessagePlaceholder(self) -> str:
        """添加AI消息占位符"""
        timestamp = datetime.now().strftime("%H:%M:%S")
        message_id = f"ai_msg_{timestamp.replace(':', '')}"

        formatted_message = f"""
<div id='{message_id}' style='margin: 10px 0;'>
    <div style='display: inline-block; max-width: 70%; background-color: #E8E8E8;
                padding: 10px; border-radius: 10px;'>
        <b>🤖 AI助手</b> <span style='color: gray; font-size: 10px;'>{timestamp}</span><br>
        <span id='{message_id}_content'>⏳ 正在思考...</span>
    </div>
</div>
"""
        self.chatHistory.append(formatted_message)
        self._scrollToBottom()

        return message_id

    def _appendToCurrentAIMessage(self, text: str):
        """追加文本到当前AI消息"""
        if not self.current_ai_message_id:
            return

        # 获取当前HTML
        current_html = self.chatHistory.toHtml()

        # 查找并替换"正在思考..."占位符
        if "⏳ 正在思考..." in current_html:
            current_html = current_html.replace("⏳ 正在思考...", text)
        else:
            # 追加到现有内容
            # 简化处理：直接追加到末尾
            cursor = self.chatHistory.textCursor()
            cursor.movePosition(QTextCursor.MoveOperation.End)
            cursor.insertHtml(self._formatMessage(text))

        self._scrollToBottom()

    def _appendThinkingToCurrentAIMessage(self, thinking: str):
        """追加思考内容到当前AI消息"""
        if not self.current_ai_message_id:
            return

        # 在单独的灰色框中显示思考内容
        thinking_html = f"""
<div style='margin: 5px 0 5px 20px; padding: 5px; background-color: #F5F5F5;
            border-left: 3px solid #888; font-size: 10px; color: #666;'>
    💭 思考: {self._formatMessage(thinking)}
</div>
"""
        cursor = self.chatHistory.textCursor()
        cursor.movePosition(QTextCursor.MoveOperation.End)
        cursor.insertHtml(thinking_html)
        self._scrollToBottom()

    def _finalizeCurrentAIMessage(self):
        """完成当前AI消息"""
        self.current_ai_message_id = None
        self._scrollToBottom()

    # ========== 模型和模式设置 ==========

    def onModelChanged(self, model: str):
        """模型切换"""
        if self.ai_assistant:
            self.ai_assistant.set_model(model)
            logger.info(f"已切换模型: {model}")
            self.addSystemMessage(f"已切换到模型: {model}")

    def onThinkingModeChanged(self, mode: str):
        """深度思考模式切换"""
        enable = (mode == "开启")
        if self.ai_assistant:
            self.ai_assistant.set_thinking_mode(enable)
            logger.info(f"深度思考模式: {mode}")
            self.addSystemMessage(f"深度思考模式: {mode}")

    def setAIAssistant(self, ai_assistant):
        """设置AI助手实例"""
        self.ai_assistant = ai_assistant
        logger.info("AI助手实例已设置")
