using AAEmu.Login.Models;
using System.Net;

namespace AAEmu.Login.Core.Network.Connections;

public interface ILoginConnectionTable
{
    void AddConnection(LoginConnection con);
    LoginConnection? GetConnection(ConnectionId id);
    LoginConnection? RemoveConnection(ConnectionId id);
    List<LoginConnection> GetConnections();
    
    // Novos métodos para verificação anti-multi-login
    LoginConnection? GetConnectionByAccountId(AccountId accountId);
    List<LoginConnection> GetConnectionsByIp(IPAddress ipAddress);
    bool HasActiveConnectionForAccount(AccountId accountId);
    bool HasActiveConnectionForIp(IPAddress ipAddress);
}
