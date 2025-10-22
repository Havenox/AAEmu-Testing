using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Core.Managers;

public class FollowManager : Singleton<FollowManager>
{
    private Dictionary<uint, uint> _followingTargets = []; // followerId, targetId

    /// <summary>
    /// Inicializa o FollowManager e registra o timer de atualização
    /// </summary>
    public void Initialize()
    {
        // Registra atualização a cada 100ms
        TickManager.Instance.OnTick.Subscribe(UpdateFollowMovement, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// Inicia o follow de um jogador por outro
    /// </summary>
    /// <param name="follower">Jogador que vai seguir</param>
    /// <param name="target">Jogador a ser seguido</param>
    /// <returns>True se o follow foi iniciado com sucesso</returns>
    public bool StartFollow(Character follower, Character target)
    {
        if (follower == null || target == null)
            return false;

        // Verificar se o jogador seguido está em um veículo
        if (IsPlayerInVehicle(target))
        {
            follower.SendMessage("|cFFFF0000Cannot follow: Target is in a vehicle.|r");
            return false;
        }

        // Verificar se o jogador seguidor está em um veículo
        if (IsPlayerInVehicle(follower))
        {
            follower.SendMessage("|cFFFF0000Cannot follow: You are in a vehicle.|r");
            return false;
        }

        // Verificar se estão na mesma party/team (opcional - desabilitado para permitir follow entre qualquer jogador)
        // var followerTeam = TeamManager.Instance.GetActiveTeamByUnit(follower.Id);
        // var targetTeam = TeamManager.Instance.GetActiveTeamByUnit(target.Id);
        // 
        // if (followerTeam == null || targetTeam == null || followerTeam.Id != targetTeam.Id)
        // {
        //     follower.SendMessage("|cFFFF0000Cannot follow: Target must be in your party.|r");
        //     return false;
        // }

        // Verificar distância
        var distance = follower.GetDistanceTo(target);
        if (distance > 100f) // 100 metros de distância máxima
        {
            follower.SendMessage("|cFFFF0000Cannot follow: Target is too far away.|r");
            return false;
        }

        // Verificar se está em combate
        if (follower.IsInBattle)
        {
            follower.SendMessage("|cFFFF0000Cannot follow: Cannot follow while in combat.|r");
            return false;
        }

        // Parar follow anterior se existir
        StopFollow(follower);

        // Iniciar novo follow
        _followingTargets[follower.ObjId] = target.ObjId;
        follower.FollowTarget = target;
        
        follower.SendMessage($"|cFF00FF00Following {target.Name}.|r");
        
        return true;
    }

    /// <summary>
    /// Para o follow de um jogador
    /// </summary>
    /// <param name="follower">Jogador que está seguindo</param>
    public void StopFollow(Character follower)
    {
        if (follower == null)
            return;

        if (_followingTargets.ContainsKey(follower.ObjId))
        {
            _followingTargets.Remove(follower.ObjId);
            follower.FollowTarget = null;
            follower.SendMessage("|cFFFFFF00Follow stopped.|r");
        }
    }

    /// <summary>
    /// Verifica se um jogador está em um veículo (não montaria)
    /// </summary>
    /// <param name="character">Jogador a verificar</param>
    /// <returns>True se estiver em veículo</returns>
    public bool IsPlayerInVehicle(Character character)
    {
        // Se está montado em uma criatura (IsRiding), é montaria, não veículo
        if (character.IsRiding)
            return false;

        // Verificar se o Transform tem um Parent que é um Slave (veículo)
        if (character.Transform.Parent?.GameObject is Slave)
            return true;

        // Verificar se está attachado a um Slave (veículo) próximo
        var nearbySlaves = WorldManager.Instance.GetAroundObjects<Slave>(character, 15f);
        foreach (var slave in nearbySlaves)
        {
            if (slave.AttachedCharacters.ContainsValue(character))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Atualiza o movimento de follow para todos os jogadores seguindo
    /// </summary>
    public void UpdateFollowMovement(TimeSpan delta)
    {
        var followersToRemove = new List<uint>();

        foreach (var kvp in _followingTargets)
        {
            var followerObjId = kvp.Key;
            var targetObjId = kvp.Value;

            var follower = WorldManager.Instance.GetCharacterByObjId(followerObjId);
            var target = WorldManager.Instance.GetCharacterByObjId(targetObjId);

            if (follower == null || target == null)
            {
                followersToRemove.Add(followerObjId);
                continue;
            }

            // Verificar se algum dos jogadores entrou em veículo
            if (IsPlayerInVehicle(follower) || IsPlayerInVehicle(target))
            {
                StopFollow(follower);
                continue;
            }

            // Verificar distância
            var distance = follower.GetDistanceTo(target);
            if (distance > 150f) // Parar follow se muito longe
            {
                StopFollow(follower);
                continue;
            }

            // Se estiver muito perto, não se move
            if (distance < 3f)
                continue;

            // Calcular nova posição (atrás do target)
            var direction = target.Transform.World.Position - follower.Transform.World.Position;
            direction = direction with { Z = 0 }; // Ignorar diferença de altura
            direction = System.Numerics.Vector3.Normalize(direction);

            var targetPosition = target.Transform.World.Position - direction * 2f; // 2 metros atrás

            // Mover o follower na direção do target
            follower.MoveTowards(targetPosition, follower.BaseMoveSpeed * 1.1f, 0);
        }

        // Remover followers inválidos
        foreach (var objId in followersToRemove)
        {
            _followingTargets.Remove(objId);
        }
    }

    /// <summary>
    /// Verifica se um jogador está seguindo alguém
    /// </summary>
    /// <param name="follower">Jogador a verificar</param>
    /// <returns>True se estiver seguindo</returns>
    public bool IsFollowing(Character follower)
    {
        return follower != null && _followingTargets.ContainsKey(follower.ObjId);
    }

    /// <summary>
    /// Obtém o target que um jogador está seguindo
    /// </summary>
    /// <param name="follower">Jogador seguidor</param>
    /// <returns>Jogador sendo seguido ou null</returns>
    public Character GetFollowTarget(Character follower)
    {
        if (follower == null || !_followingTargets.TryGetValue(follower.ObjId, out var targetObjId))
            return null;

        return WorldManager.Instance.GetCharacterByObjId(targetObjId);
    }
}