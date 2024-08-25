namespace Scheduler.Extensions;

using System;

public static class Extensions
{
    public static string AddOrdinalSuffix(this int number) {
        return number % 100 is >= 11 and <= 13
            ? number + "th"
            : (number % 10) switch {
                1 => number + "st",
                2 => number + "nd",
                3 => number + "rd",
                _ => number + "th"
            };
    }

    public static string GetRelativePosition(this int carIndex) {
        return $"{Math.Abs(carIndex).AddOrdinalSuffix()} car {(carIndex > 0 ? "behind" : "in front of")} locomotive";
    }

}
