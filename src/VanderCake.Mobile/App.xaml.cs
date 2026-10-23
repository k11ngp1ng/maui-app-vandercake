namespace VanderCake.Mobile;

public partial class App : Application
{
    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
        _shell = shell;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_shell) { Title = "VanderCake · Salesperson" };
#if WINDOWS
        window.Width = 1100; window.Height = 850; window.MinimumWidth = 420; window.MinimumHeight = 600;
#endif
        return window;
    }
}

