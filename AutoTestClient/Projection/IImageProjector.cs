namespace AutoTestClient.Projection;

/// <summary>把图卡投影到测量屏的最小抽象，便于离线测试替换为 fake。</summary>
public interface IImageProjector : IAsyncDisposable
{
    Task ProjectAsync(string imagePath, ProjectionMode mode, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
