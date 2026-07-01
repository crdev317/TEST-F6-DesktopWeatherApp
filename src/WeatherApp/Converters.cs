using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WeatherApp.Core.ViewModels;

namespace WeatherApp;

/// Visible when the bound value is non-null / non-empty string; else Collapsed.
/// Backs the search-message and error-message panels, which appear only when
/// there is a message to show.
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is null || (value is string s && string.IsNullOrEmpty(s))
            ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// Visible when ViewState == Empty (the "search for a place" prompt).
public sealed class EmptyStateToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is WeatherViewState.Empty ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// Visible when ViewState == Weather (the loaded-weather body).
public sealed class WeatherStateToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is WeatherViewState.Weather ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// Visible when WeatherLoadState matches the state named in ConverterParameter
/// (e.g. "Loading", "Loaded"). One converter drives both the Loading spinner and
/// the Loaded readout via a different parameter per binding.
public sealed class LoadStateToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is WeatherLoadState state && p is string name
        && Enum.TryParse<WeatherLoadState>(name, out var want) && state == want
            ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
