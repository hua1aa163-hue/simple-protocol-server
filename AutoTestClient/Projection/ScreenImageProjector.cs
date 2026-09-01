using System.Drawing;
using System.Windows.Forms;

namespace AutoTestClient.Projection;

/// <summary>
/// 使用无边框 WinForms 窗口在第二屏显示图卡。
/// </summary>
public sealed class ScreenImageProjector : IImageProjector
{
    private readonly SynchronizationContext? _uiContext;
    private ProjectionViewerForm? _viewer;
    private Image? _currentImage;
    private bool _disposed;

    public ScreenImageProjector(SynchronizationContext? uiContext = null)
    {
        _uiContext = uiContext ?? SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
    }

    public bool AllowPrimaryFallback { get; set; } = true;

    public async Task ProjectAsync(string imagePath, ProjectionMode mode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(imagePath)) throw new ArgumentException("图卡路径不能为空。", nameof(imagePath));
        if (!File.Exists(imagePath)) throw new FileNotFoundException("找不到图卡文件。", imagePath);
        using Image source = await Task.Run(() => Image.FromFile(imagePath), cancellationToken).ConfigureAwait(false);
        Image copy = new Bitmap(source);
        bool transferred = false;
        try
        {
            await InvokeAsync(() =>
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                EnsureViewer();
                _viewer!.Mode = mode;
                _currentImage?.Dispose();
                _currentImage = copy;
                transferred = true;
                _viewer.SetImage(_currentImage);
                if (!_viewer.Visible) _viewer.Show();
                _viewer.BringToFront();
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (!transferred) copy.Dispose();
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default) => InvokeAsync(() =>
    {
        if (_viewer is not null && !_viewer.IsDisposed) _viewer.SetImage(null);
        _currentImage?.Dispose();
        _currentImage = null;
    }, cancellationToken);

    private void EnsureViewer()
    {
        if (_viewer is not null && !_viewer.IsDisposed) return;
        _viewer = new ProjectionViewerForm();
        if (DisplayTopology.GetSecondScreen() is null && !AllowPrimaryFallback)
            throw new InvalidOperationException("未检测到第二屏显示器。");
        _viewer.MoveToSecondScreen();
    }

    private Task InvokeAsync(Action action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
        {
            action();
            return Task.CompletedTask;
        }
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiContext.Post(_ =>
        {
            try { action(); tcs.TrySetResult(null); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        }, null);
        return tcs.Task.WaitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            await InvokeAsync(() =>
            {
                if (_viewer is not null && !_viewer.IsDisposed) _viewer.Close();
                _viewer?.Dispose();
                _viewer = null;
                _currentImage?.Dispose();
                _currentImage = null;
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch { }
    }
}
