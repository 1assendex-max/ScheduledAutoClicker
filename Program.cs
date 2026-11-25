using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScheduledAutoClicker;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed class MainForm : Form
{
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

    private const uint LEFTDOWN = 0x0002;
    private const uint LEFTUP = 0x0004;

    private readonly Label positionLabel = new();
    private readonly Label statusLabel = new();
    private readonly DateTimePicker datePicker = new();
    private readonly Button pickButton = new();
    private readonly Button startButton = new();
    private readonly Button cancelButton = new();
    private readonly System.Windows.Forms.Timer captureTimer = new();
    private readonly System.Windows.Forms.Timer scheduleTimer = new();

    private int clickX;
    private int clickY;
    private bool hasPosition;
    private DateTime scheduledAt;
    private bool isScheduled;
    private int captureSeconds;

    public MainForm()
    {
        Text = "Scheduled Auto Clicker";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        ClientSize = new Size(440, 350);
        BackColor = Color.FromArgb(246, 248, 252);
        Font = new Font("Segoe UI", 10);

        var title = new Label {
            Text = "Scheduled Auto Clicker",
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            AutoSize = true, Location = new Point(22, 18)
        };
        Controls.Add(title);

        var subtitle = new Label {
            Text = "নির্দিষ্ট সময়ে স্ক্রিনের নির্বাচিত জায়গায় একবার ক্লিক",
            AutoSize = true, ForeColor = Color.DimGray,
            Location = new Point(24, 55)
        };
        Controls.Add(subtitle);

        var posTitle = new Label {
            Text = "১. ক্লিকের জায়গা নির্বাচন",
            AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(24, 95)
        };
        Controls.Add(posTitle);

        pickButton.Text = "Pick Position (৫ সেকেন্ড)";
        pickButton.Location = new Point(24, 125);
        pickButton.Size = new Size(185, 38);
        pickButton.BackColor = Color.FromArgb(15, 118, 110);
        pickButton.ForeColor = Color.White;
        pickButton.FlatStyle = FlatStyle.Flat;
        pickButton.Click += (_, _) => BeginCapture();
        Controls.Add(pickButton);

        positionLabel.Text = "নির্বাচিত অবস্থান: নেই";
        positionLabel.AutoSize = true;
        positionLabel.Location = new Point(224, 136);
        Controls.Add(positionLabel);

        var timeTitle = new Label {
            Text = "২. তারিখ ও সময় নির্বাচন",
            AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(24, 185)
        };
        Controls.Add(timeTitle);

        datePicker.Format = DateTimePickerFormat.Custom;
        datePicker.CustomFormat = "dd MMM yyyy   hh:mm:ss tt";
        datePicker.ShowUpDown = true;
        datePicker.Location = new Point(24, 215);
        datePicker.Size = new Size(390, 32);
        datePicker.Value = DateTime.Now.AddMinutes(5);
        Controls.Add(datePicker);

        startButton.Text = "Schedule Click";
        startButton.Location = new Point(24, 270);
        startButton.Size = new Size(185, 40);
        startButton.BackColor = Color.FromArgb(37, 99, 235);
        startButton.ForeColor = Color.White;
        startButton.FlatStyle = FlatStyle.Flat;
        startButton.Click += (_, _) => ScheduleClick();
        Controls.Add(startButton);

        cancelButton.Text = "Cancel";
        cancelButton.Location = new Point(224, 270);
        cancelButton.Size = new Size(185, 40);
        cancelButton.BackColor = Color.FromArgb(229, 231, 235);
        cancelButton.FlatStyle = FlatStyle.Flat;
        cancelButton.Click += (_, _) => CancelSchedule();
        Controls.Add(cancelButton);

        statusLabel.Text = "স্ট্যাটাস: প্রস্তুত";
        statusLabel.AutoSize = true;
        statusLabel.Location = new Point(24, 322);
        Controls.Add(statusLabel);

        captureTimer.Interval = 1000;
        captureTimer.Tick += CaptureTimer_Tick;
        scheduleTimer.Interval = 200;
        scheduleTimer.Tick += ScheduleTimer_Tick;
    }

    private void BeginCapture()
    {
        captureSeconds = 5;
        pickButton.Enabled = false;
        statusLabel.Text = "৫ সেকেন্ডের মধ্যে মাউস পছন্দের জায়গায় রাখুন...";
        captureTimer.Start();
    }

    private void CaptureTimer_Tick(object? sender, EventArgs e)
    {
        captureSeconds--;
        if (captureSeconds > 0)
        {
            statusLabel.Text = $"মাউসের অবস্থান নেওয়া হবে {captureSeconds} সেকেন্ড পর...";
            return;
        }

        captureTimer.Stop();
        if (GetCursorPos(out POINT p))
        {
            clickX = p.X;
            clickY = p.Y;
            hasPosition = true;
            positionLabel.Text = $"অবস্থান: X={clickX}, Y={clickY}";
            statusLabel.Text = "স্ট্যাটাস: ক্লিকের জায়গা সংরক্ষিত";
        }
        else statusLabel.Text = "মাউসের অবস্থান নেওয়া যায়নি। আবার চেষ্টা করুন।";
        pickButton.Enabled = true;
    }

    private void ScheduleClick()
    {
        if (!hasPosition)
        {
            MessageBox.Show("আগে Pick Position দিয়ে ক্লিকের জায়গা নির্বাচন করুন।", "Position required",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (datePicker.Value <= DateTime.Now)
        {
            MessageBox.Show("ভবিষ্যতের একটি সময় নির্বাচন করুন।", "Invalid time",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        scheduledAt = datePicker.Value;
        isScheduled = true;
        scheduleTimer.Start();
        statusLabel.Text = $"নির্ধারিত: {scheduledAt:dd MMM yyyy hh:mm:ss tt}";
        startButton.Enabled = false;
    }

    private void ScheduleTimer_Tick(object? sender, EventArgs e)
    {
        if (!isScheduled) return;
        var remaining = scheduledAt - DateTime.Now;
        if (remaining > TimeSpan.Zero)
        {
            statusLabel.Text = $"বাকি: {remaining:hh\\:mm\\:ss}";
            return;
        }

        scheduleTimer.Stop();
        isScheduled = false;
        startButton.Enabled = true;

        try
        {
            SetCursorPos(clickX, clickY);
            mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
            statusLabel.Text = $"সম্পন্ন: X={clickX}, Y={clickY} অবস্থানে একবার ক্লিক হয়েছে";
            MessageBox.Show("নির্ধারিত জায়গায় একবার ক্লিক করা হয়েছে।", "Completed",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            statusLabel.Text = "ক্লিক করা যায়নি: " + ex.Message;
        }
    }

    private void CancelSchedule()
    {
        scheduleTimer.Stop();
        isScheduled = false;
        startButton.Enabled = true;
        statusLabel.Text = "স্ট্যাটাস: নির্ধারিত ক্লিক বাতিল করা হয়েছে";
    }
}
