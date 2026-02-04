using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Turnos.App.Models;

namespace Turnos.App.Services;

public record TurnoEmployee(string Name, bool IsHoliday);

public class TurnosPreviewResult
{
    public ObservableCollection<TurnoRow> TurnosRecepcion { get; } = new();
    public ObservableCollection<TurnoRow> TurnosEntradas { get; } = new();
    public List<string> Warnings { get; } = new();
}

public static class TurnosPreviewGenerator
{
    private const string RecN = "REC-N (8,h)";
    private const string RecM = "REC-M (8,h)";
    private const string RecT = "REC-T (8,h)";
    private const string EntN = "ENT-N (8,h)";
    private const string EntM = "ENT-M (8,h)";
    private const string EntT = "ENT-T (8,h)";
    private const string Off = "OFF";
    private const string Holidays = "HOLYDAYS";

    public static TurnosPreviewResult Generate(
        DateOnly inicioSemana,
        DateOnly finSemana,
        IReadOnlyList<ResumenDia> resumenDias,
        IReadOnlyList<TurnoEmployee> recepcion,
        IReadOnlyList<TurnoEmployee> entradas)
    {
        var result = new TurnosPreviewResult();
        var diasSemana = Enumerable.Range(0, 7).Select(d => inicioSemana.AddDays(d)).ToList();

        var (recNoctWork, recManWork, recTarWork) = BuildWorkArrays(diasSemana, resumenDias);
        var recNeed = BuildRecNeed(recNoctWork, recManWork, recTarWork);
        var recAssignments = AssignRecepcion(recepcion, recNeed, diasSemana, result.Warnings);

        var entAssignments = AssignEntradas(entradas, recNoctWork, recManWork, recTarWork, diasSemana, result.Warnings);

        foreach (var row in recAssignments.OrderBy(r => r.Empleado))
        {
            result.TurnosRecepcion.Add(row);
        }

        foreach (var row in entAssignments.OrderBy(r => r.Empleado))
        {
            result.TurnosEntradas.Add(row);
        }

        return result;
    }

    private static int[,] BuildRecNeed(IReadOnlyList<int> noct, IReadOnlyList<int> man, IReadOnlyList<int> tar)
    {
        var need = new int[7, 3];
        for (int d = 0; d < 7; d++)
        {
            need[d, 0] = Math.Max(2, (int)Math.Ceiling((noct.ElementAtOrDefault(d)) / 20.0));
            need[d, 1] = Math.Max(2, (int)Math.Ceiling((man.ElementAtOrDefault(d)) / 20.0));
            need[d, 2] = Math.Max(2, (int)Math.Ceiling((tar.ElementAtOrDefault(d)) / 20.0));
        }
        return need;
    }

    private static List<TurnoRow> AssignRecepcion(
        IReadOnlyList<TurnoEmployee> employees,
        int[,] need,
        IReadOnlyList<DateOnly> diasSemana,
        List<string> warnings)
    {
        var cultura = CultureInfo.GetCultureInfo("es-ES");
        var states = BuildStates(employees);

        for (int d = 0; d < 7; d++)
        {
            for (int t = 0; t < 3; t++)
            {
                int required = need[d, t];
                int assigned = AssignByLeastWorked(states, d, t, required, "REC");

                if (assigned < required)
                {
                    var diaLabel = diasSemana[d].ToString("ddd dd/MM", cultura);
                    var turnoLabel = t == 0 ? "N" : t == 1 ? "M" : "T";
                    warnings.Add($"FALTA REC {diaLabel} {turnoLabel} (need={required}, asignado={assigned})");
                }
            }
        }

        return FinalizeAssignments(states);
    }

    private static List<TurnoRow> AssignEntradas(
        IReadOnlyList<TurnoEmployee> employees,
        IReadOnlyList<int> noctWork,
        IReadOnlyList<int> manWork,
        IReadOnlyList<int> tarWork,
        IReadOnlyList<DateOnly> diasSemana,
        List<string> warnings)
    {
        var cultura = CultureInfo.GetCultureInfo("es-ES");
        var states = BuildStates(employees);
        var rotationPool = states.Where(s => !s.IsHoliday).OrderBy(s => s.Name).ToList();
        int rotationIndex = 0;
        var availableEmployees = rotationPool.Count;
        var capacityTotal = availableEmployees * 5;
        var baseNeeded = 21;
        var maxExtras = Math.Max(0, capacityTotal - baseNeeded);

        // Fase 1: base 1/1/1 con rotación de nocturnos
        for (int d = 0; d < 7; d++)
        {
            if (!AssignRotatingNight(rotationPool, d, ref rotationIndex))
            {
                var diaLabel = diasSemana[d].ToString("ddd dd/MM", cultura);
                warnings.Add($"FALTA BASE ENT {diaLabel} N (asignado=0)");
            }

            if (!AssignSingle(states, d, 1, "ENT"))
            {
                var diaLabel = diasSemana[d].ToString("ddd dd/MM", cultura);
                warnings.Add($"FALTA BASE ENT {diaLabel} M (asignado=0)");
            }

            if (!AssignSingle(states, d, 2, "ENT"))
            {
                var diaLabel = diasSemana[d].ToString("ddd dd/MM", cultura);
                warnings.Add($"FALTA BASE ENT {diaLabel} T (asignado=0)");
            }
        }

        // Fase 2: refuerzos según capacidad semanal (picos de carga)
        var slots = new List<ExtraSlot>();
        for (int d = 0; d < 7; d++)
        {
            for (int t = 0; t < 3; t++)
            {
                int work = t == 0 ? noctWork.ElementAtOrDefault(d)
                    : t == 1 ? manWork.ElementAtOrDefault(d)
                    : tarWork.ElementAtOrDefault(d);
                if (work > 0)
                {
                    slots.Add(new ExtraSlot(d, t, work));
                }
            }
        }

        int assignedExtras = 0;
        var assignedSlots = new List<ExtraSlot>();

        foreach (var slot in slots.OrderByDescending(s => s.Work))
        {
            if (assignedExtras >= maxExtras)
                break;

            if (TryAssignExtra(states, slot.DayIndex, slot.Turn))
            {
                assignedExtras++;
                assignedSlots.Add(slot);
            }
        }

        // Refuerzos adicionales opcionales si queda margen
        if (assignedExtras < maxExtras)
        {
            foreach (var slot in slots.OrderByDescending(s => s.Work))
            {
                if (assignedExtras >= maxExtras)
                    break;

                if (TryAssignExtra(states, slot.DayIndex, slot.Turn))
                {
                    assignedExtras++;
                    assignedSlots.Add(slot);
                }
            }
        }

        if (maxExtras > 0)
        {
            var slotLabels = assignedSlots
                .Select(s =>
                {
                    var diaLabel = diasSemana[s.DayIndex].ToString("ddd dd/MM", cultura);
                    var turnoLabel = s.Turn == 0 ? "N" : s.Turn == 1 ? "M" : "T";
                    return $"{diaLabel} {turnoLabel}";
                })
                .Distinct()
                .ToList();

            var slotsResumen = slotLabels.Count == 0
                ? "sin_asignacion"
                : string.Join(", ", slotLabels);

            warnings.Add($"REFUERZOS ENT: presupuesto={maxExtras}, asignados={assignedExtras}, topSlots={slotsResumen}");
        }

        return FinalizeAssignments(states);
    }

    private static (int[] noct, int[] man, int[] tar) BuildWorkArrays(
        IReadOnlyList<DateOnly> diasSemana,
        IReadOnlyList<ResumenDia> resumenDias)
    {
        var noct = new int[7];
        var man = new int[7];
        var tar = new int[7];
        var resumenDict = resumenDias.ToDictionary(r => r.Dia, r => r);

        for (int d = 0; d < 7; d++)
        {
            if (resumenDict.TryGetValue(diasSemana[d], out var resumen))
            {
                noct[d] = resumen.Noct_Tot;
                man[d] = resumen.Man_Tot;
                tar[d] = resumen.Tar_Tot;
            }
        }

        return (noct, man, tar);
    }

    private static List<EmployeeState> BuildStates(IReadOnlyList<TurnoEmployee> employees)
    {
        return employees
            .Select(e => new EmployeeState(e.Name, e.IsHoliday))
            .OrderBy(e => e.Name)
            .ToList();
    }

    private static int AssignByLeastWorked(
        List<EmployeeState> states,
        int day,
        int shift,
        int required,
        string zoneKey)
    {
        int assigned = 0;
        var candidates = states
            .Where(s => s.CanWork(day))
            .OrderBy(s => s.WorkedDays)
            .ThenBy(s => s.NightCount)
            .ThenBy(s => s.Name)
            .ToList();

        foreach (var candidate in candidates)
        {
            if (assigned >= required)
                break;

            candidate.Assign(day, shift, zoneKey);
            assigned++;
        }

        return assigned;
    }

    private static bool AssignSingle(List<EmployeeState> states, int day, int shift, string zoneKey)
    {
        var candidate = states
            .Where(s => s.CanWork(day))
            .OrderBy(s => s.WorkedDays)
            .ThenBy(s => s.NightCount)
            .ThenBy(s => s.Name)
            .FirstOrDefault();

        if (candidate == null)
            return false;

        candidate.Assign(day, shift, zoneKey);
        return true;
    }

    private static bool TryAssignExtra(List<EmployeeState> states, int day, int shift)
    {
        return AssignSingle(states, day, shift, "ENT");
    }

    private static bool AssignRotatingNight(
        List<EmployeeState> rotationPool,
        int day,
        ref int rotationIndex)
    {
        if (rotationPool.Count == 0)
            return false;

        int startIndex = rotationIndex % rotationPool.Count;
        int attempts = 0;
        int index = startIndex;

        while (attempts < rotationPool.Count)
        {
            var candidate = rotationPool[index];
            if (candidate.CanWork(day))
            {
                candidate.Assign(day, 0, "ENT");
                rotationIndex = (index + 1) % rotationPool.Count;
                return true;
            }

            index = (index + 1) % rotationPool.Count;
            attempts++;
        }

        return false;
    }

    private static List<TurnoRow> FinalizeAssignments(List<EmployeeState> states)
    {
        foreach (var state in states)
        {
            if (state.IsHoliday)
            {
                for (int d = 0; d < 7; d++)
                {
                    state.Assignments[d] = Holidays;
                }
                continue;
            }

            for (int d = 0; d < 7; d++)
            {
                if (string.IsNullOrWhiteSpace(state.Assignments[d]))
                {
                    state.Assignments[d] = Off;
                }
            }
        }

        return states.Select(s => s.ToRow()).ToList();
    }

    private readonly record struct ExtraSlot(int DayIndex, int Turn, int Work);

    private class EmployeeState
    {
        public EmployeeState(string name, bool isHoliday)
        {
            Name = name;
            IsHoliday = isHoliday;
        }

        public string Name { get; }
        public bool IsHoliday { get; }
        public int WorkedDays { get; private set; }
        public int NightCount { get; private set; }
        public string[] Assignments { get; } = new string[7];

        public bool CanWork(int day)
        {
            return !IsHoliday && WorkedDays < 5 && string.IsNullOrWhiteSpace(Assignments[day]);
        }

        public void Assign(int day, int shift, string zoneKey)
        {
            Assignments[day] = BuildShiftLabel(zoneKey, shift);
            WorkedDays++;
            if (shift == 0)
            {
                NightCount++;
            }
        }

        public TurnoRow ToRow()
        {
            return new TurnoRow
            {
                Empleado = Name,
                D0 = Assignments[0],
                D1 = Assignments[1],
                D2 = Assignments[2],
                D3 = Assignments[3],
                D4 = Assignments[4],
                D5 = Assignments[5],
                D6 = Assignments[6],
                IsHoliday = IsHoliday
            };
        }
    }

    private static string BuildShiftLabel(string zoneKey, int shift)
    {
        if (zoneKey == "REC")
        {
            return shift switch
            {
                0 => RecN,
                1 => RecM,
                _ => RecT
            };
        }

        return shift switch
        {
            0 => EntN,
            1 => EntM,
            _ => EntT
        };
    }
}
