// 程序主体中的大多数类是 internal，只对本程序集可见。
// 下面的特性专门允许烟雾测试程序集访问这些内部类，便于直接测试协议与调度逻辑。
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SimpleProtocolServer.SmokeTests")]
