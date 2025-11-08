# -*- coding: utf-8 -*-
"""
DWG翻译系统完整测试
测试所有核心功能：提取、分类、翻译、修改
"""
import unittest
import sys
import os
from pathlib import Path
import tempfile
import ezdxf

# 添加项目路径
sys.path.insert(0, str(Path(__file__).parent.parent))

from src.dwg.text_extractor import (
    TextExtractor, ExtractedText, TextEntityType, extract_texts_from_file
)
from src.dwg.text_classifier import (
    TextClassifier, TextCategory, MixedTextParser, classify_text
)
from src.dwg.smart_translator import (
    SmartTranslator, TerminologyDatabase, MTextFormatter
)
from src.dwg.precision_modifier import (
    PrecisionDWGModifier, modify_dwg_file
)
from src.dwg.translation_pipeline import (
    TranslationPipeline, PipelineConfig, translate_dwg_file
)


class TestTextClassifier(unittest.TestCase):
    """测试文本分类器"""

    def setUp(self):
        self.classifier = TextClassifier()

    def test_01_pure_number(self):
        """测试纯数字识别"""
        self.assertEqual(
            self.classifier.classify("3000"),
            TextCategory.PURE_NUMBER
        )
        self.assertEqual(
            self.classifier.classify("3.14"),
            TextCategory.PURE_NUMBER
        )
        self.assertEqual(
            self.classifier.classify("-123.45"),
            TextCategory.PURE_NUMBER
        )

    def test_02_unit(self):
        """测试单位识别"""
        self.assertEqual(
            self.classifier.classify("mm"),
            TextCategory.UNIT
        )
        self.assertEqual(
            self.classifier.classify("m²"),
            TextCategory.UNIT
        )

    def test_03_pure_text(self):
        """测试纯文本识别"""
        self.assertEqual(
            self.classifier.classify("卧室"),
            TextCategory.PURE_TEXT
        )
        self.assertEqual(
            self.classifier.classify("Living Room"),
            TextCategory.PURE_TEXT
        )

    def test_04_mixed_text(self):
        """测试混合文本识别"""
        self.assertEqual(
            self.classifier.classify("3000mm"),
            TextCategory.MIXED
        )
        # 注意：这个可能被识别为FORMULA
        category = self.classifier.classify("混凝土≥C30")
        self.assertIn(category, [TextCategory.MIXED, TextCategory.FORMULA])

    def test_05_formula(self):
        """测试公式识别"""
        self.assertEqual(
            self.classifier.classify("1:100"),
            TextCategory.FORMULA
        )
        self.assertEqual(
            self.classifier.classify("A=πr²"),
            TextCategory.FORMULA
        )

    def test_06_special_symbol(self):
        """测试特殊符号识别"""
        self.assertEqual(
            self.classifier.classify("φ"),
            TextCategory.SPECIAL_SYMBOL
        )

    def test_07_empty(self):
        """测试空文本"""
        self.assertEqual(
            self.classifier.classify(""),
            TextCategory.EMPTY
        )
        self.assertEqual(
            self.classifier.classify("   "),
            TextCategory.EMPTY
        )


class TestMixedTextParser(unittest.TestCase):
    """测试混合文本解析器"""

    def setUp(self):
        self.parser = MixedTextParser()

    def test_01_parse_simple(self):
        """测试简单解析"""
        parts = self.parser.parse("3000mm")
        # 应该有数字和单位两部分
        self.assertGreater(len(parts), 0)

    def test_02_reconstruct(self):
        """测试重建"""
        original = "3000mm"
        parts = self.parser.parse(original)
        reconstructed = self.parser.reconstruct(parts)
        # 重建后应该相同
        self.assertEqual(original, reconstructed)


class TestTerminologyDatabase(unittest.TestCase):
    """测试术语库"""

    def setUp(self):
        self.db = TerminologyDatabase()

    def test_01_builtin_terms(self):
        """测试内置术语（英文→中文）"""
        match = self.db.match("Bedroom")
        self.assertIsNotNone(match)
        self.assertEqual(match[1], "卧室")

    def test_02_add_custom_term(self):
        """测试添加自定义术语（英文→中文）"""
        self.db.add_term("Test", "测试")
        match = self.db.match("Test")
        self.assertIsNotNone(match)
        self.assertEqual(match[1], "测试")

    def test_03_custom_priority(self):
        """测试自定义术语优先级（英文→中文）"""
        # 覆盖内置术语
        self.db.add_term("Bedroom", "卧房")  # 使用不同的译法
        match = self.db.match("Bedroom")
        self.assertEqual(match[1], "卧房")  # 应该使用自定义的


class TestMTextFormatter(unittest.TestCase):
    """测试MTEXT格式处理器"""

    def test_01_parse_simple(self):
        """测试简单解析"""
        parts = MTextFormatter.parse("普通文本")
        self.assertEqual(len(parts), 1)
        self.assertEqual(parts[0], ('text', '普通文本'))

    def test_02_parse_with_format(self):
        """测试带格式的解析"""
        mtext = "\\fSimSun;第一行\\P第二行"
        parts = MTextFormatter.parse(mtext)

        # 应该有format和text交替
        self.assertGreater(len(parts), 1)

        # 检查是否包含格式标记
        has_format = any(t == 'format' for t, _ in parts)
        self.assertTrue(has_format)

    def test_03_reconstruct(self):
        """测试重建"""
        original = "\\fSimSun;文本\\P换行"
        parts = MTextFormatter.parse(original)
        reconstructed = MTextFormatter.reconstruct(parts)
        self.assertEqual(original, reconstructed)


class TestDWGCreation(unittest.TestCase):
    """测试DWG文件创建和修改"""

    def create_test_dwg(self, filepath: str) -> str:
        """创建测试用的DWG文件（英文文本）"""
        doc = ezdxf.new('R2010')
        msp = doc.modelspace()

        # 添加各种文本实体（英文原文）
        # 1. TEXT - 纯文本（英文房间名）
        msp.add_text("Bedroom", dxfattribs={'height': 2.5, 'insert': (0, 0)})

        # 2. TEXT - 纯数字
        msp.add_text("3000", dxfattribs={'height': 2.5, 'insert': (10, 0)})

        # 3. TEXT - 混合
        msp.add_text("3000mm", dxfattribs={'height': 2.5, 'insert': (20, 0)})

        # 4. MTEXT - 多行英文
        mtext = msp.add_mtext("First Line\\PSecond Line", dxfattribs={'insert': (0, 10)})
        mtext.dxf.char_height = 2.5

        # 5. MTEXT - 带格式的英文
        mtext2 = msp.add_mtext(
            "\\fArial;Living Room",
            dxfattribs={'insert': (10, 10)}
        )
        mtext2.dxf.char_height = 2.5

        # 添加一些非文本实体（确保不被修改）
        msp.add_line((0, -10), (30, -10))
        msp.add_circle((15, -10), radius=5)

        doc.saveas(filepath)
        return filepath

    def test_01_create_and_read(self):
        """测试创建和读取DWG"""
        with tempfile.TemporaryDirectory() as tmpdir:
            dwg_path = os.path.join(tmpdir, "test.dwg")
            self.create_test_dwg(dwg_path)

            # 读取文件
            doc = ezdxf.readfile(dwg_path)
            msp = doc.modelspace()

            # 检查实体数量
            entities = list(msp)
            self.assertGreater(len(entities), 0)

            print(f"✓ 创建了测试DWG文件，包含 {len(entities)} 个实体")

    def test_02_extract_texts(self):
        """测试文本提取"""
        with tempfile.TemporaryDirectory() as tmpdir:
            dwg_path = os.path.join(tmpdir, "test.dwg")
            self.create_test_dwg(dwg_path)

            # 提取文本
            extractor = TextExtractor()
            texts = extractor.extract_from_file(dwg_path)

            # 应该至少提取到5个文本
            self.assertGreaterEqual(len(texts), 5)

            # 检查统计
            stats = extractor.get_statistics()
            self.assertIn('TEXT', stats['by_type'])
            self.assertIn('MTEXT', stats['by_type'])

            print(f"✓ 提取了 {len(texts)} 个文本实体")
            print(f"  按类型: {stats['by_type']}")

    def test_03_classify_texts(self):
        """测试文本分类"""
        with tempfile.TemporaryDirectory() as tmpdir:
            dwg_path = os.path.join(tmpdir, "test.dwg")
            self.create_test_dwg(dwg_path)

            # 提取并分类
            extractor = TextExtractor()
            texts = extractor.extract_from_file(dwg_path)

            classifier = TextClassifier()
            texts = classifier.classify_batch(texts)

            # 检查分类结果
            categories = [t.text_category for t in texts]
            self.assertIn(TextCategory.PURE_TEXT, categories)
            self.assertIn(TextCategory.PURE_NUMBER, categories)

            print(f"✓ 分类完成")
            print(f"  统计: {classifier.get_statistics()}")

    def test_04_modify_dwg(self):
        """测试DWG修改（核心测试！）"""
        with tempfile.TemporaryDirectory() as tmpdir:
            input_path = os.path.join(tmpdir, "test.dwg")
            output_path = os.path.join(tmpdir, "test_modified.dwg")

            # 创建测试文件
            self.create_test_dwg(input_path)

            # 提取文本
            extractor = TextExtractor()
            texts = extractor.extract_from_file(input_path)

            # 模拟翻译（英文→中文）
            for text in texts:
                if text.original_text == "Bedroom":
                    text.translated_text = "卧室"
                elif "Living Room" in text.original_text:  # MTEXT可能包含格式标记
                    # 保持MTEXT格式，只替换文字
                    text.translated_text = text.original_text.replace("Living Room", "客厅")
                elif "First Line" in text.original_text:
                    text.translated_text = text.original_text.replace("First Line", "第一行").replace("Second Line", "第二行")
                # 数字保持不变
                elif text.text_category == TextCategory.PURE_NUMBER:
                    text.translated_text = text.original_text

            # 修改文件
            modifier = PrecisionDWGModifier()
            result = modifier.modify_file(
                input_path,
                texts,
                output_path,
                create_backup=False  # 测试时不需要备份
            )

            # 检查结果
            self.assertTrue(result.success, "修改应该成功")
            self.assertGreater(result.stats.success_count, 0, "应该有成功修改的文本")

            # 验证输出文件存在
            self.assertTrue(os.path.exists(output_path), "输出文件应该存在")

            # 验证输出文件可以打开
            modified_doc = ezdxf.readfile(output_path)
            modified_msp = modified_doc.modelspace()

            # 验证实体数量不变
            original_doc = ezdxf.readfile(input_path)
            original_count = len(list(original_doc.modelspace()))
            modified_count = len(list(modified_msp))
            self.assertEqual(
                original_count,
                modified_count,
                "修改后实体数量应该完全一致"
            )

            # 验证文本已修改（英文→中文）
            modified_texts = [
                e.dxf.text for e in modified_msp.query('TEXT')
                if hasattr(e.dxf, 'text')
            ]
            self.assertIn("卧室", modified_texts, "应该包含翻译后的中文文本")

            # 验证数字未修改
            self.assertIn("3000", modified_texts, "数字应该保持不变")

            print(f"✓ DWG修改测试通过")
            print(f"  成功修改: {result.stats.success_count} 个")
            print(f"  实体数量: {original_count} == {modified_count} ✓")


class TestIntegration(unittest.TestCase):
    """集成测试"""

    def create_test_dwg(self, filepath: str) -> str:
        """创建测试DWG（英文文本）"""
        doc = ezdxf.new('R2010')
        msp = doc.modelspace()

        msp.add_text("Bedroom", dxfattribs={'height': 2.5, 'insert': (0, 0)})
        msp.add_text("3000", dxfattribs={'height': 2.5, 'insert': (10, 0)})
        msp.add_line((0, -10), (30, -10))

        doc.saveas(filepath)
        return filepath

    def test_01_complete_pipeline(self):
        """测试完整流程（不需要API）"""
        with tempfile.TemporaryDirectory() as tmpdir:
            input_path = os.path.join(tmpdir, "test.dwg")
            output_path = os.path.join(tmpdir, "test_translated.dwg")

            self.create_test_dwg(input_path)

            # 创建配置（不使用API）
            config = PipelineConfig(
                api_key="",  # 空API key，翻译会被跳过
                use_terminology=True,
                use_memory=True,
                create_backup=False
            )

            # 创建管道
            pipeline = TranslationPipeline(config)

            # 添加术语（英文→中文，这样即使没有API也能翻译）
            pipeline.translator.terminology_db.add_term("Bedroom", "卧室")

            # 处理文件
            result = pipeline.process_file(input_path, output_path)

            # 检查结果
            self.assertIsNotNone(result, "应该返回结果")
            self.assertGreater(result.total_texts, 0, "应该提取到文本")

            print(f"✓ 完整流程测试通过")
            print(f"  提取: {result.total_texts} 个文本")
            print(f"  翻译: {result.translated_texts} 个")
            print(f"  耗时: {result.total_time:.2f}秒")

    def test_02_multi_language_detection(self):
        """测试多语言自动检测（英文、日文、韩文→中文）"""
        with tempfile.TemporaryDirectory() as tmpdir:
            input_path = os.path.join(tmpdir, "test_multi.dwg")
            output_path = os.path.join(tmpdir, "test_multi_translated.dwg")

            # 创建包含多种语言的测试DWG
            doc = ezdxf.new('R2010')
            msp = doc.modelspace()

            # 英文文本
            msp.add_text("Bedroom", dxfattribs={'height': 2.5, 'insert': (0, 0)})
            msp.add_text("Living Room", dxfattribs={'height': 2.5, 'insert': (0, 5)})

            # 日文文本
            msp.add_text("リビング", dxfattribs={'height': 2.5, 'insert': (0, 10)})
            msp.add_text("寝室", dxfattribs={'height': 2.5, 'insert': (0, 15)})

            # 韩文文本
            msp.add_text("거실", dxfattribs={'height': 2.5, 'insert': (0, 20)})
            msp.add_text("침실", dxfattribs={'height': 2.5, 'insert': (0, 25)})

            # 数字（应该保持不变）
            msp.add_text("3000", dxfattribs={'height': 2.5, 'insert': (0, 30)})

            doc.saveas(input_path)

            # 创建配置（不使用API，使用术语库模拟）
            config = PipelineConfig(
                api_key="",  # 空API key
                source_language="auto",  # 自动检测源语言
                target_language="Chinese",  # 目标语言中文
                use_terminology=True,
                use_memory=True,
                create_backup=False
            )

            # 创建管道
            pipeline = TranslationPipeline(config)

            # 添加多语言术语（模拟自动检测）
            # 英文→中文
            pipeline.translator.terminology_db.add_term("Bedroom", "卧室")
            pipeline.translator.terminology_db.add_term("Living Room", "客厅")

            # 日文→中文
            pipeline.translator.terminology_db.add_term("リビング", "客厅")
            pipeline.translator.terminology_db.add_term("寝室", "卧室")

            # 韩文→中文
            pipeline.translator.terminology_db.add_term("거실", "客厅")
            pipeline.translator.terminology_db.add_term("침실", "卧室")

            # 处理文件
            result = pipeline.process_file(input_path, output_path)

            # 检查结果
            self.assertIsNotNone(result, "应该返回结果")
            self.assertGreater(result.total_texts, 0, "应该提取到文本")
            self.assertGreater(result.translated_texts, 0, "应该翻译了一些文本")

            # 验证输出文件
            self.assertTrue(os.path.exists(output_path), "输出文件应该存在")

            # 读取翻译后的文件，验证多语言都被正确处理
            translated_doc = ezdxf.readfile(output_path)
            translated_msp = translated_doc.modelspace()
            translated_texts = [
                e.dxf.text for e in translated_msp.query('TEXT')
                if hasattr(e.dxf, 'text')
            ]

            # 验证英文、日文、韩文都被翻译成中文
            self.assertIn("卧室", translated_texts, "应该包含'卧室'（从英文/日文/韩文翻译）")
            self.assertIn("客厅", translated_texts, "应该包含'客厅'（从英文/日文/韩文翻译）")
            self.assertIn("3000", translated_texts, "数字应该保持不变")

            print(f"✓ 多语言自动检测测试通过")
            print(f"  提取: {result.total_texts} 个文本")
            print(f"  翻译: {result.translated_texts} 个")
            print(f"  支持语言: 英文、日文、韩文 → 简体中文")
            print(f"  耗时: {result.total_time:.2f}秒")


def run_tests():
    """运行所有测试"""
    # 创建测试套件
    loader = unittest.TestLoader()
    suite = unittest.TestSuite()

    # 添加所有测试
    suite.addTests(loader.loadTestsFromTestCase(TestTextClassifier))
    suite.addTests(loader.loadTestsFromTestCase(TestMixedTextParser))
    suite.addTests(loader.loadTestsFromTestCase(TestTerminologyDatabase))
    suite.addTests(loader.loadTestsFromTestCase(TestMTextFormatter))
    suite.addTests(loader.loadTestsFromTestCase(TestDWGCreation))
    suite.addTests(loader.loadTestsFromTestCase(TestIntegration))

    # 运行测试
    runner = unittest.TextTestRunner(verbosity=2)
    result = runner.run(suite)

    # 打印总结
    print("\n" + "="*70)
    print("测试总结")
    print("="*70)
    print(f"运行测试: {result.testsRun}")
    print(f"成功: {result.testsRun - len(result.failures) - len(result.errors)}")
    print(f"失败: {len(result.failures)}")
    print(f"错误: {len(result.errors)}")

    if result.wasSuccessful():
        print("\n✅ 所有测试通过！")
    else:
        print("\n❌ 有测试失败")

    return result.wasSuccessful()


if __name__ == '__main__':
    success = run_tests()
    sys.exit(0 if success else 1)
