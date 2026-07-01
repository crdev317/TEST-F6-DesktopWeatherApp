using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WeatherApp.Core.Domain;
using WeatherApp.Core.Geocoding;
using WeatherApp.Core.Time;

namespace WeatherApp.Core.ViewModels;

public sealed partial class SearchViewModel : ObservableObject
{
    private const int MinQueryLength = 2;
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

    private readonly IGeocoder _geocoder;
    private readonly IDebounceScheduler _scheduler;

    private int _latestSeq;
    private CancellationTokenSource? _cts;

    public SearchViewModel(IGeocoder geocoder, IDebounceScheduler scheduler)
    {
        _geocoder = geocoder;
        _scheduler = scheduler;
    }

    public ObservableCollection<LocationCandidate> Candidates { get; } = new();

    /// Fired when the user explicitly selects a candidate (the activation handoff).
    public event Action<Location>? LocationSelected;

    [ObservableProperty] private string _query = "";
    [ObservableProperty] private string? _searchMessage;

    partial void OnQueryChanged(string value)
    {
        if (value.Length < MinQueryLength)
        {
            Candidates.Clear();
            SearchMessage = null;
            return;
        }
        _scheduler.Schedule(DebounceDelay, () => RunSearchAsync(value));
    }

    private async Task RunSearchAsync(string query)
    {
        var seq = Interlocked.Increment(ref _latestSeq);
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            var results = await _geocoder.Search(query, ct);
            if (seq != Volatile.Read(ref _latestSeq)) return; // stale — a newer search superseded this one
            Candidates.Clear();
            foreach (var c in results) Candidates.Add(c);
            SearchMessage = results.Count == 0 ? $"No places found for “{query}”." : null;
        }
        catch (Exception) when (seq == Volatile.Read(ref _latestSeq))
        {
            // Fixed neutral copy only: never surface the exception text/stack trace
            // or the request URL (which carries the query). See the Story's security
            // AC and Technical-Context "no raw stack trace in UI".
            Candidates.Clear();
            SearchMessage = "Couldn't search right now — check your connection and try again.";
        }
        catch (Exception)
        {
            // stale failure — ignore
        }
    }

    [RelayCommand]
    private void Select(LocationCandidate candidate)
    {
        var location = new Location(candidate.Name, candidate.Latitude, candidate.Longitude);
        Candidates.Clear();
        SearchMessage = null;
        LocationSelected?.Invoke(location);
    }
}
