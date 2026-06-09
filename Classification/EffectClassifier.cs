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
        private readonly Regex _commentRegex = new Regex(@"//.*", RegexOptions.Compiled);
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

            // 1. 处理注释 (一旦发现 //，整行后面都是注释)
            var commentMatch = _commentRegex.Match(text);
            if (commentMatch.Success)
            {
                var type = _registry.GetClassificationType("darkest.comment");
                list.Add(new ClassificationSpan(new SnapshotSpan(span.Snapshot, span.Start + commentMatch.Index, commentMatch.Length), type));
                // 如果整行都是注释，直接返回
                if (commentMatch.Index == 0) return list;
            }

            // 2. 处理 effect:
            foreach (Match match in _headerRegex.Matches(text))
            {
                var type = _registry.GetClassificationType("darkest.header");
                list.Add(new ClassificationSpan(new SnapshotSpan(span.Snapshot, span.Start + match.Index, match.Length), type));
            }

            // 3. 处理 .关键字
            foreach (Match match in _keywordRegex.Matches(text))
            {
                string keyword = match.Value; // 拿到如 ".name"

                // 使用 DarkestData 进行判断
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
                list.Add(new ClassificationSpan(new SnapshotSpan(span.Snapshot, span.Start + match.Index, match.Length), type));
            }

            // 4. 处理字符串 (引号内容)
            foreach (Match match in _stringRegex.Matches(text))
            {
                var type = _registry.GetClassificationType("darkest.string");
                list.Add(new ClassificationSpan(new SnapshotSpan(span.Snapshot, span.Start + match.Index, match.Length), type));
            }

            // 5. 处理未加引号的普通字符串
            foreach (Match match in _unquotedRegex.Matches(text))
            {
                // 排除掉 effect: 和 true false
                if (match.Value == "effect") continue;

                // 检查这个位置是否已经被前面的正则（如字符串或关键字）占用了
                if (list.Any(s => s.Span.IntersectsWith(new Span(span.Start + match.Index, match.Length))))
                    continue;

                var type = _registry.GetClassificationType("darkest.unquoted");

                // 单独处理布尔类型
				if (match.Value == "true" || match.Value == "false" || match.Value == "True" || match.Value == "False" || match.Value == "TRUE" || match.Value == "FALSE")
				{
					type = _registry.GetClassificationType("darkest.bool");
				}
				
				list.Add(new ClassificationSpan(new SnapshotSpan(span.Snapshot, span.Start + match.Index, match.Length), type));
            }

            // 6. 处理数值 (整数, 浮点, 百分比)
            foreach (Match match in _numberRegex.Matches(text))
            {
                if (list.Any(s => s.Span.IntersectsWith(new Span(span.Start + match.Index, match.Length))))
                    continue;

                var type = _registry.GetClassificationType("darkest.number");
                list.Add(new ClassificationSpan(new SnapshotSpan(span.Snapshot, span.Start + match.Index, match.Length), type));
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