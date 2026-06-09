using For_the_Darkest_Dungeon.DefinitionDarkest;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;

namespace For_the_Darkest_Dungeon.Classification
{
    internal class EffectErrorTagger : ITagger<IErrorTag>
    {
        private readonly ITextBuffer _buffer;

        private readonly Regex _keywordRegex = new Regex(@"\.[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.Compiled);
        private readonly Regex _stringRegex = new Regex(@"""([^""]*)""", RegexOptions.Compiled);
        private readonly Regex _nextParamRegex = new Regex(@"^\s+(?:""([^""]*)""|([a-zA-Z0-9_]+))", RegexOptions.Compiled);

        internal EffectErrorTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _buffer.Changed += OnBufferChanged;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            // 关键：当某一行改变时，必须通知编辑器刷新后面所有的行，
            // 因为“effect:”前缀的存在决定了后面所有行的合法性。
            var snapshot = e.After;
            if (e.Changes.Count > 0)
            {
                var start = e.Changes.Min(c => c.NewSpan.Start);
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, start, snapshot.Length - start)));
            }
        }

        /// <summary>
        /// 向上寻找该行是否属于一个 effect 块
        /// </summary>
        private bool IsInsideEffectBlock(ITextSnapshot snapshot, int fromLineNumber)
        {
            // 向上遍历，直到找到一个包含冒号的行
            for (int i = fromLineNumber; i >= 0; i--)
            {
                var line = snapshot.GetLineFromLineNumber(i);
                string text = line.GetText().Trim();

                // 跳过空行和注释
                if (string.IsNullOrWhiteSpace(text) || text.StartsWith("//"))
                    continue;

                // 检查这行是否有冒号（作为 Header）
                // 排除字符串内的冒号
                int colonIndex = GetFirstLogicalColon(line.GetText());

                if (colonIndex != -1)
                {
                    string header = line.GetText().Substring(0, colonIndex + 1).Trim();
                    // 如果是 effect: 则是合法的块开始
                    return header.Equals("effect:", StringComparison.OrdinalIgnoreCase);
                }

                // 如果这行没有冒号，且不是空行/注释，说明它本身也是属性行，继续向上找 Header
            }
            return false;
        }

        private int GetFirstLogicalColon(string lineText)
        {
            var stringMatches = _stringRegex.Matches(lineText).Cast<Match>();
            var stringSpans = stringMatches.Select(m => new Span(m.Index, m.Length)).ToList();

            for (int c = 0; c < lineText.Length; c++)
            {
                if (lineText[c] == ':' && !stringSpans.Any(s => s.Contains(c)))
                {
                    return c;
                }
            }
            return -1;
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (var span in spans)
            {
                var snapshot = span.Snapshot;
                int startLine = snapshot.GetLineNumberFromPosition(span.Start);
                int endLine = snapshot.GetLineNumberFromPosition(span.End);

                for (int i = startLine; i <= endLine; i++)
                {
                    var line = snapshot.GetLineFromLineNumber(i);
                    string lineText = line.GetText();

                    // 1. 完全忽略空行和纯注释行（不产生任何错误）
                    if (string.IsNullOrWhiteSpace(lineText) || lineText.TrimStart().StartsWith("//"))
                        continue;

                    int firstColonIndex = GetFirstLogicalColon(lineText);

                    // 2. 结构校验
                    if (firstColonIndex == -1)
                    {
                        // 如果本行没有冒号，也不是空行，那么它必须位于某个 effect: 块下方
                        if (!IsInsideEffectBlock(snapshot, i - 1))
                        {
                            yield return new TagSpan<IErrorTag>(
                                new SnapshotSpan(line.Start, line.Length),
                                new ErrorTag(PredefinedErrorTypeNames.SyntaxError, "此行不属于任何 'effect:'")
                            );
                        }
                        else
                        {
                            if (_keywordRegex.Match(lineText).Success)
                                yield return new TagSpan<IErrorTag>(
                                    new SnapshotSpan(line.Start, line.Length),
                                    new ErrorTag(PredefinedErrorTypeNames.Warning, "建议单条effect不在内部换行，如有需求请尽量用分行写法")
                                );
                            else
                                yield return new TagSpan<IErrorTag>(
                                    new SnapshotSpan(line.Start, line.Length),
                                    new ErrorTag(PredefinedErrorTypeNames.SyntaxError, "错误内容")
                                );
                        }
                    }
                    else
                    {
                        // 如果本行有冒号，它必须是 "effect:"
                        string header = lineText.Substring(0, firstColonIndex + 1).Trim();
                        if (!header.Equals("effect:", StringComparison.OrdinalIgnoreCase))
                        {
                            yield return new TagSpan<IErrorTag>(
                                new SnapshotSpan(line.Start, firstColonIndex + 1),
                                new ErrorTag(PredefinedErrorTypeNames.SyntaxError, $"无效的 Header '{header}'，Effect 文件需使用 'effect:'")
                            );
                        }
                    }

                    // 3. 关键字和参数校验
                    var stringMatches = _stringRegex.Matches(lineText).Cast<Match>().ToList();
                    var stringSpans = stringMatches.Select(m => new Span(m.Index, m.Length)).ToList();

                    foreach (Match match in _keywordRegex.Matches(lineText))
                    {
                        if (stringSpans.Any(s => s.Contains(match.Index))) continue;
                        if (match.Index > 0 && char.IsDigit(lineText[match.Index - 1])) continue;

                        string keyword = match.Value;
                        if (!DarkestEffectsData.AllKeywords.Contains(keyword))
                        {
                            yield return new TagSpan<IErrorTag>(
                                new SnapshotSpan(snapshot, line.Start + match.Index, match.Length),
                                new ErrorTag(PredefinedErrorTypeNames.SyntaxError, $"无效关键字: {keyword}")
                            );
                            continue;
                        }

                        if (DarkestEffectsData.KeywordToValuesMap.TryGetValue(keyword, out List<string> validValues))
                        {
                            var remainingText = lineText.Substring(match.Index + match.Length);
                            var paramMatch = _nextParamRegex.Match(remainingText);
                            if (paramMatch.Success)
                            {
                                string valInQuote = paramMatch.Groups[1].Value;
                                string valPlain = paramMatch.Groups[2].Value;
                                bool isQuoted = paramMatch.Groups[1].Success || paramMatch.Value.Contains("\"\"");
                                string actualValue = isQuoted ? valInQuote : valPlain;

                                bool isParamValid = validValues.Contains(actualValue);

                                if (!isParamValid)
                                {
                                    int valOffset = isQuoted ? paramMatch.Value.IndexOf('"') : paramMatch.Value.IndexOf(actualValue);
                                    int errorStart = line.Start + match.Index + match.Length + paramMatch.Index + valOffset;
                                    int errorLen = isQuoted ? (paramMatch.Groups[1].Length + 2) : actualValue.Length;

                                    yield return new TagSpan<IErrorTag>(
                                        new SnapshotSpan(snapshot, errorStart, errorLen),
                                        new ErrorTag(PredefinedErrorTypeNames.SyntaxError, $"值 '{actualValue}' 对关键字 '{keyword}' 无效")
                                    );
                                }
                                else if (isQuoted && DarkestEffectsData.NumBoolValues.Contains(actualValue))
                                {
                                    int valOffset = paramMatch.Value.IndexOf('"');
                                    int errorStart = line.Start + match.Index + match.Length + paramMatch.Index + valOffset;
                                    int errorLen = isQuoted ? (paramMatch.Groups[1].Length + 2) : actualValue.Length;

                                    yield return new TagSpan<IErrorTag>(
                                        new SnapshotSpan(snapshot, errorStart, errorLen),
                                        new ErrorTag(PredefinedErrorTypeNames.SyntaxError, $"值 '{actualValue}' 不应带引号")
                                    );
                                }
                            }
                        }
                    }
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }

    [Export(typeof(ITaggerProvider))]
    [ContentType("darkest-effect")]
    [TagType(typeof(IErrorTag))]
    internal class EffectErrorTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return buffer.Properties.GetOrCreateSingletonProperty(() => new EffectErrorTagger(buffer)) as ITagger<T>;
        }
    }
}