using System;

namespace SharpFocusTest
{
    public class TestClass
    {
        public void SimpleMethod()
        {
            // Simple dataflow test
            int x = 5;
            int y = x + 10;
            int z = y * 2;
            Console.WriteLine(z);
        }

        public void ConditionalFlow(bool condition)
        {
            int a = 10;
            int b;

            if (condition)
            {
                b = a + 5;
            }
            else
            {
                b = a - 5;
            }

            int result = b * 2;
            Console.WriteLine(result);
        }

        public void FieldAccess()
        {
            var obj = new Person();
            obj.Name = "Test";
            string greeting = "Hello, " + obj.Name;
            Console.WriteLine(greeting);
        }
    }

    public class Person
    {
        public string Name { get; set; }
    }
}
