using For_the_Darkest_Dungeon.DefinitionDarkest;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace For_the_Darkest_Dungeon.Classification
{
	// Art 文件的专属着色器
	internal class ArtClassifier : IClassifier
	{
		private readonly IClassificationTypeRegistryService _registry;

		// 定义正则表达式
		private readonly Regex _headerRegex = new Regex(@"^[a-zA-Z0-9_]+:", RegexOptions.Compiled);
		private readonly Regex _keywordRegex = new Regex(@"\.[a-zA-Z0-9_]+", RegexOptions.Compiled);
		private readonly Regex _numberRegex = new Regex(@"-?\d+(\.\d+)?%?", RegexOptions.Compiled);
		private readonly Regex _stringRegex = new Regex(@"""[^""]*""", RegexOptions.Compiled);
		private readonly Regex _commentRegex = new Regex(@"//.*", RegexOptions.Compiled);
		private readonly Regex _unquotedRegex = new Regex(@"\b[a-zA-Z_][a-zA-Z0-9_]*\b", RegexOptions.Compiled);

		private readonly HashSet<string> _allowedEffectsHeaders = new HashSet<string>
		{
			"riposte_skill:",
			"skill:",
			"combat_skill:",
			"combat_move_skill:"
		};

		internal ArtClassifier(IClassificationTypeRegistryService registry)
		{
			_registry = registry;
		}

		/// <summary>
		/// 从当前关键字位置向前扫描，找到上一个合法的 .关键字。
		/// 合法点号要求：点号前不是数字。
		/// 返回上一个关键字文本，例如 ".target"。
		/// </summary>
		private string FindPreviousDotKeyword(ITextSnapshot snapshot, int currentKeywordStart)
		{
			int pos = currentKeywordStart - 1;

			while (pos >= 0)
			{
				char ch = snapshot[pos];

				if (ch == '.')
				{
					// 点号前后都不能是数字
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

						// .关键字结束条件：空白、制表符、换行等
						if (char.IsWhiteSpace(c))
							break;

						// 如果中途又遇到点，说明格式不正常，停在这里
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

			// 2. 匹配 Art Header
			foreach (Match match in _headerRegex.Matches(text))
			{
				var type = _registry.GetClassificationType("darkest.header");
				list.Add(new ClassificationSpan(new SnapshotSpan(span.Snapshot, span.Start + match.Index, match.Length), type));
			}

			// 3. 匹配 Art 关键字
			foreach (Match match in _keywordRegex.Matches(text))
			{
				if (match.Index > 0 && char.IsDigit(text[match.Index - 1]))
				{
					continue;
				}

				string keyword = match.Value; // 拿到如 ".name"
				if (DarkestInfoData.InfoContextMap.Values.Any(l => l.Contains(keyword)))
				{
					var type = _registry.GetClassificationType("darkest.info.keyword");
					list.Add(new ClassificationSpan(new SnapshotSpan(span.Snapshot, span.Start + match.Index, match.Length), type));
				}
				else if (keyword.EndsWith("_effects"))
				{
					var matchDynamic = Regex.Match(keyword, @"^\.(?<body>[^\s.]+)_effects$");
					if (matchDynamic.Success)
					{
						var type = _registry.GetClassificationType("darkest.info.keyword");
						string body = matchDynamic.Groups["body"].Value;
						string matchedPrefix = _allowedEffectsHeaders
							.Where(key => DarkestInfoData.InfoContextMap.TryGetValue(key, out _))
							.SelectMany(key => DarkestInfoData.InfoContextMap[key])
							.OrderByDescending(p => p.Length)
							.FirstOrDefault(p =>
							{
								string prefix = p.StartsWith(".") ? p.Substring(1) : p; // 去除开头的.
								return body.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
							});
						if (matchedPrefix != null)
							type = _registry.GetClassificationType("darkest.error");
						else
						{
							// 特判：
							// 如果当前 .mmm_effects 前一个合法 .关键字是 .target，
							// 且 mmm/body 中包含至少一个数字，则报错。
							int currentKeywordStart = span.Start.Position + match.Index;
							string previousKeyword = FindPreviousDotKeyword(span.Snapshot, currentKeywordStart);

							if (body.Any(char.IsDigit) &&
								string.Equals(previousKeyword, ".target", StringComparison.OrdinalIgnoreCase))
							{
								type = _registry.GetClassificationType("darkest.error");
							}
						}
						list.Add(new ClassificationSpan(new SnapshotSpan(span.Snapshot, span.Start + match.Index, match.Length), type));
					}
				}
				else
				{
					var type = _registry.GetClassificationType("darkest.error");
					list.Add(new ClassificationSpan(new SnapshotSpan(span.Snapshot, span.Start + match.Index, match.Length), type));
				}
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

	// Art 文件的 Provider
	[System.ComponentModel.Composition.Export(typeof(IClassifierProvider))]
	[Microsoft.VisualStudio.Utilities.ContentType("darkest-art")] // 只给 art 文件用
	internal class ArtClassifierProvider : IClassifierProvider
	{
		[System.ComponentModel.Composition.Import]
		internal IClassificationTypeRegistryService classificationRegistry;

		public IClassifier GetClassifier(ITextBuffer buffer)
		{
			return buffer.Properties.GetOrCreateSingletonProperty(() => new ArtClassifier(classificationRegistry));
		}
	}
}