using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace LeaseLedger.Shared;

public sealed class NavigationTrail : IDisposable
{
    private readonly NavigationManager _navigation;
    private readonly List<string> _trail = new();
    private bool _expectingPop;

    public NavigationTrail(NavigationManager navigation)
    {
        _navigation = navigation;
        _trail.Add(PathOf(navigation.Uri));
        _navigation.LocationChanged += OnLocationChanged;
    }

    public bool CanGoBack => _trail.Count > 1;

    public event Action? Changed;

    public void ExpectPop() => _expectingPop = true;

    public void CancelPop()
    {
        if (!_expectingPop)
            return;

        _expectingPop = false;
        Changed?.Invoke();
    }

    public void Dispose() => _navigation.LocationChanged -= OnLocationChanged;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        var location = PathOf(e.Location);
        if (_trail.Count > 0 && _trail[^1] == location)
        {
            _expectingPop = false;
            Changed?.Invoke();
            return;
        }

        if (_trail.Count >= 2 && _trail[^2] == location)
            _trail.RemoveAt(_trail.Count - 1);
        else if (_expectingPop)
        {
            _trail.Clear();
            _trail.Add(location);
        }
        else
            _trail.Add(location);

        _expectingPop = false;
        Changed?.Invoke();
    }

    private string PathOf(string uri)
    {
        var relative = _navigation.ToBaseRelativePath(uri);
        var hash = relative.IndexOf('#');
        if (hash >= 0)
            relative = relative[..hash];

        return relative.TrimEnd('/');
    }
}
