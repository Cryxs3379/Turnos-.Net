using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Turnos.Net
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public partial class MainForm : Form
    {
        private TabControl tabControl = null!;
        private DataGridView dgvEntradasParking = null!;
        private DataGridView dgvEntradasRentACar = null!;
        private DataGridView dgvEntradasReservaParking = null!;
        private DataGridView dgvEntradaReservaRentACar = null!;
        private DataGridView dgvSalidasParking = null!;
        private DataGridView dgvSalidasRentACar = null!;
        private DataGridView dgvSalidaReservaParking = null!;

        private readonly string connectionString = "Server=192.168.0.5;" +
                                                   "Database=HELLE HOLLIS;" +
                                                   "Integrated Security=True;" +
                                                   "Encrypt=False;" +
                                                   "TrustServerCertificate=True;";

        private readonly DateTime fechaInicio = new DateTime(2026, 2, 2);
        private readonly DateTime fechaFin = new DateTime(2026, 2, 8);
        private readonly string lugar = "AER";

        public MainForm()
        {
            InitializeComponent();
            CargarTodosLosDatos();
        }

        private void InitializeComponent()
        {
            this.Text = "Turnos";
            this.Size = new System.Drawing.Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterScreen;

            // TabControl
            tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            this.Controls.Add(tabControl);

            // 1. Pestaña "Entradas Parking"
            CrearPestaña("Entradas Parking", out dgvEntradasParking, new[] { "No. Contrato", "Fecha", "Hora" });

            // 2. Pestaña "Entradas Rent a Car"
            CrearPestaña("Entradas Rent a Car", out dgvEntradasRentACar, new[] { "Nº Sucursal", "Fecha", "Hora" });

            // 3. Pestaña "Entradas Reserva Parking"
            CrearPestaña("Entradas Reserva Parking", out dgvEntradasReservaParking, new[] { "No. Reserva", "Fecha", "Hora" });

            // 4. Pestaña "Entrada Reserva Rent a Car"
            CrearPestaña("Entrada Reserva Rent a Car", out dgvEntradaReservaRentACar, new[] { "Nº Reserva", "Fecha", "Hora" });

            // 5. Pestaña "Salidas Parking"
            CrearPestaña("Salidas Parking", out dgvSalidasParking, new[] { "No. Contrato", "Fecha", "Hora" });

            // 6. Pestaña "Salidas Rent a Car"
            CrearPestaña("Salidas Rent a Car", out dgvSalidasRentACar, new[] { "Nº Reserva", "Fecha", "Hora", "FL" });

            // 7. Pestaña "Salida Reserva Parking"
            CrearPestaña("Salida Reserva Parking", out dgvSalidaReservaParking, new[] { "No. Reserva", "Fecha", "Hora" });
        }

        private void CrearPestaña(string nombre, out DataGridView dgv, string[] columnas)
        {
            var tabPage = new TabPage(nombre);
            tabControl.TabPages.Add(tabPage);

            // Panel contenedor
            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            tabPage.Controls.Add(panel);

            // Label para el contador
            var lblContador = new Label();
            lblContador.Text = "Total: 0";
            lblContador.Dock = DockStyle.Bottom;
            lblContador.Height = 30;
            lblContador.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            lblContador.Padding = new Padding(0, 0, 20, 0);
            lblContador.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            lblContador.BackColor = System.Drawing.Color.LightGray;
            panel.Controls.Add(lblContador);
            lblContador.BringToFront();

            // DataGridView
            dgv = new DataGridView();
            dgv.Dock = DockStyle.Fill;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.ReadOnly = true;
            dgv.AllowUserToAddRows = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            panel.Controls.Add(dgv);

            // Guardar referencia al label en el Tag del DataGridView
            dgv.Tag = lblContador;

            foreach (var columna in columnas)
            {
                dgv.Columns.Add(columna.Replace(" ", "").Replace(".", "").Replace("º", ""), columna);
            }
        }

        private void CargarTodosLosDatos()
        {
            CargarDatosEntradasParking();
            CargarDatosEntradasRentACar();
            CargarDatosEntradasReservaParking();
            CargarDatosEntradaReservaRentACar();
            CargarDatosSalidasParking();
            CargarDatosSalidasRentACar();
            CargarDatosSalidaReservaParking();
        }

        private void CargarDatosEntradasParking()
        {
            try
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

                dgvEntradasParking.Rows.Clear();
                ActualizarContador(dgvEntradasParking, 0);
                EjecutarConsulta(query, dgvEntradasParking, new[] { "@FechaInicio", "@FechaFin", "@Lugar" });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar Entradas Parking: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CargarDatosEntradasRentACar()
        {
            try
            {
                var query = @"
select ct.[Nº sucursal], convert(varchar(10),ct.[Fecha entrada], 103) as CtDate, 
convert(varchar(5), ct.[Hora entrada], 108) as CtHora
from [HELLE HOLLIS].dbo.vwHistContratos ct
where ct.[Fecha entrada] >= @FechaInicio and ct.[Fecha entrada] <= @FechaFin
and ct.[Lugar entrada] = @Lugar
order by 2,3";

                dgvEntradasRentACar.Rows.Clear();
                ActualizarContador(dgvEntradasRentACar, 0);
                EjecutarConsulta(query, dgvEntradasRentACar, new[] { "@FechaInicio", "@FechaFin", "@Lugar" });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar Entradas Rent a Car: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CargarDatosEntradasReservaParking()
        {
            try
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

                dgvEntradasReservaParking.Rows.Clear();
                ActualizarContador(dgvEntradasReservaParking, 0);
                EjecutarConsulta(query, dgvEntradasReservaParking, new[] { "@FechaInicio", "@FechaFin", "@Lugar" });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar Entradas Reserva Parking: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CargarDatosEntradaReservaRentACar()
        {
            try
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

                dgvEntradaReservaRentACar.Rows.Clear();
                ActualizarContador(dgvEntradaReservaRentACar, 0);
                EjecutarConsulta(query, dgvEntradaReservaRentACar, new[] { "@FechaInicio", "@FechaFin", "@Lugar" });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar Entrada Reserva Rent a Car: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CargarDatosSalidasParking()
        {
            try
            {
                var query = @"
select ct.[No_ Contrato], convert(varchar(10), ct.[Fecha Prox_ Recogida], 103) as CtDate, convert(varchar(5), ct.[Hora Prox_ Recogida], 108) as CtHora
from [HELLE HOLLIS].dbo.vwContServ ct
where ct.[Fecha Prox_ Recogida] >= @FechaInicio and ct.[Fecha Prox_ Recogida] <= @FechaFin
and ct.[Lugar Fin Servicio] = @Lugar
order by 2,3";

                dgvSalidasParking.Rows.Clear();
                ActualizarContador(dgvSalidasParking, 0);
                EjecutarConsulta(query, dgvSalidasParking, new[] { "@FechaInicio", "@FechaFin", "@Lugar" });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar Salidas Parking: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CargarDatosSalidasRentACar()
        {
            try
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

                dgvSalidasRentACar.Rows.Clear();
                ActualizarContador(dgvSalidasRentACar, 0);
                EjecutarConsulta(query, dgvSalidasRentACar, new[] { "@FechaInicio", "@FechaFin", "@Lugar" });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar Salidas Rent a Car: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CargarDatosSalidaReservaParking()
        {
            try
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

                dgvSalidaReservaParking.Rows.Clear();
                ActualizarContador(dgvSalidaReservaParking, 0);
                EjecutarConsulta(query, dgvSalidaReservaParking, new[] { "@FechaInicio", "@FechaFin", "@Lugar" });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar Salida Reserva Parking: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EjecutarConsulta(string query, DataGridView dgv, string[] nombresParametros)
        {
            using var connection = new SqlConnection(connectionString);
            using var command = new SqlCommand(query, connection);

            command.Parameters.Add(nombresParametros[0], SqlDbType.Date).Value = fechaInicio;
            command.Parameters.Add(nombresParametros[1], SqlDbType.Date).Value = fechaFin;
            command.Parameters.Add(nombresParametros[2], SqlDbType.VarChar, 50).Value = lugar;

            connection.Open();

            int contador = 0;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var valores = new object[reader.FieldCount];
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    valores[i] = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i).ToString() ?? string.Empty;
                }
                dgv.Rows.Add(valores);
                contador++;
            }

            // Actualizar el contador en el Label
            ActualizarContador(dgv, contador);
        }

        private void ActualizarContador(DataGridView dgv, int contador)
        {
            if (dgv.Tag is Label lblContador)
            {
                lblContador.Text = $"Total: {contador}";
            }
        }
    }
}
