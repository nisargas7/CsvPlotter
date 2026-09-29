using System;
using System.Globalization;

namespace CsvPlotter.Helpers
{
    public static class TimeHelper
    {
        public static double TimeStringToDouble(string time)
        {
            if (string.IsNullOrWhiteSpace(time))
                return double.NaN;

            try
            {
                string[] tempStr = time.Split(':');

                if (tempStr.Length < 3)
                    return double.NaN;

                string hoursStr = tempStr[0];
                string minutesStr = tempStr[1];

                string[] secStr = tempStr[2].Split('.');

                if (secStr.Length < 3)
                    return double.NaN;

                double hours = Convert.ToDouble(
                    hoursStr,
                    CultureInfo.InvariantCulture);

                double minutes = Convert.ToDouble(
                    minutesStr,
                    CultureInfo.InvariantCulture);

                double seconds = Convert.ToDouble(
                    secStr[0],
                    CultureInfo.InvariantCulture);

                double milliseconds = Convert.ToDouble(
                    secStr[1] + "." + secStr[2],
                    CultureInfo.InvariantCulture);

                return (hours * 3600000)
                     + (minutes * 60000)
                     + (seconds * 1000)
                     + milliseconds;
            }
            catch
            {
                return double.NaN;
            }
        }
    }
}