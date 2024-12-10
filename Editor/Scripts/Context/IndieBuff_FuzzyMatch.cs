
using System;

namespace IndieBuff.Editor
{
    internal static class IndieBuff_FuzzyMatch
    {
        public static double Calculate(string source, string target, bool caseSensitive = false)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return 0;

            if (!caseSensitive)
            {
                source = source.ToLower();
                target = target.ToLower();
            }

            if (source == target) return 1.0;

            if (source.Contains(target))
            {
                double lengthRatio = (double)target.Length / source.Length;
                return 0.5 + (lengthRatio * 0.3);
            }

            var distance = LevenshteinDistance(source, target);
            var maxLength = Math.Max(source.Length, target.Length);

            var similarity = 1.0 - ((double)distance / maxLength);

            return similarity < 0.4 ? 0 : similarity * 0.6; // Max 0.6 for partial matches
        }

        private static int LevenshteinDistance(string source, string target)
        {
            var matrix = new int[source.Length + 1, target.Length + 1];

            for (int i = 0; i <= source.Length; i++)
                matrix[i, 0] = i;
            for (int j = 0; j <= target.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = (source[i - 1] == target[j - 1]) ? 0 : 1;

                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost
                    );
                }
            }

            return matrix[source.Length, target.Length];
        }
    }
}