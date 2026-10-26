using SiteCheck.Core.Abstractions;
using SiteCheck.Core.Models;

namespace SiteCheck.Application.Tests.Fakes;

/// <summary>Prüfung mit festgelegtem Verhalten für den Test des <see cref="CheckRunner"/>.</summary>
internal sealed class StubCheck(string id, CheckCategory category, IReadOnlyList<Finding> findings, bool throws = false, bool cancels = false) : ICheck
{
    private readonly IReadOnlyList<Finding> _findings = findings;
    private readonly bool _throws = throws;
    private readonly bool _cancels = cancels;

    /// <summary>Wie oft die Prüfung ausgeführt wurde.</summary>
    public int RunCount { get; private set; }

    /// <inheritdoc/>
    public string Id { get; } = id;

    /// <inheritdoc/>
    public CheckCategory Category { get; } = category;

    /// <inheritdoc/>
    public Task<IReadOnlyList<Finding>> RunAsync(CheckTarget target, CancellationToken cancellationToken)
    {
        RunCount++;
        if (_cancels)
        {
            throw new OperationCanceledException("Prüfung abgebrochen");
        }

        if (_throws)
        {
            throw new InvalidOperationException("Fehler in der Prüfung");
        }

        return Task.FromResult(_findings);
    }
}
