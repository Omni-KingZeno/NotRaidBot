﻿using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using SysBot.Pokemon.Discord;

namespace SysBot.Pokemon.ConsoleApp
{
    /// <summary>
    /// Bot Environment implementation with Integrations added.
    /// </summary>
    public class PokeBotRunnerImpl<T> : PokeBotRunner<T> where T : PKM, new()
    {
        public PokeBotRunnerImpl(PokeRaidHub<T> hub, BotFactory<T> fac) : base(hub, fac)
        {
        }

        public PokeBotRunnerImpl(PokeRaidHubConfig config, BotFactory<T> fac) : base(config, fac)
        {
        }

        protected override void AddIntegrations()
        {
            AddDiscordBot(Hub.Config.Discord);
        }

        private void AddDiscordBot(DiscordSettings config)
        {
            var token = config.Token;
            if (string.IsNullOrWhiteSpace(token))
                return;

            var bot = new SysCord<T>(this);
            Task.Run(() => bot.MainAsync(token, CancellationToken.None), CancellationToken.None);
        }
    }
}