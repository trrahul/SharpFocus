using System;

namespace SharpFocusTest
{
    public class LocalAndControlFlowSamples
    {
        public int LoopAggregation(int[] values)
        {
            int total = 0;

            for (int i = 0; i < values.Length; i++)
            {
                int current = values[i];
                total += current;
            }

            return total;
        }

        public int SwitchBasedCalculation(int input)
        {
            int result;

            switch (input)
            {
                case < 0:
                    result = -input;
                    break;
                case 0:
                    result = 0;
                    break;
                default:
                    result = input * 2;
                    break;
            }

            return result;
        }

        public bool NestedConditionals(int a, int b)
        {
            bool flag = false;

            if (a > 0)
            {
                if (b > a)
                {
                    flag = true;
                }
                else if (b == a)
                {
                    flag = a % 2 == 0;
                }
            }

            return flag;
        }
    }
}
