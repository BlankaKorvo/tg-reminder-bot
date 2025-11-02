using System.ComponentModel;
using System.Threading.Tasks;
using Telegram.Bot;
using TgReminderBot.Data;
using TgReminderBot.Models;
using TgReminderBot.Services.Commanding.Abstractions;
using TgReminderBot.Services.Commanding.Abstractions.Attributes;
using TimeZoneConverter;

namespace TgReminderBot.Services.Commanding.Handlers;

[RequireGroup]
[RequireChatAdmin]
[Command("/tz")]
[Description("Set your time zone. Usage: /tz <IANA_zone>")]
internal sealed class TzHandler : ICommandHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly AppDbContext _db;
    public TzHandler(ITelegramBotClient bot, AppDbContext db) { _bot = bot; _db = db; }

    public async Task Execute(CommandContext ctx)
    {
        var args = ctx.Args;
        if (string.IsNullOrWhiteSpace(args))
        {
            await _bot.SendMessage(ctx.Message.Chat, "Usage: /tz <IANA_zone>",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
                cancellationToken: ctx.CancellationToken);
            return;
        }

        System.TimeZoneInfo tz;
        try { tz = System.TimeZoneInfo.FindSystemTimeZoneById(args.Trim()); }
        catch
        {
            try { tz = TZConvert.GetTimeZoneInfo(args.Trim()); }
            catch
            {
                await _bot.SendMessage(ctx.Message.Chat, "Bad timezone",
                    replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
                    cancellationToken: ctx.CancellationToken);
                return;
            }
        }

        var uid = ctx.UserId;
        var existing = await _db.UserSettings.FindAsync(new object?[] { uid }, ctx.CancellationToken);
        if (existing is null)
        {
            var us = new UserSettings { UserId = uid, TimeZone = tz.Id, UpdatedAt = System.DateTimeOffset.UtcNow };
            await _db.UserSettings.AddAsync(us, ctx.CancellationToken);
        }
        else
        {
            existing.TimeZone = tz.Id;
            existing.UpdatedAt = System.DateTimeOffset.UtcNow;
            _db.UserSettings.Update(existing);
        }

        await _db.SaveChangesAsync(ctx.CancellationToken);
        await _bot.SendMessage(ctx.Message.Chat, $"Set TZ: {tz.Id}",
            replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
            cancellationToken: ctx.CancellationToken);
    }
}
