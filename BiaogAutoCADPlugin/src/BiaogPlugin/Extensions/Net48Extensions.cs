using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace BiaogPlugin.Extensions
{
    /// <summary>
    /// .NET Framework 4.8 兼容性扩展方法
    /// 提供 .NET 5/6+ 的方法支持
    /// </summary>
    internal static class Net48Extensions
    {
        /// <summary>
        /// 分块方法 - .NET 6+ Chunk() 的实现
        /// </summary>
        public static IEnumerable<T[]> Chunk<T>(this IEnumerable<T> source, int size)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (size < 1) throw new ArgumentOutOfRangeException(nameof(size));

            var chunk = new List<T>(size);
            foreach (var item in source)
            {
                chunk.Add(item);
                if (chunk.Count == size)
                {
                    yield return chunk.ToArray();
                    chunk.Clear();
                }
            }

            if (chunk.Count > 0)
            {
                yield return chunk.ToArray();
            }
        }

        /// <summary>
        /// TakeLast 方法 - .NET Core 2.0+ TakeLast() 的实现
        /// </summary>
        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            if (count == 0) return Enumerable.Empty<T>();

            var list = source as IList<T> ?? source.ToList();
            var startIndex = Math.Max(0, list.Count - count);
            return list.Skip(startIndex);
        }

        /// <summary>
        /// GetValueOrDefault 方法 - Dictionary 扩展
        /// </summary>
        public static TValue? GetValueOrDefault<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            TKey key,
            TValue? defaultValue = default)
        {
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));
            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Contains with StringComparison - .NET Core 2.1+
        /// </summary>
        public static bool Contains(this string source, string value, StringComparison comparisonType)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (value == null) throw new ArgumentNullException(nameof(value));

            return source.IndexOf(value, comparisonType) >= 0;
        }

        /// <summary>
        /// KeyValuePair Deconstruct - C# 7+ 解构支持
        /// </summary>
        public static void Deconstruct<TKey, TValue>(
            this KeyValuePair<TKey, TValue> pair,
            out TKey key,
            out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }

        /// <summary>
        /// Replace with StringComparison - .NET 5+
        /// </summary>
        public static string Replace(this string source, string oldValue, string newValue, StringComparison comparisonType)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (oldValue == null) throw new ArgumentNullException(nameof(oldValue));
            if (oldValue.Length == 0) throw new ArgumentException("oldValue cannot be empty", nameof(oldValue));

            // .NET Framework 4.8 不支持 StringComparison 的 Replace，使用正则表达式实现
            if (comparisonType == StringComparison.OrdinalIgnoreCase || comparisonType == StringComparison.InvariantCultureIgnoreCase)
            {
                return System.Text.RegularExpressions.Regex.Replace(
                    source,
                    System.Text.RegularExpressions.Regex.Escape(oldValue),
                    newValue,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
            }
            else
            {
                return source.Replace(oldValue, newValue);
            }
        }
    }
}
