"""
阿里云百炼大模型API客户端
使用OpenAI兼容接口调用通义千问模型
"""
import os
import time
from typing import List, Dict, Optional
from dataclasses import dataclass
import requests

from ..utils.logger import logger
from ..utils.config_manager import ConfigManager


@dataclass
class TranslationResult:
    """翻译结果"""
    original_text: str
    translated_text: str
    tokens_used: int
    model: str
    cost_estimate: float  # 估算成本（元）


class BailianAPIError(Exception):
    """百炼API错误"""
    pass


class BailianClient:
    """阿里云百炼API客户端"""
    
    # 模型定价（元/1000 tokens）
    PRICING = {
        'qwen-plus': {'input': 0.004, 'output': 0.004},
        'qwen-max': {'input': 0.040, 'output': 0.040},
        'qwen-turbo': {'input': 0.002, 'output': 0.002},
    }
    
    def __init__(self, api_key: Optional[str] = None, model: str = 'qwen-plus'):
        """
        初始化百炼客户端
        
        Args:
            api_key: API密钥，如果为None则从环境变量DASHSCOPE_API_KEY读取
            model: 模型名称，默认qwen-plus
        """
        self.api_key = api_key or os.getenv('DASHSCOPE_API_KEY')
        if not self.api_key:
            raise BailianAPIError(
                "未找到API密钥，请设置环境变量DASHSCOPE_API_KEY或传入api_key参数"
            )
        
        config = ConfigManager()
        self.model = model
        self.endpoint = config.get('api.endpoint', 'https://dashscope.aliyuncs.com')
        self.base_url = f"{self.endpoint}/compatible-mode/v1"
        self.timeout = config.get('api.timeout', 60)
        self.max_retries = config.get('api.max_retries', 3)
        
        logger.info(f"百炼客户端初始化: model={self.model}, endpoint={self.endpoint}")
    
    def translate_text(
        self,
        text: str,
        from_lang: str,
        to_lang: str,
        context: Optional[str] = None
    ) -> TranslationResult:
        """
        翻译单个文本
        
        Args:
            text: 要翻译的文本
            from_lang: 源语言（如：中文、英文、日文、韩文）
            to_lang: 目标语言
            context: 上下文提示（可选）
        
        Returns:
            TranslationResult: 翻译结果
        """
        # 构建prompt
        system_prompt = self._build_translation_prompt(from_lang, to_lang, context)
        
        # 调用API
        messages = [
            {'role': 'system', 'content': system_prompt},
            {'role': 'user', 'content': text}
        ]
        
        response = self._call_api(messages)
        
        return TranslationResult(
            original_text=text,
            translated_text=response['translated_text'],
            tokens_used=response['tokens_used'],
            model=self.model,
            cost_estimate=response['cost_estimate']
        )
    
    def translate_batch(
        self,
        texts: List[str],
        from_lang: str,
        to_lang: str,
        context: Optional[str] = None
    ) -> List[TranslationResult]:
        """
        批量翻译文本（一次API调用）
        
        Args:
            texts: 要翻译的文本列表
            from_lang: 源语言
            to_lang: 目标语言
            context: 上下文提示
        
        Returns:
            List[TranslationResult]: 翻译结果列表
        """
        if not texts:
            return []
        
        # 构建批量翻译prompt
        system_prompt = self._build_batch_translation_prompt(from_lang, to_lang, context)
        
        # 构建批量文本（带编号）
        numbered_texts = '\n'.join([f"{i+1}. {text}" for i, text in enumerate(texts)])
        
        messages = [
            {'role': 'system', 'content': system_prompt},
            {'role': 'user', 'content': numbered_texts}
        ]
        
        response = self._call_api(messages)
        
        # 解析批量翻译结果
        translated_texts = self._parse_batch_response(response['translated_text'], len(texts))
        
        # 计算单个文本的token消耗（平均分配）
        tokens_per_text = response['tokens_used'] // len(texts)
        cost_per_text = response['cost_estimate'] / len(texts)
        
        results = []
        for original, translated in zip(texts, translated_texts):
            results.append(TranslationResult(
                original_text=original,
                translated_text=translated,
                tokens_used=tokens_per_text,
                model=self.model,
                cost_estimate=cost_per_text
            ))
        
        return results
    
    def _call_api(self, messages: List[Dict[str, str]]) -> Dict:
        """
        调用API（带重试）
        
        Args:
            messages: 消息列表
        
        Returns:
            Dict: 包含translated_text, tokens_used, cost_estimate的字典
        """
        headers = {
            'Authorization': f'Bearer {self.api_key}',
            'Content-Type': 'application/json'
        }
        
        payload = {
            'model': self.model,
            'messages': messages
        }
        
        last_error = None
        
        for attempt in range(self.max_retries):
            try:
                response = requests.post(
                    f"{self.base_url}/chat/completions",
                    json=payload,
                    headers=headers,
                    timeout=self.timeout
                )
                
                if response.status_code == 200:
                    data = response.json()
                    
                    # 提取结果
                    translated_text = data['choices'][0]['message']['content']
                    tokens_used = data['usage']['total_tokens']
                    
                    # 计算成本
                    input_tokens = data['usage']['prompt_tokens']
                    output_tokens = data['usage']['completion_tokens']
                    pricing = self.PRICING.get(self.model, self.PRICING['qwen-plus'])
                    cost_estimate = (
                        input_tokens * pricing['input'] / 1000 +
                        output_tokens * pricing['output'] / 1000
                    )
                    
                    logger.debug(
                        f"API调用成功: tokens={tokens_used}, cost=¥{cost_estimate:.4f}"
                    )
                    
                    return {
                        'translated_text': translated_text.strip(),
                        'tokens_used': tokens_used,
                        'cost_estimate': cost_estimate
                    }
                
                elif response.status_code == 401:
                    raise BailianAPIError(
                        "API密钥验证失败\n\n"
                        "可能的原因：\n"
                        "1. API密钥未设置或已过期\n"
                        "2. 环境变量DASHSCOPE_API_KEY配置错误\n\n"
                        "解决方法：\n"
                        "请前往阿里云控制台获取有效的API密钥，并正确配置环境变量"
                    )

                elif response.status_code == 429:
                    # 速率限制，等待后重试
                    wait_time = 2 ** attempt
                    logger.warning(f"请求过于频繁，{wait_time}秒后自动重试...")
                    time.sleep(wait_time)
                    continue

                elif response.status_code == 400:
                    raise BailianAPIError(
                        "请求参数错误\n\n"
                        "可能的原因：\n"
                        "1. 文本内容格式不正确\n"
                        "2. 模型参数配置有误\n\n"
                        "建议：请检查输入内容是否符合要求"
                    )

                elif response.status_code == 500:
                    error_msg = response.text
                    logger.error(f"服务器错误: {error_msg}")
                    last_error = BailianAPIError(
                        "阿里云服务暂时不可用\n\n"
                        "这通常是临时性问题，请稍后重试\n"
                        "如果问题持续存在，请联系阿里云技术支持"
                    )

                else:
                    error_msg = response.text
                    logger.error(f"API调用失败 (HTTP {response.status_code}): {error_msg}")
                    last_error = BailianAPIError(
                        f"翻译服务请求失败 (错误码: {response.status_code})\n\n"
                        f"详细信息：{error_msg[:200]}\n\n"
                        "建议：请检查网络连接或稍后重试"
                    )
            
            except requests.Timeout as e:
                logger.error(f"请求超时: {e}")
                last_error = BailianAPIError(
                    "网络请求超时\n\n"
                    "可能的原因：\n"
                    "1. 网络连接不稳定\n"
                    "2. 翻译内容过多，处理时间较长\n\n"
                    "建议：请检查网络连接后重试"
                )

                if attempt < self.max_retries - 1:
                    wait_time = 2 ** attempt
                    logger.info(f"{wait_time}秒后自动重试...")
                    time.sleep(wait_time)

            except requests.ConnectionError as e:
                logger.error(f"连接失败: {e}")
                last_error = BailianAPIError(
                    "无法连接到翻译服务\n\n"
                    "可能的原因：\n"
                    "1. 网络连接断开\n"
                    "2. 防火墙阻止了连接\n"
                    "3. 代理设置不正确\n\n"
                    "建议：请检查网络设置后重试"
                )

                if attempt < self.max_retries - 1:
                    wait_time = 2 ** attempt
                    logger.info(f"{wait_time}秒后自动重试...")
                    time.sleep(wait_time)

            except requests.RequestException as e:
                logger.error(f"网络请求失败: {e}")
                last_error = BailianAPIError(
                    f"网络请求异常\n\n"
                    f"错误信息：{str(e)[:200]}\n\n"
                    "建议：请检查网络连接或稍后重试"
                )

                if attempt < self.max_retries - 1:
                    wait_time = 2 ** attempt
                    logger.info(f"{wait_time}秒后自动重试...")
                    time.sleep(wait_time)
        
        # 所有重试都失败
        raise last_error or BailianAPIError(
            "翻译请求失败\n\n"
            f"已尝试{self.max_retries}次重试，但仍然失败\n"
            "建议：\n"
            "1. 检查网络连接是否正常\n"
            "2. 确认API密钥配置正确\n"
            "3. 稍后再试或联系技术支持"
        )
    
    def _build_translation_prompt(
        self,
        from_lang: str,
        to_lang: str,
        context: Optional[str] = None
    ) -> str:
        """构建翻译prompt - 人工级别翻译质量"""
        base_prompt = f"""你是一位精通建筑工程CAD图纸的资深翻译专家，拥有15年以上的专业经验。

【翻译任务】将以下{from_lang}文本翻译成{to_lang}

【专业要求】
1. **术语准确性**：严格使用建筑、结构、机械、电气等领域的标准术语
   - 建筑构件：梁(beam)、柱(column)、墙(wall)、板(slab)等
   - 材料规格：混凝土标号(C20/C30)、钢筋型号(HRB400)等
   - 尺寸单位：保持mm、m、×、Φ等符号不变

2. **数字和符号**：
   - 绝对保留所有数字、尺寸、编号
   - 保持单位符号、特殊字符不变
   - 维持原文格式（如：300×600不可改为300*600）

3. **专业规范**：
   - 遵循国家建筑制图标准(GB/T 50001)
   - 使用行业通用缩写：KL(框架梁)、KZ(框架柱)等
   - 保持专业命名规范性

4. **翻译风格**：
   - 简洁专业，符合图纸标注习惯
   - 避免口语化表达
   - 不添加解释性文字

5. **输出格式**：只输出翻译结果，不包含任何其他内容"""

        if context:
            base_prompt += f"\n\n【上下文参考】{context}"

        return base_prompt
    
    def _build_batch_translation_prompt(
        self,
        from_lang: str,
        to_lang: str,
        context: Optional[str] = None
    ) -> str:
        """构建批量翻译prompt - 人工级别翻译质量"""
        base_prompt = f"""你是一位精通建筑工程CAD图纸的资深翻译专家，拥有15年以上的专业经验。

【批量翻译任务】我将提供一批{from_lang}文本（带编号），请翻译成{to_lang}

【专业要求】
1. **术语准确性**：严格使用建筑、结构、机械、电气等领域的标准术语
   - 建筑构件：梁(beam)、柱(column)、墙(wall)、板(slab)、门(door)、窗(window)
   - 材料规格：C20/C30混凝土、Q235/Q345钢材、HRB400钢筋
   - 保持专业缩写：KL(框架梁)、KZ(框架柱)、NQ(内墙)、WQ(外墙)

2. **数字和符号**：
   - 绝对保留所有数字、尺寸、编号
   - 保持单位符号不变：mm、m、×、Φ、@等
   - 维持原文格式（如：300×600×200）

3. **输出格式**：
   - 每条翻译独立成行
   - 格式："编号. 翻译结果"
   - 严格按原编号顺序
   - 不添加任何额外说明

4. **专业规范**：
   - 遵循国家建筑制图标准
   - 保持图纸标注的简洁性
   - 使用行业通用表达方式"""

        if context:
            base_prompt += f"\n\n【上下文参考】{context}"

        return base_prompt
    
    def _parse_batch_response(self, response: str, expected_count: int) -> List[str]:
        """
        解析批量翻译响应
        
        Args:
            response: API返回的批量翻译文本
            expected_count: 期望的翻译数量
        
        Returns:
            List[str]: 翻译结果列表
        """
        lines = response.strip().split('\n')
        translations = []
        
        for line in lines:
            line = line.strip()
            if not line:
                continue
            
            # 尝试解析 "编号. 翻译文本" 格式
            if '. ' in line:
                _, text = line.split('. ', 1)
                translations.append(text.strip())
            else:
                # 如果没有编号，直接使用
                translations.append(line)
        
        # 如果解析结果数量不匹配，记录警告
        if len(translations) != expected_count:
            logger.warning(
                f"批量翻译结果数量不匹配: 期望{expected_count}, 实际{len(translations)}"
            )
        
        return translations
    
    def test_connection(self) -> bool:
        """
        测试API连接
        
        Returns:
            bool: 连接是否成功
        """
        try:
            result = self.translate_text("测试", "中文", "英文")
            logger.info(f"API连接测试成功: {result.original_text} -> {result.translated_text}")
            return True
        except Exception as e:
            logger.error(f"API连接测试失败: {e}")
            return False
