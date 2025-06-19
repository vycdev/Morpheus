using System.Text;

namespace Morpheus.Utilities;
public static partial class Extensions
{
    public static string GetAccurateTimeSpan(this DateTime start, DateTime end)
    {
        // Ensure that end is always later than start
        if (end < start)
        {
            throw new ArgumentException("End date must be greater than or equal to the start date.");
        }

        // Handle the years.
        int years = end.Year - start.Year;

        // See if we went too far.
        DateTime test_date = start.AddMonths(12 * years);
        if (test_date > end)
        {
            years--;
            test_date = start.AddMonths(12 * years);
        }

        // Add months until we go too far.
        int months = 0;
        while (test_date <= end)
        {
            months++;
            test_date = start.AddMonths(12 * years + months);
        }
        months--;

        // Subtract to see how many more days,
        // hours, minutes, etc. we need.
        start = start.AddMonths(12 * years + months);
        TimeSpan remainder = end - start;
        int days = remainder.Days;
        int hours = remainder.Hours;
        int minutes = remainder.Minutes;
        int seconds = remainder.Seconds;
        int milliseconds = remainder.Milliseconds;

        // Create the formatted string
        List<string> timeComponents = [];

        if (years > 0)
            timeComponents.Add($"{years} years");

        if (months > 0)
            timeComponents.Add($"{months} months");

        if (days > 0)
            timeComponents.Add($"{days} days");

        if (hours > 0)
            timeComponents.Add($"{hours} hours");

        if (minutes > 0)
            timeComponents.Add($"{minutes} minutes");

        if (seconds > 0)
            timeComponents.Add($"{seconds} seconds");

        if (milliseconds > 0)
            timeComponents.Add($"{milliseconds} milliseconds");

        return timeComponents.Count > 0 ? string.Join(", ", timeComponents) : "0 milliseconds";
    }

    public static string GetPercentageBar(this int value)
    {
        // Define the total length of the bar
        int totalLength = 30;

        // Calculate the number of filled cells
        int filledCells = (int)Math.Round((double)value / 100 * totalLength);

        // Create the percentage bar
        StringBuilder bar = new();
        bar.Append('█', filledCells);
        bar.Append('░', totalLength - filledCells);

        return bar.ToString();
    }
}
