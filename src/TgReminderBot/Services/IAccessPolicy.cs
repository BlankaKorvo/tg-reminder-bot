using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TgReminderBot.Services
{
    public interface IAccessPolicy
    {
        Task<bool> IsAllowed(long userId, long chatId, CancellationToken ct);
    }
}
