using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TgReminderBot.Common;
using TgReminderBot.Data;
using TgReminderBot.Services.Commanding.Abstractions;
using TgReminderBot.Services.Commanding.Abstractions.Attributes;

namespace TgReminderBot.Services.Commanding.Handlers;

[RequireGroup]
[RequireChatAdmin]
[Command("/where")]
[Description("Show where reminders are stored in this chat.")]
internal sealed class WhereHandler : ICommandHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly AppDbContext _db;
    public WhereHandler(ITelegramBotClient bot, AppDbContext db) { _bot = bot; _db = db; }

    public async Task Execute(CommandContext ctx)
    {
        var chat = await _db.ChatSettings.FindAsync(new object?[] { ctx.ChatId }, ctx.CancellationToken);
        var s = new StringBuilder()
            .AppendLine($"chat_id: `{ctx.ChatId}`")
            .AppendLine($"thread_id: `{ctx.ThreadId?.ToString() ?? "null"}`")
            .AppendLine($"default_topic_id: `{chat?.DefaultReminderThreadId?.ToString() ?? "null"}`")
            .AppendLine($"control_topic_id: `{chat?.ControlThreadId?.ToString() ?? "null"}`")
            .ToString();

        await _bot.SendMessage(ctx.Message.Chat, s.ToMd2(), parseMode: ParseMode.MarkdownV2,
            replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
            messageThreadId: ctx.ThreadId, cancellationToken: ctx.CancellationToken);
    }
}
