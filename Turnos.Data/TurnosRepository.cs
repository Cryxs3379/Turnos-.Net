using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Turnos.Data;

public class TurnosRepository
{
    private readonly string connectionString;

    public TurnosRepository(string connectionString)
    {
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<List<string[]>> GetEntradasParkingAsync(DateTime ini, DateTime fin, string lugar, CancellationToken ct)
    {
        var query = @"
select
  ct.[No_ Contrato],
  convert(varchar(10), ct.[Fecha Prox_ Entrega], 103) as CtDate,
  convert(varchar(5), ct.[Hora Prox_ Entrega], 108) as CtHora
from [HELLE HOLLIS].dbo.vwContServ ct
where ct.[Fecha Prox_ Entrega] >= @FechaInicio
  and ct.[Fecha Prox_ Entrega] <= @FechaFin
  and ct.[Lugar Fin Servicio] = @Lugar
order by 2,3";

        return await ExecuteAsync(query, ini, fin, lugar, ct);
    }

    public async Task<List<string[]>> GetEntradasRentACarAsync(DateTime ini, DateTime fin, string lugar, CancellationToken ct)
    {
        var query = @"
select ct.[Nº sucursal], convert(varchar(10),ct.[Fecha entrada], 103) as CtDate, 
convert(varchar(5), ct.[Hora entrada], 108) as CtHora
from [HELLE HOLLIS].dbo.vwHistContratos ct
where ct.[Fecha entrada] >= @FechaInicio and ct.[Fecha entrada] <= @FechaFin
and ct.[Lugar entrada] = @Lugar
order by 2,3";

        return await ExecuteAsync(query, ini, fin, lugar, ct);
    }

    public async Task<List<string[]>> GetEntradasReservaParkingAsync(DateTime ini, DateTime fin, string lugar, CancellationToken ct)
    {
        var query = @"
select rs.[No_ Reserva], convert(varchar(10), rs.[Fecha Comienzo Servicio], 103) as RsDate, convert(varchar(5), rs.[Hora Comienzo Servicio], 108) as RsHora
from [HELLE HOLLIS].dbo.vwResServ rs
where rs.[Fecha Comienzo Servicio]>= @FechaInicio and rs.[Fecha Comienzo Servicio] <= @FechaFin
and rs.[Lugar Fin Servicio] = @Lugar
and rs.[No_ Reserva] IN
	(select pl.[No_ Documento]
	 from [HELLE HOLLIS].dbo.[HELLE AUTO, S_A_U_$Planning servicios] pl)
order by 2,3";

        return await ExecuteAsync(query, ini, fin, lugar, ct);
    }

    public async Task<List<string[]>> GetEntradaReservaRentACarAsync(DateTime ini, DateTime fin, string lugar, CancellationToken ct)
    {
        var query = @"
select rs.[Nº reserva], convert(varchar(10),rs.[Fecha entrada], 103) as RsDate, 
convert(varchar(5), rs.[Hora entrada], 108) as RsHora
from [HELLE HOLLIS].dbo.vwReserva rs
where rs.[Fecha entrada] >= @FechaInicio and rs.[Fecha entrada] <= @FechaFin
and rs.[Lugar entrada] = @Lugar
and rs.[Nº reserva] IN
	(select pl.[Nº Documento]
	 from [HELLE HOLLIS].dbo.vwPlanning pl)
order by 2,3";

        return await ExecuteAsync(query, ini, fin, lugar, ct);
    }

    public async Task<List<string[]>> GetSalidasParkingAsync(DateTime ini, DateTime fin, string lugar, CancellationToken ct)
    {
        var query = @"
select ct.[No_ Contrato], convert(varchar(10), ct.[Fecha Prox_ Recogida], 103) as CtDate, convert(varchar(5), ct.[Hora Prox_ Recogida], 108) as CtHora
from [HELLE HOLLIS].dbo.vwContServ ct
where ct.[Fecha Prox_ Recogida] >= @FechaInicio and ct.[Fecha Prox_ Recogida] <= @FechaFin
and ct.[Lugar Fin Servicio] = @Lugar
order by 2,3";

        return await ExecuteAsync(query, ini, fin, lugar, ct);
    }

    public async Task<List<string[]>> GetSalidasRentACarAsync(DateTime ini, DateTime fin, string lugar, CancellationToken ct)
    {
        var query = @"
select rs.[Nº reserva], convert(varchar(10), rs.[Fecha salida], 103) as RsDate, convert(varchar(5), 
rs.[Hora salida], 108) as RsHora, 
case
  when ex.[Cód_ extra] is not null then 'FL'
end as FL
from [HELLE HOLLIS].dbo.vwReserva rs
left join [HELLE HOLLIS].dbo.vwExtras ex on rs.[Nº reserva] = ex.Documento 
and (ex.[Cód_ extra] = 'EX9' or ex.[Cód_ extra] = 'EX19' or ex.[Cód_ extra] = 'EX25')
where rs.[Fecha salida] >= @FechaInicio and rs.[Fecha salida] <= @FechaFin
and rs.[Lugar salida] = @Lugar
and rs.[Nº reserva] IN
	(select pl.[Nº Documento]
	 from [HELLE HOLLIS].dbo.vwPlanning pl)
order by 2,3";

        return await ExecuteAsync(query, ini, fin, lugar, ct);
    }

    public async Task<List<string[]>> GetSalidaReservaParkingAsync(DateTime ini, DateTime fin, string lugar, CancellationToken ct)
    {
        var query = @"
select rs.[No_ Reserva], convert(varchar(10), rs.[Fecha Fin Servicio], 103) as RsDate, convert(varchar(5), rs.[Hora Fin Servicio], 108) as RsHora
from [HELLE HOLLIS].dbo.vwResServ rs
where rs.[Fecha Fin Servicio] >= @FechaInicio and rs.[Fecha Fin Servicio] <= @FechaFin
and rs.[Lugar Fin Servicio] = @Lugar
and rs.[No_ Reserva] IN
	(select pl.[No_ Documento]
	 from [HELLE HOLLIS].dbo.[HELLE AUTO, S_A_U_$Planning servicios] pl)
order by 2,3";

        return await ExecuteAsync(query, ini, fin, lugar, ct);
    }

    private async Task<List<string[]>> ExecuteAsync(string sql, DateTime ini, DateTime fin, string lugar, CancellationToken ct)
    {
        var resultados = new List<string[]>();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@FechaInicio", SqlDbType.Date).Value = ini;
        command.Parameters.Add("@FechaFin", SqlDbType.Date).Value = fin;
        command.Parameters.Add("@Lugar", SqlDbType.VarChar, 50).Value = lugar;

        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var valores = new string[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
            {
                valores[i] = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i).ToString() ?? string.Empty;
            }
            resultados.Add(valores);
        }

        return resultados;
    }
}
