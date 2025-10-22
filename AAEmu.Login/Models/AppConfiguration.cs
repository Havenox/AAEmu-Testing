using System.ComponentModel.DataAnnotations;
using AAEmu.Commons.Models;

namespace AAEmu.Login.Models;

public class AppConfiguration
{
    [Required]
    public required string SecretKey { get; set; }
    public bool AutoAccount { get; set; }
    public bool SkipHostResolve { get; set; }
    [Required]
    public required DBConnections Connections { get; set; }
    [Required]
    public required NetworkConfig InternalNetwork { get; set; }
    [Required]
    public required NetworkConfig Network { get; set; }

    // Configurações Anti-Multi-Login
    public AntiMultiLoginConfig AntiMultiLogin { get; set; } = new();

    public class AntiMultiLoginConfig
    {
        /// <summary>
        /// Ativa verificação anti-multi-login
        /// </summary>
        public bool Enabled { get; set; } = false;
        
        /// <summary>
        /// Impede múltiplas conexões do mesmo IP
        /// </summary>
        public bool PreventMultipleIpConnections { get; set; } = true;
        
        /// <summary>
        /// Impede múltiplas conexões da mesma conta
        /// </summary>
        public bool PreventMultipleAccountConnections { get; set; } = true;
        
        /// <summary>
        /// Desconecta conexão anterior quando nova conexão é feita (se false, nega a nova conexão)
        /// </summary>
        public bool DisconnectPreviousConnection { get; set; } = true;
        
        /// <summary>
        /// Lista de IPs que são isentos da verificação (ex: IPs de admin)
        /// </summary>
        public List<string> ExemptIps { get; set; } = new();
        
        /// <summary>
        /// Máximo de conexões permitidas por IP (0 = ilimitado, funciona apenas se PreventMultipleIpConnections = false)
        /// </summary>
        public int MaxConnectionsPerIp { get; set; } = 1;
    }

    public class NetworkConfig
    {
        [Required]
        public required string Host { get; set; }
        [Required]
        public required ushort Port { get; set; }
        public required int NumConnections { get; set; }
    }

    public class DBConnections
    {
        [Required]
        public required MySqlConnectionSettings MySQLProvider { get; set; }
    }
}
