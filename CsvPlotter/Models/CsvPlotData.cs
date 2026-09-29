using System.Collections.Generic;

namespace CsvPlotter.Models
{
    public class CsvPlotData
    {
        public double[] TimeMilliseconds { get; set; } = [];
        public string[] CurrentRow { get; set; } = [];

        public string[] OriginalTimeStrings { get; set; } = [];

        public Dictionary<int, double[]> ParameterValues { get; set; }
            = new Dictionary<int, double[]>();
        public Dictionary<int, double[]> ParameterValuesDisplay{ get; set; }
            = new Dictionary<int, double[]>();

        public Dictionary<int, string> ColumnNames { get; set; }
            = new Dictionary<int, string>();

        public int RowCount { get; set; }
    }
}