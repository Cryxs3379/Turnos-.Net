using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Turnos.App.Models;

namespace Turnos.App;

public partial class GenerarTurnosWindow : Window
{
    private readonly DateOnly _inicioSemana;
    private readonly DateOnly _finSemana;

    public ObservableCollection<TurnoRow> TurnosRecepcion { get; }
    public ObservableCollection<TurnoRow> TurnosEntradas { get; }

    public GenerarTurnosWindow(
        DateOnly inicioSemana,
        DateOnly finSemana,
        ObservableCollection<TurnoRow> turnosRecepcion,
        ObservableCollection<TurnoRow> turnosEntradas,
        IReadOnlyList<string> warnings)
    {
        InitializeComponent();

        _inicioSemana = inicioSemana;
        _finSemana = finSemana;
        TurnosRecepcion = turnosRecepcion;
        TurnosEntradas = turnosEntradas;
        DataContext = this;

        ConfigureHeaders();
        ConfigureColumns(dgvRecepcion);
        ConfigureColumns(dgvEntradas);
        RenderWarnings(warnings);
    }

    private void ConfigureHeaders()
    {
        var cultura = CultureInfo.GetCultureInfo("es-ES");
        txtTitle.Text = $"Turnos semana {_inicioSemana:dd/MM} - {_finSemana:dd/MM}";
        txtSubtitle.Text = _inicioSemana.ToString("yyyy", cultura) == _finSemana.ToString("yyyy", cultura)
            ? _inicioSemana.ToString("yyyy", cultura)
            : $"{_inicioSemana:yyyy} / {_finSemana:yyyy}";
    }

    private void ConfigureColumns(DataGrid grid)
    {
        grid.Columns.Clear();

        var empleadoColumn = new DataGridTextColumn
        {
            Header = "Empleado",
            Binding = new System.Windows.Data.Binding("Empleado"),
            Width = new DataGridLength(200)
        };
        grid.Columns.Add(empleadoColumn);

        var cultura = CultureInfo.GetCultureInfo("es-ES");
        for (int d = 0; d < 7; d++)
        {
            var fecha = _inicioSemana.AddDays(d);
            var header = fecha.ToString("ddd dd/MM", cultura);

            var col = new DataGridTextColumn
            {
                Header = header,
                Binding = new System.Windows.Data.Binding($"D{d}"),
                ElementStyle = (Style)FindResource("ShiftCellTextStyle"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };
            grid.Columns.Add(col);
        }
    }

    private void RenderWarnings(IReadOnlyList<string> warnings)
    {
        if (warnings == null || warnings.Count == 0)
        {
            txtWarnings.Visibility = Visibility.Collapsed;
            return;
        }

        txtWarnings.Text = "FALTA PERSONAL: " + string.Join(" | ", warnings);
        txtWarnings.Visibility = Visibility.Visible;
    }

    private void BtnCerrar_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
