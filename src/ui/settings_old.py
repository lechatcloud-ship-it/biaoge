# -*- coding: utf-8 -*-
"""设置界面"""
from PyQt6.QtWidgets import QWidget, QVBoxLayout, QPushButton, QLineEdit, QLabel, QComboBox, QFormLayout, QMessageBox
from PyQt6.QtCore import Qt
import os
try:
    from qfluentwidgets import CardWidget, LineEdit, ComboBox, PushButton, TitleLabel
    FLUENT = True
except:
    CardWidget = QWidget
    LineEdit = QLineEdit
    ComboBox = QComboBox
    PushButton = QPushButton
    TitleLabel = QLabel
    FLUENT = False

from ..utils.logger import logger

class SettingsInterface(QWidget):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setupUI()
        self.loadSettings()
    
    def setupUI(self):
        layout = QVBoxLayout(self)
        title = TitleLabel("设置") if FLUENT else QLabel("设置")
        layout.addWidget(title)
        
        card = CardWidget() if FLUENT else QWidget()
        form = QFormLayout(card)
        
        self.apiKeyEdit = LineEdit() if FLUENT else QLineEdit()
        self.apiKeyEdit.setEchoMode(QLineEdit.EchoMode.Password)
        form.addRow("API密钥:", self.apiKeyEdit)
        
        self.modelCombo = ComboBox() if FLUENT else QComboBox()
        self.modelCombo.addItems(["qwen-plus", "qwen-max", "qwen-turbo"])
        form.addRow("模型:", self.modelCombo)
        
        layout.addWidget(card)
        
        self.saveBtn = PushButton("保存") if FLUENT else QPushButton("保存")
        self.saveBtn.clicked.connect(self.onSave)
        layout.addWidget(self.saveBtn)
        
        layout.addStretch()
    
    def loadSettings(self):
        api_key = os.getenv('DASHSCOPE_API_KEY', '')
        if api_key:
            self.apiKeyEdit.setText(api_key[:10] + "..." if len(api_key) > 10 else api_key)
    
    def onSave(self):
        api_key = self.apiKeyEdit.text()
        if api_key and not api_key.endswith("..."):
            os.environ['DASHSCOPE_API_KEY'] = api_key
        QMessageBox.information(self, "成功", "设置已保存！\n(重启应用后生效)")
        logger.info("设置已保存")
