﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using SysBot.Base;

namespace SysBot.Pokemon.Discord
{
    public class EchoModule : ModuleBase<SocketCommandContext>
    {
        private class EchoChannel
        {
            public readonly ulong ChannelID;
            public readonly string ChannelName;
            public readonly Action<string> Action;
            public readonly Action<byte[], string, EmbedBuilder> RaidAction;
            public string EmbedResult = string.Empty;

            public EchoChannel(ulong channelId, string channelName, Action<string> action, Action<byte[], string, EmbedBuilder> raidAction)
            {
                ChannelID = channelId;
                ChannelName = channelName;
                Action = action;
                RaidAction = raidAction;
            }
        }

        private class EncounterEchoChannel
        {
            public readonly ulong ChannelID;
            public readonly string ChannelName;
            public readonly Action<string, Embed> EmbedAction;
            public string EmbedResult = string.Empty;

            public EncounterEchoChannel(ulong channelId, string channelName, Action<string, Embed> embedaction)
            {
                ChannelID = channelId;
                ChannelName = channelName;
                EmbedAction = embedaction;
            }
        }

        private static readonly Dictionary<ulong, EchoChannel> Channels = new();
        private static readonly Dictionary<ulong, EncounterEchoChannel> EncounterChannels = new();

        public static void RestoreChannels(DiscordSocketClient discord, DiscordSettings cfg)
        {
            foreach (var ch in cfg.EchoChannels)
            {
                if (discord.GetChannel(ch.ID) is ISocketMessageChannel c)
                    AddEchoChannel(c, ch.ID);
            }
            // EchoUtil.Echo("Added echo notification to Discord channel(s) on Bot startup.");
        }

        [Command("Announce", RunMode = RunMode.Async)]
        [Alias("announce")]
        [Summary("Sends an announcement to all EchoChannels added by the aec command.")]
        [RequireOwner]
        public async Task AnnounceAsync([Remainder] string announcement)
        {
            var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var formattedTimestamp = $"<t:{unixTimestamp}:F>";
            var embedColor = SysCordSettings.Settings.AnnouncementSettings.RandomAnnouncementColor ? GetRandomColor() : ToDiscordColor(SysCordSettings.Settings.AnnouncementSettings.AnnouncementEmbedColor);
            var thumbnailUrl = GetThumbnail();

            var embedDescription = $"## {announcement}\n\n**Sent: {formattedTimestamp}**";

            var embed = new EmbedBuilder
            {
                Color = embedColor,
                Description = embedDescription
            }
            .WithTitle("Important Announcement!")
            .WithThumbnailUrl(thumbnailUrl)
            .Build();

            var client = Context.Client;
            foreach (var channelEntry in Channels)
            {
                var channelId = channelEntry.Key;
                var channel = client.GetChannel(channelId) as ISocketMessageChannel;
                if (channel == null)
                {
                    LogUtil.LogError($"Failed to find or access channel {channelId}", nameof(AnnounceAsync));
                    continue;
                }

                try
                {
                    await channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"Failed to send announcement to channel {channel.Name}: {ex.Message}", nameof(AnnounceAsync));
                }
            }
            var confirmationMessage = await ReplyAsync("Announcement sent to all EchoChannels.").ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            await confirmationMessage.DeleteAsync().ConfigureAwait(false);
            await Context.Message.DeleteAsync().ConfigureAwait(false);
        }

        private static Color GetRandomColor()
        {
            // Generate a random color
            var random = new Random();
            var colors = Enum.GetValues(typeof(EmbedColorOption)).Cast<EmbedColorOption>().ToList();
            return ToDiscordColor(colors[random.Next(colors.Count)]);
        }

        private static string GetThumbnail()
        {
            if (!string.IsNullOrEmpty(SysCordSettings.Settings.AnnouncementSettings.CustomAnnouncementThumbnailUrl))
            {
                return SysCordSettings.Settings.AnnouncementSettings.CustomAnnouncementThumbnailUrl;
            }

            var thumbnails = new Dictionary<ThumbnailOption, string>
            {
                { ThumbnailOption.Gengar,     "https://raw.githubusercontent.com/BakaKaito/HomeImages/Home3.0/Misc/gengarmegaphone.png" },
                { ThumbnailOption.Pikachu,    "https://raw.githubusercontent.com/BakaKaito/HomeImages/Home3.0/Misc/pikachumegaphone.png" },
                { ThumbnailOption.Umbreon,    "https://raw.githubusercontent.com/BakaKaito/HomeImages/Home3.0/Misc/umbreonmegaphone.png" },
                { ThumbnailOption.Sylveon,    "https://raw.githubusercontent.com/BakaKaito/HomeImages/Home3.0/Misc/sylveonmegaphone.png" },
                { ThumbnailOption.Charmander, "https://raw.githubusercontent.com/BakaKaito/HomeImages/Home3.0/Misc/charmandermegaphone.png" },
                { ThumbnailOption.Jigglypuff, "https://raw.githubusercontent.com/BakaKaito/HomeImages/Home3.0/Misc/jigglypuffmegaphone.png" },
                { ThumbnailOption.Flareon,    "https://raw.githubusercontent.com/BakaKaito/HomeImages/Home3.0/Misc/flareonmegaphone.png" }
            };

            if (thumbnails.TryGetValue(SysCordSettings.Settings.AnnouncementSettings.AnnouncementThumbnailOption, out var url))
            {
                return url;
            }

            return thumbnails.Values.ElementAt(new Random().Next(thumbnails.Count));
        }

        [Command("addEmbedChannel")]
        [Alias("aec")]
        [Summary("Makes the bot post raid embeds to the channel.")]
        [RequireSudo]
        public async Task AddEchoAsync()
        {
            var c = Context.Channel;
            var cid = c.Id;
            if (Channels.TryGetValue(cid, out _))
            {
                await ReplyAsync("Already notifying here.").ConfigureAwait(false);
                return;
            }

            AddEchoChannel(c, cid);

            // Add to discord global loggers (saves on program close)
            SysCordSettings.Settings.EchoChannels.AddIfNew(new[] { GetReference(Context.Channel) });
            await ReplyAsync("Added Raid Embed output to this channel!").ConfigureAwait(false);
        }

        private static async Task<bool> SendMessageWithRetry(ISocketMessageChannel c, string message, int maxRetries = 3)
        {
            int retryCount = 0;
            while (retryCount < maxRetries)
            {
                try
                {
                    await c.SendMessageAsync(message).ConfigureAwait(false);
                    return true; // Successfully sent the message, exit the loop.
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"Failed to send message to channel '{c.Name}' (Attempt {retryCount + 1}): {ex.Message}", nameof(AddEchoChannel));
                    retryCount++;
                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false); // Wait for 5 seconds before retrying.
                }
            }
            return false; // Reached max number of retries without success.
        }

        private static async Task<bool> RaidEmbedAsync(ISocketMessageChannel c, byte[] bytes, string fileName, EmbedBuilder embed, int maxRetries = 2)
        {
            int retryCount = 0;
            while (retryCount < maxRetries)
            {
                try
                {
                    if (bytes is not null && bytes.Length > 0)
                    {
                        await c.SendFileAsync(new MemoryStream(bytes), fileName, "", false, embed: embed.Build()).ConfigureAwait(false);
                    }
                    else
                    {
                        await c.SendMessageAsync("", false, embed.Build()).ConfigureAwait(false);
                    }
                    return true; // Successfully sent the message, exit the loop.
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"Failed to send embed to channel '{c.Name}' (Attempt {retryCount + 1}): {ex.Message}", nameof(AddEchoChannel));
                    retryCount++;
                    if (retryCount < maxRetries)
                        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false); // Wait for a second before retrying.
                }
            }
            return false; // Reached max number of retries without success.
        }

        private static void AddEchoChannel(ISocketMessageChannel c, ulong cid)
        {
            Action<string> l = async (msg) => await SendMessageWithRetry(c, msg).ConfigureAwait(false);
            Action<byte[], string, EmbedBuilder> rb = async (bytes, fileName, embed) => await RaidEmbedAsync(c, bytes, fileName, embed).ConfigureAwait(false);

            EchoUtil.Forwarders.Add(l);
            EchoUtil.RaidForwarders.Add(rb);
            var entry = new EchoChannel(cid, c.Name, l, rb);
            Channels.Add(cid, entry);
        }

        public static bool IsEchoChannel(ISocketMessageChannel c)
        {
            var cid = c.Id;
            return Channels.TryGetValue(cid, out _);
        }

        public static bool IsEmbedEchoChannel(ISocketMessageChannel c)
        {
            var cid = c.Id;
            return EncounterChannels.TryGetValue(cid, out _);
        }

        [Command("echoInfo")]
        [Summary("Dumps the special message (Echo) settings.")]
        [RequireSudo]
        public async Task DumpEchoInfoAsync()
        {
            foreach (var c in Channels)
                await ReplyAsync($"{c.Key} - {c.Value}").ConfigureAwait(false);
        }

        [Command("echoClear")]
        [Alias("rec")]
        [Summary("Clears the special message echo settings in that specific channel.")]
        [RequireSudo]
        public async Task ClearEchosAsync()
        {
            var id = Context.Channel.Id;
            if (!Channels.TryGetValue(id, out var echo))
            {
                await ReplyAsync("Not echoing in this channel.").ConfigureAwait(false);
                return;
            }
            EchoUtil.Forwarders.Remove(echo.Action);
            EchoUtil.RaidForwarders.Remove(echo.RaidAction);
            Channels.Remove(Context.Channel.Id);
            SysCordSettings.Settings.EchoChannels.RemoveAll(z => z.ID == id);
            await ReplyAsync($"Echoes cleared from channel: {Context.Channel.Name}").ConfigureAwait(false);
        }

        [Command("echoClearAll")]
        [Alias("raec")]
        [Summary("Clears all the special message Echo channel settings.")]
        [RequireSudo]
        public async Task ClearEchosAllAsync()
        {
            foreach (var l in Channels)
            {
                var entry = l.Value;
                await ReplyAsync($"Echoing cleared from {entry.ChannelName} ({entry.ChannelID}!").ConfigureAwait(false);
                EchoUtil.Forwarders.Remove(entry.Action);
            }
            EchoUtil.Forwarders.RemoveAll(y => Channels.Select(x => x.Value.Action).Contains(y));
            EchoUtil.RaidForwarders.RemoveAll(y => Channels.Select(x => x.Value.RaidAction).Contains(y));
            Channels.Clear();
            SysCordSettings.Settings.EchoChannels.Clear();
            await ReplyAsync("Echoes cleared from all channels!").ConfigureAwait(false);
        }

        private RemoteControlAccess GetReference(IChannel channel) => new()
        {
            ID = channel.Id,
            Name = channel.Name,
            Comment = $"Added by {Context.User.Username} on {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
        };

        public static Color ToDiscordColor(EmbedColorOption colorOption)
        {
            return colorOption switch
            {
                EmbedColorOption.Blue => Color.Blue,
                EmbedColorOption.Green => Color.Green,
                EmbedColorOption.Red => Color.Red,
                EmbedColorOption.Gold => Color.Gold,
                EmbedColorOption.Purple => Color.Purple,
                EmbedColorOption.Teal => Color.Teal,
                EmbedColorOption.Orange => Color.Orange,
                EmbedColorOption.Magenta => Color.Magenta,
                EmbedColorOption.LightGrey => Color.LightGrey,
                EmbedColorOption.DarkGrey => Color.DarkGrey,
                _ => Color.Blue,  // Default to Blue if somehow an undefined enum value is used
            };
        }
    }
}