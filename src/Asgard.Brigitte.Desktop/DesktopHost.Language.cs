namespace Asgard.Brigitte.Desktop;

/// <summary>语言偏好覆盖共享视图、现有原生窗口和托盘，切换不触发业务数据重载。</summary>
public sealed partial class DesktopHost
{
    public string LanguagePreference => _language.Mode;
    public string Language => ClientText.Language;
    public void SetLanguage(string language)
    {
        _language.Set(language);
        ApplyLanguage();
    }
    internal void RefreshSystemLanguage()
    {
        if (_language.Mode == "system" && _language.Language != ClientText.Language) ApplyLanguage();
    }
    private void ApplyLanguage()
    {
        ClientText.SetLanguage(_language.Language);
        foreach (var (view, window) in _windows)
            window.Title = Title(view.Kind) + (view.Id is null ? "" : " · " + BugDisplay.ShortId(view.Id));
        _tray?.Refresh();
        StatusChanged?.Invoke();
    }
    /// <summary>直接读取操作系统首选界面语言，不从应用覆盖后的文化反推系统选择。</summary>
    internal static string SystemLanguage()
    {
#if WINDOWS
        return Windows.System.UserProfile.GlobalizationPreferences.Languages.FirstOrDefault() ?? System.Globalization.CultureInfo.CurrentUICulture.Name;
#elif MACCATALYST
        return Foundation.NSLocale.PreferredLanguages.FirstOrDefault() ?? System.Globalization.CultureInfo.CurrentUICulture.Name;
#else
        return System.Globalization.CultureInfo.CurrentUICulture.Name;
#endif
    }
}
