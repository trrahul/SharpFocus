using System;
using System.Linq;

namespace SharpFocusTest
{
    public static class FocusModeRelationSamples
    {
        /// <summary>
        /// Focus target: <c>decorated</c>
        /// Expected backward sources: <c>raw</c>, <c>trimmed</c>
        /// Expected forward transforms: <c>message</c>, <c>LogMessage(decorated)</c>
        /// Expected forward sinks: <c>Console.WriteLine(message)</c>, <c>return decorated</c>
        /// Statements:
        /// - <c>var trimmed = raw.Trim();</c>
        /// - <c>var decorated = Format(trimmed);</c>
        /// - <c>var message = $"Decorated: {decorated}";</c>
        /// - <c>LogMessage(decorated);</c>
        /// - <c>Console.WriteLine(message);</c>
        /// - <c>return decorated;</c>
        /// </summary>
        public static string MessagePipeline(string raw)
        {
            var trimmed = raw.Trim();
            var decorated = Format(trimmed);
            var message = $"Decorated: {decorated}";
            LogMessage(decorated);
            Console.WriteLine(message);
            return decorated;
        }

        /// <summary>
        /// Focus target: <c>total</c>
        /// Expected backward sources: <c>numbers</c>, <c>filtered</c>
        /// Expected forward transforms: <c>average</c>
        /// Expected forward sinks: <c>Console.WriteLine(average)</c>, <c>return total</c>
        /// Statements:
        /// - <c>var filtered = numbers.Where(n => n &gt; threshold);</c>
        /// - <c>var total = filtered.Sum();</c>
        /// - <c>var average = total / filtered.Count();</c>
        /// - <c>Console.WriteLine(average);</c>
        /// - <c>return total;</c>
        /// </summary>
        public static int AggregateAboveThreshold(int[] numbers, int threshold)
        {
            var filtered = numbers.Where(n => n > threshold).ToArray();
            var total = filtered.Sum();
            var average = filtered.Length == 0 ? 0 : total / filtered.Length;
            Console.WriteLine(average);
            return total;
        }

        private static string Format(string value)
        {
            return value.ToUpperInvariant();
        }

        private static void LogMessage(string value)
        {
            Console.WriteLine($"[LOG] {value}");
        }
    }
}
