using For_the_Darkest_Dungeon.DefinitionDarkest;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using RegexMatch = System.Text.RegularExpressions.Match;
using Regex = System.Text.RegularExpressions.Regex;

namespace For_the_Darkest_Dungeon.Classification
{
	/// <summary>
	/// Info / Art / Override 三类文件共用的着色器基类。
	///
	/// 这三个文件目前使用完全一致的着色规则：
	/// 1. Header 着色；
	/// 2. InfoContextMap 中存在的关键字着色为 darkest.info.keyword；
	/// 3. 动态 .xxx_effects 关键字特判；
	/// 4. 非法关键字着色为 darkest.error；
	/// 5. 字符串、未引号字符串、布尔值、数字着色；
	/// 6. // 注释拥有最高优先级。
	///
	/// 子类只需要继承此类，并在 Provider 中绑定不同 ContentType。
	/// </summary>
	internal abstract class InfoBaseClassifier : IClassifier
	{
		protected readonly IClassificationTypeRegistryService _registry;

		private static readonly Regex HeaderRegex =
			new Regex(@"^[a-zA-Z0-9_]+:", RegexOptions.Compiled);

		private static readonly Regex KeywordRegex =
			new Regex(@"\.[a-zA-Z0-9_]+", RegexOptions.Compiled);

		private static readonly Regex NumberRegex =
			new Regex(@"-?\d+(\.\d+)?%?", RegexOptions.Compiled);

		private static readonly Regex StringRegex =
			new Regex(@"""[^""]*""", RegexOptions.Compiled);

		private static readonly Regex UnquotedRegex =
			new Regex(@"\b[a-zA-Z_][a-zA-Z0-9_]*\b", RegexOptions.Compiled);

		private static readonly HashSet<string> AllowedEffectsHeaders = new HashSet<string>
		{
			"riposte_skill:",
			"skill:",
			"combat_skill:",
			"combat_move_skill:"
		};

		protected InfoBaseClassifier(IClassificationTypeRegistryService registry)
		{
			_registry = registry;
		}

		public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
		{
			var list = new List<ClassificationSpan>();
			string text = span.GetText();

			// ------------------------------------------------------------
			// 最高优先级：注释
			//
			// 规则：
			// 只要出现 //，无论它是否在双引号内部，
			// 从 // 开始到当前 span 结尾全部按注释色处理。
			//
			// 例如：
			// .id "abc // def"
			//          ^^ 从这里开始全部是注释颜色
			//
			// 因此后续所有匹配只扫描 codeText，也就是 // 之前的内容。
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

			int codeLength = commentIndex >= 0 ? commentIndex : text.Length;
			string codeText = text.Substring(0, codeLength);

			// 整行都是注释，直接返回。
			if (codeLength == 0)
				return list;

			// ------------------------------------------------------------
			// 1. Header
			// ------------------------------------------------------------
			foreach (RegexMatch match in HeaderRegex.Matches(codeText))
			{
				var type = _registry.GetClassificationType("darkest.header");

				list.Add(new ClassificationSpan(
					new SnapshotSpan(
						span.Snapshot,
						span.Start + match.Index,
						match.Length),
					type));
			}

			// ------------------------------------------------------------
			// 2. 关键字
			// ------------------------------------------------------------
			foreach (RegexMatch match in KeywordRegex.Matches(codeText))
			{
				// 排除 1.xxx 这种数字后面的点号结构。
				if (match.Index > 0 && char.IsDigit(codeText[match.Index - 1]))
					continue;

				string keyword = match.Value;
				var type = GetInfoLikeKeywordClassificationType(keyword, span, match.Index);

				list.Add(new ClassificationSpan(
					new SnapshotSpan(
						span.Snapshot,
						span.Start + match.Index,
						match.Length),
					type));
			}

			// ------------------------------------------------------------
			// 3. 字符串
			//
			// 注意：
			// 因为这里只扫描 codeText，所以如果字符串内部出现 //，
			// // 后面的内容不会被字符串规则吃掉，而是已经被注释规则染色。
			// ------------------------------------------------------------
			foreach (RegexMatch match in StringRegex.Matches(codeText))
			{
				var type = _registry.GetClassificationType("darkest.string");

				list.Add(new ClassificationSpan(
					new SnapshotSpan(
						span.Snapshot,
						span.Start + match.Index,
						match.Length),
					type));
			}

			// ------------------------------------------------------------
			// 4. 未引号字符串 / 布尔值
			// ------------------------------------------------------------
			foreach (RegexMatch match in UnquotedRegex.Matches(codeText))
			{
				var currentSpan = new Span(
					(span.Start + match.Index).Position,
					match.Length);

				// 已经被 Header / Keyword / String 占用的区域不重复着色。
				if (list.Any(s => s.Span.IntersectsWith(currentSpan)))
					continue;

				var type = _registry.GetClassificationType("darkest.unquoted");

				if (IsBooleanLiteral(match.Value))
					type = _registry.GetClassificationType("darkest.bool");

				list.Add(new ClassificationSpan(
					new SnapshotSpan(
						span.Snapshot,
						span.Start + match.Index,
						match.Length),
					type));
			}

			// ------------------------------------------------------------
			// 5. 数字
			// ------------------------------------------------------------
			foreach (RegexMatch match in NumberRegex.Matches(codeText))
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

		/// <summary>
		/// Info / Art / Override 共用关键字分类逻辑。
		///
		/// 规则来自你原来的 InfoClassifier / ArtClassifier / OverrideClassifier：
		/// - DarkestInfoData.InfoContextMap 中存在的关键字：darkest.info.keyword；
		/// - 动态 .xxx_effects：通过后续规则判断；
		/// - 其他未知关键字：darkest.error。
		/// </summary>
		private IClassificationType GetInfoLikeKeywordClassificationType(
			string keyword,
			SnapshotSpan span,
			int matchIndex)
		{
			if (DarkestInfoData.InfoContextMap.Values.Any(list => list.Contains(keyword)))
			{
				return _registry.GetClassificationType("darkest.info.keyword");
			}

			if (keyword.EndsWith("_effects", StringComparison.Ordinal))
			{
				return GetDynamicEffectsClassificationType(keyword, span, matchIndex);
			}

			return _registry.GetClassificationType("darkest.error");
		}

		/// <summary>
		/// 动态 .xxx_effects 着色规则。
		///
		/// 保留原有两个错误特判：
		/// 1. body 不能以技能本身已有关键字作为前缀，例如 .critxxx_effects；
		/// 2. 如果前一个合法关键字是 .target，且 body 中包含数字，则标红。
		/// </summary>
		private IClassificationType GetDynamicEffectsClassificationType(
			string keyword,
			SnapshotSpan span,
			int matchIndex)
		{
			RegexMatch matchDynamic = Regex.Match(keyword, @"^\.(?<body>[^\s.]+)_effects$");

			if (!matchDynamic.Success)
				return _registry.GetClassificationType("darkest.error");

			string body = matchDynamic.Groups["body"].Value;

			string matchedPrefix = AllowedEffectsHeaders
				.Where(header => DarkestInfoData.InfoContextMap.TryGetValue(header, out _))
				.SelectMany(header => DarkestInfoData.InfoContextMap[header])
				.OrderByDescending(p => p.Length)
				.FirstOrDefault(p =>
				{
					string prefix = p.StartsWith(".") ? p.Substring(1) : p;
					return body.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
				});

			if (matchedPrefix != null)
				return _registry.GetClassificationType("darkest.error");

			int currentKeywordStart = span.Start.Position + matchIndex;
			string previousKeyword = FindPreviousDotKeyword(span.Snapshot, currentKeywordStart);

			if (body.Any(char.IsDigit) &&
				string.Equals(previousKeyword, ".target", StringComparison.OrdinalIgnoreCase))
			{
				return _registry.GetClassificationType("darkest.error");
			}

			return _registry.GetClassificationType("darkest.info.keyword");
		}

		/// <summary>
		/// 从当前位置向前扫描，找到上一个合法 .keyword。
		///
		/// 合法点号要求：
		/// - 点号前不能是数字；
		/// - 点号后不能是数字；
		/// - 遇到空白或另一个点号则结束。
		/// </summary>
		private string FindPreviousDotKeyword(ITextSnapshot snapshot, int currentKeywordStart)
		{
			int pos = currentKeywordStart - 1;

			while (pos >= 0)
			{
				char ch = snapshot[pos];

				if (ch == '.')
				{
					bool prevIsDigit = pos > 0 && char.IsDigit(snapshot[pos - 1]);
					bool nextIsDigit = pos + 1 < currentKeywordStart && char.IsDigit(snapshot[pos + 1]);

					if (prevIsDigit || nextIsDigit)
					{
						pos--;
						continue;
					}

					int keywordStart = pos;
					int keywordEnd = pos + 1;

					while (keywordEnd < currentKeywordStart)
					{
						char c = snapshot[keywordEnd];

						if (char.IsWhiteSpace(c))
							break;

						if (c == '.' && keywordEnd != keywordStart)
							break;

						keywordEnd++;
					}

					if (keywordEnd > keywordStart + 1)
						return snapshot.GetText(keywordStart, keywordEnd - keywordStart);

					return ".";
				}

				pos--;
			}

			return null;
		}

		private bool IsBooleanLiteral(string value)
		{
			return value == "true" || value == "false" ||
				   value == "True" || value == "False" ||
				   value == "TRUE" || value == "FALSE";
		}

		public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
	}
}