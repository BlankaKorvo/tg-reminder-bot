using System.ComponentModel;
using System.Threading.Tasks;
using Telegram.Bot;
using TgReminderBot.Services.Commanding.Abstractions;
using TgReminderBot.Services.Commanding.Abstractions.Attributes;

namespace TgReminderBot.Services.Commanding.Handlers.AdminHandlers
{
    [Command("/republish_cmds")]
    [RequireSuperAdmin]
    [Description("Перепубликовать меню команд во всех scope")]
    internal sealed class RepublishCmdsHandler : ICommandHandler
    {
        private readonly ITelegramBotClient _bot;
        private readonly BotCommandScopesPublisher _publisher;

        public RepublishCmdsHandler(ITelegramBotClient bot, BotCommandScopesPublisher publisher)
        {
            _bot = bot;
            _publisher = publisher;
        }

        public async Task Execute(CommandContext ctx)
        {
            await _bot.SendMessage(ctx.Message.Chat, "Republishing menus…",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
                messageThreadId: ctx.ThreadId, cancellationToken: ctx.CancellationToken);

            await _publisher.RepublishAllAsync(ctx.CancellationToken);

            await _bot.SendMessage(ctx.Message.Chat, "Done.",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
                messageThreadId: ctx.ThreadId, cancellationToken: ctx.CancellationToken);
        }
    }
}
