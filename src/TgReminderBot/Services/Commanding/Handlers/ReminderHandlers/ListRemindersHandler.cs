using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TgReminderBot.Data;
using TgReminderBot.Services.Commanding;
using TgReminderBot.Services.Commanding.Abstractions;
using TgReminderBot.Services.Commanding.Abstractions.Attributes;

namespace TgReminderBot.Services.Commanding.Handlers.ReminderHandlers;

[RequireChatAdmin]
[RequireGroup]
[Command("/reminders")]
[Description("Список ваших напоминаний в этом чате")]
internal sealed class ListRemindersHandler : ICommandHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly AppDbContext _db;

    public ListRemindersHandler(ITelegramBotClient bot, AppDbContext db)
    {
        _bot = bot;
        _db = db;
    }

    public async Task Execute(CommandContext ctx)
    {
        var items = _db.Reminders
            .AsNoTracking()
            .Where(x => x.ChatId == ctx.ChatId && x.CreatedBy == ctx.UserId)
            .AsEnumerable() // SQLite: ORDER BY DateTimeOffset не поддерживается — сортируем в памяти
            .OrderByDescending(x => x.UpdatedAt)
            .Take(10)
            .ToList();

        if (items.Count == 0)
        {
            await _bot.SendMessage(
                ctx.Message.Chat,
                "Нет напоминаний.",
                replyParameters: ReplyHelper.R(ctx.Message),
                messageThreadId: ctx.ThreadId,
                cancellationToken: ctx.CancellationToken);
            return;
        }

        
        string Line(TgReminderBot.Models.Reminder r)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrWhiteSpace(r.RunAt))
                parts.Add($"at {r.RunAt}");

            if (!string.IsNullOrWhiteSpace(r.Cron))
                parts.Add($"cron {r.Cron}");

            if (!string.IsNullOrWhiteSpace(r.EventAt))
            {
                var ev = $"event {r.EventAt}";
                if (!string.IsNullOrWhiteSpace(r.RemindOffsets))
                    ev += $" (offsets {r.RemindOffsets})";
                parts.Add(ev);
            }

            var suffix = parts.Count > 0 ? $"({string.Join("; ", parts)})" : "";
            suffix = Escape(suffix);
            return $"`{r.Id}` — {Escape(r.Text)} {suffix}";
        }


        var msg = "Последние напоминания:\n" + string.Join("\n", items.Select(Line));

        await _bot.SendMessage(
            ctx.Message.Chat,
            msg,
            parseMode: ParseMode.MarkdownV2,
            replyParameters: ReplyHelper.R(ctx.Message),
            messageThreadId: ctx.ThreadId,
            cancellationToken: ctx.CancellationToken);
    }

    private static string Escape(string t)
    {
        var specials = new[] { "_", "*", "[", "]", "(", ")", "~", "`", ">", "#", "+", "-", "=", "|", "{", "}", ".", "!" };
        foreach (var s in specials) t = t.Replace(s, "\\" + s);
        return t;
    }
}