using System;
using System.Threading.Tasks;
using Unison.Core.Contracts;
using Windows.Foundation.Metadata;
using Windows.Graphics.Display;
using Windows.UI;

namespace Unison.Uwp.Services
{
    /// <summary>
    /// StatusBar progress for Unison Mobile sync feedback only.
    /// WhatsApp shell (and desktop) → no-op via <see cref="IShellThemeService.UsesMobileStatusBarProgress"/>.
    /// </summary>
    public sealed class StatusBarService : IStatusBarService
    {
        private static readonly Color BrandGreen = Color.FromArgb(0xFF, 0x20, 0xC0, 0x64);
        private static readonly Color BrandWhite = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

        private readonly IShellThemeService _theme;

        public StatusBarService(IShellThemeService theme)
        {
            _theme = theme;
        }

        public bool IsAvailable =>
            ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar");

        private static object GetStatusBar()
        {
            var type = Type.GetType("Windows.UI.ViewManagement.StatusBar, Windows");
            if (type == null)
            {
                return null;
            }

            var getter = type.GetMethod("GetForCurrentView", Type.EmptyTypes);
            return getter == null ? null : getter.Invoke(null, null);
        }

        private static object GetStatusBarProgressIndicator(object statusBar)
        {
            return statusBar?.GetType().GetProperty("ProgressIndicator")?.GetValue(statusBar);
        }

        private static void SetPropertyValue(object instance, string propertyName, object value)
        {
            if (instance == null)
            {
                return;
            }

            var property = instance.GetType().GetProperty(propertyName);
            if (property != null && property.CanWrite)
            {
                property.SetValue(instance, value);
            }
        }

        private static async Task InvokeAsync(object instance, string methodName)
        {
            if (instance == null)
            {
                return;
            }

            var method = instance.GetType().GetMethod(methodName, Type.EmptyTypes);
            if (method == null)
            {
                return;
            }

            dynamic result = method.Invoke(instance, null);
            if (result != null)
            {
                await result;
            }
        }

        public async Task ShowProgressAsync(string text)
        {
            if (!IsAvailable || _theme == null || !_theme.UsesMobileStatusBarProgress)
            {
                return;
            }

            try
            {
                // Landscape: ignore — do not re-show chrome/progress over the full-bleed UI.
                if (!IsPortrait())
                {
                    return;
                }

                var statusBar = GetStatusBar();
                if (statusBar == null)
                {
                    return;
                }

                SetPropertyValue(statusBar, "BackgroundColor", BrandGreen);
                SetPropertyValue(statusBar, "ForegroundColor", BrandWhite);
                SetPropertyValue(statusBar, "BackgroundOpacity", 1.0);
                await InvokeAsync(statusBar, "ShowAsync");

                var progressIndicator = GetStatusBarProgressIndicator(statusBar);
                if (progressIndicator == null)
                {
                    return;
                }

                // null ProgressValue → indeterminate "..." indicator + label.
                SetPropertyValue(progressIndicator, "Text", text ?? string.Empty);
                SetPropertyValue(progressIndicator, "ProgressValue", null);
                await InvokeAsync(progressIndicator, "ShowAsync");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[StatusBarService] ShowProgress: " + ex.Message);
            }
        }

        public async Task HideProgressAsync()
        {
            if (!IsAvailable || _theme == null || !_theme.UsesMobileStatusBarProgress)
            {
                return;
            }

            try
            {
                var statusBar = GetStatusBar();
                if (statusBar == null)
                {
                    return;
                }

                var progressIndicator = GetStatusBarProgressIndicator(statusBar);
                if (progressIndicator != null)
                {
                    try { await InvokeAsync(progressIndicator, "HideAsync"); } catch { }
                }

                if (!IsPortrait())
                {
                    await InvokeAsync(statusBar, "HideAsync");
                    return;
                }

                // Restore brand label without spinner (same as Unison chrome).
                SetPropertyValue(statusBar, "BackgroundColor", BrandGreen);
                SetPropertyValue(statusBar, "ForegroundColor", BrandWhite);
                SetPropertyValue(statusBar, "BackgroundOpacity", 1.0);
                await InvokeAsync(statusBar, "ShowAsync");

                if (progressIndicator != null)
                {
                    SetPropertyValue(progressIndicator, "Text", "Unison");
                    SetPropertyValue(progressIndicator, "ProgressValue", 0);
                    await InvokeAsync(progressIndicator, "ShowAsync");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[StatusBarService] HideProgress: " + ex.Message);
            }
        }

        private static bool IsPortrait()
        {
            try
            {
                var orientation = DisplayInformation.GetForCurrentView().CurrentOrientation;
                return orientation == DisplayOrientations.Portrait ||
                       orientation == DisplayOrientations.PortraitFlipped;
            }
            catch
            {
                return true;
            }
        }
    }
}
