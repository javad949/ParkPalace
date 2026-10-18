namespace ParkPalace.Application.Interfaces;

public interface ISepidarReadAdapter
{
    bool IsConfigured { get; }
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
