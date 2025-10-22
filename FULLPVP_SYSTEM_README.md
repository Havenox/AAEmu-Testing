# Sistema Full PvP para ArcheAge Emulator

## Descrição

Este sistema implementa um modo Full PvP onde **todos os jogadores são inimigos (vermelhos) entre si**, exceto membros da mesma guilda ou família. Além disso, quando um jogador morre em PvP, ele dropa metade das coinpurses do seu inventário.

## Funcionalidades Implementadas

### ✅ **Sistema de Relações Full PvP**
- **Todos contra todos**: Por padrão, todos os jogadores são hostis entre si
- **Exceções amigáveis**:
  - Membros da mesma **guilda (Expedition)** são amigos (verdes)
  - Membros da mesma **família** são amigos (verdes)
  - O próprio jogador aparece como amigo para si mesmo

### ✅ **Drop de Coinpurses em PvP**
- **50% das coinpurses** são dropadas quando morrer em PvP
- **Só dropa se morto por outro jogador** (não por NPCs ou ambiente)
- **Amigos não dropam** coinpurses entre si (mesma guilda/família)
- **Itens aparecem no loot** do jogador morto para qualquer um pegar

## Componentes do Sistema

### 1. FullPvPManager.cs
- **Localização**: `AAEmu.Game/Core/Managers/FullPvPManager.cs`
- **Principais métodos**:
  - `GetFullPvPRelation()`: Determina relação entre dois jogadores
  - `CanAttackInFullPvP()`: Verifica se pode atacar
  - `ProcessCoinpurseDropOnPvPDeath()`: Processa drop de coinpurses
  - `IsCoinpurse()`: Identifica se item é coinpurse

### 2. Modificação BaseUnit.cs
- **Override de `GetRelationStateTo()`**: Usa sistema Full PvP para Characters
- **Mantém sistema normal**: Para NPCs e outras unidades

### 3. Modificação Character.cs
- **Override de `PostUpdateCurrentHp()`**: Processa morte em PvP
- **Detecção automática**: Quando HP chega a 0 por outro jogador

### 4. Comando de GM
- **Localização**: `AAEmu.Game/Scripts/Commands/FullPvP.cs`
- **Uso**: `/fullpvp <comando>`

## Como Usar

### Comandos de Administrador

```bash
# Ativar sistema Full PvP
/fullpvp enable

# Desativar sistema Full PvP (volta ao normal)
/fullpvp disable

# Ativar/desativar drop de coinpurses
/fullpvp coinpurse on
/fullpvp coinpurse off

# Alterar taxa de drop (0.0 = 0%, 1.0 = 100%)
/fullpvp droprate 0.5    # 50% das coinpurses
/fullpvp droprate 0.8    # 80% das coinpurses

# Ver status do sistema
/fullpvp status

# Testar drop de coinpurses
/fullpvp test drop
```

### Status das Relações

O comando `/fullpvp status` mostra:
- Estado do sistema (ON/OFF)
- Taxa de drop de coinpurses
- Relações com jogadores próximos
- Informações de guilda/família

## Configurações Padrão

- **Full PvP**: `ATIVADO` por padrão
- **Drop de Coinpurses**: `ATIVADO` por padrão  
- **Taxa de Drop**: `50%` das coinpurses
- **Limite de segurança**: Máximo 100 itens no loot container

## Lógica de Funcionamento

### Determinação de Relações
```csharp
1. Se é o mesmo jogador → FRIENDLY
2. Se estão na mesma guilda → FRIENDLY  
3. Se estão na mesma família → FRIENDLY
4. Caso contrário → HOSTILE (Full PvP)
```

### Processo de Morte em PvP
```csharp
1. Jogador morre (HP = 0)
2. Sistema verifica se foi morto por outro jogador
3. Se matador ≠ amigo (guilda/família):
   a. Encontra todas as coinpurses no inventário
   b. Remove 50% das coinpurses do inventário
   c. Adiciona itens ao loot container do morto
   d. Notifica jogadores sobre o drop
```

### Identificação de Coinpurses
O sistema identifica coinpurses por nome:
- `coinpurse`, `coin purse`, `purse`
- `주머니` (coreano)
- `財布` (outros idiomas)

## Integração com Sistema Existente

### GameService.cs
```csharp
FullPvPManager.Instance.Initialize(); // Inicialização do sistema
```

### BaseUnit.cs
```csharp
// Override do método de relação para usar Full PvP
public RelationState GetRelationStateTo(BaseUnit unit)
{
    if (this is Character c1 && unit is Character c2)
        return FullPvPManager.Instance.GetFullPvPRelation(c1, c2);
    return this.Faction?.GetRelationState(unit.Faction) ?? RelationState.Neutral;
}
```

### Character.cs
```csharp
// Override para processar morte em PvP
public override void PostUpdateCurrentHp(BaseUnit attackerBase, int oldHpValue, int newHpValue, KillReason killReason)
{
    base.PostUpdateCurrentHp(attackerBase, oldHpValue, newHpValue, killReason);
    if (newHpValue <= 0 && oldHpValue > 0 && attackerBase is Character killer)
        FullPvPManager.Instance.ProcessCoinpurseDropOnPvPDeath(this, killer, Transform.World.Position);
}
```

## Efeitos Visuais

- **Nomes vermelhos**: Jogadores hostis aparecerão com nomes vermelhos
- **Nomes verdes**: Membros da guilda/família aparecerão verdes
- **Loot disponível**: Coinpurses dropadas aparecerão no loot do jogador morto
- **Mensagens do sistema**: Notificações sobre drops e mudanças de status

## Arquivos Modificados/Criados

### Novos Arquivos
1. `AAEmu.Game/Core/Managers/FullPvPManager.cs`
2. `AAEmu.Game/Scripts/Commands/FullPvP.cs`
3. `FULLPVP_SYSTEM_README.md`

### Arquivos Modificados
1. `AAEmu.Game/Models/Game/Units/BaseUnit.cs` (método GetRelationStateTo)
2. `AAEmu.Game/Models/Game/Char/Character.cs` (método PostUpdateCurrentHp)
3. `AAEmu.Game/GameService.cs` (inicialização)

## Possíveis Melhorias Futuras

- **Zonas específicas**: Ativar Full PvP apenas em certas áreas
- **Proteção para iniciantes**: Imunidade para jogadores de baixo nível
- **Drop configurável**: Diferentes tipos de itens para drop
- **Ranking PvP**: Sistema de pontuação para kills
- **Guilds wars**: Sistema de guerra entre guildas específicas
- **Safe zones**: Áreas onde PvP é desabilitado

## Testando o Sistema

1. **Configurar ambiente**:
   - Compile e inicie o servidor
   - Conecte dois jogadores
   - Use `/fullpvp status` para verificar estado

2. **Testar relações**:
   - Jogadores sem guilda devem aparecer como hostis
   - Entre na mesma guilda e verifique se ficam amigos
   - Use `/fullpvp status` para ver relações próximas

3. **Testar drop de coinpurses**:
   - Coloque coinpurses no inventário
   - Mate outro jogador em PvP
   - Verifique se 50% das coinpurses apareceram no loot
   - Use `/fullpvp test drop` para simulação

4. **Testar configurações**:
   - Mude a taxa de drop com `/fullpvp droprate 0.8`
   - Desative o sistema com `/fullpvp disable`
   - Verifique mudanças no comportamento

O sistema está **pronto para produção** e atende aos requisitos de Full PvP com drop de coinpurses!