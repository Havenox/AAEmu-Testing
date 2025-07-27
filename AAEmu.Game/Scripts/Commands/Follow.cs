using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

public class Follow : ICommand
{
    public string[] CommandNames { get; set; } = ["follow"];

    public void OnLoad()
    {
        CommandManager.Instance.Register("follow", this);
    }

    public string GetCommandLineHelp()
    {
        return "<start [player_name]|stop|status>";
    }

    public string GetCommandHelpText()
    {
        return 
            "Manages player follow system:\n" +
            "- start [player_name]: Start following a player\n" +
            "- stop: Stop following\n" +
            "- status: Show current follow status";
    }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (args.Length < 1)
        {
            CommandManager.SendErrorText(this, messageOutput, "Usage: /follow <start [player_name]|stop|status>");
            return;
        }

        var command = args[0].ToLower();

        switch (command)
        {
            case "start":
                if (args.Length < 2)
                {
                    // Use current target if no player name provided
                    if (character.CurrentTarget is Character targetChar)
                    {
                        var success = FollowManager.Instance.StartFollow(character, targetChar);
                        if (success)
                            CommandManager.SendNormalText(this, messageOutput, $"Started following {targetChar.Name}");
                        else
                            CommandManager.SendErrorText(this, messageOutput, "Failed to start following target");
                    }
                    else
                    {
                        CommandManager.SendErrorText(this, messageOutput, "No target selected or provide player name");
                    }
                }
                else
                {
                    var playerName = args[1];
                    var targetPlayer = WorldManager.Instance.GetCharacter(playerName);
                    if (targetPlayer == null)
                    {
                        CommandManager.SendErrorText(this, messageOutput, $"Player '{playerName}' not found");
                        return;
                    }

                    var success = FollowManager.Instance.StartFollow(character, targetPlayer);
                    if (success)
                        CommandManager.SendNormalText(this, messageOutput, $"Started following {targetPlayer.Name}");
                    else
                        CommandManager.SendErrorText(this, messageOutput, "Failed to start following player");
                }
                break;

            case "stop":
                FollowManager.Instance.StopFollow(character);
                CommandManager.SendNormalText(this, messageOutput, "Stopped following");
                break;

            case "status":
                if (FollowManager.Instance.IsFollowing(character))
                {
                    var target = FollowManager.Instance.GetFollowTarget(character);
                    CommandManager.SendNormalText(this, messageOutput, $"Currently following: {target?.Name ?? "Unknown"}");
                }
                else
                {
                    CommandManager.SendNormalText(this, messageOutput, "Not following anyone");
                }
                
                // Show vehicle status
                var inVehicle = FollowManager.Instance.IsPlayerInVehicle(character);
                CommandManager.SendNormalText(this, messageOutput, $"In vehicle: {(inVehicle ? "Yes" : "No")}");
                CommandManager.SendNormalText(this, messageOutput, $"Is riding: {(character.IsRiding ? "Yes" : "No")}");
                break;

            default:
                CommandManager.SendErrorText(this, messageOutput, "Invalid command. Use: start, stop, or status");
                break;
        }
    }
}