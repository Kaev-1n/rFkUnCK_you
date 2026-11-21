# rFkUnCK_you

Локальное приложение для Telegram Desktop и Discord на сетях с блокировками. Первая публичная бета рассчитана на Telegram Desktop и Windows x64. Поддержка Discord запланирована после неё.

rFkUnCK_you разрабатывает Kaev. Идея одного окна для управления сетевыми инструментами вдохновлена [Slipgate](https://github.com/tenstepsbeforedecay/Slipgate).

Исходный код приложения распространяется по [GNU GPL v3.0](LICENSE). План разработки описан в [AGENTS.md](AGENTS.md).

## Консольная отладка Telegram

Нужны PowerShell 7 и uv. Скрипт подготовит локальное окружение Python 3.12 и запустит TG WS Proxy на 127.0.0.1. Порт по умолчанию — 1443, его можно изменить через -Port.

~~~powershell
pwsh -File .\scripts\Start-TelegramDebug.ps1
~~~

Открой ярлык Telegram, путь к которому напечатан в консоли, и подтверди подключение в Telegram Desktop. Проверь отправку сообщения и загрузку медиа. Ctrl+C в консоли останавливает прокси. После перезапуска используется новый ключ и новый ярлык.
