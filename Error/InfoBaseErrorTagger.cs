using For_the_Darkest_Dungeon.DefinitionDarkest;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using RegexMatch = System.Text.RegularExpressions.Match;
using Regex = System.Text.RegularExpressions.Regex;

namespace For_the_Darkest_Dungeon.Error
{
	/// <summary>
	/// Info / Art / Override 三类文件共用的错误检查基类。
	///
	/// 这三个文件类型目前规则完全一致，只是 ContentType / 类名不同，
	/// 因此把所有实际检查逻辑都放在这里，子类只负责：
	/// 1. 传入 ITextBuffer；
	/// 2. 提供对应的 ITaggerProvider 和 ContentType。
	///
	/// 当前包含：
	/// - Header 合法性检查；
	/// - 关键字是否属于当前 Header 检查；
	/// - 动态 _effects 关键字检查；
	/// - Info 参数静态值检查；
	/// - 布尔参数大小写规则检查；
	/// - .disabled_popup_text_types 多参数、重复、非法参数检查；
	/// - 跨行参数解析。
	/// </summary>
	internal abstract class InfoBaseErrorTagger : ITagger<IErrorTag>
	{
		protected readonly ITextBuffer _buffer;

		// 关键字：.xxx / .xxx_yyy / .xxx1
		private static readonly Regex KeywordRegex =
			new Regex(@"\.[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.Compiled);

		// Header：skill: / combat_skill: / display_modifier:
		private static readonly Regex HeaderRegex =
			new Regex(@"^[a-zA-Z0-9_]+:", RegexOptions.Compiled);

		// 字符串："..."
		private static readonly Regex StringRegex =
			new Regex(@"""[^""]*""", RegexOptions.Compiled);

		// 只把行首 // 视为整行注释。
		// 注意：内联注释使用 FindLineCommentStartOutsideString 处理。
		private static readonly Regex CommentLineRegex =
			new Regex(@"^\s*//", RegexOptions.Compiled);

		private static readonly HashSet<string> AllowedEffectsHeaders = new HashSet<string>
		{
			"riposte_skill:",
			"skill:",
			"combat_skill:",
			"combat_move_skill:"
		};

		protected InfoBaseErrorTagger(ITextBuffer buffer)
		{
			_buffer = buffer;
			_buffer.Changed += OnBufferChanged;
		}

		/// <summary>
		/// 文本变化时通知 VS 重新获取错误标签。
		///
		/// 这里先保持整文件刷新，逻辑最稳。
		/// 后续如果性能压力明显，可以再改成局部刷新。
		/// </summary>
		private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
		{
			TagsChanged?.Invoke(
				this,
				new SnapshotSpanEventArgs(new SnapshotSpan(e.After, 0, e.After.Length)));
		}

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		/// <summary>
		/// VS 调用此方法获取当前可见区域 / 指定范围内的错误标签。
		/// </summary>
		public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
		{
			if (spans.Count == 0)
				yield break;

			foreach (var span in spans)
			{
				ITextSnapshot snapshot = span.Snapshot;
				int startLine = span.Start.GetContainingLine().LineNumber;
				int endLine = span.End.GetContainingLine().LineNumber;

				for (int i = startLine; i <= endLine; i++)
				{
					ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
					string lineText = line.GetText();

					// 空行、整行注释直接跳过。
					if (string.IsNullOrWhiteSpace(lineText) || IsCommentLine(lineText))
						continue;

					List<Span> stringSpans = GetStringSpans(lineText);

					// 1. 判断当前行是否是 Header 行。
					string currentHeader = null;
					bool currentLineIsHeader = false;

					RegexMatch headerMatch = HeaderRegex.Match(lineText);
					if (headerMatch.Success)
					{
						currentHeader = headerMatch.Value;
						currentLineIsHeader = true;
					}

					// 2. Header 行：先检查 Header 是否存在。
					if (currentLineIsHeader)
					{
						if (!DarkestInfoData.AllHeaders.Contains(currentHeader))
						{
							yield return new TagSpan<IErrorTag>(
								new SnapshotSpan(line.Start, line.Length),
								new ErrorTag(
									PredefinedErrorTypeNames.SyntaxError,
									$"未知的 Header: {currentHeader}"));

							// Header 本身非法时，本行关键字上下文无法确定，直接跳过。
							continue;
						}
					}
					else
					{
						// 3. 非 Header 行：向上寻找最近的合法 Header。
						currentHeader = FindHeaderAbove(snapshot, i - 1);

						if (currentHeader == null)
						{
							// 没有 Header 时，只对有关键字的行报错。
							// 纯参数续行不报错，避免误伤跨行参数。
							bool hasKeyword = KeywordRegex.Matches(lineText)
								.Cast<RegexMatch>()
								.Any(m => !stringSpans.Any(s => s.Contains(m.Index))
										  && !(m.Index > 0 && char.IsDigit(lineText[m.Index - 1])));

							if (hasKeyword)
							{
								yield return new TagSpan<IErrorTag>(
									new SnapshotSpan(line.Start, line.Length),
									new ErrorTag(
										PredefinedErrorTypeNames.SyntaxError,
										"缺少 Header：该关键字前没有任何合法的 Header 定义"));
							}

							continue;
						}

						// 注意：
						// Info / Art / Override 允许关键字参数跨行。
						// 因此“没有关键字的行”可能只是上一行关键字的参数续行，
						// 这里不能再像旧版一样直接报“错误内容”。
					}

					// 4. 检查当前行所有关键字。
					foreach (RegexMatch match in KeywordRegex.Matches(lineText))
					{
						// 忽略字符串内部的 .xxx。
						if (stringSpans.Any(s => s.Contains(match.Index)))
							continue;

						// 忽略数字小数等情况，例如 1.xxx。
						if (match.Index > 0 && char.IsDigit(lineText[match.Index - 1]))
							continue;

						string keyword = match.Value;
						bool isValid = false;
						string errorMsg = $"无效的关键字: {keyword}";

						bool isDefinedInCurrentHeader =
							currentHeader != null &&
							DarkestInfoData.InfoContextMap.TryGetValue(currentHeader, out var allowedList) &&
							allowedList.Contains(keyword);

						bool isKnownStaticKeyword =
							DarkestInfoData.InfoContextMap.Values.Any(list => list.Contains(keyword));

						if (isDefinedInCurrentHeader)
						{
							isValid = true;
						}
						else if (isKnownStaticKeyword)
						{
							errorMsg = $"关键字 '{keyword}' 不属于 Header '{currentHeader}'。";
							isValid = false;
						}
						else if (keyword.EndsWith("_effects", StringComparison.Ordinal))
						{
							// 动态 _effects 关键字，例如 .xxx_effects。
							if (currentHeader != null && AllowedEffectsHeaders.Contains(currentHeader))
							{
								isValid = ValidateDynamicEffectsKeyword(
									snapshot,
									line,
									match,
									keyword,
									out errorMsg);
							}
							else
							{
								errorMsg = $"动态效果关键字 '{keyword}' 只能用于技能类 Header (如 skill:)。";
								isValid = false;
							}
						}
						else
						{
							isValid = false;
						}

						// 5. 关键字本身合法时，再检查其参数。
						if (isValid && isDefinedInCurrentHeader)
						{
							foreach (var argError in ValidateKeywordArguments(
								snapshot,
								line,
								currentHeader,
								keyword,
								match.Index + match.Length))
							{
								yield return argError;
							}
						}

						// 6. 关键字本身非法时报错。
						if (!isValid)
						{
							yield return new TagSpan<IErrorTag>(
								new SnapshotSpan(snapshot, line.Start + match.Index, match.Length),
								new ErrorTag(PredefinedErrorTypeNames.SyntaxError, errorMsg));
						}
					}
				}
			}
		}

		#region Header / keyword 检查

		/// <summary>
		/// 从指定行向上查找最近的 Header。
		///
		/// 返回值保留冒号，例如 "skill:"。
		/// 不能 TrimEnd(':')，因为 DarkestInfoData.InfoContextMap 的 key 本身带冒号。
		/// </summary>
		private string FindHeaderAbove(ITextSnapshot snapshot, int fromLineNumber)
		{
			for (int i = fromLineNumber; i >= 0; i--)
			{
				ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
				string lineText = line.GetText();

				if (string.IsNullOrWhiteSpace(lineText) || IsCommentLine(lineText))
					continue;

				RegexMatch headerMatch = HeaderRegex.Match(lineText);
				if (headerMatch.Success)
					return headerMatch.Value;
			}

			return null;
		}

		/// <summary>
		/// 动态 _effects 关键字检查。
		///
		/// 规则：
		/// 1. .xxx_effects 允许出现在技能类 Header 下；
		/// 2. 不能以技能本身已有关键字作为前缀，例如 .critxxx_effects；
		/// 3. 如果前一个合法关键字是 .target，且 xxx 中包含数字，则报错。
		/// </summary>
		private bool ValidateDynamicEffectsKeyword(
			ITextSnapshot snapshot,
			ITextSnapshotLine line,
			RegexMatch keywordMatch,
			string keyword,
			out string errorMsg)
		{
			errorMsg = null;

			RegexMatch matchDynamic = Regex.Match(keyword, @"^\.(?<body>[^\s.]+)_effects$");
			if (!matchDynamic.Success)
			{
				errorMsg = $"动态效果关键字 '{keyword}' 格式错误。";
				return false;
			}

			string body = matchDynamic.Groups["body"].Value;

			string matchedPrefix = AllowedEffectsHeaders
				.Where(key => DarkestInfoData.InfoContextMap.TryGetValue(key, out _))
				.SelectMany(key => DarkestInfoData.InfoContextMap[key])
				.OrderByDescending(p => p.Length)
				.FirstOrDefault(p =>
				{
					string prefix = p.StartsWith(".") ? p.Substring(1) : p;
					return body.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
				});

			if (matchedPrefix != null)
			{
				errorMsg = "模式差分effect不能以技能本身带有的相关关键字（如.crit）作为开头，否则将导致红钩识别错误，请更改模式名";
				return false;
			}

			int currentKeywordStart = line.Start.Position + keywordMatch.Index;
			string previousKeyword = FindPreviousDotKeyword(snapshot, currentKeywordStart);

			if (body.Any(char.IsDigit) &&
				string.Equals(previousKeyword, ".target", StringComparison.OrdinalIgnoreCase))
			{
				errorMsg = $"模式差分effect '{keyword}' 紧跟 .target 时，模式名中不能包含数字，否则可能导致识别错误。建议在这两者之间插入.valid_modes或其他内容";
				return false;
			}

			return true;
		}

		/// <summary>
		/// 从当前位置向前找上一个合法的 .keyword。
		///
		/// 合法点号要求：
		/// - 不能是 1.x；
		/// - 不能是 .1。
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

		#endregion

		#region 参数检查

		/// <summary>
		/// 检查当前关键字的参数。
		///
		/// 数据来源：
		/// - DarkestInfoData.GetValuesForKeyword(currentHeader, keyword)
		///
		/// 规则：
		/// - 没有预设参数列表的关键字不检查；
		/// - 布尔参数允许 true/false、True/False、TRUE/FALSE；
		/// - 其他普通参数必须和数据库中的标准参数完全匹配；
		/// - .disabled_popup_text_types 支持多个参数，且检查非法、重复、超量。
		/// </summary>
		private IEnumerable<ITagSpan<IErrorTag>> ValidateKeywordArguments(
			ITextSnapshot snapshot,
			ITextSnapshotLine line,
			string currentHeader,
			string keyword,
			int keywordEndIndex)
		{
			List<string> validValues = DarkestInfoData.GetValuesForKeyword(currentHeader, keyword);
			if (validValues == null)
				yield break;

			List<ParsedArgument> args = ParseArgumentsUntilNextKeywordAcrossLines(
				snapshot,
				line,
				keywordEndIndex);

			if (args.Count == 0)
				yield break;

			if (keyword == ".disabled_popup_text_types")
			{
				foreach (var tag in ValidateDisabledPopupTextTypes(snapshot, keyword, validValues, args))
					yield return tag;

				yield break;
			}

			bool isBooleanKeyword = IsBooleanValueList(validValues);

			foreach (ParsedArgument arg in args)
			{
				string value = arg.Value;

				if (value.Any(char.IsWhiteSpace))
				{
					yield return CreateError(
						snapshot,
						arg.StartPosition,
						arg.Length,
						$"参数 '{value}' 不能包含空格或制表符");
					continue;
				}

				if (isBooleanKeyword)
				{
					if (!IsAllowedBooleanLiteral(value))
					{
						yield return CreateError(
							snapshot,
							arg.StartPosition,
							arg.Length,
							$"布尔参数 '{value}' 无效，允许 true/false、True/False、TRUE/FALSE");
					}
				}
				else
				{
					if (!validValues.Contains(value))
					{
						yield return CreateError(
							snapshot,
							arg.StartPosition,
							arg.Length,
							$"参数 '{value}' 对关键字 '{keyword}' 无效");
					}
				}
			}
		}

		/// <summary>
		/// .disabled_popup_text_types 特判。
		///
		/// 规则：
		/// - 参数数量最多等于标准列表数量，目前是 54；
		/// - 参数必须存在于标准列表；
		/// - 参数不能重复；
		/// - 因为不存在任何合法的带空格参数，所以引号内部出现空格 / 制表符也报错。
		/// </summary>
		private IEnumerable<ITagSpan<IErrorTag>> ValidateDisabledPopupTextTypes(
			ITextSnapshot snapshot,
			string keyword,
			List<string> validValues,
			List<ParsedArgument> args)
		{
			var validSet = new HashSet<string>(validValues, StringComparer.Ordinal);
			var usedSet = new HashSet<string>(StringComparer.Ordinal);

			if (args.Count > validValues.Count)
			{
				ParsedArgument firstExtraArg = args[validValues.Count];

				yield return CreateError(
					snapshot,
					firstExtraArg.StartPosition,
					firstExtraArg.Length,
					$"{keyword} 参数数量不能超过 {validValues.Count} 个，当前数量为 {args.Count}");
			}

			foreach (ParsedArgument arg in args)
			{
				string value = arg.Value;

				if (value.Any(char.IsWhiteSpace))
				{
					yield return CreateError(
						snapshot,
						arg.StartPosition,
						arg.Length,
						$"{keyword} 参数 '{value}' 不能包含空格或制表符");
					continue;
				}

				if (!validSet.Contains(value))
				{
					yield return CreateError(
						snapshot,
						arg.StartPosition,
						arg.Length,
						$"{keyword} 不存在参数 '{value}'");
					continue;
				}

				if (!usedSet.Add(value))
				{
					yield return CreateError(
						snapshot,
						arg.StartPosition,
						arg.Length,
						$"{keyword} 出现重复参数 '{value}'");
				}
			}
		}

		/// <summary>
		/// 跨行解析参数，直到遇到：
		/// - 下一个 .keyword；
		/// - 下一个 header；
		/// - 文件结尾。
		///
		/// 只支持一种注释：//。
		/// 行首 // 视为整行注释，直接跳过。
		/// 行内 // 会截断当前行后续内容。
		/// 但字符串内部的 // 不视为注释。
		/// </summary>
		private List<ParsedArgument> ParseArgumentsUntilNextKeywordAcrossLines(
			ITextSnapshot snapshot,
			ITextSnapshotLine startLine,
			int startIndexInLine)
		{
			var result = new List<ParsedArgument>();

			int lineNumber = startLine.LineNumber;
			int posInLine = startIndexInLine;

			while (lineNumber < snapshot.LineCount)
			{
				ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineNumber);
				string lineText = line.GetText();

				if (IsCommentLine(lineText))
				{
					lineNumber++;
					posInLine = 0;
					continue;
				}

				if (lineNumber != startLine.LineNumber)
				{
					RegexMatch headerMatch = HeaderRegex.Match(lineText);
					if (headerMatch.Success)
						break;
				}

				List<Span> stringSpans = GetStringSpans(lineText);

				int end = lineText.Length;
				int commentIndex = FindLineCommentStartOutsideString(lineText, Math.Min(posInLine, lineText.Length), stringSpans);

				if (commentIndex >= 0)
					end = commentIndex;

				int pos = Math.Min(posInLine, end);

				while (pos < end)
				{
					while (pos < end && char.IsWhiteSpace(lineText[pos]))
						pos++;

					if (pos >= end)
						break;

					if (IsKeywordStartAt(lineText, pos, stringSpans))
						return result;

					if (lineText[pos] == '"')
					{
						int quoteStart = pos;
						int quoteEnd = lineText.IndexOf('"', quoteStart + 1);

						if (quoteEnd < 0 || quoteEnd > end)
							quoteEnd = end;

						int valueStart = quoteStart + 1;
						int valueLength = Math.Max(0, quoteEnd - valueStart);

						result.Add(new ParsedArgument
						{
							StartPosition = line.Start.Position + valueStart,
							Length = valueLength,
							Value = lineText.Substring(valueStart, valueLength)
						});

						pos = quoteEnd < end ? quoteEnd + 1 : end;
					}
					else
					{
						int argStart = pos;

						while (pos < end && !char.IsWhiteSpace(lineText[pos]))
							pos++;

						int argLength = pos - argStart;

						result.Add(new ParsedArgument
						{
							StartPosition = line.Start.Position + argStart,
							Length = argLength,
							Value = lineText.Substring(argStart, argLength)
						});
					}
				}

				lineNumber++;
				posInLine = 0;
			}

			return result;
		}

		private bool IsBooleanValueList(List<string> validValues)
		{
			return ReferenceEquals(validValues, DarkestInfoData.KeywordValueMap["BOOL"]);
		}

		private bool IsAllowedBooleanLiteral(string value)
		{
			return value == "true" || value == "false" ||
				   value == "True" || value == "False" ||
				   value == "TRUE" || value == "FALSE";
		}

		#endregion

		#region 通用辅助

		private bool IsCommentLine(string lineText)
		{
			return CommentLineRegex.IsMatch(lineText);
		}

		private List<Span> GetStringSpans(string lineText)
		{
			return StringRegex.Matches(lineText)
				.Cast<RegexMatch>()
				.Select(m => new Span(m.Index, m.Length))
				.ToList();
		}

		/// <summary>
		/// 判断当前位置是否是合法 .keyword 的起点。
		/// 排除：
		/// - 字符串内部；
		/// - 1.x；
		/// - .1。
		/// </summary>
		private bool IsKeywordStartAt(string lineText, int pos, List<Span> stringSpans)
		{
			if (pos < 0 || pos >= lineText.Length)
				return false;

			if (lineText[pos] != '.')
				return false;

			if (stringSpans.Any(s => s.Contains(pos)))
				return false;

			bool prevIsDigit = pos > 0 && char.IsDigit(lineText[pos - 1]);
			bool nextIsDigit = pos + 1 < lineText.Length && char.IsDigit(lineText[pos + 1]);

			if (prevIsDigit || nextIsDigit)
				return false;

			return pos + 1 < lineText.Length &&
				   (char.IsLetter(lineText[pos + 1]) || lineText[pos + 1] == '_');
		}

		/// <summary>
		/// 查找行内 // 注释开始位置。
		/// 字符串内部的 // 不算注释。
		/// </summary>
		private int FindLineCommentStartOutsideString(string lineText, int startIndex, List<Span> stringSpans)
		{
			for (int i = startIndex; i + 1 < lineText.Length; i++)
			{
				if (lineText[i] == '/' && lineText[i + 1] == '/')
				{
					if (!stringSpans.Any(s => s.Contains(i)))
						return i;
				}
			}

			return -1;
		}

		private TagSpan<IErrorTag> CreateError(
			ITextSnapshot snapshot,
			int startPosition,
			int length,
			string message)
		{
			SnapshotSpan span = CreateSafeSpan(snapshot, startPosition, length);
			return new TagSpan<IErrorTag>(
				span,
				new ErrorTag(PredefinedErrorTypeNames.SyntaxError, message));
		}

		/// <summary>
		/// 防止 0 长度参数导致 SnapshotSpan 不明显或越界。
		/// </summary>
		private SnapshotSpan CreateSafeSpan(ITextSnapshot snapshot, int startPosition, int length)
		{
			int safeStart = Math.Max(0, Math.Min(startPosition, snapshot.Length));
			int safeLength = Math.Max(0, Math.Min(length, snapshot.Length - safeStart));

			if (safeLength == 0 && safeStart < snapshot.Length)
				safeLength = 1;

			return new SnapshotSpan(snapshot, safeStart, safeLength);
		}

		private sealed class ParsedArgument
		{
			public int StartPosition;
			public int Length;
			public string Value;
		}

		#endregion
	}
}