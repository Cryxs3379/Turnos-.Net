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

    public VacationExcelReader(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<List<EmployeeItem>> GetAsignacionesAsync()
    {
        return await Task.Run(() =>
        {
            var asignacionesPath = _configuration["Vacaciones:AsignacionesPath"];
            if (string.IsNullOrEmpty(asignacionesPath))
            {
                throw new InvalidOperationException("No se encontró la configuración 'Vacaciones:AsignacionesPath' en appsettings.json");
            }

            // Resolver ruta relativa usando AppContext.BaseDirectory
            var basePath = AppContext.BaseDirectory;
            var fullPath = Path.Combine(basePath, asignacionesPath.Replace('\\', Path.DirectorySeparatorChar));

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"No se encontró el archivo Excel: {fullPath}");
            }

            var employees = new HashSet<EmployeeItem>(new EmployeeItemEqualityComparer());

            using var workbook = new XLWorkbook(fullPath);
            var worksheet = workbook.Worksheet(1); // Primera hoja

            // Buscar fila de encabezados
            var headerRow = worksheet.FirstRowUsed();
            if (headerRow == null)
            {
                return new List<EmployeeItem>();
            }

            // Buscar índices de columnas "Empleado" y "Zona"
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

            // Leer datos (empezar después de la fila de encabezados)
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
