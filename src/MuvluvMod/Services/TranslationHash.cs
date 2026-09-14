using System;
using System.Collections.Generic;
using System.Linq;

namespace MuvluvMod.Services;

using MasterTranslationTables = Dictionary<string, Dictionary<string, Dictionary<string, string>>>;
using NameTranslationTables = Dictionary<string, Dictionary<string, string>>;

/// <summary>
/// Computes hashes compatible with the translation repository manifest generator.
/// </summary>
internal static class TranslationHash
{
    private static readonly IComparer<string> KeyComparer = new UnicodeCodePointComparer();

    public static string ComputeScene(Dictionary<string, string> translations) =>
        Compute(
            translations
                .Keys.OrderBy(key => key, KeyComparer)
                .Select(key => ((string Key, string Value))(key, translations[key]))
        );

    public static string ComputeNames(NameTranslationTables tables) =>
        Compute(EnumerateNameEntries(tables));

    public static string ComputeMasterData(MasterTranslationTables tables) =>
        Compute(EnumerateMasterDataEntries(tables));

    private static IEnumerable<(string Key, string Value)> EnumerateNameEntries(
        NameTranslationTables tables
    )
    {
        foreach (string tableName in tables.Keys.OrderBy(key => key, KeyComparer))
        {
            var translations = tables[tableName];
            if (translations == null)
                continue;

            foreach (string source in translations.Keys.OrderBy(key => key, KeyComparer))
                yield return ($"{tableName}\x01{source}", translations[source]);
        }
    }

    private static IEnumerable<(string Key, string Value)> EnumerateMasterDataEntries(
        MasterTranslationTables tables
    )
    {
        foreach (string typeName in tables.Keys.OrderBy(key => key, KeyComparer))
        {
            var properties = tables[typeName];
            if (properties == null)
                continue;

            foreach (string propertyName in properties.Keys.OrderBy(key => key, KeyComparer))
            {
                var translations = properties[propertyName];
                if (translations == null)
                    continue;

                foreach (string source in translations.Keys.OrderBy(key => key, KeyComparer))
                    yield return (
                        $"{typeName}\x01{propertyName}\x01{source}",
                        translations[source]
                    );
            }
        }
    }

    private static string Compute(IEnumerable<(string Key, string Value)> entries) =>
        Utility.Cryptography.StringTableHash.ComputeEntries(entries);

    private sealed class UnicodeCodePointComparer : IComparer<string>
    {
        public int Compare(string left, string right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int leftIndex = 0;
            int rightIndex = 0;
            while (leftIndex < left.Length && rightIndex < right.Length)
            {
                int leftCodePoint = char.ConvertToUtf32(left, leftIndex);
                int rightCodePoint = char.ConvertToUtf32(right, rightIndex);
                int comparison = leftCodePoint.CompareTo(rightCodePoint);
                if (comparison != 0)
                    return comparison;

                leftIndex += char.IsHighSurrogate(left[leftIndex]) ? 2 : 1;
                rightIndex += char.IsHighSurrogate(right[rightIndex]) ? 2 : 1;
            }

            return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
        }
    }
}
