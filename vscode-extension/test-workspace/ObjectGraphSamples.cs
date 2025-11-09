using System;
using System.Collections.Generic;

namespace SharpFocusTest
{
    public class ObjectGraphSamples
    {
        public void AssignEngineHorsepower(Car car, int horsepower)
        {
            if (car.Engine == null)
            {
                car.Engine = new Engine();
            }

            // Assign through nested projection to exercise field tracking
            car.Engine.Horsepower = horsepower;
        }

        public string BuildOwnerGreeting(Car car)
        {
            string ownerName = car.Owner?.Name ?? "Driver";
            string greeting = "Welcome, " + ownerName;
            return greeting;
        }

        public int SumWheelPressures(Car car)
        {
            int sum = 0;
            foreach (var wheel in car.Wheels)
            {
                sum += wheel.PressurePsi;
            }

            return sum;
        }
    }

    public class Car
    {
        public Engine? Engine { get; set; }
        public Person? Owner { get; set; }
        public List<Wheel> Wheels { get; } = new();
    }

    public class Engine
    {
        public int Horsepower { get; set; }
    }

    public class Wheel
    {
        public int PressurePsi { get; set; }
    }

    public class Person
    {
        public string Name { get; set; } = string.Empty;
    }
}
