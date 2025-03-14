using System;
using System.Buffers.Binary;
using PKHeX.Core;

namespace SysBot.Pokemon.SV.BotRaid
{
    public sealed class RaidPartnerSV(RaidMyStatus info)
    {
        public string TID7 { get; } = info.DisplayTID.ToString("D6");
        public string SID7 { get; } = info.DisplaySID.ToString("D4");
        public string TrainerName { get; } = info.OT;
    }

    public sealed class RaidMyStatus
    {
        public readonly byte[] Data = new byte[0x30];

        public uint DisplaySID => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(0)) / 1_000_000;
        public uint DisplayTID => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(0)) % 1_000_000;

        public int Game => Data[4];
        public int Gender => Data[5];
        public int Language => Data[6];

        public string OT => StringConverter8.GetString(Data.AsSpan(8, 24));
    }
}