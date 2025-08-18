using Discord;
using Discord.Commands;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using System.Threading.Tasks;

namespace Morpheus.Modules;

public class QuotesModule : ModuleBase<SocketCommandContextExtended>
{
    private readonly DB db;

    public QuotesModule(DB dbContext)
    {
        db = dbContext;
    }

}
