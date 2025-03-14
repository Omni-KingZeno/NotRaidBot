using System.Collections.Concurrent;
using PKHeX.Core;

namespace SysBot.Pokemon
{
    /// <summary>
    /// Centralizes logic for Raid bot coordination.
    /// </summary>
    /// <typeparam name="T">Type of <see cref="PKM"/> to distribute.</typeparam>
    public class PokeRaidHub<T>(PokeRaidHubConfig config) where T : PKM, new()
    {
        public RotatingRaidSettingsSV RotatingRaidSV { get; set; }

        public readonly PokeRaidHubConfig Config = config;

        /// <summary> Raid Bots only, used to delegate multi-player tasks </summary>
        public readonly ConcurrentPool<PokeRoutineExecutorBase> Bots = new();

        public bool RaidBotsReady => !Bots.All(z => z.Config.CurrentRoutineType == PokeRoutineType.Idle);
    }
}