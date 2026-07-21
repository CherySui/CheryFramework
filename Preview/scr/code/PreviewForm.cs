using Hexa.NET.ImGui.Backends.Win32;

namespace CheryFramework.Preview;

public sealed class PreviewForm : Form
{
    private readonly System.Windows.Forms.Timer _renderTimer;
    private D3D12PreviewRenderer? _renderer;

    public PreviewForm()
    {
        Text = "CheryFramework · Direct3D 12 Preview · Insert 打开 UI";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1280, 800);
        MinimumSize = new Size(720, 480);
        BackColor = Color.FromArgb(13, 15, 19);

        _renderTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _renderTimer.Tick += (_, _) => RenderFrame();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            _renderer = new D3D12PreviewRenderer(Handle, ClientSize.Width, ClientSize.Height);
            _renderTimer.Start();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.ToString(), "Direct3D 12 初始化失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_renderer != null && WindowState != FormWindowState.Minimized && ClientSize.Width > 0 && ClientSize.Height > 0)
        {
            _renderer.Resize(ClientSize.Width, ClientSize.Height);
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (_renderer != null)
        {
            nint handled = ImGuiImplWin32.WndProcHandler(message.HWnd, (uint)message.Msg, (nuint)message.WParam, message.LParam);
            if (handled != 0)
            {
                message.Result = handled;
                return;
            }
        }
        base.WndProc(ref message);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _renderTimer.Stop();
            _renderTimer.Dispose();
            _renderer?.Dispose();
            _renderer = null;
        }
        base.Dispose(disposing);
    }

    private void RenderFrame()
    {
        if (_renderer == null || WindowState == FormWindowState.Minimized || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        try
        {
            _renderer.Render();
        }
        catch (Exception exception)
        {
            _renderTimer.Stop();
            MessageBox.Show(this, exception.ToString(), "预览器渲染失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }
}
