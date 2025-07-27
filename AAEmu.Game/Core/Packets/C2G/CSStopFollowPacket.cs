using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSStopFollowPacket : GamePacket
{
    public CSStopFollowPacket() : base(CSOffsets.CSStopFollowPacket, 1)
    {
    }

    public override void Read(PacketStream stream)
    {
        var follower = Connection.ActiveChar;

        if (follower == null)
            return;

        FollowManager.Instance.StopFollow(follower);
    }
}