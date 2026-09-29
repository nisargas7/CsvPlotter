using ScottPlot.Plottables;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CsvPlotter.Models
{
    public class ParameterInfo
    {
        public int ColumnIndex { get; set; }

        public string Name { get; set; } = string.Empty;

        public CheckBox? CheckBox { get; set; }

        public TextBox? ValueTextBox { get; set; }

        public Brush? UiColor { get; set; }

        public Scatter? Plot { get; set; }
        public Grid? ParameterRow { get; set; }

        public CheckBox? FilterCheckBox { get; set; }
    }
}