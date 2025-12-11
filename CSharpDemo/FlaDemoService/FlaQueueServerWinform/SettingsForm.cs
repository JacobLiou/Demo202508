namespace OTMS
{
    public class SettingsForm : Form
    {
        private TextBox txtListenPort = new();
        private TextBox txtFlaHost = new();
        private TextBox txtFlaPort = new();
        private TextBox txtCom = new();
        private TextBox txtBaud = new();
        private TextBox txtIndex = new();
        private TextBox txtInput = new();
        private Button btnOk = new() { Text = "OK" };
        private Button btnCancel = new() { Text = "Cancel" };
        private Settings _cur;

        public SettingsForm(Settings s)
        {
            _cur = s;
            Text = "Settings"; Width = 420; Height = 360; StartPosition = FormStartPosition.CenterParent;
            var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8, Padding = new Padding(10) };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            Controls.Add(tbl);
            tbl.Controls.Add(new Label { Text = "Listen Port", AutoSize = true }, 0, 0); txtListenPort.Text = _cur.ListenPort.ToString(); tbl.Controls.Add(txtListenPort, 1, 0);
            tbl.Controls.Add(new Label { Text = "FLA Host", AutoSize = true }, 0, 1); txtFlaHost.Text = _cur.FlaHost; tbl.Controls.Add(txtFlaHost, 1, 1);
            tbl.Controls.Add(new Label { Text = "FLA Port", AutoSize = true }, 0, 2); txtFlaPort.Text = _cur.FlaPort.ToString(); tbl.Controls.Add(txtFlaPort, 1, 2);
            tbl.Controls.Add(new Label { Text = "Switch COM", AutoSize = true }, 0, 3); txtCom.Text = _cur.SwitchCom; tbl.Controls.Add(txtCom, 1, 3);
            tbl.Controls.Add(new Label { Text = "Switch Baud", AutoSize = true }, 0, 4); txtBaud.Text = _cur.SwitchBaud.ToString(); tbl.Controls.Add(txtBaud, 1, 4);
            tbl.Controls.Add(new Label { Text = "Switch Index", AutoSize = true }, 0, 5); txtIndex.Text = _cur.SwitchIndex.ToString(); tbl.Controls.Add(txtIndex, 1, 5);
            tbl.Controls.Add(new Label { Text = "Switch Input", AutoSize = true }, 0, 6); txtInput.Text = _cur.SwitchInput.ToString(); tbl.Controls.Add(txtInput, 1, 6);
            var pnlBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            pnlBtns.Controls.Add(btnOk); pnlBtns.Controls.Add(btnCancel);
            tbl.Controls.Add(pnlBtns, 0, 7); tbl.SetColumnSpan(pnlBtns, 2);
            btnOk.Click += (_, __) => { DialogResult = DialogResult.OK; Close(); };
            btnCancel.Click += (_, __) => { DialogResult = DialogResult.Cancel; Close(); };
        }

        public Settings GetSettings()
        {
            return new Settings(
                int.TryParse(txtListenPort.Text, out var lp) ? lp : 5600,
                txtFlaHost.Text,
                int.TryParse(txtFlaPort.Text, out var fp) ? fp : 4300,
                txtCom.Text,
                int.TryParse(txtBaud.Text, out var bd) ? bd : 115200,
                int.TryParse(txtIndex.Text, out var idx) ? idx : 1,
                int.TryParse(txtInput.Text, out var inp) ? inp : 1
            );
        }
    }
}