# Sistema de Follow para ArcheAge Emulator

## Descrição

Este sistema implementa a funcionalidade de "follow" entre jogadores no emulador AAEmu, com as seguintes restrições:

- **Funciona apenas a pé ou em montaria**: Players podem seguir outros quando ambos estão caminhando ou montados em criaturas
- **NÃO funciona em veículos**: O sistema automaticamente para o follow se qualquer jogador entrar em um veículo (barco, trator, etc.)

## Componentes Implementados

### 1. FollowManager.cs
- **Localização**: `AAEmu.Game/Core/Managers/FollowManager.cs`
- **Função**: Gerencia todo o sistema de follow
- **Principais métodos**:
  - `StartFollow()`: Inicia o follow com validações
  - `StopFollow()`: Para o follow
  - `IsPlayerInVehicle()`: Verifica se jogador está em veículo
  - `UpdateFollowMovement()`: Atualiza movimento a cada 100ms

### 2. Propriedade FollowTarget no Character
- **Localização**: `AAEmu.Game/Models/Game/Char/Character.cs`
- **Adicionado**: `public Character FollowTarget { get; set; }`

### 3. Pacotes de Rede
- **CSStartFollowPacket.cs**: Comando para iniciar follow
- **CSStopFollowPacket.cs**: Comando para parar follow
- **Offsets**: 0x142 e 0x143 (adicionados em CSOffsets.cs)

### 4. Comando de GM
- **Localização**: `AAEmu.Game/Scripts/Commands/Follow.cs`
- **Uso**: `/follow start [player_name]`, `/follow stop`, `/follow status`

## Validações Implementadas

1. **Verificação de Veículo**: Checa se jogador está attachado a um Slave (veículo)
2. **Verificação de Montaria**: Permite follow quando em montaria (IsRiding = true)
3. **Verificação de Party**: Requer que ambos jogadores estejam no mesmo grupo
4. **Verificação de Distância**: Máximo 100m para iniciar, 150m para manter
5. **Verificação de Combate**: Não permite follow em combate
6. **Auto-stop**: Para automaticamente se jogador entrar em veículo

## Integração com Sistema Existente

### GameService.cs
```csharp
FollowManager.Instance.Initialize(); // Adicionado após TeamManager.Instance.Load()
```

### CSMoveUnitPacket.cs
```csharp
// Stop follow if player enters vehicle
FollowManager.Instance.StopFollow(character);
```

### GameNetwork.cs
```csharp
RegisterPacket(CSOffsets.CSStartFollowPacket, 1, typeof(CSStartFollowPacket));
RegisterPacket(CSOffsets.CSStopFollowPacket, 1, typeof(CSStopFollowPacket));
```

## Como Usar

### Para Administradores
1. Use o comando `/follow start [player_name]` ou selecione um jogador e use `/follow start`
2. Use `/follow stop` para parar
3. Use `/follow status` para ver status atual e informações de veículo/montaria

### Para Implementação no Cliente
Os pacotes 0x142 e 0x143 podem ser usados pelo cliente para enviar comandos de follow.

## Funcionalidades

### ✅ Implementado
- Follow entre jogadores
- Restrições de veículo/montaria conforme solicitado
- Validações de party, distância e combate
- Auto-stop quando entra em veículo
- Comando de GM para testes
- Atualização automática de movimento

### 🔄 Possíveis Melhorias Futuras
- Interface de cliente para botão de follow
- Configurações de distância customizáveis
- Logs de sistema para debug
- Opção de follow sem necessidade de party (configurável)

## Arquivos Modificados

1. `AAEmu.Game/Core/Managers/FollowManager.cs` (novo)
2. `AAEmu.Game/Models/Game/Char/Character.cs` (propriedade adicionada)
3. `AAEmu.Game/Core/Packets/C2G/CSStartFollowPacket.cs` (novo)
4. `AAEmu.Game/Core/Packets/C2G/CSStopFollowPacket.cs` (novo)
5. `AAEmu.Game/Core/Packets/C2G/CSOffsets.cs` (offsets adicionados)
6. `AAEmu.Game/Core/Network/Game/GameNetwork.cs` (registros adicionados)
7. `AAEmu.Game/GameService.cs` (inicialização adicionada)
8. `AAEmu.Game/Core/Packets/C2G/CSMoveUnitPacket.cs` (auto-stop adicionado)
9. `AAEmu.Game/Scripts/Commands/Follow.cs` (novo)

## Testando o Sistema

1. Compile o servidor
2. Inicie o servidor
3. Conecte dois jogadores
4. Forme um party
5. Use `/follow start [nome_do_jogador]` com um dos jogadores
6. Teste entrar em veículos para verificar se o follow para automaticamente
7. Teste com montarias para verificar se continua funcionando