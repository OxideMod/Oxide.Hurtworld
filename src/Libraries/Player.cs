using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using NetworkPlayer = uLink.NetworkPlayer;

namespace Oxide.Game.Hurtworld.Libraries
{
    public class Player : Library
    {
        #region Initialization

        // Game references
        internal static readonly BanManager BanManager = BanManager.Instance;
        internal static readonly ChatManagerServer ChatManager = ChatManagerServer.Instance;
        internal static readonly GameManager GameManager = GameManager.Instance;
        internal static readonly GlobalItemManager ItemManager = GlobalItemManager.Instance;

        internal readonly Permission permission = Interface.Oxide.GetLibrary<Permission>();

        #endregion Initialization

        #region Information

        /// <summary>
        /// Gets the player's language
        /// </summary>
        public CultureInfo Language(PlayerSession session)
        {
#if ITEMV2
            return CultureInfo.GetCultureInfo(session.WorldPlayerEntity.PlayerOptions.CurrentConfig.CurrentLanguage);
#else
            return CultureInfo.GetCultureInfo("en");
#endif
        }

        /// <summary>
        /// Gets the player's IP address
        /// </summary>
        public string Address(PlayerSession session) => session.Player.ipAddress;

        /// <summary>
        /// Gets the player's average network ping
        /// </summary>
        public int Ping(PlayerSession session) => session.Player.averagePing;

        /// <summary>
        /// Returns if the player is admin
        /// </summary>
        public bool IsAdmin(string id) => GameManager.IsAdmin(new CSteamID(Convert.ToUInt64(id)));

        /// <summary>
        /// Returns if the player is admin
        /// </summary>
        public bool IsAdmin(ulong id) => GameManager.IsAdmin(new CSteamID(id));

        /// <summary>
        /// Returns if the player is admin
        /// </summary>
        public bool IsAdmin(PlayerSession session) => session.IsAdmin;

        /// <summary>
        /// Gets if the player is banned
        /// </summary>
        public bool IsBanned(string id) => BanManager.IsBanned(Convert.ToUInt64(id));

        /// <summary>
        /// Gets if the player is banned
        /// </summary>
        public bool IsBanned(ulong id) => BanManager.IsBanned(id);

        /// <summary>
        /// Gets if the player is banned
        /// </summary>
        public bool IsBanned(PlayerSession session) => IsBanned(session.SteamId.m_SteamID);

        /// <summary>
        /// Gets if the player is connected
        /// </summary>
        public bool IsConnected(PlayerSession session) => session?.Player != null ? session.Player.isConnected : false;

        /// <summary>
        /// Returns if the player is sleeping
        /// </summary>
        //public bool IsSleeping(string id) => session.Identity.Sleeper != null; // TODO: Session is null OnUserDisconnected?

        /// <summary>
        /// Returns if the player is sleeping
        /// </summary>
        //public bool IsSleeping(ulong id) => session.Identity.Sleeper != null; // TODO: Session is null OnUserDisconnected?

        /// <summary>
        /// Returns if the player is sleeping
        /// </summary>
        public bool IsSleeping(PlayerSession session) => session.Identity.Sleeper != null; // TODO: Session is null OnUserDisconnected?

        #endregion Information

        #region Administration

        /// <summary>
        /// Bans the player from the server
        /// </summary>
        /// <param name="session"></param>
        /// <param name="reason"></param>
        public void Ban(PlayerSession session, string reason = "")
        {
            if (IsBanned(session))
            {
                return;
            }

            BanManager.AddBan(session.SteamId.m_SteamID);
            if (session.Player.isConnected)
            {
                Kick(session, reason);
            }
        }

        /// <summary>
        /// Makes the player do an emote
        /// </summary>
        /// <param name="session"></param>
        /// <param name="emote"></param>
#if ITEMV2
        public void Emote(PlayerSession session, int emote)
        {
            EmoteManagerServer emoteManager = session.WorldPlayerEntity.GetComponent<EmoteManagerServer>();
#else

        public void Emote(PlayerSession session, Emotes.EEmoteType emote)
        {
            Emotes.EmoteManagerServer emoteManager = session.WorldPlayerEntity.GetComponent<Emotes.EmoteManagerServer>();
#endif
            emoteManager?.BeginEmoteServer(emote);
        }

        /// <summary>
        /// Heals the player by specified amount
        /// </summary>
        /// <param name="session"></param>
        /// <param name="amount"></param>
        public void Heal(PlayerSession session, float amount)
        {
#if ITEMV2
            EntityEffectFluid effect = new EntityEffectFluid(EntityFluidEffectKeyDatabase.Instance.Health, EEntityEffectFluidModifierType.AddValuePure, amount);
#else
            EntityEffectFluid effect = new EntityEffectFluid(EEntityFluidEffectType.Health, EEntityEffectFluidModifierType.AddValuePure, amount);
#endif
            EntityStats stats = session.WorldPlayerEntity.GetComponent<EntityStats>();
            effect.Apply(stats);
        }

        /// <summary>
        /// Damages the player by specified amount
        /// </summary>
        /// <param name="session"></param>
        /// <param name="amount"></param>
        public void Hurt(PlayerSession session, float amount)
        {
#if ITEMV2
            EntityEffectFluid effect = new EntityEffectFluid(EntityFluidEffectKeyDatabase.Instance.Damage, EEntityEffectFluidModifierType.AddValuePure, -amount);
#else
            EntityEffectFluid effect = new EntityEffectFluid(EEntityFluidEffectType.Damage, EEntityEffectFluidModifierType.AddValuePure, -amount);
#endif
            EntityStats stats = session.WorldPlayerEntity.GetComponent<EntityStats>();
            effect.Apply(stats);
        }

        /// <summary>
        /// Kicks the player from the server
        /// </summary>
        /// <param name="session"></param>
        /// <param name="reason"></param>
        public void Kick(PlayerSession session, string reason = "") => GameManager.KickPlayer(session, reason);

        /// <summary>
        /// Causes the player to die
        /// </summary>
        /// <param name="session"></param>
        public void Kill(PlayerSession session)
        {
            EntityStats stats = session.WorldPlayerEntity.GetComponent<EntityStats>();
            EntityEffectSourceData entityEffectSourceDatum = new EntityEffectSourceData { SourceDescriptionKey = "EntityStats/Sources/Suicide" };
#if ITEMV2
            stats.HandleEvent(new EntityEventDataRaiseEvent { EventType = EEntityEventType.Die }, entityEffectSourceDatum);
#else
            stats.HandleEvent(new EntityEventData { EventType = EEntityEventType.Die }, entityEffectSourceDatum);
#endif
        }

        /// <summary>
        /// Renames the player to specified name
        /// <param name="session"></param>
        /// <param name="name"></param>
        /// </summary>
        public void Rename(PlayerSession session, string name)
        {
            //name = name.Substring(0, 32);
            name = ChatManagerServer.CleanupGeneral(name);
            if (string.IsNullOrEmpty(name.Trim()))
            {
                name = "Unnamed";
            }

            // Chat/display name
#if !ITEMV2
            session.Name = name;
#endif
            session.Identity.Name = name;
            session.WorldPlayerEntity.GetComponent<HurtMonoBehavior>().RPC("UpdateName", uLink.RPCMode.All, name);
            SteamGameServer.BUpdateUserData(session.SteamId, name, 0);

            // Overhead name // TODO: Implement when possible
            //var displayProxyName = session.WorldPlayerEntity.GetComponent<DisplayProxyName>();
            //displayProxyName.UpdateName(name);

            session.IPlayer.Name = name;
            permission.UpdateNickname(session.Identity.SteamId.ToString(), name);
        }

        /// <summary>
        /// Teleports the player to the specified position
        /// </summary>
        /// <param name="session"></param>
        /// <param name="destination"></param>
        public void Teleport(PlayerSession session, Vector3 destination) => session.WorldPlayerEntity.transform.position = destination;

        /// <summary>
        /// Teleports the player to the target player
        /// </summary>
        /// <param name="session"></param>
        /// <param name="target"></param>
        public void Teleport(PlayerSession session, PlayerSession target) => Teleport(session, Position(target));

        /// <summary>
        /// Teleports the player to the specified position
        /// </summary>
        /// <param name="session"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public void Teleport(PlayerSession session, float x, float y, float z) => Teleport(session, new Vector3(x, y, z));

        /// <summary>
        /// Unbans the player
        /// </summary>
        public void Unban(PlayerSession session)
        {
            if (IsBanned(session))
            {
                BanManager.RemoveBan(session.SteamId.m_SteamID);
            }
        }

        #endregion Administration

        #region Location

        /// <summary>
        /// Returns the position of player as Vector3
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public Vector3 Position(PlayerSession session) => session.WorldPlayerEntity.transform.position;

        #endregion Location

        #region Player Finding

        /// <summary>
        /// Gets the player session using a name, Steam ID, or IP address
        /// </summary>
        /// <param name="nameOrIdOrIp"></param>
        /// <returns></returns>
        public PlayerSession Find(string nameOrIdOrIp)
        {
            PlayerSession session = null;
            foreach (KeyValuePair<NetworkPlayer, PlayerSession> s in Sessions)
            {
                if (!nameOrIdOrIp.Equals(s.Value.Identity.Name, StringComparison.OrdinalIgnoreCase) &&
                    !nameOrIdOrIp.Equals(s.Value.SteamId.ToString()) && !nameOrIdOrIp.Equals(s.Key.ipAddress))
                {
                    continue;
                }

                session = s.Value;
                break;
            }
            return session;
        }

        /// <summary>
        /// Gets the player session using a uLink.NetworkPlayer
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public PlayerSession Find(uLink.NetworkPlayer player) => GameManager.Instance.GetSession(player);

        /// <summary>
        /// Gets the player session using a UnityEngine.Collider
        /// </summary>
        /// <param name="col"></param>
        /// <returns></returns>
        public PlayerSession Find(Collider col)
        {
            PlayerSession session = null;
            EntityStats stats = col.gameObject.GetComponent<EntityStatsTriggerProxy>().Stats;
            foreach (KeyValuePair<NetworkPlayer, PlayerSession> s in Sessions)
            {
                if (!s.Value.WorldPlayerEntity.GetComponent<EntityStats>() == stats)
                {
                    continue;
                }

                session = s.Value;
                break;
            }
            return session;
        }

        /// <summary>
        /// Gets the player session using a UnityEngine.GameObject
        /// </summary>
        /// <param name="go"></param>
        /// <returns></returns>
        public PlayerSession Find(GameObject go)
        {
            Dictionary<NetworkPlayer, PlayerSession> sessions = GameManager.Instance.GetSessions();
            return (from i in sessions where go.Equals(i.Value.WorldPlayerEntity) select i.Value).FirstOrDefault();
        }

        /// <summary>
        /// Gets the player session using a Steam ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public PlayerSession FindById(string id)
        {
            PlayerSession session = null;
            foreach (KeyValuePair<NetworkPlayer, PlayerSession> s in Sessions)
            {
                if (!id.Equals(s.Value.SteamId.ToString()))
                {
                    continue;
                }

                session = s.Value;
                break;
            }
            return session;
        }

        /// <summary>
        /// Gets the player session using a uLink.NetworkPlayer
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public PlayerSession Session(uLink.NetworkPlayer player) => GameManager.GetSession(player);

        /// <summary>
        /// Gets the player session using a UnityEngine.GameObject
        /// </summary>
        /// <param name="go"></param>
        /// <returns></returns>
        public PlayerSession Session(GameObject go)
        {
            return (from s in Sessions where go.Equals(s.Value.WorldPlayerEntity) select s.Value).FirstOrDefault();
        }

        /// <summary>
        /// Returns all connected sessions
        /// </summary>
        public Dictionary<uLink.NetworkPlayer, PlayerSession> Sessions => GameManager.GetSessions();

        #endregion Player Finding

        #region Chat and Commands

        /// <summary>
        /// Sends the specified message and prefix to the player
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message"></param>
        /// <param name="prefix"></param>
        /// <param name="args"></param>
        public void Message(PlayerSession session, string message, string prefix, params object[] args)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            message = args.Length > 0 ? string.Format(Formatter.ToUnity(message), args) : Formatter.ToUnity(message);
            string formatted = prefix != null ? $"{prefix} {message}" : message;
#if ITEMV2
            ChatManager.SendChatMessage(new ServerChatMessage(formatted, false), session.Player);
#else
            ChatManager.RPC("RelayChat", session.Player, formatted);
#endif
        }

        /// <summary>
        /// Sends the specified message to the player
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message"></param>
        public void Message(PlayerSession session, string message) => Message(session, message, null);

        /// <summary>
        /// Replies to the player with the specified message and prefix
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message"></param>
        /// <param name="prefix"></param>
        /// <param name="args"></param>
        public void Reply(PlayerSession session, string message, string prefix, params object[] args)
        {
            Message(session, message, prefix, prefix);
        }

        /// <summary>
        /// Replies to the player with the specified message
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void Reply(PlayerSession session, string message, params object[] args) => Message(session, message, null);

        /// <summary>
        /// Runs the specified player command
        /// </summary>
        /// <param name="session"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        public void Command(PlayerSession session, string command, params object[] args)
        {
            // TODO: Implement when possible
        }

        #endregion Chat and Commands

        #region Item Handling

        /// <summary>
        /// Drops item by item ID from player's inventory
        /// </summary>
        /// <param name="session"></param>
        /// <param name="itemId"></param>
        public void DropItem(PlayerSession session, int itemId)
        {
#if ITEMV2
            EntityReferenceCache entityReferenceCache = GameManagerUtilities.PlayerRefCache();
#else
            Vector3 position = session.WorldPlayerEntity.transform.position;
#endif
            PlayerInventory inventory = Inventory(session);
            for (int slot = 0; slot < inventory.Capacity; slot++)
            {
#if ITEMV2
                ItemObject item = inventory.GetSlot(slot);
                if (item.ItemId == itemId) inventory.DropSlot(slot, entityReferenceCache.PlayerCamera.SimData.FireDirectionWorldSpace);
#else
                ItemInstance item = inventory.GetSlot(slot);
                if (item.Item.ItemId == itemId)
                {
                    inventory.DropSlot(slot, position + new Vector3(0f, 1f, 0f) + position / 2f, (position + new Vector3(0f, 0.2f, 0f)) * 8f);
                }
#endif
            }
        }

        /// <summary>
        /// Drops item from the player's inventory
        /// </summary>
        /// <param name="session"></param>
        /// <param name="item"></param>
#if ITEMV2
        public void DropItem(PlayerSession session, IItem item)
        {
#else

        public void DropItem(PlayerSession session, Assets.Scripts.Core.IItem item)
        {
            Vector3 position = session.WorldPlayerEntity.transform.position;
            PlayerInventory inventory = Inventory(session);
            for (int s = 0; s < inventory.Capacity; s++)
            {
                ItemInstance i = inventory.GetSlot(s);
                if (i.Item == item)
                {
                    inventory.DropSlot(s, (position + new Vector3(0f, 1f, 0f)) + (position / 2f), (position + new Vector3(0f, 0.2f, 0f)) * 8f);
                }
            }
#endif
        }

        /// <summary>
        /// Gives quantity of an item to the player
        /// </summary>
        /// <param name="session"></param>
        /// <param name="itemId"></param>
        /// <param name="quantity"></param>
        public void GiveItem(PlayerSession session, int itemId, int quantity = 1) => GiveItem(session, Item.GetItem(itemId), quantity);

        /// <summary>
        /// Gives quantity of an item to the player
        /// </summary>
        /// <param name="session"></param>
        /// <param name="item"></param>
        /// <param name="quantity"></param>
#if ITEMV2
        public void GiveItem(PlayerSession session, ItemObject item, int quantity = 1) => ItemManager.GiveItem(session.Player, item.Generator, quantity);
#else

        public void GiveItem(PlayerSession session, Assets.Scripts.Core.IItem item, int quantity = 1) => ItemManager.GiveItem(session.Player, item, quantity);

#endif

        #endregion Item Handling

        #region Inventory Handling

        /// <summary>
        /// Gets the inventory of the player
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public PlayerInventory Inventory(PlayerSession session) => session.WorldPlayerEntity.GetComponent<PlayerInventory>();

        /// <summary>
        /// Clears the inventory of the player
        /// </summary>
        /// <param name="session"></param>
#if ITEMV2
        public void ClearInventory(PlayerSession session) => Inventory(session)?.ClearItems();
#else

        public void ClearInventory(PlayerSession session) => Inventory(session)?.DestroyAll();

#endif

        #endregion Inventory Handling
    }
}
