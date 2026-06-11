using For_the_Darkest_Dungeon.DefinitionDarkest;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

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

		/// <summary>
		/// 延迟全文件刷新用的取消器。
		///
		/// 每次输入都会取消上一次 300ms 延迟任务，
		/// 这样连续输入时不会反复全文件刷新。
		/// </summary>
		private CancellationTokenSource _delayedFullRefreshCts;

		/// <summary>
		/// 保护 _delayedFullRefreshCts，避免快速输入时取消/创建任务发生竞争。
		/// </summary>
		private readonly object _refreshLock = new object();

		// 关键字：.xxx / .xxx_yyy / .xxx1
		private static readonly Regex KeywordRegex =
			new Regex(@"\.[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.Compiled);

		// Header：skill: / combat_skill: / display_modifier:
		// Header 里应允许前导空白
		private static readonly Regex HeaderRegex =
			new Regex(@"^[ \t]*(?<header>[a-zA-Z0-9_]+:)", RegexOptions.Compiled);

		// 字符串："..."
		private static readonly Regex StringRegex =
			new Regex(@"""[^""]*""", RegexOptions.Compiled);

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
		/// 性能策略：
		/// 1. 输入时立刻刷新“变化行所属的 header 块”，保证用户能快速看到附近错误变化；
		/// 2. 连续输入停止 300ms 后，再刷新整个文件，保证跨块、跨行、Header 变动等复杂情况最终完全正确。
		///
		/// 注意：
		/// 这里不直接计算错误，只是通过 TagsChanged 告诉 VS：
		/// “这段范围的错误标签可能变了，请重新调用 GetTags(...)。”
		/// 真正的错误判断仍然全部保留在 GetTags(...) 中。
		/// </summary>
		private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
		{
			ITextSnapshot snapshot = e.After;

			if (e.Changes.Count == 0)
				return;

			// ------------------------------------------------------------
			// 1. 立即刷新变化区域所属的 header 块
			// ------------------------------------------------------------
			int changedStart = e.Changes.Min(c => c.NewSpan.Start);
			int changedEnd = e.Changes.Max(c => c.NewSpan.End);

			// GetLineFromPosition 允许 position == snapshot.Length，
			// 但这里仍然做一次保护，避免极端情况下越界。
			int safeStart = Math.Max(0, Math.Min(changedStart, snapshot.Length));
			int safeEnd = Math.Max(0, Math.Min(changedEnd, snapshot.Length));

			ITextSnapshotLine startLine = snapshot.GetLineFromPosition(safeStart);
			ITextSnapshotLine endLine = snapshot.GetLineFromPosition(safeEnd);

			SnapshotSpan quickRefreshSpan = GetHeaderBlockRefreshSpan(
				snapshot,
				startLine.LineNumber,
				endLine.LineNumber);

			RaiseTagsChanged(quickRefreshSpan);

			// ------------------------------------------------------------
			// 2. 250ms 防抖后刷新整个文件
			// ------------------------------------------------------------
			CancellationToken token;

			lock (_refreshLock)
			{
				_delayedFullRefreshCts?.Cancel();
				_delayedFullRefreshCts?.Dispose();

				_delayedFullRefreshCts = new CancellationTokenSource();
				token = _delayedFullRefreshCts.Token;
			}

			_ = DelayedFullFileRefreshAsync(snapshot, token);
		}

		/// <summary>
		/// 延迟 250ms 后刷新整份文件。
		///
		/// 连续输入时，前一个任务会被取消，因此只有用户停顿下来后，
		/// 才会触发一次全文件刷新。
		/// </summary>
		private async Task DelayedFullFileRefreshAsync(ITextSnapshot snapshot, CancellationToken token)
		{
			try
			{
				await Task.Delay(250, token);

				if (token.IsCancellationRequested)
					return;

				RaiseTagsChanged(new SnapshotSpan(snapshot, 0, snapshot.Length));
			}
			catch (TaskCanceledException)
			{
				// 连续输入时取消很正常，直接忽略。
			}
			catch (ObjectDisposedException)
			{
				// CancellationTokenSource 被释放时可能出现，忽略即可。
			}
		}

		/// <summary>
		/// 根据变化行，尽量找到它所在的 header 块，并返回这个块的 SnapshotSpan。
		///
		/// 为什么刷新 header 块，而不是只刷新当前行：
		/// - Info / Art / Override 允许参数跨行；
		/// - 某一行参数变化可能影响上一行关键字的参数校验；
		/// - Header 变化会影响下面多行关键字是否合法。
		///
		/// 因此，快速刷新范围取“当前 header 到下一个 header 之前”的块，
		/// 比只刷当前行更稳，又比整文件刷新快很多。
		/// </summary>
		private SnapshotSpan GetHeaderBlockRefreshSpan(
			ITextSnapshot snapshot,
			int changedStartLineNumber,
			int changedEndLineNumber)
		{
			if (snapshot.LineCount == 0)
				return new SnapshotSpan(snapshot, 0, 0);

			int blockStartLine = FindHeaderStartLineAbove(snapshot, changedStartLineNumber);

			// 如果上方没有找到 Header，说明可能在文件开头的游离内容中。
			// 此时只刷新变化行附近，避免退化成全文件刷新。
			if (blockStartLine < 0)
			{
				blockStartLine = Math.Max(0, changedStartLineNumber - 2);
			}

			int blockEndLine = FindHeaderBlockEndLine(
				snapshot,
				blockStartLine,
				changedEndLineNumber);

			ITextSnapshotLine startLine = snapshot.GetLineFromLineNumber(blockStartLine);
			ITextSnapshotLine endLine = snapshot.GetLineFromLineNumber(blockEndLine);

			return new SnapshotSpan(
				snapshot,
				startLine.Start.Position,
				endLine.End.Position - startLine.Start.Position);
		}

		/// <summary>
		/// 从指定行向上寻找最近的 Header 行。
		///
		/// 注意：
		/// - 这里也遵守“// 注释至高优先级”；
		/// - 如果 Header 写在 // 后面，不算 Header；
		/// - 如果 // 出现在引号内，也仍然从 // 开始截断。
		/// </summary>
		private int FindHeaderStartLineAbove(ITextSnapshot snapshot, int fromLineNumber)
		{
			for (int i = Math.Min(fromLineNumber, snapshot.LineCount - 1); i >= 0; i--)
			{
				string lineText = snapshot.GetLineFromLineNumber(i).GetText();
				string codeText = GetCodeTextBeforeComment(lineText);

				if (string.IsNullOrWhiteSpace(codeText))
					continue;

				if (HeaderRegex.Match(codeText).Success)
					return i;
			}

			return -1;
		}

		/// <summary>
		/// 找到 header 块结束行。
		///
		/// 从变化区域之后继续向下找，下一个 Header 的上一行就是当前块结尾。
		/// 如果下面没有 Header，就到文件结尾。
		/// </summary>
		private int FindHeaderBlockEndLine(
			ITextSnapshot snapshot,
			int blockStartLine,
			int changedEndLineNumber)
		{
			int scanStartLine = Math.Max(blockStartLine + 1, changedEndLineNumber + 1);

			for (int i = scanStartLine; i < snapshot.LineCount; i++)
			{
				string lineText = snapshot.GetLineFromLineNumber(i).GetText();
				string codeText = GetCodeTextBeforeComment(lineText);

				if (string.IsNullOrWhiteSpace(codeText))
					continue;

				if (HeaderRegex.Match(codeText).Success)
					return Math.Max(blockStartLine, i - 1);
			}

			return snapshot.LineCount - 1;
		}

		/// <summary>
		/// 获取 // 之前的代码部分。
		///
		/// 这是为了和 GetTags(...) 中的规则保持一致：
		/// 只要行内出现 //，不管它是否在引号内部，后面全部不参与报错判断。
		/// </summary>
		private string GetCodeTextBeforeComment(string lineText)
		{
			int commentIndex = lineText.IndexOf("//", StringComparison.Ordinal);
			return commentIndex >= 0
				? lineText.Substring(0, commentIndex)
				: lineText;
		}

		/// <summary>
		/// 取出真正的 header
		/// </summary>
		private string GetHeaderName(RegexMatch headerMatch)
		{
			return headerMatch.Groups["header"].Value;
		}

		/// <summary>
		/// 安全触发 TagsChanged。
		///
		/// VS 会在收到这个事件后，重新调用 GetTags(...) 计算对应范围的错误。
		/// </summary>
		private void RaiseTagsChanged(SnapshotSpan span)
		{
			TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
		}

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		/// <summary>
		/// VS 调用此方法获取当前可见区域 / 指定范围内的错误标签。
		/// </summary>
		public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
		{
			if (spans.Count == 0)
				yield break;

			// ------------------------------------------------------------
			// 整文件级 death_class 互斥关键字检查。
			//
			// 这个规则不能只在当前行 / 当前 span 中检查，
			// 因为 .monster_class_id 和 .random_monster_class_ids
			// 可能出现在两个不同的 death_class: 块中。
			//
			// 所以这里先基于当前 snapshot 扫描整个文件。
			// 但实际返回 Tag 时，只返回和 requested spans 相交的错误，
			// 避免 GetTags 请求局部范围时返回不相关位置的 Tag。
			// ------------------------------------------------------------
			ITextSnapshot wholeSnapshot = spans[0].Snapshot;
			foreach (var globalError in ValidateDeathClassMonsterClassConflict(wholeSnapshot, spans))
				yield return globalError;

			foreach (var span in spans)
			{
				ITextSnapshot snapshot = span.Snapshot;
				int startLine = span.Start.GetContainingLine().LineNumber;
				int endLine = span.End.GetContainingLine().LineNumber;

				for (int i = startLine; i <= endLine; i++)
				{
					ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
					string lineText = line.GetText();

					// 注释至高优先级：只要本行出现 //，后面全部不参与任何报错判断。
					// 注意：即使 // 在引号内部，也从 // 开始截断。
					int commentIndex = lineText.IndexOf("//", StringComparison.Ordinal);
					string codeText = commentIndex >= 0
						? lineText.Substring(0, commentIndex)
						: lineText;

					// 空行、纯注释行直接跳过。
					if (string.IsNullOrWhiteSpace(codeText))
						continue;

					// ------------------------------------------------------------
					// 中文字符检查：
					// 只检查 // 前面的 codeText。
					// 因此注释里的中文允许存在，代码区的中文字符和中文标点全部报错。
					// ------------------------------------------------------------
					foreach (var chineseError in CreateChineseCharacterErrors(snapshot, line, codeText))
						yield return chineseError;

					// 半边引号检查：只检查 // 前面的 codeText，不跨行。
					int quoteCount = codeText.Count(c => c == '"');
					if (quoteCount % 2 != 0)
					{
						int quoteIndex = codeText.LastIndexOf('"');

						yield return CreateError(
							snapshot,
							line.Start.Position + Math.Max(quoteIndex, 0),
							quoteIndex >= 0 ? 1 : Math.Max(1, codeText.Length),
							"单行内引号不成对");
					}

					foreach (var quoteError in CreateInvalidInlineQuoteErrors(snapshot, line, codeText))
						yield return quoteError;

					List<Span> stringSpans = GetStringSpans(codeText);

					// 1. 判断当前行是否是 Header 行。
					string currentHeader = null;
					bool currentLineIsHeader = false;

					RegexMatch headerMatch = HeaderRegex.Match(codeText);
					if (headerMatch.Success)
					{
						currentHeader = GetHeaderName(headerMatch);
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
							bool hasKeyword = KeywordRegex.Matches(codeText)
								.Cast<RegexMatch>()
								.Any(m => !stringSpans.Any(s => s.Contains(m.Index))
										  && !(m.Index > 0 && char.IsDigit(codeText[m.Index - 1])));

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
					}

					// 4. 检查当前行所有关键字。
					foreach (RegexMatch match in KeywordRegex.Matches(codeText))
					{
						// 忽略字符串内部的 .xxx。
						if (stringSpans.Any(s => s.Contains(match.Index)))
							continue;

						// 忽略数字小数等情况，例如 1.xxx。
						if (match.Index > 0 && char.IsDigit(codeText[match.Index - 1]))
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
								errorMsg = $"模式差分Effect关键字 '{keyword}' 只能用于技能类 Header (如 skill:)。";
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
						else if (keyword == ".was_killed_effects")
						{
							yield return new TagSpan<IErrorTag>(
								new SnapshotSpan(snapshot, line.Start + match.Index, match.Length),
								new ErrorTag(PredefinedErrorTypeNames.SyntaxError,
								"请勿使用.was_killed_effects，以防引发真伤等各种击杀情况导致的游戏崩溃，请换用.was_killed_by_hero_effects或其他效果作为替代"));
						}
					}
				}
			}
		}

		/// <summary>
		/// 整文件检查：
		/// 在整个文件中，如果 death_class: Header 下同时出现：
		/// - .monster_class_id
		/// - .random_monster_class_ids
		///
		/// 则报错。
		///
		/// 注意：
		/// 1. 两个关键字不要求在同一个 death_class: 块里；
		/// 2. 只要它们的所属 Header 都是 death_class:，就算冲突；
		/// 3. 注释后的内容不参与检查；
		/// 4. 字符串内部的 .xxx 不参与检查；
		/// 5. 这里扫描整个 snapshot，但只返回和 requestedSpans 相交的错误 Tag。
		/// </summary>
		private IEnumerable<ITagSpan<IErrorTag>> ValidateDeathClassMonsterClassConflict(
			ITextSnapshot snapshot,
			NormalizedSnapshotSpanCollection requestedSpans)
		{
			bool foundMonsterClassId = false;
			bool foundRandomMonsterClassIds = false;

			SnapshotSpan monsterClassIdSpan = default(SnapshotSpan);
			SnapshotSpan randomMonsterClassIdsSpan = default(SnapshotSpan);

			string currentHeader = null;

			for (int i = 0; i < snapshot.LineCount; i++)
			{
				ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
				string lineText = line.GetText();

				// 复用当前文件的注释规则：
				// 只检查 // 前面的内容。
				string codeText = GetCodeTextBeforeComment(lineText);

				if (string.IsNullOrWhiteSpace(codeText))
					continue;

				// 如果当前行是 Header 行，更新当前所属 Header。
				// HeaderRegex 会保留冒号，例如 death_class:。
				RegexMatch headerMatch = HeaderRegex.Match(codeText);
				if (headerMatch.Success)
				{
					currentHeader = GetHeaderName(headerMatch);
				}

				// 只检查 death_class: 下的关键字。
				if (!string.Equals(currentHeader, "death_class:", StringComparison.Ordinal))
					continue;

				List<Span> stringSpans = GetStringSpans(codeText);

				foreach (RegexMatch match in KeywordRegex.Matches(codeText))
				{
					// 忽略字符串内部的 .xxx。
					if (stringSpans.Any(s => s.Contains(match.Index)))
						continue;

					// 忽略数字小数等情况，例如 1.xxx。
					if (match.Index > 0 && char.IsDigit(codeText[match.Index - 1]))
						continue;

					string keyword = match.Value;

					if (keyword == ".monster_class_id" && !foundMonsterClassId)
					{
						foundMonsterClassId = true;
						monsterClassIdSpan = new SnapshotSpan(
							snapshot,
							line.Start.Position + match.Index,
							match.Length);
					}
					else if (keyword == ".random_monster_class_ids" && !foundRandomMonsterClassIds)
					{
						foundRandomMonsterClassIds = true;
						randomMonsterClassIdsSpan = new SnapshotSpan(
							snapshot,
							line.Start.Position + match.Index,
							match.Length);
					}

					// 两个都找到了，就可以停止扫描。
					if (foundMonsterClassId && foundRandomMonsterClassIds)
						break;
				}

				if (foundMonsterClassId && foundRandomMonsterClassIds)
					break;
			}

			if (!foundMonsterClassId || !foundRandomMonsterClassIds)
				yield break;

			const string message =
				"death_class: 下不能同时使用 .monster_class_id 与 .random_monster_class_ids";

			// 为了让两个位置都能看到问题，这里两个关键字都报错。
			if (IntersectsAnyRequestedSpan(monsterClassIdSpan, requestedSpans))
			{
				yield return new TagSpan<IErrorTag>(
					monsterClassIdSpan,
					new ErrorTag(PredefinedErrorTypeNames.SyntaxError, message));
			}

			if (IntersectsAnyRequestedSpan(randomMonsterClassIdsSpan, requestedSpans))
			{
				yield return new TagSpan<IErrorTag>(
					randomMonsterClassIdsSpan,
					new ErrorTag(PredefinedErrorTypeNames.SyntaxError, message));
			}
		}

		/// <summary>
		/// 判断一个错误 Span 是否和 VS 当前请求的 spans 相交。
		///
		/// GetTags 可能只请求可见区域或局部范围，
		/// 所以整文件扫描后不能无脑返回所有位置的 Tag。
		/// </summary>
		private bool IntersectsAnyRequestedSpan(
			SnapshotSpan errorSpan,
			NormalizedSnapshotSpanCollection requestedSpans)
		{
			foreach (SnapshotSpan requestedSpan in requestedSpans)
			{
				if (requestedSpan.IntersectsWith(errorSpan))
					return true;
			}

			return false;
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

				int commentIndex = lineText.IndexOf("//", StringComparison.Ordinal);
				string codeText = commentIndex >= 0
					? lineText.Substring(0, commentIndex)
					: lineText;

				if (string.IsNullOrWhiteSpace(codeText))
					continue;

				RegexMatch headerMatch = HeaderRegex.Match(codeText);
				if (headerMatch.Success)
					return GetHeaderName(headerMatch);
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
			// ------------------------------------------------------------
			// 先解析参数，再决定做哪些检查。
			//
			// 原因：
			// 有些关键字没有固定参数表，也就是 GetValuesForKeyword(...) 会返回 null。
			// 但是这些关键字仍然可能存在长度规则，例如：
			// DarkestInfoData.SingleString32 / SingleString64。
			//
			// 如果一开始 validValues == null 就 yield break，
			// 那么这些“只有长度规则、没有固定取值表”的关键字永远不会被检查。
			// ------------------------------------------------------------
			List<ParsedArgument> args = ParseArgumentsUntilNextKeywordAcrossLines(
				snapshot,
				line,
				keywordEndIndex);

			if (args.Count == 0)
				yield break;

			// ------------------------------------------------------------
			// 单字符串长度检查。
			//
			// 规则：
			// - 如果当前 Header + Keyword 存在于 SingleString32，则最大长度为 32；
			// - 如果当前 Header + Keyword 存在于 SingleString64，则最大长度为 64；
			// - 长度刚好等于上限：Warning；
			// - 长度超过上限：SyntaxError。
			//
			// ------------------------------------------------------------
			foreach (var lengthTag in ValidateSingleStringLength(
				snapshot,
				currentHeader,
				keyword,
				args))
			{
				yield return lengthTag;
			}

			// ------------------------------------------------------------
			// 多字符串参数长度检查。
			//
			// 例如：某关键字最多允许 4 个参数，每个参数最多 32 字符。
			// ------------------------------------------------------------
			foreach (var lengthTag in ValidateMultiStringLength(
				snapshot,
				currentHeader,
				keyword,
				args))
			{
				yield return lengthTag;
			}

			// ------------------------------------------------------------
			// 多参数参数数量检查。
			// ------------------------------------------------------------
			foreach (var argCount in ValidateMaxArgumentCount(
				snapshot,
				currentHeader,
				keyword,
				args))
			{
				yield return argCount;
			}

			// ------------------------------------------------------------
			// 固定参数表检查。
			//
			// 如果没有固定参数表，到这里就可以结束。
			// 但前面的长度检查已经执行过了，所以不会漏掉自由字符串长度规则。
			// ------------------------------------------------------------

			List<string> validValues = DarkestInfoData.GetValuesForKeyword(currentHeader, keyword);
			if (validValues == null)
				yield break;

			if (keyword == ".disabled_popup_text_types")
			{
				foreach (var tag in ValidateDisabledPopupTextTypes(snapshot, keyword, validValues, args))
					yield return tag;

				yield break;
			}

			if (keyword == ".disabled_act_out_combat_start_turn_types")
			{
				foreach (var tag in ValidateDisabledAct_outCombatStartTurnTypes(snapshot, keyword, validValues, args))
					yield return tag;

				yield break;
			}

			bool isBooleanKeyword = IsBooleanValueList(validValues);

			if (isBooleanKeyword && args.Count > 1)
			{
				yield return CreateError(
					snapshot,
					args[1].StartPosition,
					args[1].Length,
					"如无必要，请勿在使用布尔型参数的关键字后写多个参数");
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
						// 额外进行一个特判，技能 type 允许自定义
						if (AllowedEffectsHeaders.Contains(currentHeader) && keyword == ".type")
						{
							yield return CreateSuggestion(
								snapshot,
								arg.StartPosition,
								arg.Length,
								"若为自定义类型技能且该技能非友方技能，会导致其无法享受近战/远程类增益，也无法触发相关 Trigger");
						}
						else
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
		/// .disabled_act_out_combat_start_turn_types 特判
		/// </summary>
		private IEnumerable<ITagSpan<IErrorTag>> ValidateDisabledAct_outCombatStartTurnTypes(
			ITextSnapshot snapshot,
			string keyword,
			List<string> validValues,
			List<ParsedArgument> args)
		{
			var validSet = new HashSet<string>(validValues, StringComparer.Ordinal);
			var usedSet = new HashSet<string>(StringComparer.Ordinal);

			if (args.Count > 4)
			{
				ParsedArgument firstExtraArg = args[4];

				yield return CreateError(
					snapshot,
					firstExtraArg.StartPosition,
					firstExtraArg.Length,
					$"{keyword} 参数数量不能超过 4 个，当前数量为 {args.Count}");
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

				int commentIndex = lineText.IndexOf(
					"//",
					Math.Min(posInLine, lineText.Length),
					StringComparison.Ordinal);

				int end = commentIndex >= 0 ? commentIndex : lineText.Length;
				string codeText = lineText.Substring(0, end);

				if (string.IsNullOrWhiteSpace(codeText))
				{
					lineNumber++;
					posInLine = 0;
					continue;
				}

				if (lineNumber != startLine.LineNumber)
				{
					RegexMatch headerMatch = HeaderRegex.Match(codeText);
					if (headerMatch.Success)
						break;
				}

				List<Span> stringSpans = GetStringSpans(codeText);

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
		/// 创建 Warning 级别的错误标签。
		/// </summary>
		private TagSpan<IErrorTag> CreateWarning(
			ITextSnapshot snapshot,
			int startPosition,
			int length,
			string message)
		{
			SnapshotSpan span = CreateSafeSpan(snapshot, startPosition, length);

			return new TagSpan<IErrorTag>(
				span,
				new ErrorTag(PredefinedErrorTypeNames.Warning, message));
		}

		/// <summary>
		/// 创建 Suggestion 级别的错误标签
		/// </summary>
		private TagSpan<IErrorTag> CreateSuggestion(
			ITextSnapshot snapshot,
			int startPosition,
			int length,
			string message)
		{
			SnapshotSpan span = CreateSafeSpan(snapshot, startPosition, length);

			return new TagSpan<IErrorTag>(
				span,
				new ErrorTag(PredefinedErrorTypeNames.Suggestion, message));
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

		/// <summary>
		/// 检查 codeText 中是否存在中文字符或中文标点。
		///
		/// 重要规则：
		/// - 传进来的必须是 codeText，也就是 // 前面的内容；
		/// - 因此 // 后面的注释内容不会被检查；
		/// - 会把连续中文字符合并成一个错误 Span，避免每个字都报一个错误；
		/// - 支持常用 CJK 字符、扩展区生僻字、兼容汉字、常见中文标点和全角中文标点。
		/// </summary>
		private IEnumerable<ITagSpan<IErrorTag>> CreateChineseCharacterErrors(
			ITextSnapshot snapshot,
			ITextSnapshotLine line,
			string codeText)
		{
			int start = -1;
			int length = 0;

			for (int i = 0; i < codeText.Length;)
			{
				if (IsChineseCharacterOrPunctuation(codeText, i, out int charLength))
				{
					if (start < 0)
					{
						start = i;
						length = charLength;
					}
					else
					{
						length += charLength;
					}

					i += charLength;
					continue;
				}

				if (start >= 0)
				{
					string badText = codeText.Substring(start, length);

					yield return new TagSpan<IErrorTag>(
						new SnapshotSpan(
							snapshot,
							line.Start.Position + start,
							length),
						new ErrorTag(
							PredefinedErrorTypeNames.SyntaxError,
							$"不允许出现中文字符或中文标点: {badText}"));

					start = -1;
					length = 0;
				}

				i++;
			}

			if (start >= 0)
			{
				string badText = codeText.Substring(start, length);

				yield return new TagSpan<IErrorTag>(
					new SnapshotSpan(
						snapshot,
						line.Start.Position + start,
						length),
					new ErrorTag(
						PredefinedErrorTypeNames.SyntaxError,
						$"不允许出现中文字符或中文标点: {badText}"));
			}
		}

		/// <summary>
		/// 判断指定位置是否是中文字符或中文标点。
		///
		/// 说明：
		/// - BMP 内的常用汉字、扩展 A、兼容汉字直接通过 char 判断；
		/// - 扩展 B/C/D/E/F/G/H 等生僻字位于 Unicode 辅助平面，需要处理代理对；
		/// - 中文标点覆盖 CJK Symbols and Punctuation、Vertical Forms、CJK Compatibility Forms、
		///   以及常见全角标点区间。
		/// </summary>
		private bool IsChineseCharacterOrPunctuation(string text, int index, out int charLength)
		{
			charLength = 1;

			if (index < 0 || index >= text.Length)
				return false;

			int codePoint;

			if (char.IsHighSurrogate(text[index]) &&
				index + 1 < text.Length &&
				char.IsLowSurrogate(text[index + 1]))
			{
				codePoint = char.ConvertToUtf32(text[index], text[index + 1]);
				charLength = 2;
			}
			else
			{
				codePoint = text[index];
			}

			// 常用汉字与 BMP 内扩展区
			if ((codePoint >= 0x3400 && codePoint <= 0x4DBF) ||   // CJK Extension A
				(codePoint >= 0x4E00 && codePoint <= 0x9FFF) ||   // CJK Unified Ideographs
				(codePoint >= 0xF900 && codePoint <= 0xFAFF))     // CJK Compatibility Ideographs
			{
				return true;
			}

			// 生僻字扩展区，位于 Unicode 辅助平面，需要代理对。
			if ((codePoint >= 0x20000 && codePoint <= 0x2A6DF) || // Extension B
				(codePoint >= 0x2A700 && codePoint <= 0x2B73F) || // Extension C
				(codePoint >= 0x2B740 && codePoint <= 0x2B81F) || // Extension D
				(codePoint >= 0x2B820 && codePoint <= 0x2CEAF) || // Extension E
				(codePoint >= 0x2CEB0 && codePoint <= 0x2EBEF) || // Extension F
				(codePoint >= 0x30000 && codePoint <= 0x3134F) || // Extension G
				(codePoint >= 0x31350 && codePoint <= 0x323AF))   // Extension H
			{
				return true;
			}

			// 中文标点、书名号、顿号、中文括号、全角空格等。
			if ((codePoint >= 0x3000 && codePoint <= 0x303F) ||   // CJK Symbols and Punctuation
				(codePoint >= 0xFE10 && codePoint <= 0xFE1F) ||   // Vertical Forms
				(codePoint >= 0xFE30 && codePoint <= 0xFE4F))     // CJK Compatibility Forms
			{
				return true;
			}

			// 常见全角中文标点。
			// 不直接包含整个 FF00-FFEF，避免把全角英文字母/数字也全部算作中文。
			if ((codePoint >= 0xFF01 && codePoint <= 0xFF0F) ||   // ！＂＃＄％＆＇（）＊＋，－．／
				(codePoint >= 0xFF1A && codePoint <= 0xFF20) ||   // ：；＜＝＞？＠
				(codePoint >= 0xFF3B && codePoint <= 0xFF40) ||   // ［＼］＾＿｀
				(codePoint >= 0xFF5B && codePoint <= 0xFF65))     // ｛｜｝～｡､･｢｣
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// 检查当前 Header + Keyword 是否属于单字符串长度限制表。
		/// </summary>
		private bool TryGetSingleStringMaxLength(
			string currentHeader,
			string keyword,
			out int maxLength)
		{
			if (DarkestInfoData.SingleString32.Contains((currentHeader, keyword)))
			{
				maxLength = 32;
				return true;
			}

			if (DarkestInfoData.SingleString64.Contains((currentHeader, keyword)))
			{
				maxLength = 64;
				return true;
			}

			if (DarkestInfoData.SingleString128.Contains((currentHeader, keyword)))
			{
				maxLength = 128;
				return true;
			}

			if (DarkestInfoData.SingleString512.Contains((currentHeader, keyword)))
			{
				maxLength = 512;
				return true;
			}

			maxLength = 0;
			return false;
		}

		/// <summary>
		/// 检查当前 Header + Keyword 是否属于多字符串参数长度限制表。
		/// </summary>
		private bool TryGetMultiStringLengthRule(
			string currentHeader,
			string keyword,
			out int maxArgs,
			out int maxLength)
		{
			if (DarkestInfoData.MultiStringLengthRules.TryGetValue(
				(currentHeader, keyword),
				out var rule))
			{
				maxArgs = rule.MaxArgs;
				maxLength = rule.MaxLength;
				return true;
			}

			maxArgs = 0;
			maxLength = 0;

			// 针对模式 eff 的特判
			if (AllowedEffectsHeaders.Contains(currentHeader) && keyword.EndsWith("_effects"))
			{
				maxArgs = 6;
				maxLength = 64;
				return true;
			}
			return false;
		}

		/// <summary>
		/// 检查当前 Header + Keyword 是否属于多参数参数数量限制表。
		/// </summary>
		private bool TryGetMaxArgumentCountRule(
			string currentHeader,
			string keyword,
			out int maxArgs)
		{
			if (DarkestInfoData.MaxArgumentCountRules.TryGetValue(
				(currentHeader, keyword),
				out var rule))
			{
				maxArgs = rule;
				return true;
			}

			maxArgs = 0;

			return false;
		}

		/// <summary>
		/// 针对单字符串参数做长度检查。
		/// </summary>
		private IEnumerable<ITagSpan<IErrorTag>> ValidateSingleStringLength(
			ITextSnapshot snapshot,
			string currentHeader,
			string keyword,
			List<ParsedArgument> args)
		{
			if (!TryGetSingleStringMaxLength(currentHeader, keyword, out int maxLength))
				yield break;

			if (args.Count == 0)
				yield break;

			if (args.Count > 1)
			{
				ParsedArgument argExceed = args[1];
				yield return CreateError(
					snapshot,
					argExceed.StartPosition,
					argExceed.Length,
					$"{currentHeader} 的 {keyword} 理论上只允许一个参数，如无必要请勿写多个参数");
			}

			ParsedArgument arg = args[0];

			// ParsedArgument.Value 对引号参数来说是引号内部内容；
			// 对无引号参数来说是参数本身。
			// 这里排除空白字符，避免 "abc def" 被长度计算成包含空格。
			int actualLength = arg.Value.Count(c => !char.IsWhiteSpace(c));

			if (actualLength == maxLength)
			{
				yield return CreateWarning(
					snapshot,
					arg.StartPosition,
					arg.Length,
					$"{currentHeader} 的 {keyword} 参数 '{arg.Value}' 长度已经达到 {maxLength} 个字符，建议缩短");
			}
			else if (actualLength > maxLength)
			{
				yield return CreateError(
					snapshot,
					arg.StartPosition,
					arg.Length,
					$"{currentHeader} 的 {keyword} 参数 '{arg.Value}' 长度不能超过 {maxLength} 个字符，当前长度为 {actualLength}");
			}
		}

		/// <summary>
		/// 针对多字符串参数做数量和长度检查。
		/// 注意：
		/// ParsedArgument.Value 已经去掉了外层引号。
		/// 长度计算时排除空白字符，和当前单字符串长度检查保持一致。
		/// </summary>
		private IEnumerable<ITagSpan<IErrorTag>> ValidateMultiStringLength(
			ITextSnapshot snapshot,
			string currentHeader,
			string keyword,
			List<ParsedArgument> args)
		{
			if (!TryGetMultiStringLengthRule(currentHeader, keyword, out int maxArgs, out int maxLength))
				yield break;

			if (args.Count == 0)
				yield break;

			bool canExKeyword = (keyword == ".damage_heal_base_class_ids" || keyword == ".incompatible_class_ids") ? true : false;
			bool sugLessKeyword = (currentHeader == "spawn:" && keyword == ".effects") ? true : false;

			// 参数数量检查：超过允许数量时报错。
			if (args.Count > maxArgs)
			{
				ParsedArgument firstExtraArg = args[maxArgs];

				if (canExKeyword)
					yield return CreateWarning(
						snapshot,
						firstExtraArg.StartPosition,
						firstExtraArg.Length,
						$"{currentHeader} 的 {keyword} 参数数量理论上不能超过 {maxArgs} 个，但是实际上似乎可以超出，此处仍然建议使用分行避免超量使用参数");
				yield return CreateError(
					snapshot,
					firstExtraArg.StartPosition,
					firstExtraArg.Length,
					$"{currentHeader} 的 {keyword} 参数数量不能超过 {maxArgs} 个，当前数量为 {args.Count}");
			}

			// spawn 特判
			if (sugLessKeyword && args.Count > (int)Math.Floor((double)maxArgs/2))
			{
				ParsedArgument firstSuggestArg = args[(int)Math.Floor((double)maxArgs / 2)];

				yield return CreateWarning(
					snapshot,
					firstSuggestArg.StartPosition,
					firstSuggestArg.Length,
					$"{currentHeader} 的 {keyword} 参数数量不建议超过 {(int)Math.Floor((double)maxArgs / 2)} 个");
			}

			// 单个参数长度检查。
			foreach (ParsedArgument arg in args)
			{
				int actualLength = arg.Value.Count(c => !char.IsWhiteSpace(c));

				if (actualLength == maxLength)
				{
					yield return CreateWarning(
						snapshot,
						arg.StartPosition,
						arg.Length,
						$"{currentHeader} 的 {keyword} 参数 '{arg.Value}' 长度已经达到 {maxLength} 个字符，建议缩短");
				}
				else if (actualLength > maxLength)
				{
					yield return CreateError(
						snapshot,
						arg.StartPosition,
						arg.Length,
						$"{currentHeader} 的 {keyword} 参数 '{arg.Value}' 长度不能超过 {maxLength} 个字符，当前长度为 {actualLength}");
				}
			}
		}

		/// <summary>
		/// 针对多参数做数量检查。
		/// </summary>
		private IEnumerable<ITagSpan<IErrorTag>> ValidateMaxArgumentCount(
			ITextSnapshot snapshot,
			string currentHeader,
			string keyword,
			List<ParsedArgument> args)
		{
			if (!TryGetMaxArgumentCountRule(currentHeader, keyword, out int maxArgs))
				yield break;

			if (args.Count == 0)
				yield break;

			// 参数数量检查：超过允许数量时报错。
			if (args.Count > maxArgs)
			{
				ParsedArgument firstExtraArg = args[maxArgs];

				yield return CreateError(
					snapshot,
					firstExtraArg.StartPosition,
					firstExtraArg.Length,
					$"{currentHeader} 的 {keyword} 参数数量不能超过 {maxArgs} 个，当前数量为 {args.Count}");
			}
		}

		/// <summary>
		/// 检查引号是否贴着普通字符。
		/// 规则：
		/// 1. 开始引号前面不能紧贴非空白字符；
		/// 2. 结束引号后面不能紧贴普通字符；
		/// 3. 结束引号后面如果是空白、行尾、注释则允许。
		/// </summary>
		private IEnumerable<ITagSpan<IErrorTag>> CreateInvalidInlineQuoteErrors(
			ITextSnapshot snapshot,
			ITextSnapshotLine line,
			string codeText)
		{
			bool inString = false;

			for (int i = 0; i < codeText.Length; i++)
			{
				if (codeText[i] != '"')
					continue;

				if (!inString)
				{
					// 当前是开始引号。
					// 开始引号必须位于参数起点，也就是：
					// 行首，或者前一个字符是空白。
					bool hasBadPreviousChar =
						i > 0 &&
						!char.IsWhiteSpace(codeText[i - 1]);

					if (hasBadPreviousChar)
					{
						yield return CreateError(
							snapshot,
							line.Start.Position + i,
							1,
							"引号前极度不建议紧贴普通字符，请用空格分隔，或把整个参数放进引号");
					}

					inString = true;
				}
				else
				{
					// 当前是结束引号。
					// 结束引号后面允许：
					// 1. 行尾；
					// 2. 空白；
					// 3. 下一个 .keyword。
					bool hasBadNextChar =
						i + 1 < codeText.Length &&
						!char.IsWhiteSpace(codeText[i + 1]);

					if (hasBadNextChar)
					{
						if (codeText[i + 1] == '.')
							yield return CreateWarning(
								snapshot,
								line.Start.Position + i,
								1,
								"引号后极度不建议紧贴下一个关键字，请用空格分隔");
						else
							yield return CreateError(
								snapshot,
								line.Start.Position + i,
								1,
								"引号后极度不建议紧贴普通字符，请用空格分隔，或把整个参数放进引号");
					}

					inString = false;
				}
			}
		}

		#endregion
	}
}