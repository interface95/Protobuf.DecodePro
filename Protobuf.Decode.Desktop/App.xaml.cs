using Microsoft.Maui.Platform;

#if MACCATALYST
using System.Linq;
using AppKit;
#endif

namespace Protobuf.Decode.Desktop;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage())
        {
            Title = string.Empty
        };
    }
}