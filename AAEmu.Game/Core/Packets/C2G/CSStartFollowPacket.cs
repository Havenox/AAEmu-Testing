using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSStartFollowPacket : GamePacket
{
    public CSStartFollowPacket() : base(CSOffsets.CSStartFollowPacket, 1)
    {
    }

    public override void Read(PacketStream stream)
    {
        var targetObjId = stream.ReadBc();
        
        var follower = Connection.ActiveChar;
        var target = WorldManager.Instance.GetCharacterByObjId(targetObjId);

        if (follower == null)
            return;

        if (target == null)
        {
            follower.SendMessage("|cFFFF0000Cannot follow: Target not found.|r");
            return;
        }

        FollowManager.Instance.StartFollow(follower, target);
    }
}