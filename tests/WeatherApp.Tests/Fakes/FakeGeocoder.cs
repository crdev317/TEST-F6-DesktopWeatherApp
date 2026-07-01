using WeatherApp.Core.Domain;
using WeatherApp.Core.Geocoding;

namespace WeatherApp.Tests.Fakes;

/// Returns a TaskCompletionSource per query so a test can complete calls out of
/// order (to exercise the sequence-guard), or throw, or return preset results.
public sealed class FakeGeocoder : IGeocoder
{
    private readonly Dictionary<string, TaskCompletionSource<IReadOnlyList<LocationCandidate>>> _pending = new();
    public List<string> Queries { get; } = new();

    public Task<IReadOnlyList<LocationCandidate>> Search(string query, CancellationToken ct)
    {
        Queries.Add(query);
        var tcs = new TaskCompletionSource<IReadOnlyList<LocationCandidate>>();
        _pending[query] = tcs;
        return tcs.Task;
    }

    public void Complete(string query, params LocationCandidate[] results)
        => _pending[query].SetResult(results);

    public void Fail(string query, Exception ex) => _pending[query].SetException(ex);
}
