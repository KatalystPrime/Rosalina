﻿using BungieSharper.Schema.GroupsV2;
using Catamagne.API;
using Catamagne.Commands;
using Catamagne.Configuration;
using Catamagne.Events;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using Serilog;
using Serilog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Catamagne.Core
{
    class Discord
    {
        static ConfigValues ConfigValues => ConfigValues.configValues;
        public static List<DiscordChannel?> alertsChannels = new List<DiscordChannel?>();
        public static List<DiscordChannel?> updatesChannels = new List<DiscordChannel?>();
        static SerilogLoggerFactory logFactory;
        public static DiscordClient discord;
        public static List<DiscordChannel?> commandChannels;
        public static async Task SetupClient()
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console()
                .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
                .CreateLogger();
            logFactory = new SerilogLoggerFactory();

            discord = new DiscordClient(new DiscordConfiguration()
            {
                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Debug,
                Intents = DiscordIntents.GuildMembers | DiscordIntents.GuildIntegrations | DiscordIntents.GuildMessages | DiscordIntents.Guilds | DiscordIntents.GuildPresences,
                Token = ConfigValues.DiscordToken,
                TokenType = TokenType.Bot,
                AlwaysCacheMembers = true,
                LoggerFactory = logFactory
            }); ;
            var commands = discord.UseCommandsNext(new CommandsNextConfiguration()
            {
                StringPrefixes = ConfigValues.Prefixes,
                CaseSensitive = false,
            });

            commands.RegisterCommands<CoreModule>();
            commands.RegisterCommands<UserInteractionsModule>();

            discord.UseInteractivity(new InteractivityConfiguration()
            {
                PollBehaviour = PollBehaviour.KeepEmojis,
                Timeout = TimeSpan.FromSeconds(30)
            });

            discord.GuildMemberRemoved += UserEvents.Discord_GuildMemberRemoved;
            discord.MessageCreated += UserEvents.Discord_MessageCreated;
            discord.Ready += UserEvents.Discord_Ready;

            await discord.ConnectAsync(ConfigValues.DiscordActivity);
            await UpdateChannels();
        }
        public static async Task UpdateChannels()
        {
            alertsChannels = new List<DiscordChannel?>(); updatesChannels = new List<DiscordChannel?>(); commandChannels = new List<DiscordChannel>();
            foreach (var channel in ConfigValues.AlertChannels)
            {
                try
                {
                    var _ = await discord.GetChannelAsync((ulong)channel);
                    alertsChannels.Add(_);
                }
                catch (Exception e)
                {
                    Log.Information(e.GetType() + " error when getting channel id " + channel);
                }
            }

            foreach (var channel in ConfigValues.UpdatesChannels)
            {
                try
                {
                    var _ = await discord.GetChannelAsync((ulong)channel);
                    updatesChannels.Add(_);
                }
                catch (Exception e)
                {
                    Log.Information(e.GetType() + " error when getting channel id " + channel);
                }
            }
            foreach (var channel in ConfigValues.CommandChannels)
            {
                try
                {
                    commandChannels.Add(await discord.GetChannelAsync(channel));
                }
                catch (Exception e)
                {
                    Log.Information(e.GetType() + " error when getting channel id " + channel);
                }
            }
        }

        public static async Task<DiscordMessage> SendMessage(string text, DiscordChannel channel)
        {
            return await discord.SendMessageAsync(channel, text);
        }

        public static async Task UpdateMessage(string text, DiscordMessage message)
        {
            await message.ModifyAsync(text);
        }
        public static async Task<DiscordMessage> SendFancyMessage(DiscordChannel channel, DiscordEmbed embed)
        {
            return await discord.SendMessageAsync(channel, embed);
        }
        public static DiscordEmbed CreateFancyMessage(DiscordColor color, string title, string description, List<Field> fields)
        {
            var embedBuilder = new DiscordEmbedBuilder()
            {
                Color = color,
                Description = description,
                Title = title
            };
            foreach (var field in fields)
            {
                embedBuilder.AddField(field.name, field.value, field.inline);
            }
            return embedBuilder.Build();

        }
        public static DiscordEmbed CreateFancyMessage(DiscordColor color, string title, string description = null)
        {
            var embedBuilder = new DiscordEmbedBuilder()
            {
                Color = color,
                Description = description,
                Title = title
            };
            return embedBuilder.Build();

        }
        public static DiscordEmbed CreateFancyMessage(DiscordColor color, string description)
        {
            var embedBuilder = new DiscordEmbedBuilder()
            {
                Color = color,
                Description = description
            };
            return embedBuilder.Build();

        }
        public static DiscordEmbed CreateFancyMessage(DiscordColor color, List<Field> fields, string title = null, string description = null)
        {
            var embedBuilder = new DiscordEmbedBuilder()
            {
                Color = color,
                Title = title,
                Description = description
            };
            fields.ForEach(field => embedBuilder.AddField(field.name, field.value, field.inline));
            //fields.ForEach(field => embedBuilder.AddField(field.name, field.value, field.inline));
            return embedBuilder.Build();

        }
        public static DiscordEmbed GetUsersToDisplayInRange(List<Field> fields, Range range, string title = null)
        {
            List<Field> _ = new List<Field>();
            for (int i = range.Start.Value; i < range.End.Value; i++)
            {
                _.Add(fields[i]);
            }
            if (title != null)
            {
                return CreateFancyMessage(DiscordColor.CornflowerBlue, _, title);
            }
            return CreateFancyMessage(DiscordColor.CornflowerBlue, _);
        }
        public static DiscordEmbed GetUsersToDisplayInRange(DiscordColor color, List<Field> fields, Range range, string title = null)
        {
            List<Field> _ = new List<Field>();
            for (int i = range.Start.Value; i < range.End.Value; i++)
            {
                _.Add(fields[i]);
            }
            if (title != null)
            {
                return CreateFancyMessage(color, _, title);
            }
            return CreateFancyMessage(color, _);
        }
        public static void SendFancyListMessage(DiscordChannel channel, Clan clan, List<SpreadsheetTools.User> Users, string title)
        {
            if (Users.Count > 0)
            {
                List<Field> fields = new List<Field>();
                foreach (SpreadsheetTools.User user in Users)
                {
                    if (!string.IsNullOrEmpty(user.DiscordID))
                    {
                        var _ = new Field("Steam name: " + user.SteamName, "Discord ID: " + user.DiscordID);
                        fields.Add(_);
                    }
                    else
                    {
                        var _ = new Field("Steam name: " + user.SteamName, "Discord ID: N/A");
                        fields.Add(_);
                    }
                }
                List<DiscordEmbed> embeds = new List<DiscordEmbed>();
                var colour = clan.details.DiscordColour;
                if (fields.Count > 0 )
                {
                    embeds.Add(GetUsersToDisplayInRange(colour, fields, new Range(0, Math.Min(25, fields.Count)), title));
                }
                if (fields.Count > 25)
                {
                    embeds.Add(GetUsersToDisplayInRange(colour, fields, new Range(25, Math.Min(50, fields.Count))));
                }
                if (fields.Count > 50)
                {
                    embeds.Add(GetUsersToDisplayInRange(colour, fields, new Range(50, Math.Min(75, fields.Count))));
                }
                if (fields.Count > 75)
                {
                    embeds.Add(GetUsersToDisplayInRange(colour, fields, new Range(75, Math.Min(100, fields.Count))));
                }
                List<DiscordMessage> messages = new List<DiscordMessage>();
                embeds.ForEach(async embed => messages.Add(await SendFancyMessage(channel, embed)));
            }
        }
        public static void SendInactivityListMessage(DiscordChannel channel, Clan clan, List<GroupMember> Users, List<TimeSpan> InactivityNumbers, string title)
        {
            if (Users.Count > 0)
            {
                List<Field> fields = new List<Field>();
                for (int i = 0; i < Users.Count; i++)
                {
                    var a = Users[i].destinyUserInfo.membershipId;
                    //if (a != 4611686018501264896 && a != 4611686018506629705 && a != 4611686018501304351 && a != 4611686018496543963 && a != 4611686018496921425 && a != 4611686018491494724)
                    //{
                    var b = Users[i];
                    var time = InactivityNumbers[i].ToString("%d'd 'hh'h'");
                    var _ = new Field(Users[i].destinyUserInfo.displayName, time, true);
                    fields.Add(_);
                    //}
                    
                }
                List<DiscordEmbed> embeds = new List<DiscordEmbed>();
                var colour = clan.details.DiscordColour;
                if (fields.Count > 0)
                {
                    embeds.Add(GetUsersToDisplayInRange(colour, fields, new Range(0, Math.Min(25, fields.Count)), title));
                }
                if (fields.Count > 25)
                {
                    embeds.Add(GetUsersToDisplayInRange(colour, fields, new Range(25, Math.Min(50, fields.Count))));
                }
                if (fields.Count > 50)
                {
                    embeds.Add(GetUsersToDisplayInRange(colour, fields, new Range(50, Math.Min(75, fields.Count))));
                }
                if (fields.Count > 75)
                {
                    embeds.Add(GetUsersToDisplayInRange(colour, fields, new Range(75, Math.Min(100, fields.Count))));
                }
                List<DiscordMessage> messages = new List<DiscordMessage>();
                embeds.ForEach(async embed => messages.Add(await SendFancyMessage(channel, embed)));
            }
        }
    }
    public struct Field
    {

        public Field(string name, string value, bool inline = false)
        {
            this.name = name; this.value = value; this.inline = inline;
        }
        public string name; public string value; public bool inline;
    }
    public struct Response
    {
        public Response(string trigger, string response, string description = null, List<DiscordChannel> allowedChannels = null)
        {
            this.trigger = trigger; this.response = response; this.description = description; this.allowedChannels = allowedChannels;
        }
        public string trigger; public string response; public string description; public List<DiscordChannel> allowedChannels;
    }
}
