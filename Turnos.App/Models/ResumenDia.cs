namespace Turnos.App.Models;

public class ResumenDia
{
    public DateOnly Dia { get; set; }
    public string DiaLabel { get; set; } = "";
    public int Noct_Ent { get; set; }
    public int Noct_Sal { get; set; }
    public int Noct_Tot => Noct_Ent + Noct_Sal;
    public int Man_Ent { get; set; }
    public int Man_Sal { get; set; }
    public int Man_Tot => Man_Ent + Man_Sal;
    public int Tar_Ent { get; set; }
    public int Tar_Sal { get; set; }
    public int Tar_Tot => Tar_Ent + Tar_Sal;
    public int TotalEnt => Noct_Ent + Man_Ent + Tar_Ent;
    public int TotalSal => Noct_Sal + Man_Sal + Tar_Sal;
    public int TotalGen => TotalEnt + TotalSal;
}
