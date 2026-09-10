using For_the_Darkest_Dungeon.DefinitionDarkest;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace For_the_Darkest_Dungeon.Classification
{
    internal class EffectClassifier : IClassifier
    {
        private readonly IClassificationTypeRegistryService _registry;

        // 定义正则表达式
        private readonly Regex _headerRegex = new Regex(@"effect:", RegexOptions.Compiled);
        private readonly Regex _keywordRegex = new Regex(@"\.[a-zA-Z_]+", RegexOptions.Compiled);
        private readonly Regex _numberRegex = new Regex(@"-?\d+(\.\d+)?%?", RegexOptions.Compiled);
        private readonly Regex _stringRegex = new Regex(@"""[^""]*""", RegexOptions.Compiled);
        private readonly Regex _unquotedRegex = new Regex(@"\b[a-zA-Z_][a-zA-Z0-9_]*\b", RegexOptions.Compiled);

        internal EffectClassifier(IClassificationTypeRegistryService registry)
        {
            _registry = registry;
        }

		// 核心方法：当 VS 需要对一段文本进行上色时会调用此方法
		public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
		{
			var list = new List<ClassificationSpan>();
			string text = span.GetText();

			// ------------------------------------------------------------
			// 最高优先级：注释
			//
			// 规则：
			// 只要出现 //，不管它是不是在字符串内部，
			// 从 // 开始到本 span 结尾全部视为注释。
			//
			// 例如：
			// .name "abc // def"
			//          ^^ 从这里开始全部是注释颜色
			// ------------------------------------------------------------
			int commentIndex = text.IndexOf("//", StringComparison.Ordinal);

			if (commentIndex >= 0)
			{
				var commentType = _registry.GetClassificationType("darkest.comment");

				list.Add(new ClassificationSpan(
					new SnapshotSpan(
						span.Snapshot,
						span.Start + commentIndex,
						text.Length - commentIndex),
					commentType));
			}

			// 后续所有着色逻辑只处理 // 前面的部分。
			// 这样可以保证 // 后面不会再被字符串、关键字、数字等规则染色。
			int codeLength = commentIndex >= 0 ? commentIndex : text.Length;
			string codeText = text.Substring(0, codeLength);

			// 如果 // 在最开头，整行都是注释，直接返回。
			if (codeLength == 0)
				return list;

			// 1. 处理 effect:
			foreach (Match match in _headerRegex.Matches(codeText))
			{
				var type = _registry.GetClassificationType("darkest.header");

				list.Add(new ClassificationSpan(
					new SnapshotSpan(
						span.Snapshot,
						span.Start + match.Index,
						match.Length),
					type));
			}

			// 2. 处理 .关键字
			foreach (Match match in _keywordRegex.Matches(codeText))
			{
				string keyword = match.Value;

				string typeName = DarkestEffectsData.CoreKeywords.Contains(keyword)
								  ? "darkest.effects.keyword.core"
								  : "darkest.effects.keyword.prop";

				var type = _registry.GetClassificationType(typeName);

				if (match.Value == ".dotBleed")
					type = _registry.GetClassificationType("darkest.effects.keyword.bleed");
				else if (match.Value == ".dotPoison")
					type = _registry.GetClassificationType("darkest.effects.keyword.poison");
				else if (match.Value == ".dotHpHeal" || match.Value == ".heal" || match.Value == ".heal_percent")
					type = _registry.GetClassificationType("darkest.effects.keyword.heal");
				else if (match.Value == ".dotBurn")
					type = _registry.GetClassificationType("darkest.effects.keyword.burn");
				else if (match.Value == ".stun")
					type = _registry.GetClassificationType("darkest.effects.keyword.stun");
				else if (DarkestEffectsData.RiposteKeywords.Contains(keyword))
					type = _registry.GetClassificationType("darkest.effects.keyword.riposte");
				else if (DarkestEffectsData.BuffKeywords.Contains(keyword))
					type = _registry.GetClassificationType("darkest.effects.keyword.buff");
				else if (match.Value == ".kill" || match.Value == ".kill_enemy_types")
					type = _registry.GetClassificationType("darkest.effects.keyword.kill");
				else if (DarkestEffectsData.SummonKeywords.Contains(keyword))
					type = _registry.GetClassificationType("darkest.effects.keyword.summon");
				else if (!DarkestEffectsData.AllKeywords.Contains(keyword))
					type = _registry.GetClassificationType("darkest.error");

				list.Add(new ClassificationSpan(
					new SnapshotSpan(
						span.Snapshot,
						span.Start + match.Index,
						match.Length),
					type));
			}

			// 3. 处理字符串
			//
			// 注意：
			// 因为这里只扫描 codeText，所以如果字符串里出现 //，
			// 字符串正则不会继续吃掉 // 后面的内容。
			foreach (Match match in _stringRegex.Matches(codeText))
			{
				var type = _registry.GetClassificationType("darkest.string");

				list.Add(new ClassificationSpan(
					new SnapshotSpan(
						span.Snapshot,
						span.Start + match.Index,
						match.Length),
					type));
			}

			// 4. 处理未加引号的普通字符串
			foreach (Match match in _unquotedRegex.Matches(codeText))
			{
				if (match.Value == "effect")
					continue;

				var currentSpan = new Span(
					(span.Start + match.Index).Position,
					match.Length);

				if (list.Any(s => s.Span.IntersectsWith(currentSpan)))
					continue;

				var type = _registry.GetClassificationType("darkest.unquoted");

				if (match.Value == "true" || match.Value == "false" ||
					match.Value == "True" || match.Value == "False" ||
					match.Value == "TRUE" || match.Value == "FALSE")
				{
					type = _registry.GetClassificationType("darkest.bool");
				}

				list.Add(new ClassificationSpan(
					new SnapshotSpan(
						span.Snapshot,
						span.Start + match.Index,
						match.Length),
					type));
			}

			// 5. 处理数值
			foreach (Match match in _numberRegex.Matches(codeText))
			{
				var currentSpan = new Span(
					(span.Start + match.Index).Position,
					match.Length);

				if (list.Any(s => s.Span.IntersectsWith(currentSpan)))
					continue;

				var type = _registry.GetClassificationType("darkest.number");

				list.Add(new ClassificationSpan(
					new SnapshotSpan(
						span.Snapshot,
						span.Start + match.Index,
						match.Length),
					type));
			}

			return list;
		}

		public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
    }

	[System.ComponentModel.Composition.Export(typeof(IClassifierProvider))]
	[Microsoft.VisualStudio.Utilities.ContentType("darkest-effect")]
	internal class EffectClassifierProvider : IClassifierProvider
	{
		[System.ComponentModel.Composition.Import]
		internal IClassificationTypeRegistryService classificationRegistry;

		public IClassifier GetClassifier(ITextBuffer buffer)
		{
			return buffer.Properties.GetOrCreateSingletonProperty(() => new EffectClassifier(classificationRegistry));
		}
	}
}