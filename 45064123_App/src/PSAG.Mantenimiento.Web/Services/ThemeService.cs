using Microsoft.JSInterop;

namespace PSAG.Mantenimiento.Web.Services;

public interface IThemeService
{
    bool IsDarkMode { get; }
    event Action? OnThemeChanged;
    Task InitializeAsync();
    Task ToggleThemeAsync();
    Task SetThemeAsync(bool isDarkMode);
}

public class ThemeService : IThemeService
{
    private readonly IJSRuntime _jsRuntime;
    private bool _isDarkMode;
    private bool _initialized;

    public bool IsDarkMode => _isDarkMode;
    public event Action? OnThemeChanged;

    public ThemeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            var savedTheme = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "theme");
            // T-806: sin elección guardada, sigue al sistema (prefers-color-scheme): el script de index.html ya lo puso
            // en <html data-bs-theme> antes de pintar; acá sólo se lee (y no se guarda, para que siga al sistema).
            if (savedTheme is not ("dark" or "light"))
                savedTheme = await _jsRuntime.InvokeAsync<string?>("document.documentElement.getAttribute", "data-bs-theme");
            _isDarkMode = savedTheme == "dark";
            await ApplyThemeAsync();
            _initialized = true;
        }
        catch
        {
            // Ignore errors during prerendering
        }
    }

    public async Task ToggleThemeAsync()
    {
        await SetThemeAsync(!_isDarkMode);
    }

    public async Task SetThemeAsync(bool isDarkMode)
    {
        _isDarkMode = isDarkMode;
        await ApplyThemeAsync();

        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "theme", isDarkMode ? "dark" : "light");
        }
        catch
        {
            // Ignore errors during prerendering
        }

        OnThemeChanged?.Invoke();
    }

    private async Task ApplyThemeAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("document.documentElement.setAttribute", "data-bs-theme", _isDarkMode ? "dark" : "light");
        }
        catch
        {
            // Ignore errors during prerendering
        }
    }
}
