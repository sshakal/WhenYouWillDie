using DotNetEnv;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

try
{
    Env.Load();
}
catch (Exception ex)
{
    Console.WriteLine($"Ошибка загрузки .env: {ex.Message}");
    return;
}

var botToken = Env.GetString("TELEGRAM_BOT_TOKEN");
if (string.IsNullOrEmpty(botToken))
{
    Console.WriteLine("Error: TELEGRAM_BOT_TOKEN не найден в .env");
    return;
}

var botClient = new TelegramBotClient(botToken);

using var cts = new CancellationTokenSource();

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = []
};

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if (update.Message is not { } message)
        return;
    
    if (message.Text is not { } messageText)
        return;

    var chatId = message.Chat.Id;

    if(messageText == "/start")
    {
        Message startMessage = await botClient.SendMessage(
            chatId: chatId,
            text: $"Напиши свою дату рождения в формате \"10.11.2010\"",
            cancellationToken: cancellationToken);
    }

    if (IsValidDate(messageText))
    {
        Message sentMessage = await botClient.SendMessage(
            chatId: chatId,
            text: $"Тебе осталось жить {FormatTimeSpan(CalculateRemainingTime(messageText).Value)}",
            cancellationToken: cancellationToken);
    }
    else
    {
        Message sentMessage = await botClient.SendMessage(
            chatId: chatId,
            text: $"Неверно введен формат даты",
            cancellationToken: cancellationToken);
    }

    Console.WriteLine($"Получено '{messageText}' сообщение в чате {chatId}.");
}

Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}

botClient.StartReceiving(
    HandleUpdateAsync,
    HandlePollingErrorAsync,
    receiverOptions,
    cts.Token
);

var me = await botClient.GetMe();

Console.ReadLine();

cts.Cancel();

TimeSpan? CalculateRemainingTime(string birthDateStr)
{
    if (!DateTime.TryParseExact(birthDateStr, "dd.MM.yyyy",
        System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.None,
        out DateTime birthDate))
    {
        Console.WriteLine("Неверный формат даты");
        return null;
    }

    DateTime deathDate = birthDate
        .AddYears(70)
        .AddDays(0.06 * 365.2425);

    TimeSpan remaining = deathDate - DateTime.Now;

    return remaining;
}

string FormatTimeSpan(TimeSpan span)
{
    const double daysPerYear = 365.2425;
    const double daysPerMonth = 30.4368;

    int years = (int)(span.TotalDays / daysPerYear);
    int months = (int)((span.TotalDays % daysPerYear) / daysPerMonth);
    int days = (int)(span.TotalDays % daysPerMonth);
    int hours = span.Hours;
    int minutes = span.Minutes;

    return $"""
        {years} лет,
        {months} месяцев,
        {days} дней,
        {hours} часов,
        {minutes} минут
        """;
}

bool IsValidDate(string input)
{
    string pattern = @"^(0[1-9]|[12][0-9]|3[01])\.(0[1-9]|1[0-2])\.(19|20)\d{2}$";
    return Regex.IsMatch(input, pattern);
}
