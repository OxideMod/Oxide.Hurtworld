using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;
using NetworkPlayer = uLink.NetworkPlayer;

namespace Oxide.Game.Hurtworld
{
    /// <summary>
    /// Game hooks and wrappers for the core Hurtworld plugin
    /// </summary>
    public partial class HurtworldCore
    {
        #region Player Hooks

        /// <summary>
        /// Called when the player is attempting to craft
        /// </summary>
        /// <param name="player"></param>
        /// <param name="recipe"></param>
        /// <returns></returns>
        [HookMethod("ICanCraft")]
        private object ICanCraft(uLink.NetworkPlayer player, ICraftable recipe)
        {
            PlayerSession session = Player.Find(player);
            return Interface.CallHook("CanCraft", session, recipe);
        }

        /// <summary>
        /// Called when the player is attempting to connect
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        [HookMethod("IOnUserApprove")]
        private object IOnUserApprove(PlayerSession session)
        {
            session.Identity.Name = session.Identity.Name ?? "Unnamed";
            string id = session.SteamId.ToString();
            string ip = session.Player.ipAddress;

            Covalence.PlayerManager.PlayerJoin(session);

            object loginSpecific = Interface.CallHook("CanClientLogin", session);
            object loginCovalence = Interface.CallHook("CanUserLogin", session.Identity.Name, id, ip);
            object canLogin = loginSpecific ?? loginCovalence;

            if (canLogin is string || canLogin is bool && !(bool)canLogin)
            {
                GameManager.Instance.StartCoroutine(GameManager.Instance.DisconnectPlayerSync(session.Player, canLogin is string ? canLogin.ToString() : "Connection was rejected")); // TODO: Localization
                if (GameManager.Instance._playerSessions.ContainsKey(session.Player))
                {
                    GameManager.Instance._playerSessions.Remove(session.Player);
                }
                if (GameManager.Instance._steamIdSession.ContainsKey(session.SteamId))
                {
                    GameManager.Instance._steamIdSession.Remove(session.SteamId);
                }
                return true;
            }

            GameManager.Instance._playerSessions[session.Player] = session;

            object approvedSpecific = Interface.CallHook("OnUserApprove", session);
            object approvedCovalence = Interface.CallHook("OnUserApproved", session.Identity.Name, id, ip);
            return approvedSpecific ?? approvedCovalence;
        }

        /// <summary>
        /// Called when the player sends a chat message
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [HookMethod("IOnPlayerChat")]
        private object IOnPlayerChat(PlayerSession session, string message)
        {
            object chatSpecific = Interface.CallHook("OnPlayerChat", session, message);
            object chatCovalence = Interface.CallHook("OnUserChat", session.IPlayer, message);
            return chatSpecific ?? chatCovalence;
        }

        /// <summary>
        /// Called when the player sends a chat command
        /// </summary>
        /// <param name="session"></param>
        /// <param name="command"></param>
        [HookMethod("IOnPlayerCommand")]
        private object IOnPlayerCommand(PlayerSession session, string command)
        {
            // Get the full command
            string str = command.TrimStart('/');

            // Parse it
            string cmd;
            string[] args;
            ParseCommand(str, out cmd, out args);
            if (cmd == null) return null;

            // Is the command blocked?
            object blockedSpecific = Interface.CallHook("OnPlayerCommand", session, cmd, args); // TODO: Deprecate OnChatCommand
            object blockedCovalence = Interface.CallHook("OnUserCommand", session.IPlayer, cmd, args);
            if (blockedSpecific != null || blockedCovalence != null) return true;

            // Is it a covalance command?
            if (Covalence.CommandSystem.HandleChatMessage(session.IPlayer, command)) return true;

            // Is it a regular chat command?
            if (!cmdlib.HandleChatCommand(session, cmd, args))
                session.IPlayer.Reply(string.Format(lang.GetMessage("UnknownCommand", this, session.IPlayer.Id), cmd));

            return true;
        }

        /// <summary>
        /// Called when the player is attempting to claim territory
        /// </summary>
        /// <param name="player"></param>
        /// <param name="clan"></param>
        /// <param name="territory"></param>
        /// <returns></returns>
        [HookMethod("IOnPlayerClaimTerritory")]
        private object IOnPlayerClaimTerritory(uLink.NetworkPlayer player, Clan clan, float territory)
        {
            PlayerSession session = Player.Find(player);
            return Interface.CallHook("OnPlayerClaimTerritory", session, clan, territory);
        }

        /// <summary>
        /// Called when the player has claimed territory
        /// </summary>
        /// <param name="player"></param>
        /// <param name="clan"></param>
        /// <param name="territory"></param>
        /// <returns></returns>
        [HookMethod("IOnPlayerClaimedTerritory")]
        private object IOnPlayerClaimedTerritory(uLink.NetworkPlayer player, Clan clan, float territory)
        {
            PlayerSession session = Player.Find(player);
            return Interface.CallHook("OnPlayerClaimedTerritory", session, clan, territory);
        }

        /// <summary>
        /// Called when the player has connected
        /// </summary>
        /// <param name="session"></param>
        [HookMethod("OnPlayerConnected")]
        private void OnPlayerConnected(PlayerSession session)
        {
            if (session == null)
            {
                return;
            }

            string id = session.SteamId.ToString();

            // Update player's permissions group and name
            if (permission.IsLoaded)
            {
                permission.UpdateNickname(id, session.Identity.Name);
                OxideConfig.DefaultGroups defaultGroups = Interface.Oxide.Config.Options.DefaultGroups;
                if (!permission.UserHasGroup(id, defaultGroups.Players))
                {
                    permission.AddUserGroup(id, defaultGroups.Players);
                }

                if (session.IsAdmin && !permission.UserHasGroup(id, defaultGroups.Administrators))
                {
                    permission.AddUserGroup(id, defaultGroups.Administrators);
                }
            }


            // Set default language for player if not set
            if (string.IsNullOrEmpty(lang.GetLanguage(id)))
            {
                lang.SetLanguage(session.WorldPlayerEntity.PlayerOptions.CurrentConfig.CurrentLanguage, id);
            }

            // Let covalence know
            Covalence.PlayerManager.PlayerConnected(session);
            IPlayer iplayer = Covalence.PlayerManager.FindPlayerById(session.SteamId.ToString());
            if (iplayer != null)
            {
                session.IPlayer = iplayer;
                Interface.CallHook("OnUserConnected", session.IPlayer);
            }
        }

        /// <summary>
        /// Called when the player has disconnected
        /// </summary>
        /// <param name="session"></param>
        [HookMethod("IOnPlayerDisconnected")]
        private void IOnPlayerDisconnected(PlayerSession session)
        {
            if (!session.IsLoaded)
            {
                return;
            }

            // Call game-specific hook
            Interface.CallHook("OnPlayerDisconnected", session);

            // Let covalence know
            Interface.CallHook("OnUserDisconnected", session.IPlayer, "Unknown");
            Covalence.PlayerManager.PlayerDisconnected(session);
        }

        /// <summary>
        /// Called when the server receives input from the player
        /// </summary>
        /// <param name="player"></param>
        /// <param name="input"></param>
        [HookMethod("IOnPlayerInput")]
        private void IOnPlayerInput(uLink.NetworkPlayer player, InputControls input)
        {
            PlayerSession session = Player.Find(player);
            if (session != null)
            {
                Interface.CallHook("OnPlayerInput", session, input);
            }
        }

        /// <summary>
        /// Called when the player attempts to suicide
        /// </summary>
        /// <param name="player"></param>
        [HookMethod("IOnPlayerSuicide")]
        private object IOnPlayerSuicide(uLink.NetworkPlayer player)
        {
            PlayerSession session = Player.Find(player);
            return session != null ? Interface.CallHook("OnPlayerSuicide", session) : null;
        }

        /// <summary>
        /// Called when the player attempts to suicide
        /// </summary>
        /// <param name="player"></param>
        [HookMethod("IOnPlayerVoice")]
        private object IOnPlayerVoice(uLink.NetworkPlayer player)
        {
            PlayerSession session = Player.Find(player);
            return session != null ? Interface.CallHook("OnPlayerVoice", session) : null;
        }

        #endregion Player Hooks

        #region Entity Hooks

        /// <summary>
        /// Called when an entity takes damage
        /// </summary>
        /// <param name="effect"></param>
        /// <param name="target"></param>
        /// <param name="source"></param>
        [HookMethod("IOnTakeDamage")]
        private void IOnTakeDamage(EntityEffectFluid effect, EntityStats target, EntityEffectSourceData source)
        {
            if (effect == null || target == null || source == null || source.Value.Equals(0f))
            {
                return;
            }

            AIEntity entity = target.GetComponent<AIEntity>();
            if (entity != null)
            {
                Interface.CallHook("OnEntityTakeDamage", entity, source);
                return;
            }

            HNetworkView networkView = target.networkView;
            if (networkView != null)
            {
                PlayerSession session = GameManager.Instance.GetSession(networkView.owner);
                if (session != null)
                {
                    Interface.CallHook("OnPlayerTakeDamage", session, source);
                }
            }
        }

        #endregion Entity Hooks

        #region Structure Hooks

        /// <summary>
        /// Called when a single door is used
        /// </summary>
        /// <param name="door"></param>
        /// <returns></returns>
        [HookMethod("IOnSingleDoorUsed")]
        private void IOnSingleDoorUsed(DoorSingleServer door)
        {
            NetworkPlayer? player = door.LastUsedBy;
            if (player == null)
            {
                return;
            }

            PlayerSession session = Player.Find((uLink.NetworkPlayer)player);
            if (session != null)
            {
                Interface.CallHook("OnSingleDoorUsed", door, session);
            }
        }

        /// <summary>
        /// Called when a double door is used
        /// </summary>
        /// <param name="door"></param>
        /// <returns></returns>
        [HookMethod("IOnDoubleDoorUsed")]
        private void IOnDoubleDoorUsed(DoubleDoorServer door)
        {
            NetworkPlayer? player = door.LastUsedBy;
            if (player == null)
            {
                return;
            }

            PlayerSession session = Player.Find((uLink.NetworkPlayer)player);
            if (session != null)
            {
                Interface.CallHook("OnDoubleDoorUsed", door, session);
            }
        }

        /// <summary>
        /// Called when a garage door is used
        /// </summary>
        /// <param name="door"></param>
        /// <returns></returns>
        [HookMethod("IOnGarageDoorUsed")]
        private void IOnGarageDoorUsed(GarageDoorServer door)
        {
            NetworkPlayer? player = door.LastUsedBy;
            if (player == null)
            {
                return;
            }

            PlayerSession session = Player.Find((uLink.NetworkPlayer)player);
            if (session != null)
            {
                Interface.CallHook("OnGarageDoorUsed", door, session);
            }
        }

        #endregion Structure Hooks

        #region Vehicle Hooks

        /// <summary>
        /// Called when a player tries to enter a vehicle
        /// </summary>
        /// <param name="session"></param>
        /// <param name="go"></param>
        /// <returns></returns>
        [HookMethod("ICanEnterVehicle")]
        private object ICanEnterVehicle(PlayerSession session, GameObject go)
        {
            return Interface.CallHook("CanEnterVehicle", session, go.GetComponent<VehiclePassenger>());
        }

        /// <summary>
        /// Called when a player tries to exit a vehicle
        /// </summary>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        [HookMethod("ICanExitVehicle")]
        private object ICanExitVehicle(VehiclePassenger vehicle)
        {
            PlayerSession session = Player.Find(vehicle.networkView.owner);
            return session != null ? Interface.CallHook("CanExitVehicle", session, vehicle) : null;
        }

        /// <summary>
        /// Called when a player enters a vehicle
        /// </summary>
        /// <param name="player"></param>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        [HookMethod("IOnEnterVehicle")]
        private void IOnEnterVehicle(uLink.NetworkPlayer player, VehiclePassenger vehicle)
        {
            PlayerSession session = Player.Find(player);
            Interface.CallHook("OnEnterVehicle", session, vehicle);
        }

        /// <summary>
        /// Called when a player exits a vehicle
        /// </summary>
        /// <param name="player"></param>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        [HookMethod("IOnExitVehicle")]
        private void IOnExitVehicle(uLink.NetworkPlayer player, VehiclePassenger vehicle)
        {
            PlayerSession session = Player.Find(player);
            Interface.CallHook("OnExitVehicle", session, vehicle);
        }

        #endregion Vehicle Hooks
    }
}
