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
        IReadOnlyList<DateOnly> diasSemana,
        IReadOnlyList<int> recNoctWork,
        IReadOnlyList<int> recManWork,
        IReadOnlyList<int> recTarWork,
        IReadOnlyList<TurnoEmployee> recepcion,
        IReadOnlyList<TurnoEmployee> entradas)
    {
        var result = new TurnosPreviewResult();
        var cultura = CultureInfo.GetCultureInfo("es-ES");

        var recNeed = BuildRecNeed(recNoctWork, recManWork, recTarWork);
        var recAssignments = AssignZone("REC", recepcion, recNeed, false, cultura, diasSemana, result.Warnings);
        var entNeed = BuildEntNeed();
        var entAssignments = AssignZone("ENT", entradas, entNeed, true, cultura, diasSemana, result.Warnings);

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

    private static int[,] BuildEntNeed()
    {
        var need = new int[7, 3];
        for (int d = 0; d < 7; d++)
        {
            need[d, 0] = 1;
            need[d, 1] = 2;
            need[d, 2] = 2;
        }
        return need;
    }

    private static List<TurnoRow> AssignZone(
        string zoneKey,
        IReadOnlyList<TurnoEmployee> employees,
        int[,] need,
        bool rotateNight,
        CultureInfo cultura,
        IReadOnlyList<DateOnly> diasSemana,
        List<string> warnings)
    {
        var states = employees
            .Select(e => new EmployeeState(e.Name, e.IsHoliday))
            .OrderBy(e => e.Name)
            .ToList();

        var staffing = new int[7, 3];

        int rotationIndex = 0;
        var rotationPool = states.Where(s => !s.IsHoliday).OrderBy(s => s.Name).ToList();

        for (int d = 0; d < 7; d++)
        {
            for (int t = 0; t < 3; t++)
            {
                int required = need[d, t];
                int assigned = 0;

                if (rotateNight && t == 0)
                {
                    assigned = AssignWithRotation(rotationPool, d, t, required, staffing, zoneKey, ref rotationIndex);
                }
                else
                {
                    assigned = AssignByLeastWorked(states, d, t, required, staffing, zoneKey);
                }

                if (assigned < required)
                {
                    var diaLabel = diasSemana[d].ToString("ddd dd/MM", cultura);
                    var turnoLabel = t == 0 ? "N" : t == 1 ? "M" : "T";
                    warnings.Add($"{zoneKey} {diaLabel} {turnoLabel} ({required - assigned})");
                }
            }
        }

        // Completar hasta 5 días trabajados si hace falta (permitiendo exceso)
        foreach (var state in states.Where(s => !s.IsHoliday))
        {
            while (state.WorkedDays < 5)
            {
                if (!TryAssignExtraShift(state, states, staffing, zoneKey))
                {
                    break;
                }
            }
        }

        // Marcar OFF y HOLYDAYS
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

    private static int AssignByLeastWorked(
        List<EmployeeState> states,
        int day,
        int shift,
        int required,
        int[,] staffing,
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
            staffing[day, shift]++;
            assigned++;
        }

        return assigned;
    }

    private static int AssignWithRotation(
        List<EmployeeState> rotationPool,
        int day,
        int shift,
        int required,
        int[,] staffing,
        string zoneKey,
        ref int rotationIndex)
    {
        int assigned = 0;
        if (rotationPool.Count == 0)
            return 0;

        int startIndex = rotationIndex % rotationPool.Count;
        int attempts = 0;
        int index = startIndex;

        while (assigned < required && attempts < rotationPool.Count * 2)
        {
            var candidate = rotationPool[index];
            if (candidate.CanWork(day))
            {
                candidate.Assign(day, shift, zoneKey);
                staffing[day, shift]++;
                assigned++;
            }

            index = (index + 1) % rotationPool.Count;
            attempts++;
        }

        rotationIndex = (startIndex + assigned) % rotationPool.Count;
        return assigned;
    }

    private static bool TryAssignExtraShift(
        EmployeeState state,
        List<EmployeeState> allStates,
        int[,] staffing,
        string zoneKey)
    {
        for (int d = 0; d < 7; d++)
        {
            if (!state.CanWork(d))
                continue;

            int shift = GetLowestStaffingShift(d, staffing);
            state.Assign(d, shift, zoneKey);
            staffing[d, shift]++;
            return true;
        }

        return false;
    }

    private static int GetLowestStaffingShift(int day, int[,] staffing)
    {
        int minShift = 0;
        int minValue = staffing[day, 0];
        for (int t = 1; t < 3; t++)
        {
            if (staffing[day, t] < minValue)
            {
                minValue = staffing[day, t];
                minShift = t;
            }
        }
        return minShift;
    }

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
