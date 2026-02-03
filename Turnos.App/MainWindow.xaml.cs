using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
        
        // Modo turnos: ocultar panel izquierdo y panel derecho de trabajadores, mostrar contenido de turnos
        drawerColumn.Width = new GridLength(0);
        RightWorkerContent.Visibility = Visibility.Collapsed;
        RightContent.Visibility = Visibility.Visible;
        btnCargar.IsEnabled = true;
        txtEstado.Text = "Listo";
        
        // Actualizar estilos de botones
        ActualizarEstilosBotones();
    }

    private void ActivarModoTrabajadores()
    {
        _isWorkerMode = true;
        
        // Modo trabajadores: mostrar panel izquierdo con ancho fijo y panel derecho de trabajadores
        drawerColumn.Width = new GridLength(380);
        RightContent.Visibility = Visibility.Collapsed;
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
            progressBar.Visibility = Visibility.Collapsed;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
