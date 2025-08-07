using System.Linq;
using System.Reflection;
using FishNet.Object;
using HarmonyLib;
using UnlimitedMages.Components;
using UnlimitedMages.System.Components;
using UnlimitedMages.Utilities;

namespace UnlimitedMages.System.Networking
{
    public class TeamAssignment : NetworkBehaviour, IModComponent
    {
        // Fields are cached to avoid repeated lookups on subsequent calls.
        private MainMenuManagerNetworked? _mmmNetworked;
        private LobbyStateManager? _stateManager;
        private FieldInfo? _team1PlayersField;
        private FieldInfo? _team2PlayersField;
        private MethodInfo? _obsJoinTeamMethod;
        private MethodInfo? _obsRemoveFromTeamMethod;

        public void Initialize(BepInEx.Logging.ManualLogSource log)
        {
            log.LogInfo("TeamAssignment component injected.");
        }

        /// <summary>
        /// Initializes all required components and reflection data on the server.
        /// This is called just-in-time to avoid race conditions.
        /// </summary>
        /// <returns>True if initialization was successful, otherwise false.</returns>
        private bool InitializeOnServer()
        {
            // If already initialized, no need to do it again.
            if (_mmmNetworked != null) return true;
            
            UnlimitedMagesPlugin.Log?.LogInfo("[Server] First-time initialization of TeamAssignment...");

            _mmmNetworked = FindFirstObjectByType<MainMenuManagerNetworked>();
            _stateManager = LobbyStateManager.Instance;
            
            if (_mmmNetworked == null)
            {
                UnlimitedMagesPlugin.Log?.LogError("Initialization failed: MainMenuManagerNetworked not found in scene.");
                return false;
            }

            var mmmType = typeof(MainMenuManagerNetworked);
            _team1PlayersField = AccessTools.Field(mmmType, GameConstants.MainMenuManagerNetworked.Team1PlayersField);
            _team2PlayersField = AccessTools.Field(mmmType, GameConstants.MainMenuManagerNetworked.Team2PlayersField);
            _obsJoinTeamMethod = AccessTools.Method(mmmType, GameConstants.MainMenuManagerNetworked.ObserversJoinTeam);
            _obsRemoveFromTeamMethod = AccessTools.Method(mmmType, GameConstants.MainMenuManagerNetworked.ObsRemoveFromTeam);

            if (_stateManager != null && _team1PlayersField != null && _team2PlayersField != null && 
                _obsJoinTeamMethod != null && _obsRemoveFromTeamMethod != null)
            {
                UnlimitedMagesPlugin.Log?.LogInfo("[Server] TeamAssignment initialized successfully.");
                return true;
            }

            UnlimitedMagesPlugin.Log?.LogError("Initialization failed: Could not find all required fields/methods via reflection.");
            return false;
        }

        [ServerRpc(RequireOwnership = true)]
        public void Server_ForceBalanceTeams(string[] team1Names, string[] team2Names)
        {
            // Run the initialization check. If it fails, abort.
            if (!InitializeOnServer())
            {
                UnlimitedMagesPlugin.Log?.LogError("[Server] Aborting balance because TeamAssignment is not initialized.");
                return;
            }

            UnlimitedMagesPlugin.Log?.LogInfo("[Server] Received request to force team balance.");

            if (ConfigManager.Instance != null)
            {
                var teamSize = ConfigManager.Instance.TeamSize;
            
                var team1Players = (string[])_team1PlayersField!.GetValue(_mmmNetworked);
                var team2Players = (string[])_team2PlayersField!.GetValue(_mmmNetworked);

                // 1. Clear UI for all players
                for (int i = 0; i < team1Players.Length; i++)
                {
                    _obsRemoveFromTeamMethod!.Invoke(_mmmNetworked, [0, i]);
                }
                for (int i = 0; i < team2Players.Length; i++)
                {
                    _obsRemoveFromTeamMethod!.Invoke(_mmmNetworked, [2, i]);
                }
            
                // Re-initialize the server's authoritative arrays
                var newTeam1 = new string[teamSize];
                var newTeam2 = new string[teamSize];
                _team1PlayersField.SetValue(_mmmNetworked, newTeam1);
                _team2PlayersField.SetValue(_mmmNetworked, newTeam2);
            
                // Assign new teams and broadcast
                for (int i = 0; i < team1Names.Length; i++)
                {
                    if (i >= teamSize) break;
                
                    var playerName = team1Names[i];
                    newTeam1[i] = playerName; // Update authoritative state
                
                    var playerInfo = _stateManager!.AllPlayers.FirstOrDefault(p => p.FullName == playerName);
                    string lvlandrank = playerInfo?.Rank ?? "Lvl 0 Lackey";

                    _obsJoinTeamMethod!.Invoke(_mmmNetworked, [playerName, 0, i, lvlandrank]);
                }

                for (int i = 0; i < team2Names.Length; i++)
                {
                    if (i >= teamSize) break;
                
                    var playerName = team2Names[i];
                    newTeam2[i] = playerName; // Update authoritative state

                    var playerInfo = _stateManager!.AllPlayers.FirstOrDefault(p => p.FullName == playerName);
                    string lvlandrank = playerInfo?.Rank ?? "Lvl 0 Lackey";
                
                    _obsJoinTeamMethod!.Invoke(_mmmNetworked, [playerName, 2, i, lvlandrank]);
                }
            }

            UnlimitedMagesPlugin.Log?.LogInfo("[Server] Team balance forced successfully.");
        }
    }
}