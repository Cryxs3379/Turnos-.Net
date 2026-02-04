using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.Configuration;
using Turnos.App.Models;
using Turnos.App.Services;
using Turnos.Data;
namespace Turnos.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly TurnosRepository _turnosRepository;
    private readonly VacationExcelReader _excelReader;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isWorkerMode = false;

    // Colecciones para grids de 3 columnas
    public ObservableCollection<GridRow3> EntradasParking { get; } = new();
    public ObservableCollection<GridRow3> EntradasRentACar { get; } = new();
    public ObservableCollection<GridRow3> EntradasReservaParking { get; } = new();
    public ObservableCollection<GridRow3> EntradaReservaRentACar { get; } = new();
    public ObservableCollection<GridRow3> SalidasParking { get; } = new();
    public ObservableCollection<GridRow3> SalidaReservaParking { get; } = new();

    // Colección para grid de 4 columnas
    public ObservableCollection<GridRow4> SalidasRentACar { get; } = new();

    // Colecciones para empleados
    public ObservableCollection<EmployeeItem> AllEmployees { get; } = new();
    public ObservableCollection<EmployeeItem> FilteredEmployees { get; } = new();
    public ObservableCollection<string> SelectedEmployeeBreaks { get; } = new();

    // Colecciones para totales por día y hora
    public ObservableCollection<SemanaHoraRow> TotalesSemana { get; } = new();
    public ObservableCollection<ResumenDia> ResumenDias { get; } = new();

    // Campos privados para guardar datos originales de los 7 datasets
    private List<string[]>? _datosEntradasParking;
    private List<string[]>? _datosEntradasRentACar;
    private List<string[]>? _datosEntradasReservaParking;
    private List<string[]>? _datosEntradaReservaRentACar;
    private List<string[]>? _datosSalidasParking;
    private List<string[]>? _datosSalidasRentACar;
    private List<string[]>? _datosSalidaReservaParking;

    private string _selectedZona = "Todas";
    public string SelectedZona
    {
        get => _selectedZona;
        set
        {
            if (_selectedZona != value)
            {
                _selectedZona = value;
                AplicarFiltros();
            }
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                AplicarFiltros();
            }
        }
    }

    private EmployeeItem? _selectedEmployee;
    public EmployeeItem? SelectedEmployee
    {
        get => _selectedEmployee;
        set
        {
            if (_selectedEmployee != value)
            {
                _selectedEmployee = value;
                ActualizarDetalleEmpleado();
                OnPropertyChanged(nameof(SelectedEmployee));
            }
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _turnosRepository = App.TurnosRepository;
        _excelReader = new VacationExcelReader(App.Configuration);
        
        // Inicializar controles con valores por defecto
        cmbLugar.Items.Add("AER");
        cmbLugar.SelectedItem = "AER";
        dpFechaInicio.SelectedDate = new DateTime(2026, 2, 2);
        dpFechaFin.SelectedDate = new DateTime(2026, 2, 8);
        txtEstado.Text = "Listo";
        
        // Inicializar estilos de botones después de que la ventana esté cargada
        Loaded += MainWindow_Loaded;
        
        // Cancelar carga al cerrar ventana
        Closing += MainWindow_Closing;
        
        _cancellationTokenSource = new CancellationTokenSource();
        _ = CargarTodosLosDatosAsync(_cancellationTokenSource.Token);
    }

    private void BtnTotales_Click(object sender, RoutedEventArgs e)
    {
        ActivarModoTotales();
    }

    private void BtnTurnos_Click(object sender, RoutedEventArgs e)
    {
        ActivarModoTurnos();
    }

    private void BtnTrabajadores_Click(object sender, RoutedEventArgs e)
    {
        ActivarModoTrabajadores();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Inicializar estilos de botones (modo turnos activo por defecto)
        ActualizarEstilosBotones();
    }

    private void ActualizarEstilosBotones()
    {
        var raisedStyle = TryFindResource("MaterialDesignRaisedButton") as System.Windows.Style;
        var outlinedStyle = TryFindResource("MaterialDesignOutlinedButton") as System.Windows.Style;
        
        if (raisedStyle != null && outlinedStyle != null)
        {
            if (_isWorkerMode)
            {
                btnTrabajadores.Style = raisedStyle;
                btnTurnos.Style = outlinedStyle;
            }
            else
            {
                btnTurnos.Style = raisedStyle;
                btnTrabajadores.Style = outlinedStyle;
            }
        }
    }

    private void ActivarModoTurnos()
    {
        _isWorkerMode = false;
        
        // Modo turnos: ocultar panel izquierdo y paneles de trabajadores/totales, mostrar contenido de turnos
        drawerColumn.Width = new GridLength(0);
        RightWorkerContent.Visibility = Visibility.Collapsed;
        RightTotalesContent.Visibility = Visibility.Collapsed;
        RightContent.Visibility = Visibility.Visible;
        btnCargar.IsEnabled = true;
        txtEstado.Text = "Listo";
        
        // Actualizar estilos de botones
        ActualizarEstilosBotones();
    }

    private void ActivarModoTotales()
    {
        _isWorkerMode = false;
        
        // Modo totales: ocultar panel izquierdo y contenido de turnos, mostrar panel de totales
        drawerColumn.Width = new GridLength(0);
        RightWorkerContent.Visibility = Visibility.Collapsed;
        RightContent.Visibility = Visibility.Collapsed;
        RightTotalesContent.Visibility = Visibility.Visible;
        btnCargar.IsEnabled = true;
        txtEstado.Text = "Modo totales";
        
        // Actualizar estilos de botones
        ActualizarEstilosBotones();
    }

    private void ActivarModoTrabajadores()
    {
        _isWorkerMode = true;
        
        // Modo trabajadores: mostrar panel izquierdo con ancho fijo y panel derecho de trabajadores
        drawerColumn.Width = new GridLength(380);
        RightContent.Visibility = Visibility.Collapsed;
        RightTotalesContent.Visibility = Visibility.Collapsed;
        RightWorkerContent.Visibility = Visibility.Visible;
        btnCargar.IsEnabled = false;
        progressBar.Visibility = Visibility.Collapsed;
        txtEstado.Text = "Modo trabajadores";
        
        // Cargar empleados cuando se entra al modo trabajadores por primera vez
        if (AllEmployees.Count == 0)
        {
            _ = CargarEmpleadosAsync();
        }
        
        // Actualizar estilos de botones
        ActualizarEstilosBotones();
    }

    private async Task CargarEmpleadosAsync()
    {
        try
        {
            if (txtWorkerSubtitle != null)
            {
                txtWorkerSubtitle.Text = "Cargando empleados...";
            }
            
            var empleados = await _excelReader.GetAsignacionesAsync();
            
            AllEmployees.Clear();
            foreach (var empleado in empleados)
            {
                AllEmployees.Add(empleado);
            }

            AplicarFiltros();
            
            if (txtWorkerSubtitle != null)
            {
                txtWorkerSubtitle.Text = AllEmployees.Count == 0 
                    ? "No se encontraron empleados" 
                    : "Seleccione un trabajador";
            }
            
            if (txtWorkerEmptyMessage != null && AllEmployees.Count > 0)
            {
                txtWorkerEmptyMessage.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            if (txtWorkerSubtitle != null)
            {
                txtWorkerSubtitle.Text = $"Error al cargar: {ex.Message}";
            }
            MessageBox.Show($"Error al cargar empleados: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AplicarFiltros()
    {
        var query = AllEmployees.AsEnumerable();

        // Filtrar por zona
        if (SelectedZona != "Todas")
        {
            query = query.Where(e => string.Equals(e.Zona, SelectedZona, StringComparison.OrdinalIgnoreCase));
        }

        // Filtrar por texto de búsqueda
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            query = query.Where(e => e.Name.ToLowerInvariant().Contains(searchLower));
        }

        FilteredEmployees.Clear();
        foreach (var empleado in query.OrderBy(e => e.Name))
        {
            FilteredEmployees.Add(empleado);
        }
    }

    private void CmbZona_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (cmbZona.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
        {
            var zona = selectedItem.Content?.ToString() ?? "Todas";
            if (_selectedZona != zona)
            {
                _selectedZona = zona;
                AplicarFiltros();
            }
        }
    }

    private void TxtBusquedaEmpleado_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var newText = txtBusquedaEmpleado.Text ?? string.Empty;
        if (_searchText != newText)
        {
            _searchText = newText;
            AplicarFiltros();
        }
    }

    private void LstEmpleados_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SelectedEmployee = lstEmpleados.SelectedItem as EmployeeItem;
    }

    private async void ActualizarDetalleEmpleado()
    {
        SelectedEmployeeBreaks.Clear();
        
        if (SelectedEmployee != null)
        {
            try
            {
                // Actualizar subtítulo
                if (txtWorkerSubtitle != null)
                {
                    txtWorkerSubtitle.Text = $"{SelectedEmployee.Name} - {SelectedEmployee.Zona}";
                }
                
                if (txtWorkerEmptyMessage != null)
                {
                    txtWorkerEmptyMessage.Visibility = Visibility.Collapsed;
                }
                
                // Cargar módulos y asignaciones si no están cacheados
                var modulos = await _excelReader.GetModulosAsync();
                var asignacionesModulos = await _excelReader.GetAsignacionesModulosAsync();
                
                // Obtener módulos del empleado seleccionado
                if (asignacionesModulos.TryGetValue(SelectedEmployee.Name, out var modulosEmpleado) && modulosEmpleado.Count > 0)
                {
                    // Crear diccionario de módulos para búsqueda rápida
                    var modulosDict = modulos.ToDictionary(m => m.Modulo, StringComparer.OrdinalIgnoreCase);
                    
                    // Ordenar módulos numéricamente (parsear como int si es posible)
                    var modulosOrdenados = modulosEmpleado.OrderBy(m =>
                    {
                        if (int.TryParse(m, out int num))
                            return num;
                        return int.MaxValue; // Si no es numérico, ponerlo al final
                    }).ThenBy(m => m);
                    
                    foreach (var moduloNum in modulosOrdenados)
                    {
                        if (modulosDict.TryGetValue(moduloNum, out var modulo))
                        {
                            var fechaInicioStr = modulo.FechaInicio.ToString("dd/MM/yyyy");
                            var fechaFinStr = modulo.FechaFin.ToString("dd/MM/yyyy");
                            SelectedEmployeeBreaks.Add($"M{moduloNum} ({fechaInicioStr} - {fechaFinStr})");
                        }
                        else
                        {
                            SelectedEmployeeBreaks.Add($"M{moduloNum} (sin fechas)");
                        }
                    }
                    
                    if (SelectedEmployeeBreaks.Count == 0)
                    {
                        SelectedEmployeeBreaks.Add($"No se encontraron semanas de descanso para {SelectedEmployee.Name}");
                    }
                }
                else
                {
                    SelectedEmployeeBreaks.Add($"No se encontraron módulos asignados para {SelectedEmployee.Name}");
                }
            }
            catch (Exception ex)
            {
                SelectedEmployeeBreaks.Add($"Error al cargar semanas de descanso: {ex.Message}");
                MessageBox.Show($"Error al cargar semanas de descanso:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            // No hay empleado seleccionado
            if (txtWorkerSubtitle != null)
            {
                txtWorkerSubtitle.Text = "Seleccione un trabajador";
            }
            
            if (txtWorkerEmptyMessage != null)
            {
                txtWorkerEmptyMessage.Visibility = Visibility.Visible;
            }
        }
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
        // Si ya hay una carga en curso, cancelarla y disposear el CTS anterior
        if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        // Crear nuevo CancellationTokenSource para esta carga
        _cancellationTokenSource = new CancellationTokenSource();
        await CargarTodosLosDatosAsync(_cancellationTokenSource.Token);
    }

    private async Task CargarTodosLosDatosAsync(CancellationToken ct)
    {
        var hayError = false;
        var swTotal = Stopwatch.StartNew();

        try
        {
            Debug.WriteLine("=== Iniciando carga de datos ===");
            txtEstado.Text = "Cargando...";
            btnCargar.IsEnabled = false;
            progressBar.Visibility = Visibility.Visible;

            var fechaInicio = dpFechaInicio.SelectedDate ?? new DateTime(2026, 2, 2);
            var fechaFin = dpFechaFin.SelectedDate ?? new DateTime(2026, 2, 8);
            var lugar = cmbLugar.SelectedItem?.ToString() ?? cmbLugar.Text ?? "AER";

            // Entradas Parking
            try
            {
                var sw = Stopwatch.StartNew();
                var datosEntradasParking = await _turnosRepository.GetEntradasParkingAsync(fechaInicio, fechaFin, lugar, ct);
                sw.Stop();
                Debug.WriteLine($"[Entradas Parking] Consulta completada en {sw.ElapsedMilliseconds}ms - {datosEntradasParking.Count} registros");
                
                // Guardar datos originales para totales
                _datosEntradasParking = datosEntradasParking;
                
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
                _datosEntradasParking = null;
                throw;
            }
            catch (Exception ex)
            {
                hayError = true;
                _datosEntradasParking = null;
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
                
                // Guardar datos originales para totales
                _datosEntradasRentACar = datosEntradasRentACar;
                
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
                _datosEntradasRentACar = null;
                throw;
            }
            catch (Exception ex)
            {
                hayError = true;
                _datosEntradasRentACar = null;
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
                
                // Guardar datos originales para totales
                _datosEntradasReservaParking = datosEntradasReservaParking;
                
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
                _datosEntradasReservaParking = null;
                throw;
            }
            catch (Exception ex)
            {
                hayError = true;
                _datosEntradasReservaParking = null;
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
                
                // Guardar datos originales para totales
                _datosEntradaReservaRentACar = datosEntradaReservaRentACar;
                
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
                _datosEntradaReservaRentACar = null;
                throw;
            }
            catch (Exception ex)
            {
                hayError = true;
                _datosEntradaReservaRentACar = null;
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
                
                // Guardar datos originales para totales
                _datosSalidasParking = datosSalidasParking;
                
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
                _datosSalidasParking = null;
                throw;
            }
            catch (Exception ex)
            {
                hayError = true;
                _datosSalidasParking = null;
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
                
                // Guardar datos originales para totales
                _datosSalidasRentACar = datosSalidasRentACar;
                
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
                _datosSalidasRentACar = null;
                throw;
            }
            catch (Exception ex)
            {
                hayError = true;
                _datosSalidasRentACar = null;
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
                
                // Guardar datos originales para totales
                _datosSalidaReservaParking = datosSalidaReservaParking;
                
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
                _datosSalidaReservaParking = null;
                throw;
            }
            catch (Exception ex)
            {
                hayError = true;
                _datosSalidaReservaParking = null;
                Debug.WriteLine($"[Salida Reserva Parking] Error: {ex.Message}");
                MessageBox.Show($"Error en pestaña 'Salida Reserva Parking': {ex.Message}", "Error - Salida Reserva Parking", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Calcular totales por día y hora
            try
            {
                var inicioSemana = DateOnly.FromDateTime(fechaInicio);
                var finSemana = DateOnly.FromDateTime(fechaFin);
                
                var (tabla, resumen) = ConstruirTotalesSemana(inicioSemana, finSemana);
                
                // Actualizar UI en el hilo correcto
                Dispatcher.Invoke(() =>
                {
                    TotalesSemana.Clear();
                    foreach (var fila in tabla)
                    {
                        TotalesSemana.Add(fila);
                    }
                    
                    ResumenDias.Clear();
                    foreach (var dia in resumen)
                    {
                        ResumenDias.Add(dia);
                    }
                    
                    // Generar columnas dinámicas
                    int numDias = (finSemana.DayNumber - inicioSemana.DayNumber) + 1;
                    if (numDias > 7) numDias = 7;
                    GenerarColumnasTotalesSemana(inicioSemana, numDias);
                    GenerarColumnasResumenDias();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Totales] Error al calcular totales: {ex.Message}");
                // No mostrar MessageBox aquí para no interrumpir el flujo
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
            progressBar.Visibility = Visibility.Collapsed;
        }
    }

    #region Helpers de parseo robusto

    private bool TryParseFecha(string fechaStr, out DateOnly fecha)
    {
        fecha = default;
        if (string.IsNullOrWhiteSpace(fechaStr))
            return false;

        var fechaStrTrimmed = fechaStr.Trim();

        // Intentar con cultura es-ES
        if (DateTime.TryParse(fechaStrTrimmed, System.Globalization.CultureInfo.GetCultureInfo("es-ES"), 
            System.Globalization.DateTimeStyles.None, out DateTime dtEs))
        {
            fecha = DateOnly.FromDateTime(dtEs);
            return true;
        }

        // Intentar con InvariantCulture
        if (DateTime.TryParse(fechaStrTrimmed, System.Globalization.CultureInfo.InvariantCulture, 
            System.Globalization.DateTimeStyles.None, out DateTime dtInv))
        {
            fecha = DateOnly.FromDateTime(dtInv);
            return true;
        }

        // Intentar formato específico "dd/MM/yyyy"
        if (DateTime.TryParseExact(fechaStrTrimmed, "dd/MM/yyyy", 
            System.Globalization.CultureInfo.InvariantCulture, 
            System.Globalization.DateTimeStyles.None, out DateTime dtExact))
        {
            fecha = DateOnly.FromDateTime(dtExact);
            return true;
        }

        return false;
    }

    private bool TryParseHora(string horaStr, out int hour)
    {
        hour = 0;
        if (string.IsNullOrWhiteSpace(horaStr))
            return false;

        var horaStrTrimmed = horaStr.Trim();

        // Intentar TimeSpan (acepta "HH:mm", "HH:mm:ss", "H:mm")
        if (TimeSpan.TryParse(horaStrTrimmed, out TimeSpan ts))
        {
            hour = ts.Hours;
            return true;
        }

        // Intentar DateTime y extraer hora
        if (DateTime.TryParse(horaStrTrimmed, out DateTime dt))
        {
            hour = dt.Hour;
            return true;
        }

        // Intentar formato específico "HH:mm"
        if (TimeSpan.TryParseExact(horaStrTrimmed, "HH:mm", 
            System.Globalization.CultureInfo.InvariantCulture, out TimeSpan tsExact))
        {
            hour = tsExact.Hours;
            return true;
        }

        return false;
    }

    #endregion

    #region Procesamiento de totales

    private void ProcesarDataset(
        List<string[]>? dataset,
        int idxFecha,
        int idxHora,
        int[,] contadoresEntradas,
        int[,] contadoresSalidas,
        DateOnly inicioSemana,
        DateOnly finSemana,
        bool esEntrada)
    {
        if (dataset == null)
            return;

        foreach (var fila in dataset)
        {
            if (fila == null || fila.Length <= Math.Max(idxFecha, idxHora))
                continue;

            if (!TryParseFecha(fila[idxFecha], out DateOnly fecha))
                continue;

            if (!TryParseHora(fila[idxHora], out int hora))
                continue;

            // Verificar que la fecha esté en el rango
            if (fecha < inicioSemana || fecha > finSemana)
                continue;

            // Calcular índice del día (0-6)
            int diaIdx = (fecha.DayNumber - inicioSemana.DayNumber);
            if (diaIdx < 0 || diaIdx >= 7)
                continue;

            // Validar hora (0-23)
            if (hora < 0 || hora > 23)
                continue;

            // Incrementar contador correspondiente
            if (esEntrada)
            {
                contadoresEntradas[diaIdx, hora]++;
            }
            else
            {
                contadoresSalidas[diaIdx, hora]++;
            }
        }
    }

    private (List<SemanaHoraRow> tabla, List<ResumenDia> resumen) ConstruirTotalesSemana(
        DateOnly inicioSemana,
        DateOnly finSemana)
    {
        var tabla = new List<SemanaHoraRow>();
        var resumen = new List<ResumenDia>();

        // Validar y ajustar rango
        int diasRango = (finSemana.DayNumber - inicioSemana.DayNumber) + 1;
        bool rangoRecortado = false;
        if (diasRango > 7)
        {
            finSemana = inicioSemana.AddDays(6);
            diasRango = 7;
            rangoRecortado = true;
        }

        // Inicializar arrays de contadores
        int[,] contadoresEntradas = new int[7, 24];
        int[,] contadoresSalidas = new int[7, 24];

        // Lista de datasets fallidos para mostrar aviso
        var datasetsFallidos = new List<string>();

        // Procesar cada dataset con sus índices específicos
        // Entradas
        if (_datosEntradasParking != null)
        {
            ProcesarDataset(_datosEntradasParking, 1, 2, contadoresEntradas, contadoresSalidas, inicioSemana, finSemana, true);
        }
        else
        {
            datasetsFallidos.Add("Entradas Parking");
        }

        if (_datosEntradasRentACar != null)
        {
            ProcesarDataset(_datosEntradasRentACar, 1, 2, contadoresEntradas, contadoresSalidas, inicioSemana, finSemana, true);
        }
        else
        {
            datasetsFallidos.Add("Entradas Rent a Car");
        }

        if (_datosEntradasReservaParking != null)
        {
            ProcesarDataset(_datosEntradasReservaParking, 1, 2, contadoresEntradas, contadoresSalidas, inicioSemana, finSemana, true);
        }
        else
        {
            datasetsFallidos.Add("Entradas Reserva Parking");
        }

        if (_datosEntradaReservaRentACar != null)
        {
            ProcesarDataset(_datosEntradaReservaRentACar, 1, 2, contadoresEntradas, contadoresSalidas, inicioSemana, finSemana, true);
        }
        else
        {
            datasetsFallidos.Add("Entrada Reserva Rent a Car");
        }

        // Salidas
        if (_datosSalidasParking != null)
        {
            ProcesarDataset(_datosSalidasParking, 1, 2, contadoresEntradas, contadoresSalidas, inicioSemana, finSemana, false);
        }
        else
        {
            datasetsFallidos.Add("Salidas Parking");
        }

        if (_datosSalidasRentACar != null)
        {
            ProcesarDataset(_datosSalidasRentACar, 1, 2, contadoresEntradas, contadoresSalidas, inicioSemana, finSemana, false);
        }
        else
        {
            datasetsFallidos.Add("Salidas Rent a Car");
        }

        if (_datosSalidaReservaParking != null)
        {
            ProcesarDataset(_datosSalidaReservaParking, 1, 2, contadoresEntradas, contadoresSalidas, inicioSemana, finSemana, false);
        }
        else
        {
            datasetsFallidos.Add("Salida Reserva Parking");
        }

        // Construir tabla de 24 filas (una por hora)
        for (int h = 0; h < 24; h++)
        {
            var fila = new SemanaHoraRow
            {
                Rango = h == 23 ? "23-00" : $"{h:D2}-{(h + 1):D2}"
            };

            // Asignar valores para cada día (0-6)
            fila.D0_Ent = contadoresEntradas[0, h];
            fila.D0_Sal = contadoresSalidas[0, h];
            fila.D1_Ent = contadoresEntradas[1, h];
            fila.D1_Sal = contadoresSalidas[1, h];
            fila.D2_Ent = contadoresEntradas[2, h];
            fila.D2_Sal = contadoresSalidas[2, h];
            fila.D3_Ent = contadoresEntradas[3, h];
            fila.D3_Sal = contadoresSalidas[3, h];
            fila.D4_Ent = contadoresEntradas[4, h];
            fila.D4_Sal = contadoresSalidas[4, h];
            fila.D5_Ent = contadoresEntradas[5, h];
            fila.D5_Sal = contadoresSalidas[5, h];
            fila.D6_Ent = contadoresEntradas[6, h];
            fila.D6_Sal = contadoresSalidas[6, h];

            tabla.Add(fila);
        }

        // Añadir fila "Total" que suma todas las horas por día
        var filaTotal = new SemanaHoraRow
        {
            Rango = "Total"
        };

        // Calcular totales por día (suma de todas las horas)
        for (int d = 0; d < 7; d++)
        {
            int totalEnt = 0;
            int totalSal = 0;
            
            for (int h = 0; h < 24; h++)
            {
                totalEnt += contadoresEntradas[d, h];
                totalSal += contadoresSalidas[d, h];
            }

            // Asignar según el día
            switch (d)
            {
                case 0:
                    filaTotal.D0_Ent = totalEnt;
                    filaTotal.D0_Sal = totalSal;
                    break;
                case 1:
                    filaTotal.D1_Ent = totalEnt;
                    filaTotal.D1_Sal = totalSal;
                    break;
                case 2:
                    filaTotal.D2_Ent = totalEnt;
                    filaTotal.D2_Sal = totalSal;
                    break;
                case 3:
                    filaTotal.D3_Ent = totalEnt;
                    filaTotal.D3_Sal = totalSal;
                    break;
                case 4:
                    filaTotal.D4_Ent = totalEnt;
                    filaTotal.D4_Sal = totalSal;
                    break;
                case 5:
                    filaTotal.D5_Ent = totalEnt;
                    filaTotal.D5_Sal = totalSal;
                    break;
                case 6:
                    filaTotal.D6_Ent = totalEnt;
                    filaTotal.D6_Sal = totalSal;
                    break;
            }
        }

        tabla.Add(filaTotal);

        // Construir resumen por día
        for (int d = 0; d < diasRango; d++)
        {
            var fechaDia = inicioSemana.AddDays(d);
            var diaLabel = fechaDia.ToString("ddd dd/MM", System.Globalization.CultureInfo.GetCultureInfo("es-ES"));

            var resumenDia = new ResumenDia
            {
                Dia = fechaDia,
                DiaLabel = diaLabel
            };

            // Nocturno (horas 0-7)
            for (int h = 0; h <= 7; h++)
            {
                resumenDia.Noct_Ent += contadoresEntradas[d, h];
                resumenDia.Noct_Sal += contadoresSalidas[d, h];
            }

            // Mañanas (horas 8-15)
            for (int h = 8; h <= 15; h++)
            {
                resumenDia.Man_Ent += contadoresEntradas[d, h];
                resumenDia.Man_Sal += contadoresSalidas[d, h];
            }

            // Tardes (horas 16-23)
            for (int h = 16; h <= 23; h++)
            {
                resumenDia.Tar_Ent += contadoresEntradas[d, h];
                resumenDia.Tar_Sal += contadoresSalidas[d, h];
            }

            resumen.Add(resumenDia);
        }

        // Mostrar avisos si es necesario
        if (rangoRecortado || datasetsFallidos.Count > 0)
        {
            var mensajes = new List<string>();
            if (rangoRecortado)
            {
                mensajes.Add("Rango recortado a 7 días");
            }
            if (datasetsFallidos.Count > 0)
            {
                mensajes.Add($"Totales parciales: falló {string.Join(", ", datasetsFallidos)}");
            }
            
            Dispatcher.Invoke(() =>
            {
                txtEstado.Text = string.Join("; ", mensajes);
            });
        }

        return (tabla, resumen);
    }

    private void GenerarColumnasTotalesSemana(DateOnly inicioSemana, int numDias)
    {
        if (dgvTotalesSemana == null)
            return;

        dgvTotalesSemana.Columns.Clear();

        // Columna fija "Rango"
        dgvTotalesSemana.Columns.Add(new DataGridTextColumn
        {
            Header = "Rango",
            Binding = new System.Windows.Data.Binding("Rango"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Auto)
        });

        // Columnas dinámicas por día
        for (int diaIdx = 0; diaIdx < numDias && diaIdx < 7; diaIdx++)
        {
            var fecha = inicioSemana.AddDays(diaIdx);
            var label = fecha.ToString("ddd dd/MM", System.Globalization.CultureInfo.GetCultureInfo("es-ES"));

            // Columna Entradas
            dgvTotalesSemana.Columns.Add(new DataGridTextColumn
            {
                Header = $"{label} - Entradas",
                Binding = new System.Windows.Data.Binding($"D{diaIdx}_Ent"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            // Columna Salidas
            dgvTotalesSemana.Columns.Add(new DataGridTextColumn
            {
                Header = $"{label} - Salidas",
                Binding = new System.Windows.Data.Binding($"D{diaIdx}_Sal"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
        }
    }

    private void GenerarColumnasResumenDias()
    {
        if (dgvResumenDias == null)
            return;

        dgvResumenDias.Columns.Clear();

        // Columna Día
        dgvResumenDias.Columns.Add(new DataGridTextColumn
        {
            Header = "Día",
            Binding = new System.Windows.Data.Binding("DiaLabel"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Auto)
        });

        // Nocturno
        dgvResumenDias.Columns.Add(new DataGridTextColumn
        {
            Header = "Noct. Ent",
            Binding = new System.Windows.Data.Binding("Noct_Ent")
        });
        dgvResumenDias.Columns.Add(new DataGridTextColumn
        {
            Header = "Noct. Sal",
            Binding = new System.Windows.Data.Binding("Noct_Sal")
        });
        dgvResumenDias.Columns.Add(new DataGridTextColumn
        {
            Header = "Noct. Tot",
            Binding = new System.Windows.Data.Binding("Noct_Tot")
        });

        // Mañanas
        dgvResumenDias.Columns.Add(new DataGridTextColumn
        {
            Header = "Mañ. Ent",
            Binding = new System.Windows.Data.Binding("Man_Ent")
        });
        dgvResumenDias.Columns.Add(new DataGridTextColumn
        {
            Header = "Mañ. Sal",
            Binding = new System.Windows.Data.Binding("Man_Sal")
        });
        dgvResumenDias.Columns.Add(new DataGridTextColumn
        {
            Header = "Mañ. Tot",
            Binding = new System.Windows.Data.Binding("Man_Tot")
        });

        // Tardes
        dgvResumenDias.Columns.Add(new DataGridTextColumn
        {
            Header = "Tar. Ent",
            Binding = new System.Windows.Data.Binding("Tar_Ent")
        });
        dgvResumenDias.Columns.Add(new DataGridTextColumn
        {
            Header = "Tar. Sal",
            Binding = new System.Windows.Data.Binding("Tar_Sal")
        });
        dgvResumenDias.Columns.Add(new DataGridTextColumn
        {
            Header = "Tar. Tot",
            Binding = new System.Windows.Data.Binding("Tar_Tot")
        });

        // Totales - con estilos destacados
        var styleTotalCell = new Style(typeof(TextBlock));
        styleTotalCell.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        styleTotalCell.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
        styleTotalCell.Setters.Add(new Setter(TextBlock.BackgroundProperty, new SolidColorBrush(Color.FromArgb(255, 200, 230, 201)))); // Verde claro
        styleTotalCell.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2)));

        dgvResumenDias.Columns.Add(new DataGridTextColumn
        {
            Header = "Total Ent",
            Binding = new System.Windows.Data.Binding("TotalEnt"),
            ElementStyle = styleTotalCell
        });
        
        var styleTotalSalCell = new Style(typeof(TextBlock));
        styleTotalSalCell.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        styleTotalSalCell.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
        styleTotalSalCell.Setters.Add(new Setter(TextBlock.BackgroundProperty, new SolidColorBrush(Color.FromArgb(255, 187, 222, 251)))); // Azul claro
        styleTotalSalCell.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2)));

        dgvResumenDias.Columns.Add(new DataGridTextColumn
        {
            Header = "Total Sal",
            Binding = new System.Windows.Data.Binding("TotalSal"),
            ElementStyle = styleTotalSalCell
        });
        
        var styleTotalGenCell = new Style(typeof(TextBlock));
        styleTotalGenCell.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        styleTotalGenCell.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
        styleTotalGenCell.Setters.Add(new Setter(TextBlock.BackgroundProperty, new SolidColorBrush(Color.FromArgb(255, 255, 245, 157)))); // Amarillo claro
        styleTotalGenCell.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2)));

        dgvResumenDias.Columns.Add(new DataGridTextColumn
        {
            Header = "Total Gen",
            Binding = new System.Windows.Data.Binding("TotalGen"),
            ElementStyle = styleTotalGenCell
        });
    }

    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
