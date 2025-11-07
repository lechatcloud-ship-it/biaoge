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
                    raise BailianAPIError("API密钥无效，请检查DASHSCOPE_API_KEY")
                
                elif response.status_code == 429:
                    # 速率限制，等待后重试
                    wait_time = 2 ** attempt
                    logger.warning(f"速率限制，等待{wait_time}秒后重试...")
                    time.sleep(wait_time)
                    continue
                
                else:
                    error_msg = response.text
                    logger.error(f"API调用失败 (HTTP {response.status_code}): {error_msg}")
                    last_error = BailianAPIError(f"HTTP {response.status_code}: {error_msg}")
            
            except requests.RequestException as e:
                logger.error(f"网络请求失败: {e}")
                last_error = BailianAPIError(f"网络错误: {e}")
                
                if attempt < self.max_retries - 1:
                    wait_time = 2 ** attempt
                    logger.info(f"等待{wait_time}秒后重试...")
                    time.sleep(wait_time)
        
        # 所有重试都失败
        raise last_error or BailianAPIError("API调用失败")
    
    def _build_translation_prompt(
        self,
        from_lang: str,
        to_lang: str,
        context: Optional[str] = None
    ) -> str:
        """构建翻译prompt"""
        base_prompt = f"""你是一个专业的CAD图纸翻译专家。
请将以下{from_lang}文本翻译成{to_lang}，要求：
1. 保持专业术语的准确性（建筑、工程、机械领域）
2. 保留数字、单位、符号不变
3. 简洁准确，不添加额外解释
4. 只输出翻译结果，不输出其他内容"""
        
        if context:
            base_prompt += f"\n5. 参考上下文：{context}"
        
        return base_prompt
    
    def _build_batch_translation_prompt(
        self,
        from_lang: str,
        to_lang: str,
        context: Optional[str] = None
    ) -> str:
        """构建批量翻译prompt"""
        base_prompt = f"""你是一个专业的CAD图纸翻译专家。
我将给你一批{from_lang}文本（带编号），请翻译成{to_lang}。要求：
1. 保持专业术语的准确性（建筑、工程、机械领域）
2. 保留数字、单位、符号不变
3. 每条翻译独立成行，格式为："编号. 翻译结果"
4. 严格按照原编号顺序输出
5. 只输出翻译结果，不添加额外内容"""
        
        if context:
            base_prompt += f"\n6. 参考上下文：{context}"
        
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
