using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Extensions.Configuration;
using Turnos.App.Models;

namespace Turnos.App.Services;

public class VacationExcelReader
{
    private readonly IConfiguration _configuration;
    private List<EmployeeItem>? _cachedEmployees;
    private List<ModuloVacacion>? _cachedModulos;
    private Dictionary<string, List<string>>? _cachedAsignacionesModulos;

    public VacationExcelReader(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<List<EmployeeItem>> GetAsignacionesAsync()
    {
        if (_cachedEmployees != null)
        {
            return _cachedEmployees;
        }

        var result = await Task.Run(() =>
        {
            var asignacionesPath = _configuration["Vacaciones:AsignacionesPath"];
            if (string.IsNullOrEmpty(asignacionesPath))
            {
                throw new InvalidOperationException("No se encontró la configuración 'Vacaciones:AsignacionesPath' en appsettings.json");
            }

            var basePath = AppContext.BaseDirectory;
            var fullPath = Path.Combine(basePath, asignacionesPath.Replace('\\', Path.DirectorySeparatorChar));

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"No se encontró el archivo Excel: {fullPath}");
            }

            var employees = new HashSet<EmployeeItem>(new EmployeeItemEqualityComparer());

            using var workbook = new XLWorkbook(fullPath);
            var worksheet = workbook.Worksheet(1);

            var headerRow = worksheet.FirstRowUsed();
            if (headerRow == null)
            {
                return new List<EmployeeItem>();
            }

            int empleadoColIndex = -1;
            int zonaColIndex = -1;

            foreach (var cell in headerRow.CellsUsed())
            {
                var cellValue = cell.GetString().Trim();
                if (string.Equals(cellValue, "Empleado", StringComparison.OrdinalIgnoreCase))
                {
                    empleadoColIndex = cell.Address.ColumnNumber;
                }
                else if (string.Equals(cellValue, "Zona", StringComparison.OrdinalIgnoreCase))
                {
                    zonaColIndex = cell.Address.ColumnNumber;
                }
            }

            if (empleadoColIndex == -1 || zonaColIndex == -1)
            {
                throw new InvalidOperationException("No se encontraron las columnas 'Empleado' y/o 'Zona' en el archivo Excel");
            }

            var dataRange = worksheet.Range(headerRow.RowNumber() + 1, empleadoColIndex, worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber() + 1, Math.Max(empleadoColIndex, zonaColIndex));

            foreach (var row in dataRange.RowsUsed())
            {
                var empleado = row.Cell(empleadoColIndex).GetString().Trim();
                var zona = row.Cell(zonaColIndex).GetString().Trim();

                if (!string.IsNullOrEmpty(empleado) && !string.IsNullOrEmpty(zona))
                {
                    employees.Add(new EmployeeItem
                    {
                        Name = empleado,
                        Zona = zona
                    });
                }
            }

            return employees.OrderBy(e => e.Name).ToList();
        });

        _cachedEmployees = result;
        return _cachedEmployees;
    }

    public async Task<List<ModuloVacacion>> GetModulosAsync()
    {
        if (_cachedModulos != null)
        {
            return _cachedModulos;
        }

        _cachedModulos = await Task.Run(() =>
        {
            var modulosPath = _configuration["Vacaciones:ModulosPath"];
            if (string.IsNullOrEmpty(modulosPath))
            {
                throw new InvalidOperationException("No se encontró la configuración 'Vacaciones:ModulosPath' en appsettings.json");
            }

            var basePath = AppContext.BaseDirectory;
            var fullPath = Path.Combine(basePath, modulosPath.Replace('\\', Path.DirectorySeparatorChar));

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"No se encontró el archivo Excel: {fullPath}");
            }

            var modulos = new List<ModuloVacacion>();

            using var workbook = new XLWorkbook(fullPath);
            var worksheet = workbook.Worksheet(1);

            var headerRow = worksheet.FirstRowUsed();
            if (headerRow == null)
            {
                return modulos;
            }

            int moduloColIndex = -1;
            int fechaInicioColIndex = -1;
            int fechaFinColIndex = -1;

            foreach (var cell in headerRow.CellsUsed())
            {
                var cellValue = cell.GetString().Trim();
                if (string.Equals(cellValue, "Modulo", StringComparison.OrdinalIgnoreCase))
                {
                    moduloColIndex = cell.Address.ColumnNumber;
                }
                else if (string.Equals(cellValue, "Fecha Inicio", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(cellValue, "FechaInicio", StringComparison.OrdinalIgnoreCase))
                {
                    fechaInicioColIndex = cell.Address.ColumnNumber;
                }
                else if (string.Equals(cellValue, "Fecha Fin", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(cellValue, "FechaFin", StringComparison.OrdinalIgnoreCase))
                {
                    fechaFinColIndex = cell.Address.ColumnNumber;
                }
            }

            if (moduloColIndex == -1 || fechaInicioColIndex == -1 || fechaFinColIndex == -1)
            {
                throw new InvalidOperationException("No se encontraron las columnas 'Modulo', 'Fecha Inicio' y/o 'Fecha Fin' en el archivo Excel");
            }

            var dataRange = worksheet.Range(headerRow.RowNumber() + 1, moduloColIndex, worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber() + 1, Math.Max(moduloColIndex, Math.Max(fechaInicioColIndex, fechaFinColIndex)));

            foreach (var row in dataRange.RowsUsed())
            {
                var moduloStr = row.Cell(moduloColIndex).GetString().Trim();
                var fechaInicioCell = row.Cell(fechaInicioColIndex);
                var fechaFinCell = row.Cell(fechaFinColIndex);

                if (!string.IsNullOrEmpty(moduloStr) && fechaInicioCell.TryGetValue(out DateTime fechaInicio) && fechaFinCell.TryGetValue(out DateTime fechaFin))
                {
                    modulos.Add(new ModuloVacacion
                    {
                        Modulo = moduloStr,
                        FechaInicio = fechaInicio,
                        FechaFin = fechaFin
                    });
                }
            }

            return modulos.OrderBy(m => m.FechaInicio).ToList();
        });

        return _cachedModulos;
    }

    public async Task<Dictionary<string, List<string>>> GetAsignacionesModulosAsync()
    {
        if (_cachedAsignacionesModulos != null)
        {
            return _cachedAsignacionesModulos;
        }

        _cachedAsignacionesModulos = await Task.Run(() =>
        {
            var asignacionesPath = _configuration["Vacaciones:AsignacionesPath"];
            if (string.IsNullOrEmpty(asignacionesPath))
            {
                throw new InvalidOperationException("No se encontró la configuración 'Vacaciones:AsignacionesPath' en appsettings.json");
            }

            var basePath = AppContext.BaseDirectory;
            var fullPath = Path.Combine(basePath, asignacionesPath.Replace('\\', Path.DirectorySeparatorChar));

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"No se encontró el archivo Excel: {fullPath}");
            }

            var asignaciones = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            using var workbook = new XLWorkbook(fullPath);
            var worksheet = workbook.Worksheet(1);

            var headerRow = worksheet.FirstRowUsed();
            if (headerRow == null)
            {
                return asignaciones;
            }

            int empleadoColIndex = -1;
            int moduloColIndex = -1;

            foreach (var cell in headerRow.CellsUsed())
            {
                var cellValue = cell.GetString().Trim();
                if (string.Equals(cellValue, "Empleado", StringComparison.OrdinalIgnoreCase))
                {
                    empleadoColIndex = cell.Address.ColumnNumber;
                }
                else if (string.Equals(cellValue, "ModuloVacaciones", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(cellValue, "Modulo Vacaciones", StringComparison.OrdinalIgnoreCase))
                {
                    moduloColIndex = cell.Address.ColumnNumber;
                }
            }

            if (empleadoColIndex == -1 || moduloColIndex == -1)
            {
                throw new InvalidOperationException("No se encontraron las columnas 'Empleado' y/o 'ModuloVacaciones' en el archivo Excel");
            }

            var dataRange = worksheet.Range(headerRow.RowNumber() + 1, empleadoColIndex, worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber() + 1, Math.Max(empleadoColIndex, moduloColIndex));

            foreach (var row in dataRange.RowsUsed())
            {
                var empleado = row.Cell(empleadoColIndex).GetString().Trim();
                var modulo = row.Cell(moduloColIndex).GetString().Trim();

                if (!string.IsNullOrEmpty(empleado) && !string.IsNullOrEmpty(modulo))
                {
                    if (!asignaciones.ContainsKey(empleado))
                    {
                        asignaciones[empleado] = new List<string>();
                    }
                    asignaciones[empleado].Add(modulo);
                }
            }

            return asignaciones;
        });

        return _cachedAsignacionesModulos;
    }

    private class EmployeeItemEqualityComparer : IEqualityComparer<EmployeeItem>
    {
        public bool Equals(EmployeeItem? x, EmployeeItem? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null || y == null) return false;
            return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.Zona, y.Zona, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(EmployeeItem obj)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            return HashCode.Combine(
                obj.Name != null ? comparer.GetHashCode(obj.Name) : 0,
                obj.Zona != null ? comparer.GetHashCode(obj.Zona) : 0
            );
        }
    }
}
