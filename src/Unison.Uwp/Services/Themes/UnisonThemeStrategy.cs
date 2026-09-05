using System;
using System.Threading.Tasks;
using Unison.Core.Contracts;
using Windows.Foundation.Metadata;
using Windows.Graphics.Display;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Unison.Uwp.Services.Themes
{
    /// <summary>
    /// Unison chrome: green title bar on PC; green "Unison" status bar on Mobile portrait.
    /// Sync progress uses StatusBar on Mobile (header hidden).
    /// </summary>
    public sealed class UnisonThemeStrategy : ShellThemeStrategy
    {
        private static readonly Color BrandGreen = Color.FromArgb(0xFF, 0x20, 0xC0, 0x64);
        private static readonly Color BrandGreenHover = Color.FromArgb(0xFF, 0x1A, 0xA8, 0x54);
        private static readonly Color BrandGreenPressed = Color.FromArgb(0xFF, 0x18, 0x96, 0x4B);
        private static readonly Color BrandWhite = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

        private readonly ISystemInfoProvider _systemInfo;

        public UnisonThemeStrategy(ISystemInfoProvider systemInfo)
        {
            _systemInfo = systemInfo;
        }

        public override bool DisplaySyncInChatList =>
            _systemInfo == null || !_systemInfo.IsMobile();

        public override bool UsesMobileStatusBarProgress =>
            _systemInfo != null && _systemInfo.IsMobile();

        public override void SetTitleBar()
        {
            try
            {
                var titleBar = ApplicationView.GetForCurrentView().TitleBar;

                titleBar.BackgroundColor = BrandGreen;
                titleBar.ForegroundColor = BrandWhite;
                titleBar.InactiveBackgroundColor = BrandGreen;
                titleBar.InactiveForegroundColor = BrandWhite;

                titleBar.ButtonBackgroundColor = BrandGreen;
                titleBar.ButtonForegroundColor = BrandWhite;
                titleBar.ButtonHoverBackgroundColor = BrandGreenHover;
                titleBar.ButtonHoverForegroundColor = BrandWhite;
                titleBar.ButtonPressedBackgroundColor = BrandGreenPressed;
                titleBar.ButtonPressedForegroundColor = BrandWhite;
                titleBar.ButtonInactiveBackgroundColor = BrandGreen;
                titleBar.ButtonInactiveForegroundColor = BrandWhite;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[UnisonTheme] SetTitleBar: " + ex.Message);
            }
        }

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

        private static object GetProgressIndicator(object statusBar)
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

        public override async Task SetMobileStatusBarAsync()
        {
            try
            {
                if (!ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
                {
                    return;
                }

                var statusBar = GetStatusBar();
                if (statusBar == null)
                {
                    return;
                }

                var orientation = DisplayInformation.GetForCurrentView().CurrentOrientation;
                bool portrait =
                    orientation == DisplayOrientations.Portrait ||
                    orientation == DisplayOrientations.PortraitFlipped;

                var progressIndicator = GetProgressIndicator(statusBar);
                if (!portrait)
                {
                    try { if (progressIndicator != null) await InvokeAsync(progressIndicator, "HideAsync"); } catch { }
                    await InvokeAsync(statusBar, "HideAsync");
                    return;
                }

                SetPropertyValue(statusBar, "BackgroundColor", BrandGreen);
                SetPropertyValue(statusBar, "ForegroundColor", BrandWhite);
                SetPropertyValue(statusBar, "BackgroundOpacity", 1.0);

                await InvokeAsync(statusBar, "ShowAsync");

                if (progressIndicator == null)
                {
                    return;
                }

                // ProgressValue = 0 → label without indeterminate spinner.
                SetPropertyValue(progressIndicator, "Text", "Unison");
                SetPropertyValue(progressIndicator, "ProgressValue", 0);
                await InvokeAsync(progressIndicator, "ShowAsync");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[UnisonTheme] SetMobileStatusBar: " + ex.Message);
            }
        }
    }
}
