using System;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
using System.Drawing;
using System.Runtime.InteropServices;

public class MainForm : Form
{
    private readonly AppConfig _cfg;
    private readonly AudioSwitcher _audioSwitcher;
    private readonly BluetoothService _ble;

    private readonly ComboBox[] _inputBoxes = new ComboBox[8];
    private readonly ComboBox[] _outputBoxes = new ComboBox[8];
    private readonly Label[] _sideLabels = new Label[8];
    private readonly CheckBox _chkStartOnBoot = new CheckBox { Text = "Start on boot" };
    private readonly CheckBox _chkStartMinimized = new CheckBox { Text = "Start minimized" };
    private readonly NotifyIcon _tray = new NotifyIcon();
    private readonly ContextMenuStrip _trayMenu = new ContextMenuStrip();
    private readonly Label _lblBleStatus = new Label { AutoSize = true, Text = "BLE: Idle" };

    private TableLayoutPanel _table = null!;
    private Icon? _dynamicIcon;
    private int _activeSide = 0;
    private bool _bleIsConnected = false;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public MainForm(AppConfig cfg, AudioSwitcher audioSwitcher, BluetoothService ble)
    {
        _cfg = cfg;
        _audioSwitcher = audioSwitcher;
        _ble = ble;

        Text = "Timeular Audio Switcher";
        Width = 900;
        Height = 500;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        MaximizeBox = true;
        MinimumSize = new Size(700, 420);
        MaximumSize = new Size(1400, 900);

        // Top bar with Exit button (top-right)
        var topBar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = SystemColors.ControlLight };
        var btnExit = new Button
        {
            Text = "Exit",
            Dock = DockStyle.Right,
            Width = 80,
            FlatStyle = FlatStyle.System,
            TabStop = false
        };
        btnExit.Click += (_, __) => Application.Exit();
        topBar.Controls.Add(btnExit);

        _table = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, RowCount = 10, AutoSize = true };
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _table.CellPaint += Table_CellPaint;

        _table.Controls.Add(new Label { Text = "Side", AutoSize = true }, 0, 0);
        _table.Controls.Add(new Label { Text = "Input", AutoSize = true }, 1, 0);
        _table.Controls.Add(new Label { Text = "Output", AutoSize = true }, 2, 0);

        using var mm = new MMDeviceEnumerator();
        var inputs = GetSafeDeviceNames(mm, DataFlow.Capture, NAudio.CoreAudioApi.DeviceState.Active);
        var outputs = GetSafeDeviceNames(mm, DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active);
        var inputsWithBlank = new string[] { string.Empty }.Concat(inputs).ToArray();
        var outputsWithBlank = new string[] { string.Empty }.Concat(outputs).ToArray();

        for (int i = 0; i < 8; i++)
        {
            var row = i + 1;
            var lbl = new Label { Text = (i + 1).ToString(), AutoSize = true };
            _table.Controls.Add(lbl, 0, row);
            _sideLabels[i] = lbl;

            var cbIn = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            var cbOut = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cbIn.Items.AddRange(inputsWithBlank);
            cbOut.Items.AddRange(outputsWithBlank);
            cbIn.SelectedIndex = 0;
            cbOut.SelectedIndex = 0;

            var key = (i + 1).ToString();
            if (_cfg.SideToDevice.TryGetValue(key, out var side))
            {
                if (!string.IsNullOrWhiteSpace(side.Input))
                {
                    var inVal = inputs.FirstOrDefault(s => s != null && s.IndexOf(side.Input, StringComparison.InvariantCultureIgnoreCase) >= 0);
                    if (inVal != null) cbIn.SelectedItem = inVal;
                }
                if (!string.IsNullOrWhiteSpace(side.Output))
                {
                    var outVal = outputs.FirstOrDefault(s => s != null && s.IndexOf(side.Output, StringComparison.InvariantCultureIgnoreCase) >= 0);
                    if (outVal != null) cbOut.SelectedItem = outVal;
                }
            }

            int sideIdx = i + 1;
            cbIn.SelectionChangeCommitted += (_, __) => { OnDeviceChanged(sideIdx, isInput: true); SaveConfig(); };
            cbOut.SelectionChangeCommitted += (_, __) => { OnDeviceChanged(sideIdx, isInput: false); SaveConfig(); };

            _inputBoxes[i] = cbIn;
            _outputBoxes[i] = cbOut;
            _table.Controls.Add(cbIn, 1, row);
            _table.Controls.Add(cbOut, 2, row);
        }

        _chkStartOnBoot.Checked = StartupManager.IsEnabled() || _cfg.StartOnBoot;
        _chkStartMinimized.Checked = _cfg.StartMinimized;
        _chkStartOnBoot.CheckedChanged += (_, __) => SaveConfig();
        _chkStartMinimized.CheckedChanged += (_, __) => SaveConfig();

        // Bottom area: improved layout with a padded panel and spacing
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(10, 8, 10, 8) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = false };        
        flow.Controls.Add(_chkStartOnBoot);
        flow.Controls.Add(_chkStartMinimized);
        flow.Controls.Add(new Label { AutoSize = true, Text = " | " });
        flow.Controls.Add(_lblBleStatus);
        bottom.Controls.Add(flow);

        Controls.Add(_table);
        Controls.Add(bottom);
        Controls.Add(topBar); // add last Top panel to appear at the very top

        // Tray icon
        _tray.Icon = SystemIcons.Application;
        _tray.Visible = true;
        _tray.Text = "Timeular Audio Switcher";
        _tray.ContextMenuStrip = _trayMenu;
        _tray.DoubleClick += (_, __) => { Show(); ShowInTaskbar = true; WindowState = FormWindowState.Normal; Activate(); };

        BuildTrayMenu();

        // subscribe to BLE status changes and active side changes
        _ble.StatusChanged += OnBleStatusChanged;
        _ble.SideChanged += OnBleSideChanged;

        // Draw initial icon with status dot
        UpdateTrayIcon();

        FormClosing += OnFormClosing;
        Resize += OnResize;
    }

    private void Table_CellPaint(object? sender, TableLayoutCellPaintEventArgs e)
    {
        // Highlight active data rows (row 1..8) with a darker grey background across the full row
        if (_activeSide >= 1 && _activeSide <= 8 && e.Row == _activeSide)
        {
            using var b = new SolidBrush(Color.FromArgb(200, 200, 200)); // darker grey
            e.Graphics.FillRectangle(b, e.CellBounds);
        }
    }

    private void OnDeviceChanged(int side, bool isInput)
    {
        try
        {
            var key = side.ToString();
            var inputName = _inputBoxes[side - 1].SelectedItem as string ?? string.Empty;
            var outputName = _outputBoxes[side - 1].SelectedItem as string ?? string.Empty;

            if (!_cfg.SideToDevice.TryGetValue(key, out var current))
            {
                current = new SideAudioConfig(inputName, outputName);
            }

            var newConfig = isInput
                ? new SideAudioConfig(inputName, current.Output)
                : new SideAudioConfig(current.Input, outputName);

            _cfg.SideToDevice[key] = newConfig; // keep runtime mapping in sync

            if (side == _activeSide)
            {
                if (isInput && !string.IsNullOrWhiteSpace(newConfig.Input))
                {
                    Logger.Log($"Applying input change for active side {side}: '{newConfig.Input}'");
                    _audioSwitcher.SetDefaultInputDevice(newConfig.Input);
                }
                if (!isInput && !string.IsNullOrWhiteSpace(newConfig.Output))
                {
                    Logger.Log($"Applying output change for active side {side}: '{newConfig.Output}'");
                    _audioSwitcher.SetDefaultPlaybackDevice(newConfig.Output);
                }
            }
        }
        catch (Exception ex)
        {
            try { Logger.Log("Error applying device change: " + ex.Message); } catch { }
        }
    }

    private void OnBleSideChanged(int side)
    {
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(() => { _activeSide = side; UpdateTrayIcon(); UpdateActiveSideHighlight(); })); } catch { }
        }
        else
        {
            _activeSide = side;
            UpdateTrayIcon();
            UpdateActiveSideHighlight();
        }
    }

    private void UpdateActiveSideHighlight()
    {
        // Reset all combo/label colors while row background is handled in CellPaint
        for (int i = 0; i < 8; i++)
        {
            _inputBoxes[i].BackColor = SystemColors.Window;
            _outputBoxes[i].BackColor = SystemColors.Window;
            _sideLabels[i].BackColor = SystemColors.Control;
            _sideLabels[i].ForeColor = SystemColors.ControlText;
        }

        if (_activeSide >= 1 && _activeSide <= 8)
        {
            int idx = _activeSide - 1;
            var grey = Color.FromArgb(192, 192, 192); // darker cohesive control background
            _inputBoxes[idx].BackColor = grey;
            _outputBoxes[idx].BackColor = grey;
            _sideLabels[idx].BackColor = grey;
            _sideLabels[idx].ForeColor = Color.Black;
        }

        _table.Invalidate();
    }

    private void UpdateTrayIcon()
    {
        try
        {
            var newIcon = CreateTrayIcon(_activeSide, _bleIsConnected);
            var old = _dynamicIcon;
            _dynamicIcon = newIcon;
            _tray.Icon = newIcon;
            _tray.Text = _activeSide >= 1 ? $"Timeular Audio Switcher - Side {_activeSide}" : "Timeular Audio Switcher";
            old?.Dispose();
        }
        catch (Exception ex)
        {
            try { Logger.Log("Failed to update tray icon: " + ex.Message); } catch { }
        }
    }

    private static Icon CreateTrayIcon(int number, bool isConnected)
    {
        var size = SystemInformation.SmallIconSize;
        using var bmp = new Bitmap(size.Width, size.Height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Base circle for side number
            var padding = Math.Max(1, size.Width / 16);
            var rect = new Rectangle(padding, padding, size.Width - padding * 2, size.Height - padding * 2);
            using var bgBrush = new SolidBrush(Color.FromArgb(0x2D, 0x89, 0xEF)); // blue
            using var pen = new Pen(Color.White, Math.Max(1f, size.Width / 16f));
            g.FillEllipse(bgBrush, rect);
            g.DrawEllipse(pen, rect);

            // Draw number if valid
            if (number >= 1 && number <= 99)
            {
                var text = number.ToString();
                float fontSize = size.Height; // start big
                Size textSize;
                Font font;
                do
                {
                    font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                    textSize = TextRenderer.MeasureText(text, font, size, TextFormatFlags.NoPadding);
                    fontSize -= 1f;
                } while ((textSize.Width > size.Width - padding * 2 || textSize.Height > size.Height - padding * 2) && fontSize > 6f);

                var textRect = new Rectangle(0, 0, size.Width, size.Height);
                TextRenderer.DrawText(g, text, font, textRect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                font.Dispose();
            }

            // Connection status dot (top-right, slightly sticking out)
            int dotRadius = Math.Max(4, size.Width / 5);
            int overlap = Math.Max(1, dotRadius / 3); // push outside a bit for emphasis
            var dotRect = new Rectangle(size.Width - (dotRadius * 2) + overlap, -overlap, dotRadius * 2, dotRadius * 2);
            var dotColor = isConnected ? Color.FromArgb(0x27, 0xAE, 0x60) : Color.FromArgb(0xF1, 0xC4, 0x0F); // green or yellow
            using var dotBrush = new SolidBrush(dotColor);
            using var dotPen = new Pen(Color.White, Math.Max(2f, size.Width / 12f));
            g.FillEllipse(dotBrush, dotRect);
            g.DrawEllipse(dotPen, dotRect);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            var icon = (Icon)temp.Clone();
            DestroyIcon(hIcon);
            return icon;
        }
        catch
        {
            DestroyIcon(hIcon);
            throw;
        }
    }

    private void OnBleStatusChanged(BleStatus status, string message)
    {
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(() => HandleBleStatusChanged(status, message))); } catch { }
        }
        else
        {
            HandleBleStatusChanged(status, message);
        }
    }

    private void HandleBleStatusChanged(BleStatus status, string message)
    {
        UpdateStatusLabel(status, message);
        _bleIsConnected = status == BleStatus.Connected || status == BleStatus.ServicesDiscovered || status == BleStatus.Subscribed || status == BleStatus.Listening;
        UpdateTrayIcon();
    }

    private void UpdateStatusLabel(BleStatus status, string message)
    {
        _lblBleStatus.Text = $"BLE: {status} - {message}";
        switch (status)
        {
            case BleStatus.Connected:
            case BleStatus.Subscribed:
            case BleStatus.Listening:
                _lblBleStatus.ForeColor = System.Drawing.Color.DarkGreen;
                break;
            case BleStatus.Error:
                _lblBleStatus.ForeColor = System.Drawing.Color.DarkRed;
                break;
            case BleStatus.Connecting:
                _lblBleStatus.ForeColor = System.Drawing.Color.DarkOrange;
                break;
            default:
                _lblBleStatus.ForeColor = System.Drawing.Color.Black;
                break;
        }
        _lblBleStatus.Refresh();
    }

    private static string[] GetSafeDeviceNames(MMDeviceEnumerator mm, DataFlow flow, NAudio.CoreAudioApi.DeviceState state)
    {
        var names = new System.Collections.Generic.List<string>();
        var endpoints = mm.EnumerateAudioEndPoints(flow, state);
        for (int i = 0; i < endpoints.Count; i++)
        {
            try
            {
                var name = endpoints[i]?.FriendlyName;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }
            catch (Exception ex)
            {
                try { Logger.Log($"Skipping {flow} device index {i}: {ex.GetType().Name} {ex.Message}"); } catch { }
            }
        }
        return names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToArray();
    }

    private void BuildTrayMenu()
    {
        _trayMenu.Items.Clear();
        for (int i = 1; i <= 8; i++)
        {
            int side = i;
            var item = new ToolStripMenuItem($"Trigger side {side}");
            item.Click += (_, __) => TriggerSide(side);
            _trayMenu.Items.Add(item);
        }
        _trayMenu.Items.Add(new ToolStripSeparator());
        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, __) => Application.Exit();
        _trayMenu.Items.Add(exit);
    }

    private void TriggerSide(int side)
    {
        var key = side.ToString();
        if (!_cfg.SideToDevice.TryGetValue(key, out var map))
        {
            MessageBox.Show($"No mapping for side {side}");
            return;
        }
        if (!string.IsNullOrWhiteSpace(map.Output))
            _audioSwitcher.SetDefaultPlaybackDevice(map.Output);
        if (!string.IsNullOrWhiteSpace(map.Input))
            _audioSwitcher.SetDefaultInputDevice(map.Input);
        Logger.Log($"Manual trigger side {side}: output='{map.Output}', input='{map.Input}'");
        _activeSide = side;
        UpdateTrayIcon();
        UpdateActiveSideHighlight();
    }

    private void OnResize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
            ShowInTaskbar = false;
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            ShowInTaskbar = false;
        }
    }

    private void SaveConfig()
    {
        StartupManager.Set(_chkStartOnBoot.Checked);

        var updated = new System.Collections.Generic.Dictionary<string, SideAudioConfig>();
        for (int i = 0; i < 8; i++)
        {
            var key = (i + 1).ToString();
            var input = _inputBoxes[i].SelectedItem as string ?? string.Empty;
            var output = _outputBoxes[i].SelectedItem as string ?? string.Empty;
            updated[key] = new SideAudioConfig(input, output);
        }

        _cfg.SideToDevice.Clear();
        foreach (var kvp in updated)
            _cfg.SideToDevice[kvp.Key] = kvp.Value;

        var cfgToSave = new AppConfig(
            _cfg.BluetoothAddress,
            _cfg.CharacteristicUuid,
            updated,
            _cfg.LogPath,
            _chkStartOnBoot.Checked,
            _chkStartMinimized.Checked
        );

        try
        {
            var baseFolder = AppContext.BaseDirectory;
            var configPath = System.IO.Path.Combine(baseFolder, "appsettings.json");
            var json = JsonSerializer.Serialize(cfgToSave, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(configPath, json);
            Logger.Log("Saved configuration.");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to save config: " + ex.Message);
        }
    }
}
