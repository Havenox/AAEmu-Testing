using System.Collections.Concurrent;
using System.Net;
using AAEmu.Commons.Utils.DB;
using AAEmu.Login.Core.Network.Connections;
using AAEmu.Login.Core.Packets.L2C;
using AAEmu.Login.Core.Packets.L2G;
using AAEmu.Login.Models;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using NLog;

namespace AAEmu.Login.Core.Controllers;

public class LoginController(IGameController gameController, ILoginConnectionTable connectionTable, IOptions<AppConfiguration> appConfig) : ILoginController
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    private readonly bool _autoAccount = appConfig.Value.AutoAccount;
    private readonly AppConfiguration.AntiMultiLoginConfig _antiMultiLoginConfig = appConfig.Value.AntiMultiLogin;

    private readonly ConcurrentDictionary<GameServerId, ConcurrentDictionary<uint, AccountId>>
        _tokens = []; // gsId, [token, accountId]

    /// <summary>
    /// Verifica se a conexão deve ser permitida baseado nas políticas anti-multi-login
    /// </summary>
    /// <param name="connection">Conexão atual</param>
    /// <param name="accountId">ID da conta (se disponível)</param>
    /// <returns>True se a conexão deve ser permitida</returns>
    private bool CheckAntiMultiLogin(LoginConnection connection, AccountId? accountId = null)
    {
        if (!_antiMultiLoginConfig.Enabled)
            return true;

        // Verifica se o IP está na lista de isentos
        if (_antiMultiLoginConfig.ExemptIps.Contains(connection.Ip.ToString()))
        {
            Logger.Debug($"IP {connection.Ip} está na lista de isentos, permitindo conexão");
            return true;
        }

        // Verifica conexões por IP
        if (_antiMultiLoginConfig.PreventMultipleIpConnections)
        {
            var existingIpConnections = connectionTable.GetConnectionsByIp(connection.Ip);
            existingIpConnections = existingIpConnections.Where(c => c.Id != connection.Id).ToList(); // Remove a conexão atual

            if (existingIpConnections.Any())
            {
                Logger.Info($"Detectada tentativa de múltipla conexão do IP {connection.Ip}");
                
                if (_antiMultiLoginConfig.DisconnectPreviousConnection)
                {
                    Logger.Info($"Desconectando {existingIpConnections.Count} conexão(ões) anterior(es) do IP {connection.Ip}");
                    foreach (var existingConnection in existingIpConnections)
                    {
                        existingConnection.SendPacket(new ACLoginDeniedPacket(3)); // Motivo: desconectado por nova conexão
                        existingConnection.Shutdown();
                    }
                }
                else
                {
                    Logger.Info($"Negando nova conexão do IP {connection.Ip} - já existe conexão ativa");
                    return false;
                }
            }
        }
        else if (_antiMultiLoginConfig.MaxConnectionsPerIp > 0)
        {
            var existingIpConnections = connectionTable.GetConnectionsByIp(connection.Ip);
            existingIpConnections = existingIpConnections.Where(c => c.Id != connection.Id).ToList();

            if (existingIpConnections.Count >= _antiMultiLoginConfig.MaxConnectionsPerIp)
            {
                Logger.Info($"IP {connection.Ip} atingiu o máximo de {_antiMultiLoginConfig.MaxConnectionsPerIp} conexões");
                return false;
            }
        }

        // Verifica conexões por conta (apenas se accountId foi fornecido)
        if (accountId != null && _antiMultiLoginConfig.PreventMultipleAccountConnections)
        {
            var existingAccountConnection = connectionTable.GetConnectionByAccountId(accountId.Value);
            
            if (existingAccountConnection != null && existingAccountConnection.Id != connection.Id)
            {
                Logger.Info($"Detectada tentativa de múltipla conexão da conta {accountId.Value.Value}");
                
                if (_antiMultiLoginConfig.DisconnectPreviousConnection)
                {
                    Logger.Info($"Desconectando conexão anterior da conta {accountId.Value.Value}");
                    existingAccountConnection.SendPacket(new ACLoginDeniedPacket(4)); // Motivo: desconectado por login em outro lugar
                    existingAccountConnection.Shutdown();
                }
                else
                {
                    Logger.Info($"Negando nova conexão da conta {accountId.Value.Value} - já está logada");
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Kr Method Auth
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="username"></param>
    public void Login(LoginConnection connection, string username)
    {
        using var connect = MySQL.CreateConnection();
        using var command = connect.CreateCommand();
        command.CommandText = "SELECT * FROM users where username=@username";
        command.Parameters.AddWithValue("@username", username);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            connection.SendPacket(new ACLoginDeniedPacket(2));
            return;
        }

        var accountId = new AccountId(reader.GetUInt32("id"));
        
        // Verificação anti-multi-login
        if (!CheckAntiMultiLogin(connection, accountId))
        {
            connection.SendPacket(new ACLoginDeniedPacket(5)); // Motivo: múltiplas conexões não permitidas
            return;
        }

        // TODO ... validation password

        connection.AccountId = accountId;
        connection.AccountName = username;
        connection.LastLogin = DateTime.UtcNow;
        connection.LastIp = connection.Ip;

        connection.SendPacket(new ACJoinResponsePacket(0, 6));
        connection.SendPacket(new ACAuthResponsePacket(connection.AccountId, 6));

        reader.Close();

        #region update account

        command.Parameters.Clear();
        command.CommandText =
            "UPDATE `users` SET last_ip = @last_ip, last_login = @last_login, updated_at = @updated_at WHERE id = @id";
        command.Parameters.AddWithValue("@id", connection.AccountId.Value);
        command.Parameters.AddWithValue("@last_ip", connection.LastIp.ToString());
        command.Parameters.AddWithValue("@last_login", ((DateTimeOffset)connection.LastLogin).ToUnixTimeSeconds());
        command.Parameters.AddWithValue("@updated_at", ((DateTimeOffset)connection.LastLogin).ToUnixTimeSeconds());

        if (command.ExecuteNonQuery() != 1)
        {
            Logger.Warn("Database update failed, error occurred while updating account login IP and time");
        }

        # endregion
    }

    /// <summary>
    /// Eu Method Auth
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="username"></param>
    /// <param name="password"></param>
    public void Login(LoginConnection connection, string username, ReadOnlySpan<byte> password)
    {
        using var connect = MySQL.CreateConnection();
        using var command = connect.CreateCommand();
        command.CommandText = "SELECT * FROM users where username=@username";
        command.Parameters.AddWithValue("@username", username);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            if (_autoAccount)
            {
                reader.Close();
                CreateAndLoginInvalid(connection, username, password, connect);
            }
            else
            {
                connection.SendPacket(new ACLoginDeniedPacket(2));
            }

            return;
        }

        var expectedPassword = Convert.FromBase64String(reader.GetString("password"));
        if (!password.SequenceEqual(expectedPassword))
        {
            connection.SendPacket(new ACLoginDeniedPacket(2));
            return;
        }

        var banned = reader.GetBoolean("banned");
        if (banned)
        {
            var banReason = (byte)reader.GetUInt32("ban_reason");
            connection.SendPacket(new ACLoginDeniedPacket(banReason));
            return;
        }

        var accountId = new AccountId(reader.GetUInt32("id"));
        
        // Verificação anti-multi-login
        if (!CheckAntiMultiLogin(connection, accountId))
        {
            connection.SendPacket(new ACLoginDeniedPacket(5)); // Motivo: múltiplas conexões não permitidas
            return;
        }

        connection.AccountId = accountId;
        connection.AccountName = username;
        connection.LastLogin = DateTime.UtcNow;
        connection.LastIp = connection.Ip;

        Logger.Info("{0} connected.", connection.AccountName);
        connection.SendPacket(new ACJoinResponsePacket(0, 6));
        connection.SendPacket(new ACAuthResponsePacket(connection.AccountId, 6));

        reader.Close();

        #region update account

        command.Parameters.Clear();
        command.CommandText =
            "UPDATE `users` SET last_ip = @last_ip, last_login = @last_login, updated_at = @updated_at WHERE id = @id";
        command.Parameters.AddWithValue("@id", connection.AccountId.Value);
        command.Parameters.AddWithValue("@last_ip", connection.LastIp.ToString());
        command.Parameters.AddWithValue("@last_login", ((DateTimeOffset)connection.LastLogin).ToUnixTimeSeconds());
        command.Parameters.AddWithValue("@updated_at", ((DateTimeOffset)connection.LastLogin).ToUnixTimeSeconds());

        if (command.ExecuteNonQuery() != 1)
        {
            Logger.Warn("Database update failed, error occurred while updating account login IP and time");
        }

        # endregion
    }

    public void CreateAndLoginInvalid(LoginConnection connection, string username, ReadOnlySpan<byte> password,
        MySqlConnection connect)
    {
        var pass = Convert.ToBase64String(password);

        using var command = connect.CreateCommand();
        command.CommandText =
            "INSERT into users (username, password, email, last_ip, last_login, created_at, updated_at) VALUES (@username, @password, @email, @last_ip, @last_login, @created_at, @updated_at)";
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@password", pass);
        command.Parameters.AddWithValue("@email", "");
        command.Parameters.AddWithValue("@last_ip", connection.Ip.ToString());
        command.Parameters.AddWithValue("@last_login", ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds());
        command.Parameters.AddWithValue("@created_at", ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds());
        command.Parameters.AddWithValue("@updated_at", ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds());

        if (command.ExecuteNonQuery() != 1)
        {
            connection.SendPacket(new ACLoginDeniedPacket(2));
            return;
        }

        Logger.Debug("Created account from invalid username login with value:" + username);
        Login(connection, username, password);
    }

    public void AddReconnectionToken(InternalConnection connection, GameServerId gsId, AccountId accountId, uint token)
    {
        var tokensForGameServer = _tokens.GetOrAdd(gsId, static _ => []);
        tokensForGameServer.TryAdd(token, accountId);
        connection.SendPacket(new LGPlayerReconnectPacket(token));
    }

    public void Reconnect(LoginConnection connection, GameServerId gsId, AccountId accountId, uint token)
    {
        if (!_tokens.ContainsKey(gsId))
        {
            if (gameController.TryGetParentId(gsId, out var parentId))
                gsId = parentId;
            else
            {
                // TODO ...
                return;
            }
        }

        if (!_tokens[gsId].TryGetValue(token, out var value))
        {
            // TODO ...
            return;
        }

        if (value == accountId)
        {
            connection.AccountId = accountId;
            connection.SendPacket(new ACJoinResponsePacket(0, 6));
            connection.SendPacket(new ACAuthResponsePacket(connection.AccountId, 6));
        }
        else
        {
            // TODO ...
        }
    }
}
