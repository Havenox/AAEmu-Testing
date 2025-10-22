using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

public class FullPvP : ICommand
{
    public string[] CommandNames { get; set; } = ["fullpvp"];

    public void OnLoad()
    {
        CommandManager.Instance.Register("fullpvp", this);
    }

    public string GetCommandLineHelp()
    {
        return "<enable|disable|coinpurse <on|off>|droprate <0.0-1.0>|status>";
    }

    public string GetCommandHelpText()
    {
        return 
            "Manages Full PvP system:\n" +
            "- enable: Enable Full PvP (all players hostile except guild/family)\n" +
            "- disable: Disable Full PvP (return to normal faction system)\n" +
            "- coinpurse <on|off>: Enable/disable coinpurse drop on PvP death\n" +
            "- droprate <0.0-1.0>: Set coinpurse drop rate (0.5 = 50%)\n" +
            "- status: Show current system status";
    }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (args.Length < 1)
        {
            CommandManager.SendErrorText(this, messageOutput, "Usage: /fullpvp <enable|disable|coinpurse|droprate|status>");
            return;
        }

        var command = args[0].ToLower();

        switch (command)
        {
            case "enable":
                FullPvPManager.Instance.SetFullPvPEnabled(true);
                CommandManager.SendNormalText(this, messageOutput, "Full PvP system enabled - All players are now hostile except guild/family members");
                break;

            case "disable":
                FullPvPManager.Instance.SetFullPvPEnabled(false);
                CommandManager.SendNormalText(this, messageOutput, "Full PvP system disabled - Normal faction system restored");
                break;

            case "coinpurse":
                if (args.Length < 2)
                {
                    CommandManager.SendErrorText(this, messageOutput, "Usage: /fullpvp coinpurse <on|off>");
                    return;
                }

                var coinpurseArg = args[1].ToLower();
                if (coinpurseArg == "on" || coinpurseArg == "true" || coinpurseArg == "1")
                {
                    FullPvPManager.Instance.SetCoinpurseDropEnabled(true);
                    CommandManager.SendNormalText(this, messageOutput, "Coinpurse drop on PvP death enabled");
                }
                else if (coinpurseArg == "off" || coinpurseArg == "false" || coinpurseArg == "0")
                {
                    FullPvPManager.Instance.SetCoinpurseDropEnabled(false);
                    CommandManager.SendNormalText(this, messageOutput, "Coinpurse drop on PvP death disabled");
                }
                else
                {
                    CommandManager.SendErrorText(this, messageOutput, "Invalid argument. Use: on, off, true, false, 1, or 0");
                }
                break;

            case "droprate":
                if (args.Length < 2)
                {
                    CommandManager.SendErrorText(this, messageOutput, "Usage: /fullpvp droprate <0.0-1.0>");
                    return;
                }

                if (float.TryParse(args[1], out float rate))
                {
                    FullPvPManager.Instance.SetCoinpurseDropRate(rate);
                    CommandManager.SendNormalText(this, messageOutput, $"Coinpurse drop rate set to {rate * 100}%");
                }
                else
                {
                    CommandManager.SendErrorText(this, messageOutput, "Invalid rate. Use a decimal number between 0.0 and 1.0 (e.g., 0.5 for 50%)");
                }
                break;

            case "status":
                var status = FullPvPManager.Instance.GetSystemStatus();
                CommandManager.SendNormalText(this, messageOutput, $"Full PvP System Status: {status}");
                
                // Show current player's relations with nearby players
                var nearbyPlayers = character.ParentWorld.GetAroundObjects<Character>(character, 100f);
                if (nearbyPlayers.Count > 1)
                {
                    CommandManager.SendNormalText(this, messageOutput, "Nearby player relations:");
                    foreach (var player in nearbyPlayers)
                    {
                        if (player == character) continue;
                        
                        var relation = FullPvPManager.Instance.GetFullPvPRelation(character, player);
                        var relationColor = relation switch
                        {
                            Models.Game.Faction.RelationState.Friendly => "|cFF00FF00Friendly|r",
                            Models.Game.Faction.RelationState.Hostile => "|cFFFF0000Hostile|r",
                            _ => "|cFFFFFF00Neutral|r"
                        };
                        
                        var expeditionInfo = player.Expedition != null ? $" (Guild: {player.Expedition.Name})" : "";
                        var familyInfo = player.Family != 0 ? $" (Family: {player.Family})" : "";
                        
                        CommandManager.SendNormalText(this, messageOutput, 
                            $"  {player.Name}: {relationColor}{expeditionInfo}{familyInfo}");
                    }
                }
                break;

            case "test":
                // Comando de teste para verificar drop de coinpurses
                if (args.Length >= 2 && args[1].ToLower() == "drop")
                {
                    // Simular morte em PvP para teste
                    var nearbyPlayer = character.ParentWorld.GetAroundObjects<Character>(character, 50f)
                        .FirstOrDefault(p => p != character);
                    
                    if (nearbyPlayer != null)
                    {
                        FullPvPManager.Instance.ProcessCoinpurseDropOnPvPDeath(character, nearbyPlayer, character.Transform.World.Position);
                        CommandManager.SendNormalText(this, messageOutput, "Test coinpurse drop executed");
                    }
                    else
                    {
                        CommandManager.SendErrorText(this, messageOutput, "No nearby player found for test");
                    }
                }
                else
                {
                    CommandManager.SendErrorText(this, messageOutput, "Test command: /fullpvp test drop");
                }
                break;

            default:
                CommandManager.SendErrorText(this, messageOutput, "Invalid command. Use: enable, disable, coinpurse, droprate, status, or test");
                break;
        }
    }
}