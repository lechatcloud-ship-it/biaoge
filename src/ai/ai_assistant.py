"""
AI助手核心类 - 提供智能对话和上下文感知能力
集成百炼QwenMax模型、流式输出、深度思考
"""
from typing import List, Dict, Optional, Any, Callable, Generator
from dataclasses import dataclass, field
from datetime import datetime
import json

from ..services.bailian_client import BailianClient, BailianAPIError
from ..dwg.entities import DWGDocument
from ..utils.logger import logger
from ..utils.config_manager import ConfigManager


@dataclass
class Message:
    """对话消息"""
    role: str  # 'user', 'assistant', 'system', 'tool'
    content: str
    timestamp: str
    tool_calls: Optional[List[Dict]] = None  # 工具调用信息
    tool_call_id: Optional[str] = None  # 工具调用ID
    reasoning_content: Optional[str] = None  # 深度思考内容


@dataclass
class Conversation:
    """对话会话"""
    id: str
    title: str
    created_at: str
    updated_at: str
    messages: List[Message] = field(default_factory=list)
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class Tool:
    """AI工具定义"""
    name: str
    description: str
    parameters: Dict
    function: Callable


class AIAssistant:
    """
    AI助手核心类

    功能：
    1. 集成百炼LLM进行智能对话（QwenMax多模态模型）
    2. 访问完整软件上下文（DWG + 翻译 + 算量）
    3. 工具调用框架（Function Calling）
    4. 流式输出支持
    5. 深度思考模式
    6. 多会话管理
    7. 对话历史管理
    """

    def __init__(
        self,
        client: Optional[BailianClient] = None,
        context_manager: Optional[Any] = None
    ):
        """
        初始化AI助手

        Args:
            client: 百炼API客户端，如果为None则自动创建
            context_manager: 上下文管理器，提供对软件数据的访问
        """
        self.client = client or BailianClient()
        self.context_manager = context_manager
        self.config = ConfigManager()

        # 会话管理
        self.conversations: Dict[str, Conversation] = {}
        self.current_conversation_id: Optional[str] = None

        # 工具注册表
        self.tools: Dict[str, Tool] = {}
        self._register_built_in_tools()

        # 系统提示词模板
        self.system_prompt = self._build_system_prompt()

        # AI配置
        self.model = self.config.get('ai.model', 'qwen-max')  # 🚀 默认使用qwen-max
        self.temperature = self.config.get('ai.temperature', 0.7)
        self.top_p = self.config.get('ai.top_p', 0.9)
        self.enable_thinking = self.config.get('ai.enable_thinking', False)  # 🚀 深度思考
        self.use_streaming = self.config.get('ai.use_streaming', True)  # 🚀 流式输出

        logger.info(
            f"AI助手初始化完成 - 模型: {self.model}, "
            f"流式: {self.use_streaming}, "
            f"深度思考: {self.enable_thinking}"
        )

        # 创建默认会话
        self.new_conversation()

    def _build_system_prompt(self) -> str:
        """构建系统提示词"""
        return """你是一个专业的DWG图纸智能分析助手，具备以下能力：

**核心能力：**
1. 📐 图纸分析：理解DWG图纸内容、图层结构、构件信息
2. 🌐 翻译质量分析：检查翻译准确性、识别专业术语问题
3. 📊 算量结果分析：解释工程量计算、材料用量统计
4. 🏗️ 钢筋分析：钢筋配置方案、用量汇总
5. 💡 智能建议：优化方案、规范检查、成本估算
6. 📚 学习改进：根据用户反馈持续优化

**专业知识：**
- 建筑行业标准：GB 50011-2010（抗震规范）、GB 50009-2012（荷载规范）、16G101-1（钢筋图集）
- 构件类型：框架梁(KL)、框架柱(KZ)、剪力墙(Q)、楼板(B)、基础(J)等
- 材料规格：混凝土等级(C20-C50)、钢筋等级(HPB300/HRB400/HRB500)
- 尺寸规范：梁最小宽度200mm、高度250mm，柱最小截面300×300等

**交互原则：**
1. 专业准确：使用建筑行业专业术语
2. 详细解释：提供清晰的分析和建议
3. 数据支撑：基于实际图纸和计算数据
4. 主动建议：发现问题主动提醒用户
5. 友好互动：保持专业但易于理解的语言风格

**工具使用：**
当需要获取图纸信息、翻译结果、算量数据时，优先调用可用工具获取准确数据。
"""

    def _register_built_in_tools(self):
        """注册内置工具"""

        # 工具1: 获取图纸信息
        self.register_tool(
            name="get_dwg_info",
            description="获取当前打开的DWG图纸的基本信息（文件名、图层数、实体数等）",
            parameters={
                "type": "object",
                "properties": {},
                "required": []
            },
            function=self._tool_get_dwg_info
        )

        # 工具2: 获取翻译结果
        self.register_tool(
            name="get_translation_results",
            description="获取图纸翻译结果统计信息（翻译数量、质量分数、问题列表等）",
            parameters={
                "type": "object",
                "properties": {
                    "include_details": {
                        "type": "boolean",
                        "description": "是否包含详细翻译内容"
                    }
                },
                "required": []
            },
            function=self._tool_get_translation_results
        )

        # 工具3: 获取算量结果
        self.register_tool(
            name="get_calculation_results",
            description="获取工程量计算结果（构件数量、体积、面积、费用等）",
            parameters={
                "type": "object",
                "properties": {
                    "component_type": {
                        "type": "string",
                        "description": "构件类型（BEAM/COLUMN/WALL/SLAB/ALL）",
                        "enum": ["BEAM", "COLUMN", "WALL", "SLAB", "ALL"]
                    }
                },
                "required": []
            },
            function=self._tool_get_calculation_results
        )

        # 工具4: 获取材料汇总
        self.register_tool(
            name="get_material_summary",
            description="获取材料用量汇总（混凝土、钢筋等）",
            parameters={
                "type": "object",
                "properties": {},
                "required": []
            },
            function=self._tool_get_material_summary
        )

        # 工具5: 获取成本估算
        self.register_tool(
            name="get_cost_estimate",
            description="获取工程成本估算",
            parameters={
                "type": "object",
                "properties": {
                    "include_breakdown": {
                        "type": "boolean",
                        "description": "是否包含详细成本分解"
                    }
                },
                "required": []
            },
            function=self._tool_get_cost_estimate
        )

        # 工具6: 生成报表
        self.register_tool(
            name="generate_report",
            description="生成工程量清单或材料汇总报表",
            parameters={
                "type": "object",
                "properties": {
                    "report_type": {
                        "type": "string",
                        "description": "报表类型",
                        "enum": ["quantity_list", "material_summary", "cost_breakdown"]
                    },
                    "format": {
                        "type": "string",
                        "description": "输出格式",
                        "enum": ["excel", "pdf", "text"]
                    }
                },
                "required": ["report_type"]
            },
            function=self._tool_generate_report
        )

        logger.info(f"已注册{len(self.tools)}个内置工具")

    def register_tool(
        self,
        name: str,
        description: str,
        parameters: Dict,
        function: Callable
    ):
        """
        注册自定义工具

        Args:
            name: 工具名称
            description: 工具描述
            parameters: 参数定义（JSON Schema格式）
            function: 工具函数
        """
        tool = Tool(
            name=name,
            description=description,
            parameters=parameters,
            function=function
        )
        self.tools[name] = tool
        logger.debug(f"已注册工具: {name}")

    def chat(
        self,
        user_message: str,
        use_streaming: Optional[bool] = None,
        enable_thinking: Optional[bool] = None
    ) -> str:
        """
        与AI进行对话（非流式）

        Args:
            user_message: 用户消息
            use_streaming: 是否使用流式输出，None则使用默认配置
            enable_thinking: 是否启用深度思考，None则使用默认配置

        Returns:
            AI回复内容
        """
        if use_streaming is None:
            use_streaming = self.use_streaming

        if use_streaming:
            # 如果要求流式，收集所有流式内容并返回
            full_response = ""
            for chunk in self.chat_stream(user_message, enable_thinking):
                if 'delta' in chunk and 'content' in chunk['delta']:
                    full_response += chunk['delta']['content']
            return full_response
        else:
            # 非流式对话
            return self._chat_completion(user_message, enable_thinking)

    def _chat_completion(
        self,
        user_message: str,
        enable_thinking: Optional[bool] = None
    ) -> str:
        """
        非流式对话

        Args:
            user_message: 用户消息
            enable_thinking: 是否启用深度思考

        Returns:
            AI回复内容
        """
        if enable_thinking is None:
            enable_thinking = self.enable_thinking

        # 添加用户消息到历史
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        self._add_message_to_current_conversation(Message(
            role='user',
            content=user_message,
            timestamp=timestamp
        ))

        logger.info(f"用户提问: {user_message[:50]}...")

        try:
            # 构建对话消息列表
            messages = self._build_messages()

            # 构建工具定义（Function Calling）
            tools_def = self._build_tools_definition()

            # 调用百炼API
            response = self.client.chat_completion(
                messages=messages,
                model=self.model,
                temperature=self.temperature,
                top_p=self.top_p,
                tools=tools_def if tools_def else None,
                stream=False,
                enable_thinking=enable_thinking
            )

            # 处理AI回复
            ai_message = self._process_response(response)

            # 添加AI回复到历史
            self._add_message_to_current_conversation(Message(
                role='assistant',
                content=ai_message,
                timestamp=datetime.now().strftime("%Y-%m-%d %H:%M:%S")
            ))

            logger.info(f"AI回复: {ai_message[:50]}...")
            return ai_message

        except BailianAPIError as e:
            error_msg = f"AI服务错误: {str(e)}"
            logger.error(error_msg)
            return f"抱歉，AI服务暂时不可用。错误信息：{str(e)}"

        except Exception as e:
            error_msg = f"对话异常: {str(e)}"
            logger.error(error_msg, exc_info=True)
            return f"抱歉，处理您的问题时发生了错误。请稍后再试。"

    def chat_stream(
        self,
        user_message: str,
        enable_thinking: Optional[bool] = None
    ) -> Generator[Dict, None, None]:
        """
        流式对话（生成器）

        Args:
            user_message: 用户消息
            enable_thinking: 是否启用深度思考

        Yields:
            Dict: 流式响应块
        """
        if enable_thinking is None:
            enable_thinking = self.enable_thinking

        # 添加用户消息到历史
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        self._add_message_to_current_conversation(Message(
            role='user',
            content=user_message,
            timestamp=timestamp
        ))

        logger.info(f"用户提问(流式): {user_message[:50]}...")

        try:
            # 构建对话消息列表
            messages = self._build_messages()

            # 构建工具定义
            tools_def = self._build_tools_definition()

            # 调用流式API
            full_content = ""
            reasoning_content = ""

            for chunk in self.client.chat_stream(
                messages=messages,
                model=self.model,
                temperature=self.temperature,
                top_p=self.top_p,
                enable_thinking=enable_thinking
            ):
                # 返回流式块给调用者
                yield chunk

                # 收集完整内容
                if 'choices' in chunk and len(chunk['choices']) > 0:
                    delta = chunk['choices'][0].get('delta', {})

                    # 收集思考内容
                    if 'reasoning_content' in delta:
                        reasoning_content += delta['reasoning_content']

                    # 收集回复内容
                    if 'content' in delta:
                        full_content += delta['content']

            # 保存完整的AI回复到历史
            self._add_message_to_current_conversation(Message(
                role='assistant',
                content=full_content,
                timestamp=datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
                reasoning_content=reasoning_content if reasoning_content else None
            ))

            logger.info(f"AI流式回复完成: {full_content[:50]}...")

        except BailianAPIError as e:
            error_msg = f"AI服务错误: {str(e)}"
            logger.error(error_msg)
            yield {
                'choices': [{
                    'delta': {'content': f"抱歉，AI服务暂时不可用。错误信息：{str(e)}"}
                }]
            }

        except Exception as e:
            error_msg = f"对话异常: {str(e)}"
            logger.error(error_msg, exc_info=True)
            yield {
                'choices': [{
                    'delta': {'content': f"抱歉，处理您的问题时发生了错误。请稍后再试。"}
                }]
            }

    def _build_messages(self) -> List[Dict]:
        """构建对话消息列表"""
        messages = []

        # 系统提示词
        messages.append({
            'role': 'system',
            'content': self.system_prompt
        })

        # 添加上下文信息（如果有）
        if self.context_manager:
            context_info = self._get_context_summary()
            if context_info:
                messages.append({
                    'role': 'system',
                    'content': f"**当前软件状态：**\n{context_info}"
                })

        # 当前会话的历史对话（保留最近10轮）
        current_conv = self.get_current_conversation()
        if current_conv:
            recent_history = current_conv.messages[-20:]  # 最多10轮对话
            for msg in recent_history:
                messages.append({
                    'role': msg.role,
                    'content': msg.content
                })

        return messages

    def _build_tools_definition(self) -> List[Dict]:
        """构建工具定义（Function Calling格式）"""
        tools_def = []

        for tool in self.tools.values():
            tools_def.append({
                'type': 'function',
                'function': {
                    'name': tool.name,
                    'description': tool.description,
                    'parameters': tool.parameters
                }
            })

        return tools_def

    def _process_response(self, response: Dict) -> str:
        """处理AI响应"""
        # 检查是否有工具调用
        if 'choices' in response and len(response['choices']) > 0:
            message = response['choices'][0].get('message', {})

            if 'tool_calls' in message and message['tool_calls']:
                # 执行工具调用
                tool_results = self._execute_tools(message['tool_calls'])
                # 将工具结果反馈给AI（需要再次调用API）
                # 这里简化处理，直接返回工具结果
                return self._format_tool_results(tool_results)

            # 返回AI文本回复
            return message.get('content', '')

        return response.get('content', '')

    def _execute_tools(self, tool_calls: List[Dict]) -> List[Dict]:
        """执行工具调用"""
        results = []

        for tool_call in tool_calls:
            tool_name = tool_call['function']['name']
            tool_args = json.loads(tool_call['function']['arguments'])

            if tool_name in self.tools:
                logger.info(f"执行工具: {tool_name}, 参数: {tool_args}")
                try:
                    result = self.tools[tool_name].function(**tool_args)
                    results.append({
                        'tool': tool_name,
                        'success': True,
                        'result': result
                    })
                except Exception as e:
                    logger.error(f"工具执行失败: {tool_name}, {e}")
                    results.append({
                        'tool': tool_name,
                        'success': False,
                        'error': str(e)
                    })
            else:
                logger.warning(f"未知工具: {tool_name}")

        return results

    def _format_tool_results(self, results: List[Dict]) -> str:
        """格式化工具结果"""
        formatted = []
        for r in results:
            if r['success']:
                formatted.append(f"**{r['tool']}结果：**\n{r['result']}")
            else:
                formatted.append(f"**{r['tool']}执行失败：** {r['error']}")

        return '\n\n'.join(formatted)

    def _get_context_summary(self) -> str:
        """获取当前软件上下文摘要"""
        if not self.context_manager:
            return ""

        try:
            summary_parts = []

            # DWG信息
            dwg_info = self.context_manager.get_dwg_info()
            if dwg_info:
                summary_parts.append(f"📐 图纸: {dwg_info.get('filename', 'Unknown')}")
                summary_parts.append(f"   实体数: {dwg_info.get('entity_count', 0)}")

            # 翻译状态
            trans_info = self.context_manager.get_translation_info()
            if trans_info:
                summary_parts.append(f"🌐 翻译: 已完成 {trans_info.get('translated_count', 0)} 条")
                summary_parts.append(f"   质量分数: {trans_info.get('average_quality_score', 'N/A')}")

            # 算量状态
            calc_info = self.context_manager.get_calculation_info()
            if calc_info:
                summary_parts.append(f"📊 算量: 已识别 {calc_info.get('component_count', 0)} 个构件")
                summary_parts.append(f"   总费用: ¥{calc_info.get('total_cost', 0):,.2f}")

            return '\n'.join(summary_parts)

        except Exception as e:
            logger.error(f"获取上下文摘要失败: {e}")
            return ""

    # ========== 会话管理 ==========

    def new_conversation(self, title: Optional[str] = None) -> str:
        """
        创建新会话

        Args:
            title: 会话标题

        Returns:
            str: 会话ID
        """
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        conv_id = f"conv_{datetime.now().strftime('%Y%m%d%H%M%S')}"

        conversation = Conversation(
            id=conv_id,
            title=title or f"对话 {len(self.conversations) + 1}",
            created_at=timestamp,
            updated_at=timestamp
        )

        self.conversations[conv_id] = conversation
        self.current_conversation_id = conv_id

        logger.info(f"创建新会话: {conv_id}")
        return conv_id

    def switch_conversation(self, conv_id: str) -> bool:
        """
        切换到指定会话

        Args:
            conv_id: 会话ID

        Returns:
            bool: 切换是否成功
        """
        if conv_id in self.conversations:
            self.current_conversation_id = conv_id
            logger.info(f"切换到会话: {conv_id}")
            return True
        else:
            logger.warning(f"会话不存在: {conv_id}")
            return False

    def get_current_conversation(self) -> Optional[Conversation]:
        """获取当前会话"""
        if self.current_conversation_id:
            return self.conversations.get(self.current_conversation_id)
        return None

    def get_all_conversations(self) -> List[Conversation]:
        """获取所有会话列表"""
        return list(self.conversations.values())

    def delete_conversation(self, conv_id: str) -> bool:
        """
        删除指定会话

        Args:
            conv_id: 会话ID

        Returns:
            bool: 删除是否成功
        """
        if conv_id in self.conversations:
            del self.conversations[conv_id]

            # 如果删除的是当前会话，创建新会话
            if conv_id == self.current_conversation_id:
                self.new_conversation()

            logger.info(f"删除会话: {conv_id}")
            return True
        else:
            logger.warning(f"会话不存在: {conv_id}")
            return False

    def clear_current_conversation(self):
        """清空当前会话的消息"""
        current_conv = self.get_current_conversation()
        if current_conv:
            current_conv.messages.clear()
            current_conv.updated_at = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
            logger.info(f"清空会话消息: {current_conv.id}")

    def _add_message_to_current_conversation(self, message: Message):
        """添加消息到当前会话"""
        current_conv = self.get_current_conversation()
        if current_conv:
            current_conv.messages.append(message)
            current_conv.updated_at = datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    # ========== 工具函数实现 ==========

    def _tool_get_dwg_info(self) -> str:
        """工具：获取DWG图纸信息"""
        if not self.context_manager:
            return "未加载图纸"

        try:
            info = self.context_manager.get_dwg_info()
            if not info:
                return "当前没有打开的图纸"

            return f"""**DWG图纸信息：**
- 文件名: {info.get('filename', 'Unknown')}
- 图层数: {info.get('layer_count', 0)}
- 实体总数: {info.get('entity_count', 0)}
- 文本实体: {info.get('text_entity_count', 0)}
- 线条实体: {info.get('line_entity_count', 0)}
- 圆/弧实体: {info.get('circle_entity_count', 0)}
"""
        except Exception as e:
            return f"获取图纸信息失败: {str(e)}"

    def _tool_get_translation_results(self, include_details: bool = False) -> str:
        """工具：获取翻译结果"""
        if not self.context_manager:
            return "未进行翻译"

        try:
            info = self.context_manager.get_translation_info()
            if not info:
                return "当前没有翻译结果"

            result = f"""**翻译结果统计：**
- 总实体数: {info.get('total_entities', 0)}
- 已翻译: {info.get('translated_count', 0)}
- 缓存命中: {info.get('cached_count', 0)}
- 跳过: {info.get('skipped_count', 0)}
- 平均质量分数: {info.get('average_quality_score', 'N/A')}
- 完美翻译: {info.get('quality_perfect', 0)}
- 自动修正: {info.get('quality_corrected', 0)}
- 警告: {info.get('quality_warnings', 0)}
- 错误: {info.get('quality_errors', 0)}
"""

            if include_details and 'issues' in info:
                result += "\n**问题详情：**\n"
                for issue in info['issues'][:5]:  # 最多显示5个
                    result += f"- {issue}\n"

            return result

        except Exception as e:
            return f"获取翻译结果失败: {str(e)}"

    def _tool_get_calculation_results(self, component_type: str = 'ALL') -> str:
        """工具：获取算量结果"""
        if not self.context_manager:
            return "未进行算量"

        try:
            info = self.context_manager.get_calculation_info(component_type)
            if not info:
                return "当前没有算量结果"

            result = f"""**工程量计算结果：**
- 构件数量: {info.get('component_count', 0)}
- 总体积: {info.get('total_volume', 0):.2f} m³
- 总面积: {info.get('total_area', 0):.2f} m²
- 总费用: ¥{info.get('total_cost', 0):,.2f}
"""

            # 按类型统计
            if 'by_type' in info:
                result += "\n**按构件类型：**\n"
                for ctype, data in info['by_type'].items():
                    result += f"- {ctype}: {data.get('count', 0)}个, {data.get('volume', 0):.2f}m³\n"

            return result

        except Exception as e:
            return f"获取算量结果失败: {str(e)}"

    def _tool_get_material_summary(self) -> str:
        """工具：获取材料汇总"""
        if not self.context_manager:
            return "未进行算量"

        try:
            info = self.context_manager.get_material_summary()
            if not info:
                return "当前没有材料统计"

            result = "**材料用量汇总：**\n\n"

            # 混凝土
            if 'concrete' in info:
                result += "**混凝土：**\n"
                for grade, volume in info['concrete'].items():
                    result += f"- {grade}: {volume:.2f} m³\n"

            # 钢筋
            if 'rebar' in info:
                result += "\n**钢筋：**\n"
                for spec, weight in info['rebar'].items():
                    result += f"- {spec}: {weight:.2f} t\n"

            return result

        except Exception as e:
            return f"获取材料汇总失败: {str(e)}"

    def _tool_get_cost_estimate(self, include_breakdown: bool = False) -> str:
        """工具：获取成本估算"""
        if not self.context_manager:
            return "未进行算量"

        try:
            info = self.context_manager.get_cost_estimate()
            if not info:
                return "当前没有成本估算"

            result = f"""**工程成本估算：**
- 总成本: ¥{info.get('total_cost', 0):,.2f}
- 混凝土成本: ¥{info.get('concrete_cost', 0):,.2f}
- 钢筋成本: ¥{info.get('rebar_cost', 0):,.2f}
- 其他成本: ¥{info.get('other_cost', 0):,.2f}
"""

            if include_breakdown and 'breakdown' in info:
                result += "\n**详细分解：**\n"
                for item, cost in info['breakdown'].items():
                    result += f"- {item}: ¥{cost:,.2f}\n"

            return result

        except Exception as e:
            return f"获取成本估算失败: {str(e)}"

    def _tool_generate_report(self, report_type: str, format: str = 'text') -> str:
        """工具：生成报表"""
        if not self.context_manager:
            return "无可用数据"

        try:
            result = self.context_manager.generate_report(report_type, format)
            return f"报表生成成功！\n类型: {report_type}\n格式: {format}\n{result}"

        except Exception as e:
            return f"生成报表失败: {str(e)}"

    def set_context_manager(self, context_manager: Any):
        """设置上下文管理器"""
        self.context_manager = context_manager
        logger.info("上下文管理器已设置")

    def set_model(self, model: str):
        """设置使用的模型"""
        self.model = model
        logger.info(f"切换模型: {model}")

    def set_thinking_mode(self, enable: bool):
        """设置深度思考模式"""
        self.enable_thinking = enable
        logger.info(f"深度思考模式: {'启用' if enable else '禁用'}")

    def set_streaming_mode(self, enable: bool):
        """设置流式输出模式"""
        self.use_streaming = enable
        logger.info(f"流式输出模式: {'启用' if enable else '禁用'}")
