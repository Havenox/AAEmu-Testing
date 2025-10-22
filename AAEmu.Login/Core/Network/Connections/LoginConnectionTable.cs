using System.Collections.Concurrent;
using System.Net;
using AAEmu.Login.Models;

namespace AAEmu.Login.Core.Network.Connections;

public class LoginConnectionTable : ILoginConnectionTable
{
    private readonly ConcurrentDictionary<ConnectionId, LoginConnection> _connections = [];

    public void AddConnection(LoginConnection con)
    {
        _connections.TryAdd(con.Id, con);
    }

    public LoginConnection? GetConnection(ConnectionId id)
    {
        _connections.TryGetValue(id, out var con);
        return con;
    }

    public LoginConnection? RemoveConnection(ConnectionId id)
    {
        _connections.TryRemove(id, out var con);
        return con;
    }

    public List<LoginConnection> GetConnections()
    {
        return [.. _connections.Values];
    }

    // Implementação dos novos métodos anti-multi-login
    public LoginConnection? GetConnectionByAccountId(AccountId accountId)
    {
        return _connections.Values.FirstOrDefault(c => c.AccountId.Value != 0 && c.AccountId == accountId);
    }

    public List<LoginConnection> GetConnectionsByIp(IPAddress ipAddress)
    {
        return _connections.Values.Where(c => c.Ip.Equals(ipAddress)).ToList();
    }

    public bool HasActiveConnectionForAccount(AccountId accountId)
    {
        return GetConnectionByAccountId(accountId) != null;
    }

    public bool HasActiveConnectionForIp(IPAddress ipAddress)
    {
        return GetConnectionsByIp(ipAddress).Any();
    }
}
