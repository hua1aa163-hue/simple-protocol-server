// 应用程序入口文件。它只负责初始化 WinForms 并打开主窗体。
namespace SimpleProtocolServer;

/// <summary>Windows 应用程序的启动入口。</summary>
internal static class Program
{
    // WinForms、剪贴板和部分系统对话框要求主线程使用 STA 单线程单元模型。
    [STAThread]
    private static void Main()
    {
        // 应用默认字体、高 DPI 等 .NET WinForms 配置。
        ApplicationConfiguration.Initialize();
        // Application.Run 启动消息循环；主窗体关闭后程序退出。
        Application.Run(new MainForm());
    }
}
