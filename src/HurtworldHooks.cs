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
        #region Clan Hooks

        /// <summary>
        /// Called when the player attempts to create a clan
        /// </summary>
        /// <param name="clanName"></param>
        /// <param name="clanTag"></param>
        /// <param name="color"></param>
        /// <param name="description"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        [HookMethod("IOnClanCreate")]
        private object IOnClanCreate(string clanName, string clanTag, Color color, string description, PlayerSession session)
        {
            object canCreateClan = Interface.CallHook("OnClanCreate", clanName, clanTag, color, description, session);
            if (canCreateClan is string || canCreateClan is bool && !(bool)canCreateClan)
            {
                ClanManager.Instance.RPC("RPCClanCreationError", session.Player, canCreateClan is string ? canCreateClan.ToString() : "Clan creation was denied"); // TODO: Localization
                return false;
            }

            return null;
        }

        #endregion Clan Hooks

        #region Event Hooks

        /// <summary>
        /// Called when territory claim messages is broadcasted to chat
        /// </summary>
        /// <param name="territoryMarker"></param>
        /// <param name="clan"></param>
        [HookMethod("IOnClanTerritoryClaimBroadcast")]
        private void IOnClanTerritoryClaimBroadcast(TerritoryControlMarker territoryMarker, Clan clan)
        {
            object shouldBroadcast = Interface.CallHook("OnClanTerritoryClaimBroadcast", territoryMarker, clan);
            if (shouldBroadcast == null)
            {
                Singleton<ChatManagerServer>.Instance.SendChatMessage(new ServerChatMessage("Territory " + territoryMarker.Stake.TerritoryName + " has been claimed by " + clan.ClanName, Color.magenta, true));
            }
        }

        /// <summary>
        /// Called when town event message is broadcasted to chat
        /// </summary>
        /// <param name="townEvent"></param>
        /// <param name="name"></param>
        [HookMethod("IOnTownEventBroadcast")]
        private void IOnTownEventBroadcast(BaseTownEvent townEvent, string name)
        {
            object shouldBroadcast = Interface.CallHook("OnTownEventBroadcast", townEvent, name);
            if (shouldBroadcast == null)
            {
                Singleton<ChatManagerServer>.Instance.SendChatMessage((IChatMessage)new LocalizedChatMessage(Color.yellow, "UI/Chat/TownEventStartFormat", new string[1]
                {
                    name
                }));
            }
        }

        #endregion Event Hooks

        #region Player Hooks

        /// <summary>
        /// Called when the player craft attempt is initialized
        /// </summary>
        /// <param name="crafter"></param>
        /// <param name="networkPlayer"></param>
        [HookMethod("ICanCraftInitialize")]
        private void ICanCraftInitialize(Crafter crafter, uLink.NetworkPlayer networkPlayer)
        {
            crafter.NetworkPlayer = networkPlayer;
        }

        /// <summary>
        /// Called when the player is attempting to craft
        /// </summary>
        /// <param name="netPlayer"></param>
        /// <param name="recipe"></param>
        /// <returns></returns>
        [HookMethod("ICanCraft")]
        private object ICanCraft(Crafter crafter, ICraftable recipe, int count)
        {
            PlayerSession session = Player.Find(crafter.NetworkPlayer);
            return Interface.CallHook("CanCraft", crafter, session, recipe, count);
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
        /// Called when the player atempts to claim territory
        /// </summary>
        /// <param name="netPlayer"></param>
        /// <param name="clan"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        [HookMethod("IOnPlayerClaimTerritory")]
        private object IOnPlayerClaimTerritory(NetworkPlayer netPlayer, Clan clan, int point)
        {
            return Interface.CallHook("OnPlayerClaimTerritory", Player.Find(netPlayer), clan, point);
        }

        /// <summary>
        /// Called when the player has claimed territory
        /// </summary>
        /// <param name="netPlayer"></param>
        /// <param name="clan"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        [HookMethod("IOnPlayerClaimedTerritory")]
        private void IOnPlayerClaimedTerritory(NetworkPlayer netPlayer, Clan clan, int point)
        {
            Interface.CallHook("OnPlayerClaimedTerritory", Player.Find(netPlayer), clan, point);
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

            // Let covalence know
            Covalence.PlayerManager.PlayerConnected(session);
            IPlayer iplayer = Covalence.PlayerManager.FindPlayerById(session.SteamId.ToString());

            if (iplayer != null)
            {
                // Set default language for player if not set
                if (string.IsNullOrEmpty(lang.GetLanguage(id)))
                {
                    lang.SetLanguage(iplayer.Language.TwoLetterISOLanguageName, id);
                }

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
        /// <param name="netPlayer"></param>
        /// <param name="input"></param>
        [HookMethod("IOnPlayerInput")]
        private void IOnPlayerInput(NetworkPlayer netPlayer, InputControls input)
        {
            PlayerSession session = Player.Find(netPlayer);
            if (session != null)
            {
                Interface.CallHook("OnPlayerInput", session, input);
            }
        }

        /// <summary>
        /// Called when the player attempts to suicide
        /// </summary>
        /// <param name="netPlayer"></param>
        [HookMethod("IOnPlayerSuicide")]
        private object IOnPlayerSuicide(NetworkPlayer netPlayer)
        {
            PlayerSession session = Player.Find(netPlayer);
            return session != null ? Interface.CallHook("OnPlayerSuicide", session) : null;
        }

        /// <summary>
        /// Called when the player attempts to suicide
        /// </summary>
        /// <param name="netPlayer"></param>
        [HookMethod("IOnPlayerVoice")]
        private object IOnPlayerVoice(NetworkPlayer netPlayer)
        {
            PlayerSession session = Player.Find(netPlayer);
            return session != null ? Interface.CallHook("OnPlayerVoice", session) : null;
        }

        #endregion Player Hooks

        #region Entity Hooks

        /// <summary>
        /// Called when an entity effect is initialized
        /// </summary>
        /// <param name="effect"></param>
        /// <param name="stats"></param>
        [HookMethod("IOnEntityEffectInitialize")]
        private void IOnEntityEffectInitialize(StandardEntityFluidEffect effect, EntityStats stats)
        {
            effect.EntityStats = stats;
        }

        /// <summary>
        /// Called when an entity effect is applied
        /// </summary>
        /// <param name="effect"></param>
        /// <param name="source"></param>
        /// <param name="relativeValue"></param>
        [HookMethod("IOnEntityEffect")]
        private object IOnEntityEffect(StandardEntityFluidEffect effect, EntityEffectSourceData source, float relativeValue)
        {
            if (source == null || effect.ResolveTargetType() != EntityFluidEffectKeyDatabase.Instance?.Health)
            {
                return null;
            }

            EntityStats stats = effect.EntityStats;
            if (stats == null)
            {
                return null;
            }

            float newValue = Mathf.Clamp((effect.Value + relativeValue), effect.MinValue, effect.MaxValue);
            float updatedValue = newValue - effect.Value;
            source.Value = updatedValue;

            AIEntity entity = stats.GetComponent<AIEntity>();
            if (entity != null)
            {
                if (updatedValue > 0)
                {
                    return Interface.CallHook("OnEntityHeal", entity, source);
                }
                else if (updatedValue < 0)
                {
                    return Interface.CallHook("OnEntityTakeDamage", entity, source);
                }

                return null;
            }

            HNetworkView networkView = stats.networkView;
            if (networkView != null)
            {
                PlayerSession session = GameManager.Instance.GetSession(stats.networkView.owner);
                if (session != null)
                {
                    if (updatedValue > 0)
                    {
                        return Interface.CallHook("OnPlayerHeal", session, source);
                    }
                    else if (updatedValue < 0)
                    {
                        return Interface.CallHook("OnPlayerTakeDamage", session, source);
                    }
                }
            }

            return null;
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
            NetworkPlayer? netPlayer = door.LastUsedBy;
            if (netPlayer != null)
            {
                PlayerSession session = Player.Find((NetworkPlayer)netPlayer);
                if (session != null)
                {
                    Interface.CallHook("OnGarageDoorUsed", door, session);
                }
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
        /// <param name="netPlayer"></param>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        [HookMethod("IOnEnterVehicle")]
        private void IOnEnterVehicle(NetworkPlayer netPlayer, VehiclePassenger vehicle)
        {
            PlayerSession session = Player.Find(netPlayer);
            Interface.CallHook("OnEnterVehicle", session, vehicle);
        }

        /// <summary>
        /// Called when a player exits a vehicle
        /// </summary>
        /// <param name="netPlayer"></param>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        [HookMethod("IOnExitVehicle")]
        private void IOnExitVehicle(NetworkPlayer netPlayer, VehiclePassenger vehicle)
        {
            PlayerSession session = Player.Find(netPlayer);
            Interface.CallHook("OnExitVehicle", session, vehicle);
        }

        #endregion Vehicle Hooks
    }
}
