namespace Scheduler.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;
using Model;

public static class CarExtensions {

    public static IEnumerable<(Car Car, int Position)> EnumerateConsist(this Car car) {
        var consist = car.EnumerateCoupled(Car.End.F)!.ToArray();
        var carIndex = Array.IndexOf(consist, car);
        return consist.Select((o, i) => (Car: o, position: i - carIndex));
    }

}