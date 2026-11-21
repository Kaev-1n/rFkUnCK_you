using Avalonia.Controls;
using Avalonia.Interactivity;
using Rfkunck.Core;

namespace Rfkunck.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly TelegramProxy proxy;
    private readonly string settingsPath;
    private TelegramSettings settings;
    private TelegramProxySession? session;
    private Task? operation;
    private CancellationTokenSource? startup;
    private bool closing;
    private bool allowClose;

    public MainWindow() : this(
        Path.Combine(AppContext.BaseDirectory, "tg-ws-proxy", "python.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "rFkUnCK_you", "telegram.json"))
    {
    }

    public MainWindow(string proxyPath, string settingsPath)
    {
        InitializeComponent();
        proxy = new TelegramProxy(proxyPath, "-m", "proxy.tg_ws_proxy");
        this.settingsPath = Path.GetFullPath(settingsPath);

        try
        {
            settings = TelegramSettings.Load(this.settingsPath);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            settings = TelegramSettings.CreateDefault();
            SetStatus("Настройки недоступны", "Проверь права на папку rFkUnCK_you в LocalAppData.");
        }

        PortInput.Value = settings.Port;
        DisableCfTlsInput.IsChecked = settings.DisableCfTls;
        Closing += CloseAfterStopping;
    }

    private async void ToggleProxy(object? sender, RoutedEventArgs e)
    {
        if (operation is not null || closing)
            return;

        operation = session is null ? StartProxyAsync() : StopProxyAsync();
        UpdateControls();
        await operation;
        operation = null;
        UpdateControls();
    }

    private async Task StartProxyAsync()
    {
        if (PortInput.Value is not decimal value || value != decimal.Truncate(value) ||
            value is < 1024 or > 65535)
        {
            SetStatus("Проверь порт", "Укажи целое число от 1024 до 65535.");
            return;
        }

        using var cancellation = new CancellationTokenSource();
        startup = cancellation;
        int port = (int)value;
        SetStatus("Запускаем прокси…", $"127.0.0.1:{port}");
        try
        {
            settings = settings with { Port = port, DisableCfTls = DisableCfTlsInput.IsChecked == true };
            settings.Save(settingsPath);
            session = await proxy.StartAsync(port, settings.DisableCfTls, cancellation.Token, settings.Secret);
            SetStatus("Локальный прокси готов", $"127.0.0.1:{port} · Подтверди подключение в Telegram.");
            _ = WatchExitAsync(session);
        }
        catch (OperationCanceledException) when (closing)
        {
            SetStatus("Прокси выключен", "");
        }
        catch (Exception error)
        {
            string detail = error switch
            {
                FileNotFoundException => "Распакуй всю сборку. Компонент TG WS Proxy не найден.",
                TimeoutException => "TG WS Proxy не сообщил о готовности. Попробуй запустить ещё раз.",
                InvalidOperationException when error.Message.Contains("already in use") =>
                    $"Порт {port} занят. Укажи другой порт или выключи другой прокси.",
                IOException or UnauthorizedAccessException => "Не удалось сохранить настройки в LocalAppData.",
                _ => "TG WS Proxy завершился при запуске. Попробуй запустить ещё раз.",
            };
            SetStatus("Прокси не запустился", detail);
        }
        finally
        {
            startup = null;
        }
    }

    private async Task StopProxyAsync()
    {
        TelegramProxySession? previous = session;
        session = null;
        SetStatus("Выключаем прокси…", "");
        try
        {
            await proxy.StopAsync();
            SetStatus("Прокси выключен", "Запусти прокси для нового подключения.");
        }
        catch (Exception)
        {
            session = previous is { ExitCode.IsCompleted: false } ? previous : null;
            SetStatus("Не удалось остановить прокси", "Попробуй выключить его ещё раз.");
        }
    }

    private async Task WatchExitAsync(TelegramProxySession active)
    {
        int code = await active.ExitCode;
        if (!ReferenceEquals(session, active))
            return;

        session = null;
        SetStatus("Прокси остановился", $"Код завершения {code}. Прокси можно запустить снова.");
        UpdateControls();
    }

    private async void OpenTelegram(object? sender, RoutedEventArgs e)
    {
        if (session is not { } active)
            return;

        try
        {
            if (await Launcher.LaunchUriAsync(active.ConnectionLink))
                return;
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The link handler may be missing or misconfigured.
        }

        if (ReferenceEquals(session, active))
            SetStatus("Telegram не открылся", "Проверь, что в Windows выбран обработчик ссылок Telegram.");
    }

    private async void CloseAfterStopping(object? sender, WindowClosingEventArgs e)
    {
        if (allowClose)
            return;

        e.Cancel = true;
        if (closing)
            return;

        closing = true;
        startup?.Cancel();
        UpdateControls();
        if (operation is not null)
            await operation;

        try
        {
            await proxy.StopAsync();
            allowClose = true;
            Close();
        }
        catch (Exception)
        {
            closing = false;
            SetStatus("Не удалось остановить прокси", "Повтори попытку закрыть окно.");
            UpdateControls();
        }
    }

    private void UpdateControls()
    {
        bool idle = operation is null && !closing;
        ToggleButton.Content = session is null ? "Включить" : "Выключить";
        ToggleButton.IsEnabled = idle;
        OpenTelegramButton.IsEnabled = idle && session is not null;
        PortInput.IsEnabled = idle && session is null;
        DisableCfTlsInput.IsEnabled = idle && session is null;
    }

    private void SetStatus(string title, string detail)
    {
        StatusText.Text = title;
        StatusDetail.Text = detail;
        StatusDetail.IsVisible = detail.Length != 0;
    }
}
