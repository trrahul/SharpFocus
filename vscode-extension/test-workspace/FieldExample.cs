using System;

namespace TestApp
{
    public class FieldExample
    {
        private int _counter;

        public void Initialize()
        {
            _counter = 0;
        }

        public void Increment()
        {
            _counter++;
        }

        public void AddValue(int value)
        {
            _counter += value;
        }

        public int GetCount()
        {
            return _counter;
        }

        public void Reset()
        {
            _counter = 0;
        }

        public void Display()
        {
            Console.WriteLine($"Counter: {_counter}");
        }
    }
}
