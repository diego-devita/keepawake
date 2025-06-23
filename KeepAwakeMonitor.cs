using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;
using System.IO;
using System.Collections.Generic;

class IdleOverlayForm : Form
{
    private Label label;
    private System.Windows.Forms.Timer updateTimer;
    private Func<uint> getIdleSeconds;

    public IdleOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.Black;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10, FontStyle.Bold);
        Padding = new Padding(10);

        label = new Label();
        label.AutoSize = true;
        label.ForeColor = Color.White;
        label.BackColor = Color.Black;
        Controls.Add(label);

        updateTimer = new System.Windows.Forms.Timer();
        updateTimer.Interval = 1000;
        updateTimer.Tick += UpdateLabel;
    }

    public void StartTimer(Func<uint> idleFunc)
    {
        getIdleSeconds = idleFunc;
        UpdateLabel(null, null);
        updateTimer.Start();
        PositionAboveTray();
    }

    private void UpdateLabel(object sender, EventArgs e)
    {
        uint seconds = getIdleSeconds();
        TimeSpan ts = TimeSpan.FromSeconds(seconds);
        label.Text = string.Format("Inattivo da {0:D2}:{1:D2}", ts.Hours * 60 + ts.Minutes, ts.Seconds);
        Size = label.PreferredSize + new Size(Padding.Horizontal, Padding.Vertical);
        PositionAboveTray();
    }

    private void PositionAboveTray()
    {
        Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(workingArea.Right - Width - 10, workingArea.Bottom - Height - 10);
    }
}

class RealTimeMenu : Form
{
    private Label label;
    private Button exitButton;
    private KeepAwakeMonitor monitor;

    public RealTimeMenu(KeepAwakeMonitor monitor)
    {
        this.monitor = monitor;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Text = "KeepAwake Monitor";
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(10);

        label = new Label();
        label.AutoSize = true;
        label.Padding = new Padding(0, 0, 0, 10);
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.Dock = DockStyle.Top;

        exitButton = new Button();
        exitButton.Text = "Esci";
        exitButton.Dock = DockStyle.Top;
        exitButton.Margin = new Padding(0, 10, 0, 0);
        exitButton.AutoSize = true;
        exitButton.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        exitButton.Click += delegate { Application.Exit(); };

        TableLayoutPanel layout = new TableLayoutPanel();
        layout.ColumnCount = 1;
        layout.RowCount = 2;
        layout.Dock = DockStyle.Fill;
        layout.AutoSize = true;
        layout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(exitButton, 0, 1);

        Controls.Add(layout);

        UpdateDisplay();
        PositionNearTray();
    }

    public void UpdateDisplay()
    {
        string text = "Inattivo da: " + monitor.GetLastIdleSeconds() + "s\nF15 attivo: " + monitor.GetF15Status() +
            "\nNotifiche: " + monitor.GetShowNotifications() +
            "\nSoglia inattività: " + monitor.GetIdleThreshold() + "s\nOverlay attivo: " + monitor.GetOverlayStatus();
        label.Text = text;
    }

    private void PositionNearTray()
    {
        Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(workingArea.Right - Width - 10, workingArea.Bottom - Height - 10);
    }
}

class KeepAwakeMonitor : Form
{
    private NotifyIcon trayIcon;
    private Thread monitorThread;
    private Thread f15Thread;
    private Icon iconIdle;
    private Icon iconActive;
    private volatile bool isPressingF15 = false;
    private uint actualIdleSeconds = 0;
    private uint lastIdle = 0;
    private int idleThresholdSeconds = 300;
    private int f15IntervalSeconds = 59;
    private bool showNotifications = true;
    private int notificationDuration = 1000;
    private bool showIdleOverlay = false;
    private string iconIdleName = "idle.ico";
    private string iconActiveName = "active.ico";
    private IdleOverlayForm overlayForm;
    private RealTimeMenu realTimeMenu;
    private System.Windows.Forms.Timer menuUpdateTimer;
    private Point lastMousePosition = Point.Empty;

    [DllImport("user32.dll")]
    static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const byte VK_F15 = 0x7E;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    public KeepAwakeMonitor()
    {
        LoadConfig();
        LoadIcons();

        trayIcon = new NotifyIcon();
        trayIcon.Icon = iconIdle;
        trayIcon.Text = "KeepAwake Monitor";
        trayIcon.Visible = true;
        trayIcon.MouseClick += TrayIcon_MouseClick;

        realTimeMenu = new RealTimeMenu(this);

        monitorThread = new Thread(new ThreadStart(MonitorActivity));
        monitorThread.IsBackground = true;
        monitorThread.Start();

        f15Thread = new Thread(new ThreadStart(PressF15Loop));
        f15Thread.IsBackground = true;
        f15Thread.Start();

        menuUpdateTimer = new System.Windows.Forms.Timer();
        menuUpdateTimer.Interval = 1000;
        menuUpdateTimer.Tick += delegate { if (realTimeMenu != null && realTimeMenu.Visible) realTimeMenu.UpdateDisplay(); };
        menuUpdateTimer.Start();

        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Visible = false;
    }

    private void LogEvent(string message)
    {
        try
        {
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            string logFile = Path.Combine(logDir, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
            string timestamp = DateTime.Now.ToString("dd/MM/yyyy HH.mm.ss");
            string line = message + ": " + timestamp;
            File.AppendAllText(logFile, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Log error: " + ex.Message);
        }
    }

    private void TrayIcon_MouseClick(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (realTimeMenu == null || realTimeMenu.IsDisposed)
            {
                realTimeMenu = new RealTimeMenu(this);
            }

            if (realTimeMenu.Visible)
                realTimeMenu.Hide();
            else
            {
                realTimeMenu.UpdateDisplay();
                realTimeMenu.Show();
            }
        }
    }

    private void LoadConfig()
    {
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
        if (!File.Exists(configPath)) return;
        string[] lines = File.ReadAllLines(configPath);
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("[") || trimmed == "" || trimmed.StartsWith(";")) continue;
            string[] parts = trimmed.Split('=');
            if (parts.Length != 2) continue;
            string key = parts[0].Trim().ToLower();
            string value = parts[1].Trim();
            if (key == "idlethresholdseconds") int.TryParse(value, out idleThresholdSeconds);
            if (key == "f15intervalseconds") int.TryParse(value, out f15IntervalSeconds);
            if (key == "shownotifications") showNotifications = value.ToLower() == "true";
            if (key == "notificationduration") int.TryParse(value, out notificationDuration);
            if (key == "showidleoverlay") showIdleOverlay = value.ToLower() == "true";
            if (key == "iconidle") iconIdleName = value;
            if (key == "iconactive") iconActiveName = value;
        }
    }

    private void LoadIcons()
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        string idlePath = Path.Combine(dir, iconIdleName);
        string activePath = Path.Combine(dir, iconActiveName);
        iconIdle = File.Exists(idlePath) ? new Icon(idlePath) : SystemIcons.Application;
        iconActive = File.Exists(activePath) ? new Icon(activePath) : SystemIcons.Information;
    }

    private void MonitorActivity()
    {
        while (true)
        {
            uint systemIdle = GetIdleTimeSeconds();
            Point currentMouse = Cursor.Position;
            bool mouseMoved = (currentMouse != lastMousePosition);
            bool keyboardUsed = systemIdle == 0;
            lastMousePosition = currentMouse;

            if (isPressingF15)
            {
                if (mouseMoved)
                {
                    LogEvent("Activity detected (" + (actualIdleSeconds / 60) + "m" + (actualIdleSeconds % 60) + "s)");
                    actualIdleSeconds = 0;
                    isPressingF15 = false;
                    trayIcon.Icon = iconIdle;
                    if (showNotifications)
                        trayIcon.ShowBalloonTip(notificationDuration, "KeepAwake", "Attività rilevata — F15 disattivato", ToolTipIcon.Info);
                    if (overlayForm != null)
                    {
                        overlayForm.Invoke(new MethodInvoker(overlayForm.Close));
                        overlayForm = null;
                    }
                }
                else
                {
                    actualIdleSeconds++;
                }
            }
            else
            {
                if (mouseMoved || keyboardUsed)
                {
                    if (actualIdleSeconds >= idleThresholdSeconds)
                    {
                        LogEvent("Activity detected (" + (actualIdleSeconds / 60) + "m" + (actualIdleSeconds % 60) + "s)");
                    }
                    actualIdleSeconds = 0;
                }
                else
                {
                    actualIdleSeconds++;
                }

                if (actualIdleSeconds == idleThresholdSeconds)
                {
                    LogEvent("Inactivity detected [timeout: " + idleThresholdSeconds + "sec]");
                    isPressingF15 = true;
                    trayIcon.Icon = iconActive;
                    if (showNotifications)
                        trayIcon.ShowBalloonTip(notificationDuration, "KeepAwake", "Inattività rilevata — F15 attivo", ToolTipIcon.Info);
                    if (showIdleOverlay)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            overlayForm = new IdleOverlayForm();
                            overlayForm.StartTimer(delegate { return lastIdle; });
                            overlayForm.Show();
                        });
                    }
                }
            }

            lastIdle = actualIdleSeconds;
            Thread.Sleep(1000);
        }
    }

    private void PressF15Loop()
    {
        while (true)
        {
            if (isPressingF15)
            {
                keybd_event(VK_F15, 0, 0, UIntPtr.Zero);
                keybd_event(VK_F15, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                Thread.Sleep(f15IntervalSeconds * 1000);
            }
            else
            {
                Thread.Sleep(500);
            }
        }
    }

    private uint GetIdleTimeSeconds()
    {
        LASTINPUTINFO lii = new LASTINPUTINFO();
        lii.cbSize = (uint)Marshal.SizeOf(lii);
        GetLastInputInfo(ref lii);
        return ((uint)Environment.TickCount - lii.dwTime) / 1000;
    }

    public uint GetLastIdleSeconds() { return lastIdle; }
    public bool GetF15Status() { return isPressingF15; }
    public bool GetShowNotifications() { return showNotifications; }
    public bool GetOverlayStatus() { return showIdleOverlay; }
    public int GetIdleThreshold() { return idleThresholdSeconds; }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new KeepAwakeMonitor());
    }
}
