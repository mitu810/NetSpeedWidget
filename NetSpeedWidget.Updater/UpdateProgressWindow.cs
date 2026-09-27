using System.Drawing;
using System.Windows.Forms;

internal sealed class UpdateProgressWindow : IDisposable
{
    private readonly Form _form;
    private readonly Label _label;
    private readonly ProgressBar _bar;
    private long _lastReport;

    /// <summary>主程序退出后显示独立的便携版安装进度，不依赖 WinUI 窗口。</summary>
    public UpdateProgressWindow()
    {
        Form? form = null;
        Label? label = null;
        ProgressBar? bar = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            form = new Form { Text = "NetSpeedWidget 更新", ClientSize = new Size(440, 150), StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ControlBox = false,
                AutoScaleMode = AutoScaleMode.Dpi, Font = new Font("Microsoft YaHei UI", 10) };
            label = new Label { Text = "正在备份旧版本…", Left = 24, Top = 26, Width = 392, Height = 40 };
            bar = new ProgressBar { Left = 24, Top = 82, Width = 392, Height = 22, Minimum = 0, Maximum = 100 };
            form.Controls.Add(label);
            form.Controls.Add(bar);
            _ = form.Handle;
            ready.Set();
            Application.Run(form);
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        _form = form!;
        _label = label!;
        _bar = bar!;
    }

    public void Report(double value)
    {
        var now = Environment.TickCount64;
        if (value < 100 && now - _lastReport < 100) return;
        _lastReport = now;
        _form.BeginInvoke(() => { _bar.Value = Math.Clamp((int)value, 0, 100); _label.Text = $"正在安装更新：{value:F0}%"; });
    }

    public void Dispose() => _form.BeginInvoke(() => _form.Close());
}
