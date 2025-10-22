using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Faction;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Core.Packets.G2C;
using System.Numerics;
using NLog;

namespace AAEmu.Game.Core.Managers;

public class FullPvPManager : Singleton<FullPvPManager>
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
    
    private bool _fullPvPEnabled = true;
    private bool _coinpurseDropEnabled = true;
    private float _coinpurseDropRate = 0.5f; // 50% das coinpurses

    /// <summary>
    /// Inicializa o sistema de Full PvP
    /// </summary>
    public void Initialize()
    {
        Logger.Info("FullPvP System initialized - All players are hostile except guild/family members");
    }

    /// <summary>
    /// Determina a relação entre dois personagens no sistema Full PvP
    /// </summary>
    /// <param name="character1">Primeiro personagem</param>
    /// <param name="character2">Segundo personagem</param>
    /// <returns>Estado da relação</returns>
    public RelationState GetFullPvPRelation(Character character1, Character character2)
    {
        if (!_fullPvPEnabled)
            return RelationState.Neutral;

        if (character1 == null || character2 == null)
            return RelationState.Neutral;

        // Se é o mesmo jogador
        if (character1.Id == character2.Id)
            return RelationState.Friendly;

        // Verificar se estão na mesma guilda (Expedition)
        if (character1.Expedition != null && character2.Expedition != null)
        {
            if (character1.Expedition.Id == character2.Expedition.Id)
                return RelationState.Friendly;
        }

        // Verificar se estão na mesma família
        if (character1.Family != 0 && character2.Family != 0)
        {
            if (character1.Family == character2.Family)
                return RelationState.Friendly;
        }

        // Caso contrário, todos são inimigos em Full PvP
        return RelationState.Hostile;
    }

    /// <summary>
    /// Determina se um personagem pode atacar outro no sistema Full PvP
    /// </summary>
    /// <param name="attacker">Personagem atacante</param>
    /// <param name="target">Personagem alvo</param>
    /// <returns>True se pode atacar</returns>
    public bool CanAttackInFullPvP(Character attacker, Character target)
    {
        if (!_fullPvPEnabled)
            return false;

        var relation = GetFullPvPRelation(attacker, target);
        return relation == RelationState.Hostile;
    }

    /// <summary>
    /// Processa o drop de coinpurses quando um jogador morre em PvP
    /// </summary>
    /// <param name="killedPlayer">Jogador que morreu</param>
    /// <param name="killer">Jogador que matou (pode ser null)</param>
    /// <param name="deathPosition">Posição onde morreu</param>
    public void ProcessCoinpurseDropOnPvPDeath(Character killedPlayer, Character killer, Vector3 deathPosition)
    {
        if (!_coinpurseDropEnabled || killedPlayer == null)
            return;

        // Se não foi morto por outro jogador, não dropa coinpurses
        if (killer == null || killer == killedPlayer)
            return;

        // Verificar se estão na mesma guilda/família (amigos não dropam coinpurses entre si)
        var relation = GetFullPvPRelation(killedPlayer, killer);
        if (relation == RelationState.Friendly)
            return;

        var coinpursesToDrop = new List<Item>();
        var totalCoinpurses = 0;

        // Procurar por coinpurses no inventário
        var inventory = killedPlayer.Inventory;
        foreach (var container in inventory.Containers)
        {
            var itemsToRemove = new List<Item>();
            
            foreach (var item in container.Value.Items)
            {
                if (item == null) continue;

                // Verificar se é uma coinpurse (baseado no template ou nome)
                if (IsCoinpurse(item))
                {
                    totalCoinpurses += (int)item.Count;
                    itemsToRemove.Add(item);
                }
            }

            // Remover coinpurses do inventário
            foreach (var item in itemsToRemove)
            {
                container.Value.RemoveItem(ItemTaskType.PvPDrop, item, true);
                coinpursesToDrop.Add(item);
            }
        }

        if (coinpursesToDrop.Count == 0)
            return;

        // Calcular quantas coinpurses serão dropadas (50%)
        var totalToDrop = (int)(totalCoinpurses * _coinpurseDropRate);
        var droppedCount = 0;

        foreach (var coinpurse in coinpursesToDrop)
        {
            if (droppedCount >= totalToDrop)
                break;

            var dropCount = Math.Min((int)coinpurse.Count, totalToDrop - droppedCount);
            
            if (dropCount > 0)
            {
                // Criar item no container de loot do jogador morto
                var droppedItem = ItemManager.Instance.Create(coinpurse.TemplateId, (uint)dropCount, coinpurse.Grade);
                
                // Adicionar ao container de loot do jogador para que apareça quando lootado
                if (killedPlayer.LootingContainer.Items.Count < 100) // Limite de segurança
                {
                    var itemIndex = killedPlayer.LootingContainer.Items.Count;
                    var lootEntry = new Models.Game.Items.Loots.LootItem
                    {
                        Item = droppedItem,
                        ItemIndex = (byte)itemIndex,
                        Count = (uint)dropCount
                    };
                    killedPlayer.LootingContainer.Items.Add((byte)itemIndex, lootEntry);
                }
                
                droppedCount += dropCount;
            }
        }

        // Notificar jogadores sobre o drop
        if (droppedCount > 0)
        {
            killedPlayer.SendMessage($"|cFFFF0000You dropped {droppedCount} coinpurses due to PvP death!|r");
            killer?.SendMessage($"|cFF00FF00{killedPlayer.Name} dropped {droppedCount} coinpurses!|r");
            
            // Notificar jogadores próximos
            var nearbyPlayers = WorldManager.Instance.GetAroundObjects<Character>(killedPlayer, 50f);
            foreach (var player in nearbyPlayers)
            {
                if (player != killedPlayer && player != killer)
                {
                    player.SendMessage($"|cFFFFFF00{killedPlayer.Name} dropped coinpurses nearby!|r");
                }
            }
        }
    }

    /// <summary>
    /// Verifica se um item é uma coinpurse
    /// </summary>
    /// <param name="item">Item a verificar</param>
    /// <returns>True se for uma coinpurse</returns>
    private bool IsCoinpurse(Item item)
    {
        if (item?.Template == null)
            return false;

        // Verificar por nome ou categoria
        var name = item.Template.Name.ToLower();
        return name.Contains("coinpurse") || 
               name.Contains("coin purse") || 
               name.Contains("purse") ||
               name.Contains("주머니") || // Korean
               name.Contains("財布"); // Other languages
    }

    /// <summary>
    /// Ativa ou desativa o sistema Full PvP
    /// </summary>
    /// <param name="enabled">True para ativar</param>
    public void SetFullPvPEnabled(bool enabled)
    {
        _fullPvPEnabled = enabled;
        Logger.Info($"FullPvP System {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Ativa ou desativa o drop de coinpurses
    /// </summary>
    /// <param name="enabled">True para ativar</param>
    public void SetCoinpurseDropEnabled(bool enabled)
    {
        _coinpurseDropEnabled = enabled;
        Logger.Info($"Coinpurse drop on PvP death {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Define a taxa de drop de coinpurses (0.0 a 1.0)
    /// </summary>
    /// <param name="rate">Taxa de drop</param>
    public void SetCoinpurseDropRate(float rate)
    {
        _coinpurseDropRate = Math.Clamp(rate, 0f, 1f);
        Logger.Info($"Coinpurse drop rate set to {_coinpurseDropRate * 100}%");
    }

    /// <summary>
    /// Obtém informações sobre o estado do sistema
    /// </summary>
    /// <returns>String com informações</returns>
    public string GetSystemStatus()
    {
        return $"FullPvP: {(_fullPvPEnabled ? "ON" : "OFF")}, " +
               $"Coinpurse Drop: {(_coinpurseDropEnabled ? "ON" : "OFF")}, " +
               $"Drop Rate: {_coinpurseDropRate * 100}%";
    }
}