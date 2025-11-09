using System;

namespace SharpFocusTest
{
    public class AliasAndReferenceSamples
    {
        public int ReferenceAliasPropagation()
        {
            var source = new Counter { Value = 10 };
            var alias = source;

            alias.Value += 5;

            return source.Value;
        }

        public int RefArgumentUpdate()
        {
            int original = 3;
            UpdateValue(ref original, 7);
            return original;
        }

        private static void UpdateValue(ref int target, int source)
        {
            target = source + 1;
        }

        public int ArrayAliasSum()
        {
            int[] numbers = { 1, 2, 3 };
            int[] alias = numbers;

            alias[1] = 10;

            int total = 0;
            foreach (var number in numbers)
            {
                total += number;
            }

            return total;
        }

        private sealed class Counter
        {
            public int Value;
        }
    }
}
