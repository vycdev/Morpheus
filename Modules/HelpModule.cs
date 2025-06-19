using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Handlers;
using Morpheus.Utilities;
using System.IO.Compression;

namespace Morpheus.Modules;

public class HelpModule : ModuleBase<SocketCommandContextExtended>
{
    private readonly CommandService commands;
    private readonly IServiceProvider serviceProvider;
    private readonly DB dbContext;

    private const int HelpPageSize = 10;

    SelectMenuBuilder? helpMenu = null;
    readonly Dictionary<string, Embed> helpModules = [];

    public HelpModule(DiscordSocketClient client, CommandService commands, InteractionsHandler interactionHandler, IServiceProvider serviceProvider, DB dbContext)
    {
        this.commands = commands;
        this.serviceProvider = serviceProvider;
        this.dbContext = dbContext;

        interactionHandler.RegisterInteraction("module_selector", HandleHelpSelectorInteraction);
    }

    // Handle the interaction when a module is selected
    private async Task HandleHelpSelectorInteraction(SocketInteraction interaction)
    {
        if (interaction is SocketMessageComponent messageComponent)
        {
            if (messageComponent.Data.CustomId == "module_selector")
            {
                Guild? guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == interaction.GuildId);
                string commandPrefix = guild?.Prefix ?? Env.Variables["BOT_DEFAULT_COMMAND_PREFIX"];

                string selectedModule = messageComponent.Data.Values.First();
                Embed embed = CreateModuleHelpEmbed(selectedModule, commandPrefix);

                // Fetch the original message using message ID

                if (await messageComponent.Channel.GetMessageAsync(messageComponent.Message.Id) is IUserMessage message)
                {
                    await message.ModifyAsync(prop => prop.Embed = embed); // Sends a private message
                    await messageComponent.DeferAsync();
                }
            }
        }
    }

    private Embed CreateModuleHelpEmbed(string moduleName, string commandPrefix)
    {
        if (helpModules.TryGetValue(commandPrefix + moduleName, out Embed? embed))
            return embed;

        int page = int.Parse(moduleName.Split("_")[0]);
        string name = moduleName.Split("_")[1].Replace("Module", "");

        EmbedBuilder builder = new()
        {
            Color = Colors.Blue,
            Title = $"{name} {page} commands",
            Description = "Here are the commands available in this module:"
        };

        ModuleInfo? module = commands.Modules.FirstOrDefault(m => m.Name == name + "Module");

        if (module != null)
        {
            for (int i = (page - 1) * HelpPageSize; i < page * HelpPageSize && i < module.Commands.Count; i++)
            {
                CommandInfo cmd = module.Commands.ElementAt(i);

                string aliases = cmd.Aliases.Count > 1
                    ? $"Aliases: {string.Join(", ", cmd.Aliases.Skip(1).Select(a => commandPrefix + a))}"
                    : "No aliases available.";

                string commandDescription = cmd.Summary ?? "No description available.";

                string commandUsage = cmd.Parameters.Count > 0 && cmd.Parameters.Any(p => p.Name != "_")
                    ? $"Usage: `{commandPrefix}{cmd.Aliases[0]} {string.Join(" ", cmd.Parameters.Select(p => $"[{p.Name}{(p.IsOptional ? "?" : "")}{(!string.IsNullOrEmpty(p.DefaultValue?.ToString()) ? " = " + p.DefaultValue.ToString() : "")}]"))}`"
                    : $"Usage: `{commandPrefix}{cmd.Aliases[0]}`";

                builder.AddField(x =>
                {
                    x.Name = cmd.Name;
                    x.Value = $"{commandDescription}\n{commandUsage}\n{aliases}";
                    x.IsInline = false;
                });
            }
        }

        embed = builder.Build();

        helpModules.Add(commandPrefix + moduleName, embed);
        return embed;
    }

    [Name("Help")]
    [Summary("Displays a list of commands.")]
    [Command("help")]
    [Alias("commands", "cmds", "h")]
    [RateLimit(1, 30)]
    public async Task Help()
    {
        EmbedBuilder builder = new()
        {
            Color = Colors.Blue,
            Description = "Select a command module to view its commands."
        };

        // Create the selector (dropdown)
        if (helpMenu == null)
        {
            List<string> modules = [.. commands.Modules.Select(m => m.Name)];
            helpMenu = new SelectMenuBuilder()
                .WithPlaceholder("Select a module")
                .WithCustomId("module_selector");

            // Add the options
            foreach (ModuleInfo? module in commands.Modules)
            {
                if (module.Commands.Count <= HelpPageSize)
                    helpMenu.AddOption(new SelectMenuOptionBuilder().WithLabel(module.Name.Replace("Module", "")).WithValue("1_" + module.Name));
                else
                {
                    int j = 1;
                    for (int i = 0; i < module.Commands.Count; i += HelpPageSize)
                    {
                        helpMenu.AddOption(new SelectMenuOptionBuilder().WithLabel(module.Name.Replace("Module", "") + " " + j).WithValue($"{j}_{module.Name}"));
                        j++;
                    }
                }
            }
        }

        // Create an interaction message
        MessageComponent component = new ComponentBuilder().WithSelectMenu(helpMenu).Build();

        // Create the initial embed
        IUserMessage message = await ReplyAsync(embed: builder.Build());

        await message.ModifyAsync(msg => msg.Components = component);
    }


}
