using CsvHelper;
using CsvPlotter.Helpers;
using CsvPlotter.Models;
using System.Globalization;
using System.IO;

namespace CsvPlotter.Services
{
    public static class CsvDataReader
    {
        public static CsvPlotData Read(string filePath, int[] selectedColumnIndexes, int maxRows, int minRows = 0, int Resolution = 10000)
        {
            int avgFactor_1 = (Resolution < (maxRows - minRows)) ? (((maxRows - minRows) / Resolution)) : 1;
            int avgFactor_2 = (((maxRows - minRows) / Resolution) + 1);
            int avgRatio = (((maxRows - minRows) % Resolution));

            var result = new CsvPlotData();
            var timeList = new List<double>();
            var currRowList = new List<string>();
            var originalTimeList = new List<string>();
            var parameterLists = new Dictionary<int, List<double>>();

            foreach (int columnIndex in selectedColumnIndexes)
            {
                if (columnIndex == 0)
                    continue;

                parameterLists[columnIndex] = new List<double>();
            }

            using (var reader = new StreamReader(filePath))

            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                if (!csv.Read())
                    return result;

                csv.ReadHeader();

                string[] headers = csv.HeaderRecord ?? Array.Empty<string>();

                foreach (int columnIndex in selectedColumnIndexes)
                {
                    if (columnIndex >= 0 && columnIndex < headers.Length)
                    {
                        result.ColumnNames[columnIndex] = headers[columnIndex];
                    }
                }


                int rowCount = 0;
                int valuesIdx = 0;
                int avgRecordsCount = 0;
                int avgFactor = 0;
                double[,] values_1 = new double[selectedColumnIndexes.Length, avgFactor_1];
                double[,] values_2 = new double[selectedColumnIndexes.Length, avgFactor_2];

                while (csv.Read())
                {
                    if ((maxRows - minRows) > 0 && rowCount >= maxRows)
                    {
                        break;
                    }
                    rowCount++;
                    if (rowCount < minRows)
                    {
                        continue;
                    }
                    if (avgRecordsCount < avgRatio)
                    {
                        avgFactor = avgFactor_2;
                    }
                    else
                    {
                        avgFactor = avgFactor_1;
                    }
                    if (valuesIdx < avgFactor)
                    {
                        if (valuesIdx == (avgFactor - 1))
                        {
                            string timeString = GetFieldSafely(csv, 0);
                            double time = TimeHelper.TimeStringToDouble(timeString);
                            if (double.IsNaN(time))
                                continue;

                            timeList.Add(time);
                            currRowList.Add((rowCount - 1).ToString());
                            originalTimeList.Add(timeString);
                        }
                        int idx = 0;
                        foreach (int columnIndex in selectedColumnIndexes)
                        {
                            if (columnIndex == 0)
                                continue;

                            string valueString = GetFieldSafely(csv, columnIndex);
                            double value = ParseValue(valueString);
                            if (valuesIdx < avgFactor - 1)
                            {
                                if (avgRecordsCount < avgRatio)
                                {
                                    values_2[idx, valuesIdx] = value;
                                }
                                else
                                {
                                    values_1[idx, valuesIdx] = value;
                                }
                            }
                            else
                            {
                                if (avgRecordsCount < avgRatio)
                                {
                                    values_2[idx, valuesIdx] = value;
                                    parameterLists[columnIndex].Add(Enumerable.Range(0, values_2.GetLength(1)).Select(col => values_2[idx, col]).Average());
                                }
                                else
                                {
                                    values_1[idx, valuesIdx] = value;
                                    parameterLists[columnIndex].Add(Enumerable.Range(0, values_1.GetLength(1)).Select(col => values_1[idx, col]).Average());
                                }
                                if (idx >= selectedColumnIndexes.Length - 1)
                                {
                                    avgRecordsCount++;
                                }
                            }
                            idx++;
                        }
                    }
                    valuesIdx++;
                    if (valuesIdx == avgFactor)
                    {
                        valuesIdx = 0;
                    }
                }
            }


            result.TimeMilliseconds = timeList.ToArray();
            result.CurrentRow = currRowList.ToArray();
            result.OriginalTimeStrings = originalTimeList.ToArray();

            foreach (var item in parameterLists)
            {

                var data = item.Value.ToArray();

                if (data.Length > 0)

                {

                    if (MainWindow.NormTime)

                    {

                        var notNullArray = data.Where(x => !double.IsNaN(x));

                        if (notNullArray.Count() > 0)

                        {

                            double min = notNullArray.Min();

                            double max = notNullArray.Max();

                            if (min == max)

                            {
                                result.ParameterValues[item.Key] = data.Select(x =>

                                -100.0).ToArray();
                                result.ParameterValuesDisplay[item.Key] = data;
                            }

                            else

                            {


                                result.ParameterValues[item.Key] = data.Select(x =>

                                ((x - min) * 1000.0 / (max - min))).ToArray();

                                result.ParameterValuesDisplay[item.Key] = data;

                            }
                        }

                        else
                        {
                            result.ParameterValues[item.Key] = data;

                            result.ParameterValuesDisplay[item.Key] = data;
                        }

                    }



                    else

                    {
                        result.ParameterValues[item.Key] = data;

                        result.ParameterValuesDisplay[item.Key] = data;




                    }

                }

            }

            result.RowCount = timeList.Count;

            return result;

        }



        private static string GetFieldSafely(CsvReader csv, int columnIndex)
        {
            try
            {
                string? value = csv.GetField(columnIndex);

                return value ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }


        private static double ParseValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return double.NaN;


            value = value.Trim();


            if (value.Equals("--", StringComparison.OrdinalIgnoreCase))
            {
                return double.NaN;
            }


            if (value.Equals("invalid", StringComparison.OrdinalIgnoreCase))
            {
                return double.NaN;
            }


            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            {
                return result;
            }


            return double.NaN;
        }
    }
}