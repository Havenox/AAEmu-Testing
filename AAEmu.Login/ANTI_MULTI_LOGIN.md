# Sistema Anti-Multi-Login

Este documento explica como configurar e usar o sistema anti-multi-login no emulador ArcheAge.

## O que é Multi-Login?

Multi-login refere-se à capacidade de um jogador conectar múltiplas contas simultaneamente, geralmente para:
- Usar múltiplos personagens ao mesmo tempo
- Explorar mecânicas de jogo (como comércio entre próprias contas)
- Obter vantagens injustas no jogo

## Funcionalidades Implementadas

### 1. **Prevenção por IP Address**
Impede que múltiplas contas sejam conectadas do mesmo endereço IP.

### 2. **Prevenção por Account ID**
Impede que a mesma conta seja logada múltiplas vezes simultaneamente.

### 3. **Lista de IPs Isentos**
Permite que determinados IPs (como admins) sejam isentos das verificações.

### 4. **Comportamento Configurável**
- **Disconnect Previous**: Desconecta conexões anteriores quando uma nova é feita
- **Deny New**: Nega novas conexões se já existe uma ativa

## Configuração

### Config.json

```json
{
  "AntiMultiLogin": {
    "Enabled": true,
    "PreventMultipleIpConnections": true,
    "PreventMultipleAccountConnections": true,
    "DisconnectPreviousConnection": true,
    "ExemptIps": [
      "127.0.0.1",
      "::1",
      "192.168.1.100"
    ],
    "MaxConnectionsPerIp": 1
  }
}
```

### Parâmetros de Configuração

| Parâmetro | Tipo | Padrão | Descrição |
|-----------|------|--------|-----------|
| `Enabled` | bool | false | Ativa/desativa o sistema anti-multi-login |
| `PreventMultipleIpConnections` | bool | true | Impede múltiplas conexões do mesmo IP |
| `PreventMultipleAccountConnections` | bool | true | Impede múltiplas conexões da mesma conta |
| `DisconnectPreviousConnection` | bool | true | Se true, desconecta conexão anterior; se false, nega nova conexão |
| `ExemptIps` | string[] | [] | Lista de IPs isentos da verificação |
| `MaxConnectionsPerIp` | int | 1 | Máximo de conexões por IP (funciona se PreventMultipleIpConnections = false) |

## Cenários de Uso

### 1. **Servidor PvP Competitivo**
```json
{
  "AntiMultiLogin": {
    "Enabled": true,
    "PreventMultipleIpConnections": true,
    "PreventMultipleAccountConnections": true,
    "DisconnectPreviousConnection": false,
    "ExemptIps": ["127.0.0.1"],
    "MaxConnectionsPerIp": 1
  }
}
```

### 2. **Servidor Familiar/Casual**
```json
{
  "AntiMultiLogin": {
    "Enabled": true,
    "PreventMultipleIpConnections": false,
    "PreventMultipleAccountConnections": true,
    "DisconnectPreviousConnection": true,
    "ExemptIps": [],
    "MaxConnectionsPerIp": 3
  }
}
```

### 3. **Servidor de Desenvolvimento**
```json
{
  "AntiMultiLogin": {
    "Enabled": false
  }
}
```

## Códigos de Erro

Quando uma conexão é negada, o cliente recebe um `ACLoginDeniedPacket` com os seguintes códigos:

| Código | Significado |
|--------|-------------|
| 2 | Usuário/senha inválido |
| 3 | Desconectado por nova conexão do mesmo IP |
| 4 | Desconectado por login da mesma conta em outro lugar |
| 5 | Múltiplas conexões não permitidas |

## Logs

O sistema gera logs informativos quando detecta tentativas de multi-login:

```
[INFO] Detectada tentativa de múltipla conexão do IP 192.168.1.100
[INFO] Desconectando conexão anterior da conta 12345
[INFO] IP 192.168.1.200 está na lista de isentos, permitindo conexão
```

## Implementação Técnica

### Arquivos Modificados

1. **ILoginConnectionTable.cs** - Adiciona métodos para verificar conexões por IP/conta
2. **LoginConnectionTable.cs** - Implementa os novos métodos de verificação
3. **AppConfiguration.cs** - Adiciona configurações anti-multi-login
4. **LoginController.cs** - Implementa a lógica de verificação
5. **ExampleConfig.json** - Exemplo de configuração

### Métodos Principais

- `CheckAntiMultiLogin()` - Verifica se uma conexão deve ser permitida
- `GetConnectionsByIp()` - Obtém conexões ativas de um IP
- `GetConnectionByAccountId()` - Obtém conexão ativa de uma conta

## Considerações de Performance

- As verificações são executadas apenas durante o login
- Usa `ConcurrentDictionary` para thread-safety
- Impacto mínimo na performance do servidor
- Verificações são otimizadas com LINQ

## Limitações

1. **Detecção por IP**: Pode afetar usuários legítimos atrás do mesmo NAT/Proxy
2. **Bypass via VPN**: Usuários podem usar VPNs para contornar restrições de IP
3. **Não afeta conexões já estabelecidas**: Apenas verifica durante novo login

## Sugestões Avançadas

Para maior segurança, considere implementar:

1. **Verificação por Hardware ID** - Mais difícil de contornar
2. **Sistema de Whitelist** - Apenas IPs aprovados podem se conectar
3. **Rate Limiting** - Limita tentativas de login por IP
4. **Log de Auditoria** - Registra todas as tentativas de multi-login

## Troubleshooting

### Problema: Usuários legítimos sendo bloqueados
**Solução**: Adicione o IP na lista `ExemptIps` ou ajuste `MaxConnectionsPerIp`

### Problema: Sistema não está funcionando
**Solução**: Verifique se `Enabled: true` e revise os logs do servidor

### Problema: Performance degradada
**Solução**: O sistema é otimizado, mas verifique se há muitas conexões simultâneas