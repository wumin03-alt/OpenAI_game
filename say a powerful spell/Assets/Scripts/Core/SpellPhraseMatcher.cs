using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PowerfulSpell
{
    public readonly struct PhraseMatchResult
    {
        public readonly bool Success;
        public readonly float Score;
        public readonly string NormalizedExpected;
        public readonly string NormalizedRecognized;
        public readonly float LengthRatio;
        public readonly float PhoneticScore;
        public readonly float BigramScore;
        public readonly float WordCoverage;
        public readonly float EndingScore;

        public PhraseMatchResult(bool success, float score, string expected, string recognized,
            float lengthRatio = 0f, float phoneticScore = 0f, float bigramScore = 0f,
            float wordCoverage = 0f, float endingScore = 0f)
        {
            Success = success;
            Score = score;
            NormalizedExpected = expected;
            NormalizedRecognized = recognized;
            LengthRatio = lengthRatio;
            PhoneticScore = phoneticScore;
            BigramScore = bigramScore;
            WordCoverage = wordCoverage;
            EndingScore = endingScore;
        }
    }

    public static class SpellPhraseMatcher
    {
        private static readonly Regex NonKoreanLetterDigit = new Regex("[^가-힣a-z0-9 ]", RegexOptions.Compiled);
        private static readonly Regex Spaces = new Regex("\\s+", RegexOptions.Compiled);

        public static PhraseMatchResult Match(string expected, string recognized, float threshold = .72f)
        {
            string a = Normalize(expected);
            string b = Normalize(recognized);
            if (a.Length == 0 || b.Length == 0)
                return new PhraseMatchResult(false, 0f, a, b);

            string compactA = a.Replace(" ", string.Empty);
            string compactB = b.Replace(" ", string.Empty);
            float editScore = 1f - Levenshtein(compactA, compactB) / (float)Math.Max(compactA.Length, compactB.Length);

            string phoneticA = DecomposeHangul(compactA);
            string phoneticB = DecomposeHangul(compactB);
            float phoneticScore = 1f - Levenshtein(phoneticA, phoneticB) / (float)Math.Max(phoneticA.Length, phoneticB.Length);
            float sequenceScore = LongestCommonSubsequence(compactA, compactB) / (float)Math.Max(compactA.Length, compactB.Length);
            float bigramScore = BigramDice(compactA, compactB);

            string[] expectedWords = a.Split(' ');
            string[] recognizedWords = b.Split(' ');
            float wordCoverage = ConsumedWordCoverage(expectedWords, recognizedWords);

            float lengthRatio = Math.Min(compactA.Length, compactB.Length) / (float)Math.Max(compactA.Length, compactB.Length);
            // 어느 한 지표만 높아도 성공시키던 방식은 비슷한 일부 단어만 말하는 편법을 허용했다.
            // 전체 철자/발음, 순서, 연속 음절, 핵심 단어를 모두 반영하고 별도 하한도 검사한다.
            float weightedScore = editScore * .24f + phoneticScore * .30f + sequenceScore * .16f
                + bigramScore * .18f + wordCoverage * .12f;
            // 혀 꼬임은 정확히 말해도 Whisper가 찹쌀→첩살, 법학→팝팝처럼 표기를
            // 뭉갠다. 이때 자모 흐름은 보존되므로 발음 중심 점수를 함께 사용한다.
            float pronunciationScore = phoneticScore * .90f + lengthRatio * .10f;
            float score = Math.Max(weightedScore, pronunciationScore);

            int tailWordCount = Math.Min(6, Math.Max(3, (int)Math.Ceiling(expectedWords.Length * .30f)));
            string expectedTail = JoinLastWords(expectedWords, tailWordCount);
            string recognizedTail = JoinLastWords(recognizedWords, tailWordCount);
            float tailPhoneticScore = PhoneticSimilarity(expectedTail, recognizedTail);
            float tailSequenceCoverage = SuffixSequenceCoverage(compactA, compactB, .38f, 1.6f);
            string expectedTerminal = JoinLastWords(expectedWords, Math.Min(2, expectedWords.Length));
            string recognizedTerminal = JoinLastWords(recognizedWords, Math.Min(2, recognizedWords.Length));
            float terminalPhoneticScore = Math.Max(PhoneticSimilarity(expectedTerminal, recognizedTerminal),
                PhoneticSequenceCoverage(expectedTerminal, recognizedTerminal));
            string expectedFinalWord = expectedWords[expectedWords.Length - 1];
            string recognizedFinalWord = recognizedWords[recognizedWords.Length - 1];
            float terminalSuffixScore = EndingSyllableSimilarity(expectedFinalWord, recognizedFinalWord, 2);
            float finalWordSoundScore = WordSoundSimilarity(expectedFinalWord, recognizedFinalWord);
            float endingScore = Math.Max(tailSequenceCoverage,
                tailPhoneticScore * .58f + terminalPhoneticScore * .42f);

            string finalWord = expectedFinalWord;
            int requiredFinalOccurrences = expectedWords.Count(word => word == finalWord);
            int recognizedFinalOccurrences = recognizedWords.Count(word => WordSoundSimilarity(word, finalWord) >= .52f);
            bool repeatedEndingComplete = requiredFinalOccurrences <= 1 || recognizedFinalOccurrences >= requiredFinalOccurrences;
            bool grammaticalEnding = expectedFinalWord.EndsWith("이다") || expectedFinalWord.EndsWith("는가");
            bool terminalComplete = terminalSuffixScore >= .72f
                && (finalWordSoundScore >= .58f || (grammaticalEnding && tailSequenceCoverage >= .72f));
            bool isCloudIncantation = a.Contains("새털구름") && a.Contains("깃털구름");
            float completionLength = compactA.Length <= 24 ? .92f : (isCloudIncantation ? .78f : .84f);
            bool endingComplete = lengthRatio >= completionLength && endingScore >= .57f
                && terminalComplete && repeatedEndingComplete;

            float minimumLengthRatio = threshold >= .68f ? .70f : .66f;
            float minimumWordCoverage = threshold >= .68f ? .40f : .36f;
            bool strictMatch = score >= threshold
                && lengthRatio >= minimumLengthRatio
                && phoneticScore >= threshold - .12f
                && bigramScore >= threshold - .25f
                && wordCoverage >= minimumWordCoverage;
            bool strongPhoneticRecovery = lengthRatio >= .86f && phoneticScore >= .73f;
            bool structuredPhoneticRecovery = lengthRatio >= .86f && phoneticScore >= .63f
                && bigramScore >= .40f && wordCoverage >= .30f;
            bool success = endingComplete && (strictMatch || strongPhoneticRecovery || structuredPhoneticRecovery);
            return new PhraseMatchResult(success, Math.Max(0f, Math.Min(1f, score)), a, b,
                lengthRatio, phoneticScore, bigramScore, wordCoverage, endingScore);
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string normalized = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
            normalized = NonKoreanLetterDigit.Replace(normalized, " ");
            return Spaces.Replace(normalized, " ").Trim();
        }

        private static bool WordClose(string a, string b)
        {
            if (a == b) return true;
            if (a.Length > 2 && b.Length > 2 && (a.Contains(b) || b.Contains(a))) return true;
            int max = Math.Max(a.Length, b.Length);
            return max > 0 && 1f - Levenshtein(a, b) / (float)max >= .68f;
        }

        private static float ConsumedWordCoverage(string[] expectedWords, string[] recognizedWords)
        {
            if (expectedWords.Length == 0) return 0f;
            var used = new bool[recognizedWords.Length];
            int matched = 0;
            foreach (string expectedWord in expectedWords)
            {
                int bestIndex = -1;
                float bestScore = 0f;
                for (int i = 0; i < recognizedWords.Length; i++)
                {
                    if (used[i]) continue;
                    float score = WordSoundSimilarity(expectedWord, recognizedWords[i]);
                    if ((WordClose(expectedWord, recognizedWords[i]) || score >= .68f) && score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }
                if (bestIndex < 0) continue;
                used[bestIndex] = true;
                matched++;
            }
            return matched / (float)expectedWords.Length;
        }

        private static float WordSoundSimilarity(string a, string b) => PhoneticSimilarity(a, b);

        private static float PhoneticSimilarity(string a, string b)
        {
            string compactA = a.Replace(" ", string.Empty);
            string compactB = b.Replace(" ", string.Empty);
            if (compactA.Length == 0 || compactB.Length == 0) return 0f;
            string phoneticA = DecomposeHangul(compactA);
            string phoneticB = DecomposeHangul(compactB);
            return 1f - Levenshtein(phoneticA, phoneticB) / (float)Math.Max(phoneticA.Length, phoneticB.Length);
        }

        private static float PhoneticSequenceCoverage(string expected, string recognized)
        {
            string a = DecomposeHangul(expected.Replace(" ", string.Empty));
            string b = DecomposeHangul(recognized.Replace(" ", string.Empty));
            return a.Length == 0 ? 0f : LongestCommonSubsequence(a, b) / (float)a.Length;
        }

        private static float SuffixSequenceCoverage(string expected, string recognized, float ratio, float windowMultiplier)
        {
            int tailLength = Math.Min(expected.Length, Math.Max(8, (int)Math.Ceiling(expected.Length * ratio)));
            string expectedTail = expected.Substring(expected.Length - tailLength);
            int windowLength = Math.Min(recognized.Length, Math.Max(tailLength, (int)Math.Ceiling(tailLength * windowMultiplier)));
            string recognizedWindow = recognized.Substring(recognized.Length - windowLength);
            return PhoneticSequenceCoverage(expectedTail, recognizedWindow);
        }

        private static float EndingSyllableSimilarity(string expected, string recognized, int syllables)
        {
            int expectedStart = Math.Max(0, expected.Length - syllables);
            int recognizedStart = Math.Max(0, recognized.Length - syllables);
            return PhoneticSimilarity(expected.Substring(expectedStart), recognized.Substring(recognizedStart));
        }

        private static string JoinLastWords(string[] words, int count)
        {
            int start = Math.Max(0, words.Length - count);
            return string.Join(" ", words.Skip(start));
        }

        private static int Levenshtein(string a, string b)
        {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) previous[j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }
                (previous, current) = (current, previous);
            }
            return previous[b.Length];
        }

        private static string DecomposeHangul(string value)
        {
            var builder = new StringBuilder(value.Length * 3);
            foreach (char character in value)
            {
                if (character >= 0xAC00 && character <= 0xD7A3)
                {
                    int syllable = character - 0xAC00;
                    int initial = syllable / 588;
                    int medial = (syllable % 588) / 28;
                    int final = syllable % 28;
                    builder.Append((char)(0xE000 + initial));
                    builder.Append((char)(0xE020 + medial));
                    if (final > 0) builder.Append((char)(0xE040 + final));
                }
                else
                {
                    builder.Append(character);
                }
            }
            return builder.ToString();
        }

        private static int LongestCommonSubsequence(string a, string b)
        {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                    current[j] = a[i - 1] == b[j - 1] ? previous[j - 1] + 1 : Math.Max(previous[j], current[j - 1]);
                (previous, current) = (current, previous);
                Array.Clear(current, 0, current.Length);
            }
            return previous[b.Length];
        }

        private static float BigramDice(string a, string b)
        {
            if (a.Length < 2 || b.Length < 2) return a == b ? 1f : 0f;
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < a.Length - 1; i++)
            {
                string gram = a.Substring(i, 2);
                counts.TryGetValue(gram, out int count);
                counts[gram] = count + 1;
            }

            int intersection = 0;
            for (int i = 0; i < b.Length - 1; i++)
            {
                string gram = b.Substring(i, 2);
                if (counts.TryGetValue(gram, out int count) && count > 0)
                {
                    intersection++;
                    counts[gram] = count - 1;
                }
            }
            return 2f * intersection / ((a.Length - 1) + (b.Length - 1));
        }
    }
}
