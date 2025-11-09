using System;

namespace SharpFocusTest
{
    public static class MiscSamples
    {
        public static int ComputeSum(ReadOnlySpan<int> values)
        {
            int total = 0;

            for (int i = 0; i < values.Length; i++)
            {
                total += values[i];
            }

            return total;
        }

        public static bool PatternMatchingFlow(object item)
        {
            return item switch
            {
                null => false,
                int number when number > 10 => true,
                string text when text.Length > 3 => true,
                _ => false
            };
        }

        public static IDisposable CreateDisposable()
        {
            return new SampleDisposable();
        }

        private sealed class SampleDisposable : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }
        }
    }
}
