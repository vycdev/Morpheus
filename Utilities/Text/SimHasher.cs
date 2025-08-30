using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Morpheus.Utilities.Text;

public static class SimHasher
{
    // Normalize: lowercase, NFKD, strip diacritics, remove punctuation/emoji/control, collapse whitespace, map digits
    public static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        string nfkd = input.Normalize(NormalizationForm.FormKD).ToLowerInvariant();
        StringBuilder sb = new(nfkd.Length);
        bool lastWasSpace = false;

        foreach (var ch in nfkd.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
                continue;
            }
            lastWasSpace = false;

            // Strip combining marks (diacritics)
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch.ToString(), 0);
            if (cat == UnicodeCategory.NonSpacingMark || cat == UnicodeCategory.SpacingCombiningMark)
                continue;

            // Remove punctuation, symbols, control chars, surrogate/format
            if (Rune.IsControl(ch) || Rune.IsPunctuation(ch) || Rune.IsSymbol(ch))
                continue;

            // Map digits to a placeholder
            if (Rune.IsDigit(ch))
            {
                sb.Append('0');
                continue;
            }

            // Remove variation selectors and zero-width chars
            if (ch.Value == 0xFE0F || ch.Value == 0x200D || ch.Value == 0x200B)
                continue;

            sb.Append(ch.ToString());
        }

        var result = sb.ToString().Trim();
        return result;
    }

    // 64-bit SimHash over character trigrams
    public static (ulong hash, int normalizedLength) ComputeSimHash(string input)
    {
        var norm = Normalize(input);
        int n = norm.Length;
        if (n < 3)
            return (0UL, n);

        Span<long> weights = stackalloc long[64];
        // iterate trigrams
        for (int i = 0; i <= n - 3; i++)
        {
            ulong h = Fnv1a64(norm.AsSpan(i, 3));
            for (int b = 0; b < 64; b++)
            {
                long w = ((h >> b) & 1UL) == 1UL ? 1 : -1; // unit weights
                weights[b] += w;
            }
        }

        ulong sim = 0UL;
        for (int b = 0; b < 64; b++)
        {
            if (weights[b] >= 0) sim |= 1UL << b;
        }
        return (sim, n);
    }

    public static int HammingDistance(ulong a, ulong b)
    {
        return BitOperations.PopCount(a ^ b);
    }

    private static ulong Fnv1a64(ReadOnlySpan<char> span)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        for (int i = 0; i < span.Length; i++)
        {
            // simple char to byte mapping: use UTF-16 low byte and high byte
            ushort c = span[i];
            hash ^= (byte)(c & 0xFF);
            hash *= prime;
            hash ^= (byte)(c >> 8);
            hash *= prime;
        }
        return hash;
    }
}
