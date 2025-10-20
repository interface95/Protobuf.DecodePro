using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;

#if MACCATALYST
using UIKit;
#endif

namespace Protobuf.Decode.Desktop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

#if MACCATALYST
        WindowHandler.Mapper.AppendToMapping("TransparentTitleBar", (handler, view) =>
        {
            if (handler.PlatformView is UIWindow uiWindow)
            {
                var titlebar = uiWindow.WindowScene?.Titlebar;
                if (titlebar is not null)
                {
                    titlebar.TitleVisibility = UITitlebarTitleVisibility.Hidden;
                    titlebar.ToolbarStyle = UITitlebarToolbarStyle.Unified;
                    titlebar.Toolbar = null;
                }
            }
        });
#endif

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}