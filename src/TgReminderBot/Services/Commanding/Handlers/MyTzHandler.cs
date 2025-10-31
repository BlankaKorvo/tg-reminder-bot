using System.Threading.Tasks;
using Telegram.Bot;
using TgReminderBot.Data;
using TgReminderBot.Services.Commanding.Abstractions;

namespace TgReminderBot.Services.Commanding.Handlers;

[Command("/mytz")]
internal sealed class MyTzHandler : ICommandHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly AppDbContext _db;
    public MyTzHandler(ITelegramBotClient bot, AppDbContext db) { _bot = bot; _db = db; }

    public async Task Execute(CommandContext ctx)
    {
        var us = await _db.UserSettings.FindAsync(new object?[] { ctx.UserId }, ctx.CancellationToken);
        if (us == null)
        {
            await _bot.SendMessage(ctx.Message.Chat, "Not set. Use: /tz <IANA_zone>",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
                cancellationToken: ctx.CancellationToken);
            return;
        }

        await _bot.SendMessage(ctx.Message.Chat, $"Your TZ: {us.TimeZone}",
            replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
            cancellationToken: ctx.CancellationToken);
    }
}
