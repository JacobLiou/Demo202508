using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace OTMS
{
    public class MainForm : Form
    {
        // UI Controls
        private Label lblHeader;

        private Button btnInit;
        private Button btnSetting;
        private Label lblServerInfo;
        private ListView lvClients;
        private ListView lvRequests;
        private RichTextBox rtbLog;
        private RichTextBox rtbErr;

        // Components
        private Settings _settings = Settings.Default();

        private UiSink _uiSinkAll;
        private UiSink _uiSinkErr;
        private Logger _logger;
        private TcpServer? _server;
        private MeasurementWorker? _worker;
        private FlaInstrumentAdapter? _fla;
        private OpticalSwitchController? _switch;
        private CancellationTokenSource _cts = new();

        public MainForm()
        {
            Text = "Optical Share Manager System -- OTMS";
            Width = 1100; Height = 700;
            StartPosition = FormStartPosition.CenterScreen;

            BuildLayout();
            InitLogger();
            InitRuntime();
        }

        private void BuildLayout()
        {
            lblHeader = new Label { Text = "(C) Molex Confidential-Internal Information(C) Molex, LLC – All Rights Reserved.", Dock = DockStyle.Top, Height = 24, TextAlign = ContentAlignment.MiddleCenter };
            Controls.Add(lblHeader);

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 60 };
            btnInit = new Button { Text = "Init Device", Width = 120, Height = 32, Left = 10, Top = 12 };
            btnSetting = new Button { Text = "Setting", Width = 120, Height = 32, Left = 140, Top = 12 };
            lblServerInfo = new Label { Text = "Server IP:" + GetLocalIPv4() + $" Port:{_settings.ListenPort}", Left = 280, Top = 18, AutoSize = true };
            btnInit.Click += (_, __) => InitDevice();
            btnSetting.Click += (_, __) => OpenSettings();
            topPanel.Controls.Add(btnInit);
            topPanel.Controls.Add(btnSetting);
            topPanel.Controls.Add(lblServerInfo);
            Controls.Add(topPanel);

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 340 };
            Controls.Add(split);

            // Upper half: Clients + Log
            var upperSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 520 };
            split.Panel1.Controls.Add(upperSplit);

            var grpClients = new GroupBox { Text = "Clients", Dock = DockStyle.Fill };
            lvClients = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
            lvClients.Columns.Add("ClientID", 100);
            lvClients.Columns.Add("Remote", 220);
            lvClients.Columns.Add("Status", 150);
            grpClients.Controls.Add(lvClients);
            upperSplit.Panel1.Controls.Add(grpClients);

            var grpLog = new GroupBox { Text = "Log", Dock = DockStyle.Fill };
            rtbLog = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.LightGreen, Font = new Font("Consolas", 10) };
            grpLog.Controls.Add(rtbLog);
            upperSplit.Panel2.Controls.Add(grpLog);

            // Lower half: Requests + ErrorLog
            var lowerSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 520 };
            split.Panel2.Controls.Add(lowerSplit);

            var grpReq = new GroupBox { Text = "Request", Dock = DockStyle.Fill };
            lvRequests = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
            lvRequests.Columns.Add("SlotID", 80);
            lvRequests.Columns.Add("ClientID", 100);
            lvRequests.Columns.Add("UserID", 100);
            lvRequests.Columns.Add("ClientIP", 220);
            lvRequests.Columns.Add("Mode", 120);
            grpReq.Controls.Add(lvRequests);
            lowerSplit.Panel1.Controls.Add(grpReq);

            var grpErr = new GroupBox { Text = "ErrorLog", Dock = DockStyle.Fill };
            rtbErr = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.OrangeRed, Font = new Font("Consolas", 10) };
            grpErr.Controls.Add(rtbErr);
            lowerSplit.Panel2.Controls.Add(grpErr);
        }

        private void InitLogger()
        {
            _uiSinkAll = new UiSink(AppendLogLine);
            _uiSinkErr = new UiSink(AppendErrLine);

            var logsDir = System.IO.Path.Combine(AppContext.BaseDirectory, "logs");
            System.IO.Directory.CreateDirectory(logsDir);

            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(System.IO.Path.Combine("logs", "server-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14,
                              outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Sink(_uiSinkAll)
                .WriteTo.Logger(lc => lc.Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error).WriteTo.Sink(_uiSinkErr))
                .CreateLogger();

            Log.Logger = _logger; // allow other classes using static Log
        }

        private void InitRuntime()
        {
            // instantiate components with current settings
            _fla = new FlaInstrumentAdapter(_settings.FlaHost, _settings.FlaPort);
            _switch = new OpticalSwitchController(_settings.SwitchCom, _settings.SwitchBaud, _settings.SwitchIndex, _settings.SwitchInput);
            var queue = System.Threading.Channels.Channel.CreateUnbounded<MeasureTask>(new System.Threading.Channels.UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
            _server = new TcpServer(_settings.ListenPort, queue);
            _worker = new MeasurementWorker(queue, _server, _fla, _switch);

            // subscribe to server events
            _server.ClientConnected += (id, remote) => { BeginInvoke(() => AddClient(id, remote)); Log.Information("Client connected: {Remote}", remote); };
            _server.ClientDisconnected += (id, remote) => { BeginInvoke(() => RemoveClient(id)); Log.Information("Client disconnected: {Remote}", remote); };
            _server.TaskQueued += (task) => { BeginInvoke(() => AddRequest(task)); Log.Information("Queued task {TaskId} ch={Channel} mode={Mode}", task.TaskId, task.Channel, task.Mode); };
            _server.ResultPushed += (taskId, ok) => { Log.Information("Result pushed {TaskId} success={Ok}", taskId, ok); };

            // start services
            _ = _server.StartAsync(_cts.Token);
            _ = _worker.StartAsync(_cts.Token);
            Log.Information("Service started on {IP}:{Port}", GetLocalIPv4(), _settings.ListenPort);
        }

        private void InitDevice()
        {
            try
            {
                Log.Information("Init device started");
                // switch: try set to channel 1
                _switch?.SwitchToOutputAsync(1, _cts.Token).GetAwaiter().GetResult();
                // fla: connect and handshake, then disconnect
                _fla?.ConnectAsync(_cts.Token).GetAwaiter().GetResult();
                Log.Information("FLA handshake OK");
                _fla?.DisconnectAsync().GetAwaiter().GetResult();
                Log.Information("Init device success");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Init device failed");
            }
        }

        private void OpenSettings()
        {
            using var dlg = new SettingsForm(_settings);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _settings = dlg.GetSettings();
                lblServerInfo.Text = "Server IP:" + GetLocalIPv4() + $" Port:{_settings.ListenPort}";
                Log.Information("Settings updated: {Json}", JsonSerializer.Serialize(_settings));
            }
        }

        // UI helpers
        private void AppendLogLine(string line)
        { if (rtbLog.IsHandleCreated) rtbLog.BeginInvoke(new Action(() => { rtbLog.AppendText(line + "\n"); rtbLog.ScrollToCaret(); })); }

        private void AppendErrLine(string line)
        { if (rtbErr.IsHandleCreated) rtbErr.BeginInvoke(new Action(() => { rtbErr.AppendText(line + "\n"); rtbErr.ScrollToCaret(); })); }

        private void AddClient(string id, string remote)
        { var item = new ListViewItem(new[] { id, remote, "Connected" }); item.Name = id; lvClients.Items.Add(item); }

        private void RemoveClient(string id)
        { if (lvClients.Items.ContainsKey(id)) lvClients.Items.RemoveByKey(id); }

        private void AddRequest(MeasureTask t)
        { lvRequests.Items.Add(new ListViewItem(new[] { "-", t.Session.Id, "-", t.Session.RemoteEndPoint?.ToString() ?? "", t.Mode })); }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _cts.Cancel();
            base.OnFormClosed(e);
        }

        private static string GetLocalIPv4()
        {
            string ip = "127.0.0.1";
            try
            {
                foreach (var ni in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (ni.AddressFamily == AddressFamily.InterNetwork) { ip = ni.ToString(); break; }
                }
            }
            catch { }
            return ip;
        }
    }
}