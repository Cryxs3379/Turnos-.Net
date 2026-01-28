using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Turnos.App.Models;
using Turnos.Data;

namespace Turnos.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly TurnosRepository _turnosRepository;
    private CancellationTokenSource? _cancellationTokenSource;

    // Colecciones para grids de 3 columnas
    public ObservableCollection<GridRow3> EntradasParking { get; } = new();
    public ObservableCollection<GridRow3> EntradasRentACar { get; } = new();
    public ObservableCollection<GridRow3> EntradasReservaParking { get; } = new();
    public ObservableCollection<GridRow3> EntradaReservaRentACar { get; } = new();
    public ObservableCollection<GridRow3> SalidasParking { get; } = new();
    public ObservableCollection<GridRow3> SalidaReservaParking { get; } = new();

    // Colección para grid de 4 columnas
    public ObservableCollection<GridRow4> SalidasRentACar { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _turnosRepository = App.TurnosRepository;
        
        // Inicializar controles con valores por defecto
        cmbLugar.Items.Add("AER");
        cmbLugar.SelectedItem = "AER";
        dpFechaInicio.SelectedDate = new DateTime(2026, 2, 2);
        dpFechaFin.SelectedDate = new DateTime(2026, 2, 8);
        txtEstado.Text = "Listo";
        
        // Cancelar carga al cerrar ventana
        Closing += MainWindow_Closing;
        
        _ = CargarTodosLosDatosAsync();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private async void BtnCargar_Click(object sender, RoutedEventArgs e)
    {
        // Si ya hay una carga en curso, cancelarla
        if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }

        // Crear nuevo CancellationTokenSource para esta carga
        _cancellationTokenSource = new CancellationTokenSource();
        await CargarTodosLosDatosAsync();
    }

    private async Task CargarTodosLosDatosAsync()
    {
        var ct = _cancellationTokenSource?.Token ?? CancellationToken.None;
        var hayError = false;
        var swTotal = Stopwatch.StartNew();

        try
        {
            Debug.WriteLine("=== Iniciando carga de datos ===");
            txtEstado.Text = "Cargando...";
            btnCargar.IsEnabled = false;

            var fechaInicio = dpFechaInicio.SelectedDate ?? new DateTime(2026, 2, 2);
            var fechaFin = dpFechaFin.SelectedDate ?? new DateTime(2026, 2, 8);
            var lugar = cmbLugar.SelectedItem?.ToString() ?? "AER";

            // Entradas Parking
            try
            {
                var sw = Stopwatch.StartNew();
                var datosEntradasParking = await _turnosRepository.GetEntradasParkingAsync(fechaInicio, fechaFin, lugar, ct);
                sw.Stop();
                Debug.WriteLine($"[Entradas Parking] Consulta completada en {sw.ElapsedMilliseconds}ms - {datosEntradasParking.Count} registros");
                
                EntradasParking.Clear();
                foreach (var fila in datosEntradasParking)
                {
                    EntradasParking.Add(new GridRow3 { Col1 = fila[0] ?? "", Col2 = fila[1] ?? "", Col3 = fila[2] ?? "" });
                }
                txtContadorEntradasParking.Text = $"Total: {EntradasParking.Count}";
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[Entradas Parking] Operación cancelada");
                throw;
            }
            catch (Exception ex)
            {
                hayError = true;
                Debug.WriteLine($"[Entradas Parking] Error: {ex.Message}");
                MessageBox.Show($"Error en pestaña 'Entradas Parking': {ex.Message}", "Error - Entradas Parking", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Entradas Rent a Car
            try
            {
                var sw = Stopwatch.StartNew();
                var datosEntradasRentACar = await _turnosRepository.GetEntradasRentACarAsync(fechaInicio, fechaFin, lugar, ct);
                sw.Stop();
                Debug.WriteLine($"[Entradas Rent a Car] Consulta completada en {sw.ElapsedMilliseconds}ms - {datosEntradasRentACar.Count} registros");
                
                EntradasRentACar.Clear();
                foreach (var fila in datosEntradasRentACar)
                {
                    EntradasRentACar.Add(new GridRow3 { Col1 = fila[0] ?? "", Col2 = fila[1] ?? "", Col3 = fila[2] ?? "" });
                }
                txtContadorEntradasRentACar.Text = $"Total: {EntradasRentACar.Count}";
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[Entradas Rent a Car] Operación cancelada");
                throw;
            }
            catch (Exception ex)
            {
                hayError = true;
                Debug.WriteLine($"[Entradas Rent a Car] Error: {ex.Message}");
                MessageBox.Show($"Error en pestaña 'Entradas Rent a Car': {ex.Message}", "Error - Entradas Rent a Car", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Entradas Reserva Parking
            try
            {
                var sw = Stopwatch.StartNew();
                var datosEntradasReservaParking = await _turnosRepository.GetEntradasReservaParkingAsync(fechaInicio, fechaFin, lugar, ct);
                sw.Stop();
                Debug.WriteLine($"[Entradas Reserva Parking] Consulta completada en {sw.ElapsedMilliseconds}ms - {datosEntradasReservaParking.Count} registros");
                
                EntradasReservaParking.Clear();
                foreach (var fila in datosEntradasReservaParking)
                {
                    EntradasReservaParking.Add(new GridRow3 { Col1 = fila[0] ?? "", Col2 = fila[1] ?? "", Col3 = fila[2] ?? "" });
                }
                txtContadorEntradasReservaParking.Text = $"Total: {EntradasReservaParking.Count}";
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[Entradas Reserva Parking] Operación cancelada");
                throw;
            }
            catch (Exception ex)
            {
                hayError = true;
                Debug.WriteLine($"[Entradas Reserva Parking] Error: {ex.Message}");
                MessageBox.Show($"Error en pestaña 'Entradas Reserva Parking': {ex.Message}", "Error - Entradas Reserva Parking", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Entrada Reserva Rent a Car
            try
            {
                var sw = Stopwatch.StartNew();
                var datosEntradaReservaRentACar = await _turnosRepository.GetEntradaReservaRentACarAsync(fechaInicio, fechaFin, lugar, ct);
                sw.Stop();
                Debug.WriteLine($"[Entrada Reserva Rent a Car] Consulta completada en {sw.ElapsedMilliseconds}ms - {datosEntradaReservaRentACar.Count} registros");
                
                EntradaReservaRentACar.Clear();
                foreach (var fila in datosEntradaReservaRentACar)
                {
                    EntradaReservaRentACar.Add(new GridRow3 { Col1 = fila[0] ?? "", Col2 = fila[1] ?? "", Col3 = fila[2] ?? "" });
                }
                txtContadorEntradaReservaRentACar.Text = $"Total: {EntradaReservaRentACar.Count}";
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[Entrada Reserva Rent a Car] Operación cancelada");
                throw;
            }
            catch (Exception ex)
            {
                hayError = true;
                Debug.WriteLine($"[Entrada Reserva Rent a Car] Error: {ex.Message}");
                MessageBox.Show($"Error en pestaña 'Entrada Reserva Rent a Car': {ex.Message}", "Error - Entrada Reserva Rent a Car", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Salidas Parking
            try
            {
                var sw = Stopwatch.StartNew();
                var datosSalidasParking = await _turnosRepository.GetSalidasParkingAsync(fechaInicio, fechaFin, lugar, ct);
                sw.Stop();
                Debug.WriteLine($"[Salidas Parking] Consulta completada en {sw.ElapsedMilliseconds}ms - {datosSalidasParking.Count} registros");
                
                SalidasParking.Clear();
                foreach (var fila in datosSalidasParking)
                {
                    SalidasParking.Add(new GridRow3 { Col1 = fila[0] ?? "", Col2 = fila[1] ?? "", Col3 = fila[2] ?? "" });
                }
                txtContadorSalidasParking.Text = $"Total: {SalidasParking.Count}";
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[Salidas Parking] Operación cancelada");
                throw;
            }
            catch (Exception ex)
            {
                hayError = true;
                Debug.WriteLine($"[Salidas Parking] Error: {ex.Message}");
                MessageBox.Show($"Error en pestaña 'Salidas Parking': {ex.Message}", "Error - Salidas Parking", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Salidas Rent a Car (4 columnas)
            try
            {
                var sw = Stopwatch.StartNew();
                var datosSalidasRentACar = await _turnosRepository.GetSalidasRentACarAsync(fechaInicio, fechaFin, lugar, ct);
                sw.Stop();
                Debug.WriteLine($"[Salidas Rent a Car] Consulta completada en {sw.ElapsedMilliseconds}ms - {datosSalidasRentACar.Count} registros");
                
                SalidasRentACar.Clear();
                foreach (var fila in datosSalidasRentACar)
                {
                    SalidasRentACar.Add(new GridRow4 
                    { 
                        Col1 = fila[0] ?? "", 
                        Col2 = fila[1] ?? "", 
                        Col3 = fila[2] ?? "",
                        Col4 = fila.Length > 3 ? (fila[3] ?? "") : ""
                    });
                }
                txtContadorSalidasRentACar.Text = $"Total: {SalidasRentACar.Count}";
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[Salidas Rent a Car] Operación cancelada");
                throw;
            }
            catch (Exception ex)
            {
                hayError = true;
                Debug.WriteLine($"[Salidas Rent a Car] Error: {ex.Message}");
                MessageBox.Show($"Error en pestaña 'Salidas Rent a Car': {ex.Message}", "Error - Salidas Rent a Car", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Salida Reserva Parking
            try
            {
                var sw = Stopwatch.StartNew();
                var datosSalidaReservaParking = await _turnosRepository.GetSalidaReservaParkingAsync(fechaInicio, fechaFin, lugar, ct);
                sw.Stop();
                Debug.WriteLine($"[Salida Reserva Parking] Consulta completada en {sw.ElapsedMilliseconds}ms - {datosSalidaReservaParking.Count} registros");
                
                SalidaReservaParking.Clear();
                foreach (var fila in datosSalidaReservaParking)
                {
                    SalidaReservaParking.Add(new GridRow3 { Col1 = fila[0] ?? "", Col2 = fila[1] ?? "", Col3 = fila[2] ?? "" });
                }
                txtContadorSalidaReservaParking.Text = $"Total: {SalidaReservaParking.Count}";
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[Salida Reserva Parking] Operación cancelada");
                throw;
            }
            catch (Exception ex)
            {
                hayError = true;
                Debug.WriteLine($"[Salida Reserva Parking] Error: {ex.Message}");
                MessageBox.Show($"Error en pestaña 'Salida Reserva Parking': {ex.Message}", "Error - Salida Reserva Parking", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            swTotal.Stop();
            Debug.WriteLine($"=== Carga completada en {swTotal.ElapsedMilliseconds}ms ===");
            txtEstado.Text = hayError ? "Error" : "Listo";
        }
        catch (OperationCanceledException)
        {
            swTotal.Stop();
            Debug.WriteLine($"=== Carga cancelada después de {swTotal.ElapsedMilliseconds}ms ===");
            txtEstado.Text = "Cancelado";
        }
        catch (Exception ex)
        {
            hayError = true;
            swTotal.Stop();
            Debug.WriteLine($"=== Error general después de {swTotal.ElapsedMilliseconds}ms: {ex.Message} ===");
            txtEstado.Text = "Error";
            MessageBox.Show($"Error general al cargar datos: {ex.Message}", "Error General", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btnCargar.IsEnabled = true;
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
