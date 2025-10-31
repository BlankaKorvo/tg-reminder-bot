using Telegram.Bot.Types;
namespace TgReminderBot.Services.Commanding;
internal static class ReplyHelper
{
    public static ReplyParameters R(Message m) =>
        new ReplyParameters { MessageId = m.MessageId, AllowSendingWithoutReply = true };
}
