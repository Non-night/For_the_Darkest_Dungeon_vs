using System;
using System.Collections.Generic;
using System.Linq;

namespace For_the_Darkest_Dungeon.Completion
{
	internal sealed class FuzzyCandidate
	{
		public string Text { get; }
		public string Normalized { get; }

		public FuzzyCandidate(string text)
		{
			Text = text;
			Normalized = Normalize(text);
		}

		public static string Normalize(string text)
		{
			if (string.IsNullOrEmpty(text))
				return string.Empty;

			char[] buffer = new char[text.Length];
			int count = 0;

			foreach (char ch in text)
			{
				if (ch == '.' || ch == '_')
					continue;

				buffer[count++] = char.ToLowerInvariant(ch);
			}

			return new string(buffer, 0, count);
		}
	}

	internal static class FuzzyCompletionCache
	{
		private static readonly Dictionary<IReadOnlyList<string>, List<FuzzyCandidate>> Cache =
			new Dictionary<IReadOnlyList<string>, List<FuzzyCandidate>>();

		public static List<string> GetMatches(IReadOnlyList<string> source, string input)
		{
			if (source == null || source.Count == 0)
				return new List<string>();

			if (string.IsNullOrWhiteSpace(input))
				return source.ToList();

			// 先做普通前缀匹配。正常输入 .h / .heal 时最快。
			List<string> prefixMatches = new List<string>();

			foreach (string item in source)
			{
				if (item.StartsWith(input, StringComparison.OrdinalIgnoreCase))
					prefixMatches.Add(item);
			}

			if (prefixMatches.Count > 0)
				return prefixMatches;

			// 前缀完全没有结果时，才进入模糊匹配。
			string normalizedInput = FuzzyCandidate.Normalize(input);
			if (normalizedInput.Length == 0)
				return source.ToList();

			List<FuzzyCandidate> candidates = GetOrCreateCandidates(source);
			List<string> fuzzyMatches = new List<string>();

			foreach (FuzzyCandidate candidate in candidates)
			{
				if (IsFuzzyMatchNormalized(candidate.Normalized, normalizedInput))
					fuzzyMatches.Add(candidate.Text);
			}

			return fuzzyMatches;
		}

		private static List<FuzzyCandidate> GetOrCreateCandidates(IReadOnlyList<string> source)
		{
			if (Cache.TryGetValue(source, out var cached))
				return cached;

			var created = source.Select(s => new FuzzyCandidate(s)).ToList();
			Cache[source] = created;
			return created;
		}

		private static bool IsFuzzyMatchNormalized(string candidate, string input)
		{
			int candidateIndex = 0;

			for (int i = 0; i < input.Length; i++)
			{
				char inputChar = input[i];
				bool found = false;

				while (candidateIndex < candidate.Length)
				{
					if (candidate[candidateIndex++] == inputChar)
					{
						found = true;
						break;
					}
				}

				if (!found)
					return false;
			}

			return true;
		}
	}
}