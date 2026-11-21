# rFkUnCK_you

Локальное приложение для Telegram Desktop и Discord на сетях с блокировками. Первая публичная бета рассчитана на Telegram Desktop и Windows x64. Поддержка Discord запланирована после неё.

rFkUnCK_you разрабатывает Kaev. Идея одного окна для управления сетевыми инструментами вдохновлена [Slipgate](https://github.com/tenstepsbeforedecay/Slipgate).

Исходный код приложения распространяется по [GNU GPL v3.0](LICENSE). План разработки описан в [AGENTS.md](AGENTS.md).

## Windows x64

Распакуй архив целиком и запусти `rFkUnCK_you.exe`. TG WS Proxy [v1.11.4](https://github.com/Flowseal/tg-ws-proxy/releases/tag/v1.11.4) и среды выполнения входят в сборку.

Нажми **«Включить»**, затем **«Открыть в Telegram»** и подтверди подключение. Если порт 1443 занят другим прокси, выбери свободный. Когда резервный маршрут через Cloudflare не отвечает с TLS, выключи прокси, отметь **«Выключить TLS для CF-прокси и CF-worker»** и запусти его снова. Настройка действует на резервный WebSocket до Cloudflare.

Для сборки нужны .NET 10 SDK, PowerShell 7 и uv. Запусти `pwsh -File .\scripts\Publish-Windows.ps1`. Архив появится в `artifacts`.

## Консольная отладка Telegram

Нужны PowerShell 7 и uv. Скрипт подготовит локальное окружение Python 3.12 и запустит TG WS Proxy на 127.0.0.1. Порт по умолчанию — 1443, его можно изменить через -Port.

~~~powershell
pwsh -File .\scripts\Start-TelegramDebug.ps1
~~~

Открой ярлык Telegram, путь к которому напечатан в консоли, и подтверди подключение в Telegram Desktop. Проверь отправку сообщения и загрузку медиа. Ctrl+C в консоли останавливает прокси. После перезапуска используется новый ключ и новый ярлык.

Когда резервный маршрут через Cloudflare не отвечает с TLS, можно повторить проверку с параметром `-DisableCfTls`. Он соответствует настройке Flowseal «Выключить TLS для CF-прокси и CF-worker». Этот запуск использует WebSocket без TLS для резервного маршрута. По умолчанию TLS включён.
