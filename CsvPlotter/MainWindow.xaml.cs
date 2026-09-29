
using CsvPlotter.Models;
using CsvPlotter.Services;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.TickGenerators.TimeUnits;
using ScottPlot.WPF;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CsvPlotter
{
    public partial class MainWindow : Window
    {
        private CsvPlotData? csvData;

        private readonly List<int> _loadedColumnIndexes =
            new List<int>();

        private readonly HashSet<int> _visibleColumnIndexes =
            new HashSet<int>();

        private readonly List<ParameterInfo> _parameters =
            new List<ParameterInfo>();

        private readonly Dictionary<int, string> _csvColumnNames =
            new Dictionary<int, string>();

        private enum ChartType
        {
            Superimposed,
            Stacked
        }

        private ChartType _currentChartType =
            ChartType.Superimposed;

        private readonly List<WpfPlot> _stackedPlots =
            new List<WpfPlot>();

        private VerticalLine? verticalCursorLine;

        private readonly List<VerticalLine> _stackedCursorLines =
            new List<VerticalLine>();

        public static bool NormTime = true;

        private DateTime _lastMouseMoveTime =
            DateTime.MinValue;

        private const int MouseMoveIntervalMs = 30;

        private readonly ScottPlot.Color[] _plotColors =
        {
            ScottPlot.Colors.Blue,
            ScottPlot.Colors.Red,
            ScottPlot.Colors.Green,
            ScottPlot.Colors.Orange,
            ScottPlot.Colors.Purple,
            ScottPlot.Colors.Brown,
            ScottPlot.Colors.Magenta,
            ScottPlot.Colors.Cyan,
            ScottPlot.Colors.DarkBlue,
            ScottPlot.Colors.DarkRed
        };

        public MainWindow()
        {
            InitializeComponent();

            PlotControl.MouseMove += PlotControl_MouseMove;
            PlotControl.MouseLeave += PlotControl_MouseLeave;
        }

        private void BrowseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenFileDialog dialog =
                new OpenFileDialog
                {
                    Filter =
                        "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
                };

            if (dialog.ShowDialog() != true)
                return;

            FilePathTextBox.Text = dialog.FileName;

            try
            {
                var allLines = File.ReadLines(dialog.FileName);

                int lineCount = allLines.Count();

                MaxRowsTextBox.Text = lineCount.ToString();
                MinRowsTextBox.Text = "0";

                string firstLine =
                    allLines.FirstOrDefault();

                if (string.IsNullOrWhiteSpace(firstLine))
                {
                    MessageBox.Show(
                        "CSV file is empty.",
                        "CSV",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                string[] headers =
                    firstLine.Split(',');

                int columnCount =
                    headers.Length;

                if (columnCount <= 1)
                {
                    MessageBox.Show(
                        "No parameter columns found.",
                        "CSV",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                _csvColumnNames.Clear();

                for (int i = 0; i < headers.Length; i++)
                {
                    _csvColumnNames[i] =
                        headers[i].Trim().Trim('"');
                }

                _loadedColumnIndexes.Clear();

                for (int i = 0; i < columnCount; i++)
                {
                    _loadedColumnIndexes.Add(i);
                }

                _visibleColumnIndexes.Clear();
                _parameters.Clear();

                CreateParameterPanel();

                StatusText.Text =
                    $"CSV selected: {lineCount:N0} rows, " +
                    $"{columnCount - 1} parameters";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Browse Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void LoadButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(
                FilePathTextBox.Text))
            {
                MessageBox.Show(
                    "Please select a CSV file.");

                return;
            }

            if (_loadedColumnIndexes.Count == 0)
            {
                MessageBox.Show(
                    "Please select a CSV file first.");

                return;
            }

            int maxRows = 2000;

            if (!int.TryParse(
                MaxRowsTextBox.Text,
                out maxRows))
            {
                maxRows = 2000;
            }

            int minRows = 0;

            if (!int.TryParse(
                MinRowsTextBox.Text,
                out minRows))
            {
                minRows = 0;
            }

            int Resolution = 1000;

            if (!int.TryParse(
                ResolutionTextBox.Text,
                out Resolution))
            {
                Resolution = 1000;
            }

            if (Resolution <= 0)
                Resolution = 1000;

            StatusText.Text =
                "Loading CSV...";

            LoadButton.IsEnabled = false;
            BrowseButton.IsEnabled = false;

            try
            {
                string filePath =
                    FilePathTextBox.Text;

                int[] selectedColumns =
                    _loadedColumnIndexes.ToArray();

                Stopwatch stopwatch =
                    Stopwatch.StartNew();

                CsvPlotData data =
                    await Task.Run(() =>
                        CsvDataReader.Read(
                            filePath,
                            selectedColumns,
                            maxRows,
                            minRows,
                            Resolution));

                stopwatch.Stop();

                csvData = data;

                _visibleColumnIndexes.Clear();

                foreach (ParameterInfo parameter
                         in _parameters)
                {
                    if (parameter.CheckBox != null)
                        parameter.CheckBox.IsChecked = false;

                    if (parameter.FilterCheckBox != null)
                        parameter.FilterCheckBox.IsChecked = false;

                    if (parameter.ParameterRow != null)
                        parameter.ParameterRow.Visibility =
                            Visibility.Collapsed;

                    parameter.Plot = null;
                }

                ClearAllPlots();

                CursorTimeTextBox.Text = "--";
                CursorRecordTextBox.Text = "--";

                StatusText.Text =
                    $"Loaded {csvData.RowCount:N0} plot points " +
                    $"in {stopwatch.ElapsedMilliseconds:N0} ms";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error loading CSV:\n\n" +
                    ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText.Text =
                    "Loading failed.";
            }
            finally
            {
                LoadButton.IsEnabled = true;
                BrowseButton.IsEnabled = true;
            }
        }

        private void CreateParameterPanel()
        {
            ParameterPanel.Children.Clear();
            FilterStack.Children.Clear();
            _parameters.Clear();

            foreach (int columnIndex
                     in _loadedColumnIndexes)
            {
                if (columnIndex == 0)
                    continue;

                string parameterName =
                    GetColumnName(columnIndex);

                Grid row = new Grid();

                row.Margin =
                    new Thickness(0, 2, 0, 2);

                row.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width = GridLength.Auto
                    });

                row.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width = new GridLength(22)
                    });

                row.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width =
                            new GridLength(
                                1,
                                GridUnitType.Star)
                    });

                row.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width = new GridLength(100)
                    });

                CheckBox parameterCheckBox =
                    new CheckBox();

                parameterCheckBox.VerticalAlignment =
                    System.Windows.VerticalAlignment.Center;

                parameterCheckBox.IsChecked =
                    false;

                Grid.SetColumn(
                    parameterCheckBox,
                    0);

                Border colorBorder =
                    new Border();

                colorBorder.Width = 14;
                colorBorder.Height = 14;

                colorBorder.Margin =
                    new Thickness(3, 0, 5, 0);

                colorBorder.CornerRadius =
                    new CornerRadius(2);

                colorBorder.Background =
                    new SolidColorBrush(
                        ConvertColor(
                            GetPlotColor(
                                _parameters.Count)));

                Grid.SetColumn(
                    colorBorder,
                    1);

                TextBlock nameText =
                    new TextBlock();

                nameText.Text =
                    parameterName;

                nameText.VerticalAlignment =
                    System.Windows.VerticalAlignment.Center;

                nameText.TextTrimming =
                    TextTrimming.CharacterEllipsis;

                nameText.ToolTip =
                    parameterName;

                Grid.SetColumn(
                    nameText,
                    2);

                TextBox valueTextBox =
                    new TextBox();

                valueTextBox.Text = "--";
                valueTextBox.Height = 26;
                valueTextBox.IsReadOnly = true;

                valueTextBox.VerticalContentAlignment =
                    System.Windows.VerticalAlignment.Center;

                valueTextBox.Margin =
                    new Thickness(5, 0, 0, 0);

                Grid.SetColumn(
                    valueTextBox,
                    3);

                row.Children.Add(
                    parameterCheckBox);

                row.Children.Add(
                    colorBorder);

                row.Children.Add(
                    nameText);

                row.Children.Add(
                    valueTextBox);

                row.Visibility =
                    Visibility.Collapsed;

                ParameterInfo parameter =
                    new ParameterInfo
                    {
                        ColumnIndex =
                            columnIndex,

                        Name =
                            parameterName,

                        CheckBox =
                            parameterCheckBox,

                        ValueTextBox =
                            valueTextBox,

                        UiColor =
                            new SolidColorBrush(
                                ConvertColor(
                                    GetPlotColor(
                                        _parameters.Count))),

                        ParameterRow =
                            row,

                        Plot = null
                    };

                parameterCheckBox.Checked +=
                    ParameterCheckBox_Changed;

                parameterCheckBox.Unchecked +=
                    ParameterCheckBox_Changed;

                _parameters.Add(parameter);

                ParameterPanel.Children.Add(row);

                CheckBox filterCheckBox =
                    new CheckBox();

                filterCheckBox.Content =
                    parameterName;

                filterCheckBox.Margin =
                    new Thickness(2, 3, 2, 3);

                filterCheckBox.IsChecked =
                    false;

                parameter.FilterCheckBox =
                    filterCheckBox;

                filterCheckBox.Checked +=
                    FilterParameterCheckBox_Changed;

                filterCheckBox.Unchecked +=
                    FilterParameterCheckBox_Changed;

                FilterStack.Children.Add(
                    filterCheckBox);
            }
        }

        private string GetColumnName(
            int columnIndex)
        {
            if (_csvColumnNames.ContainsKey(
                columnIndex))
            {
                return _csvColumnNames[
                    columnIndex];
            }

            if (csvData != null &&
                csvData.ColumnNames.ContainsKey(
                    columnIndex))
            {
                return csvData.ColumnNames[
                    columnIndex];
            }

            return $"Parameter {columnIndex}";
        }

        private void Filter_Click(
            object sender,
            RoutedEventArgs e)
        {
            FilterPopUp.IsOpen =
                !FilterPopUp.IsOpen;

            if (FilterPopUp.IsOpen)
            {
                FilterSearchTextBox.Focus();
            }
        }

        private void FilterSearchTextBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            UpdateFilterList();
        }

        private void UpdateFilterList()
        {
            if (FilterStack == null)
                return;

            string searchText =
                FilterSearchTextBox.Text
                .Trim()
                .ToLower();

            FilterStack.Children.Clear();

            foreach (ParameterInfo parameter
                     in _parameters)
            {
                if (parameter.FilterCheckBox == null)
                    continue;

                bool matches =
                    string.IsNullOrEmpty(searchText) ||
                    parameter.Name
                    .ToLower()
                    .Contains(searchText);

                if (matches)
                {
                    FilterStack.Children.Add(
                        parameter.FilterCheckBox);
                }
            }
        }

        private void FilterParameterCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not CheckBox filterCheckBox)
                return;

            ParameterInfo? parameter =
                _parameters.FirstOrDefault(
                    p =>
                        p.FilterCheckBox ==
                        filterCheckBox);

            if (parameter == null)
                return;

            if (filterCheckBox.IsChecked == true)
            {
                if (parameter.ParameterRow != null)
                {
                    parameter.ParameterRow.Visibility =
                        Visibility.Visible;
                }

                if (parameter.CheckBox != null)
                {
                    parameter.CheckBox.IsChecked =
                        true;
                }
            }
            else
            {
                if (parameter.ParameterRow != null)
                {
                    parameter.ParameterRow.Visibility =
                        Visibility.Collapsed;
                }

                if (parameter.CheckBox != null)
                {
                    parameter.CheckBox.IsChecked =
                        false;
                }
            }
        }

        private void ParameterCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox)
                return;

            ParameterInfo? parameter =
                _parameters.FirstOrDefault(
                    p =>
                        p.CheckBox ==
                        checkBox);

            if (parameter == null)
                return;

            if (checkBox.IsChecked == true)
            {
                _visibleColumnIndexes.Add(
                    parameter.ColumnIndex);
            }
            else
            {
                _visibleColumnIndexes.Remove(
                    parameter.ColumnIndex);
            }

            CreatePlots();
        }

        private void ChartTypeComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (ChartTypeComboBox == null)
                return;

            if (ChartTypeComboBox.SelectedIndex == 0)
            {
                _currentChartType =
                    ChartType.Superimposed;
            }
            else
            {
                _currentChartType =
                    ChartType.Stacked;
            }

            CreatePlots();
        }

        private void CreatePlots()
        {
            if (csvData == null)
            {
                ClearAllPlots();
                return;
            }

            ClearAllPlots();

            if (_currentChartType ==
                ChartType.Superimposed)
            {
                PlotControl.Visibility =
                    Visibility.Visible;

                StackedPlotScrollViewer.Visibility =
                    Visibility.Collapsed;

                CreateSuperimposedPlot();
            }
            else
            {
                PlotControl.Visibility =
                    Visibility.Collapsed;

                StackedPlotScrollViewer.Visibility =
                    Visibility.Visible;

                CreateStackedPlots();
            }
        }

        private void ClearAllPlots()
        {
            if (PlotControl != null)
            {
                PlotControl.Plot.Clear();

                verticalCursorLine = null;

                PlotControl.Refresh();
            }

            if (StackedPlotPanel != null)
            {
                StackedPlotPanel.Children.Clear();
            }

            _stackedPlots.Clear();

            _stackedCursorLines.Clear();

            foreach (ParameterInfo parameter
                     in _parameters)
            {
                parameter.Plot = null;
            }
        }

        private void CreateSuperimposedPlot()
        {
            if (csvData == null)
                return;

            PlotControl.Plot.Clear();

            verticalCursorLine =
                PlotControl.Plot.Add.VerticalLine(0);

            verticalCursorLine.LineWidth = 1;

            verticalCursorLine.Color =
                ScottPlot.Colors.DarkRed;

            foreach (ParameterInfo parameter in _parameters)
            {
                parameter.Plot = null;

                if (!_visibleColumnIndexes.Contains(
                    parameter.ColumnIndex))
                {
                    continue;
                }

                double[]? values =
                    GetParameterValues(
                        parameter.ColumnIndex);

                if (values == null ||
                    values.Length == 0)
                {
                    continue;
                }

                var scatter =
                    PlotControl.Plot.Add.Scatter(
                        csvData.TimeMilliseconds,
                        values);

                // Keep the parameter's original color
                // regardless of selection order.
                int parameterIndex =
                    _parameters.IndexOf(parameter);

                scatter.Color =
                    GetPlotColor(parameterIndex);

                scatter.LineWidth = 1;

                parameter.Plot =
                    scatter;
            }

            PlotControl.Plot.Axes.AutoScale();

            PlotControl.Refresh();
        }

        private void CreateStackedPlots()
        {
            if (csvData == null)
                return;

            StackedPlotPanel.Children.Clear();

            _stackedPlots.Clear();

            _stackedCursorLines.Clear();

            foreach (ParameterInfo parameter in _parameters)
            {
                parameter.Plot = null;

                if (!_visibleColumnIndexes.Contains(
                    parameter.ColumnIndex))
                {
                    continue;
                }

                double[]? values =
                    GetParameterValues(
                        parameter.ColumnIndex);

                if (values == null ||
                    values.Length == 0)
                {
                    continue;
                }

                Border border =
                    new Border();

                border.BorderBrush =
                    Brushes.LightGray;

                border.BorderThickness =
                    new Thickness(1);

                border.Margin =
                    new Thickness(0, 0, 0, 5);

                Grid grid =
                    new Grid();

                grid.RowDefinitions.Add(
                    new RowDefinition
                    {
                        Height =
                            GridLength.Auto
                    });

                grid.RowDefinitions.Add(
                    new RowDefinition
                    {
                        Height =
                            new GridLength(220)
                    });

                TextBlock title =
                    new TextBlock();

                title.Text =
                    parameter.Name;

                title.FontWeight =
                    FontWeights.Bold;

                title.Margin =
                    new Thickness(5, 3, 5, 3);

                Grid.SetRow(title, 0);

                grid.Children.Add(title);

                WpfPlot plot =
                    new WpfPlot();

                Grid.SetRow(plot, 1);

                grid.Children.Add(plot);

                border.Child =
                    grid;

                StackedPlotPanel.Children.Add(
                    border);

                _stackedPlots.Add(plot);

                var scatter =
                    plot.Plot.Add.Scatter(
                        csvData.TimeMilliseconds,
                        values);

                // Use the parameter's permanent color.
                int parameterIndex =
                    _parameters.IndexOf(parameter);

                scatter.Color =
                    GetPlotColor(parameterIndex);

                scatter.LineWidth = 1;

                VerticalLine cursorLine =
                    plot.Plot.Add.VerticalLine(0);

                cursorLine.LineWidth = 1;

                cursorLine.Color =
                    ScottPlot.Colors.DarkRed;

                _stackedCursorLines.Add(
                    cursorLine);

                plot.MouseMove +=
                    StackedPlot_MouseMove;

                plot.MouseLeave +=
                    StackedPlot_MouseLeave;

                plot.Plot.Axes.AutoScale();

                plot.Refresh();

                parameter.Plot =
                    scatter;
            }
        }

        private ScottPlot.Color GetPlotColor(
            int index)
        {
            return _plotColors[
                index % _plotColors.Length];
        }

        private System.Windows.Media.Color ConvertColor(
            ScottPlot.Color color)
        {
            return System.Windows.Media.Color.FromArgb(
                color.A,
                color.R,
                color.G,
                color.B);
        }

private void PlotControl_MouseMove(
    object sender,
    MouseEventArgs e)
        {
            if (_currentChartType !=
                ChartType.Superimposed)
            {
                return;
            }

            if (csvData == null)
                return;

            if (csvData.TimeMilliseconds.Length == 0)
                return;

            DateTime now =
                DateTime.Now;

            if ((now - _lastMouseMoveTime)
                .TotalMilliseconds <
                MouseMoveIntervalMs)
            {
                return;
            }

            _lastMouseMoveTime =
                now;

            Point position =
                e.GetPosition(PlotControl);

            try
            {
                double scaledX =
                    position.X * PlotControl.DisplayScale;

                double scaledY =
                    position.Y * PlotControl.DisplayScale;

                var coordinates =
                    PlotControl.Plot.GetCoordinates(
                        new Pixel(
                            scaledX,
                            scaledY));

                double mouseTime =
                    coordinates.X;

                int index =
                    FindNearestTimeIndex(
                        csvData.TimeMilliseconds,
                        mouseTime);

                if (index < 0 ||
                    index >=
                    csvData.TimeMilliseconds.Length)
                {
                    return;
                }

                if (verticalCursorLine != null)
                {
                    verticalCursorLine.X =
                        mouseTime;
                }

                UpdateCursorInformation(index);

                PlotControl.Refresh();
            }
            catch
            {
            }
        }



private void StackedPlot_MouseMove(
    object sender,
    MouseEventArgs e)
        {
            if (_currentChartType !=
                ChartType.Stacked)
            {
                return;
            }

            if (csvData == null)
                return;

            if (csvData.TimeMilliseconds.Length == 0)
                return;

            if (sender is not WpfPlot currentPlot)
                return;

            DateTime now =
                DateTime.Now;

            if ((now - _lastMouseMoveTime)
                .TotalMilliseconds <
                MouseMoveIntervalMs)
            {
                return;
            }

            _lastMouseMoveTime =
                now;

            Point position =
                e.GetPosition(currentPlot);

            try
            {
              
                double scaledX =
                    position.X * currentPlot.DisplayScale;

                double scaledY =
                    position.Y * currentPlot.DisplayScale;

                var coordinates =
                    currentPlot.Plot.GetCoordinates(
                        new Pixel(
                            scaledX,
                            scaledY));

                double mouseTime =
                    coordinates.X;

                int index =
                    FindNearestTimeIndex(
                        csvData.TimeMilliseconds,
                        mouseTime);

                if (index < 0 ||
                    index >=
                    csvData.TimeMilliseconds.Length)
                {
                    return;
                }

          
                foreach (VerticalLine cursorLine
                         in _stackedCursorLines)
                {
                    cursorLine.X =
                        mouseTime;
                }

                UpdateCursorInformation(index);

                foreach (WpfPlot plot
                         in _stackedPlots)
                {
                    plot.Refresh();
                }
            }
            catch
            {
            }
        }



        private void StackedPlot_MouseLeave(
            object sender,
            MouseEventArgs e)
        {
        }

        private void UpdateCursorInformation(
            int index)
        {
            if (csvData == null)
                return;

            if (index < 0 ||
                index >=
                csvData.TimeMilliseconds.Length)
            {
                return;
            }

            double actualTime =
                csvData.TimeMilliseconds[index];

            if (index <
                csvData.OriginalTimeStrings.Length)
            {
                CursorTimeTextBox.Text =
                    csvData.OriginalTimeStrings[index];
            }
            else
            {
                CursorTimeTextBox.Text =
                    actualTime.ToString("0.###");
            }

            if (index <
                csvData.CurrentRow.Length)
            {
                CursorRecordTextBox.Text =
                    csvData.CurrentRow[index];
            }
            else
            {
                CursorRecordTextBox.Text =
                    index.ToString();
            }

            foreach (ParameterInfo parameter
                     in _parameters)
            {
                if (parameter.ValueTextBox == null)
                    continue;

                if (!csvData.ParameterValues.ContainsKey(
                    parameter.ColumnIndex))
                {
                    parameter.ValueTextBox.Text =
                        "--";

                    continue;
                }

                double[]? values =
                    GetParameterValues(
                        parameter.ColumnIndex);

                if (values == null)
                {
                    parameter.ValueTextBox.Text =
                        "--";

                    continue;
                }

                if (index < values.Length)
                {
                    double value =
                        values[index];

                    if (double.IsNaN(value))
                    {
                        parameter.ValueTextBox.Text =
                            "NaN";
                    }
                    else
                    {
                        parameter.ValueTextBox.Text =
                            value.ToString("0.#####");
                    }
                }
                else
                {
                    parameter.ValueTextBox.Text =
                        "--";
                }
            }
        }

        private void PlotControl_MouseLeave(
            object sender,
            MouseEventArgs e)
        {
            if (verticalCursorLine != null)
            {
                verticalCursorLine.IsVisible =
                    true;

                PlotControl.Refresh();
            }
        }

        private int FindNearestTimeIndex(
            double[] values,
            double target)
        {
            if (values == null ||
                values.Length == 0)
            {
                return -1;
            }

            if (target <= values[0])
                return 0;

            if (target >=
                values[values.Length - 1])
            {
                return values.Length - 1;
            }

            int low = 0;
            int high =
                values.Length - 1;

            while (low <= high)
            {
                int mid =
                    low +
                    (high - low) / 2;

                double value =
                    values[mid];

                if (value == target)
                    return mid;

                if (value < target)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            if (low >= values.Length)
                return values.Length - 1;

            if (high < 0)
                return 0;

            double differenceLow =
                Math.Abs(
                    values[low] -
                    target);

            double differenceHigh =
                Math.Abs(
                    values[high] -
                    target);

            if (differenceLow <
                differenceHigh)
            {
                return low;
            }

            return high;
        }

        private void StandardTime_Checked(
            object sender,
            RoutedEventArgs e)
        {
            NormTime = false;

            if (csvData != null)
                CreatePlots();
        }

        private void NormalisedTime_Checked(
            object sender,
            RoutedEventArgs e)
        {
            NormTime = true;

            if (csvData != null)
                CreatePlots();
        }

        private double[]? GetParameterValues(
            int columnIndex)
        {
            if (csvData == null)
                return null;

            if (NormTime)
            {
                // Normalised values
                if (csvData.ParameterValues.ContainsKey(
                    columnIndex))
                {
                    return csvData.ParameterValues[
                        columnIndex];
                }
            }
            else
            {
                // Standard/original values
                if (csvData.ParameterValuesDisplay.ContainsKey(
                    columnIndex))
                {
                    return csvData.ParameterValuesDisplay[
                        columnIndex];
                }
            }
            return null;
        }
    }
}
