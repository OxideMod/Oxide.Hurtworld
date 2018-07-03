﻿using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Globalization;
using System.Net;

namespace Oxide.Game.Hurtworld.Libraries.Covalence
{
    /// <summary>
    /// Represents the server hosting the game instance
    /// </summary>
    public class HurtworldServer : IServer
    {
        #region Initialization

        internal readonly Server Server = new Server();
        internal static readonly BanManager BanManager = BanManager.Instance;

        #endregion Initialization

        #region Information

        /// <summary>
        /// Gets/sets the public-facing name of the server
        /// </summary>
        public string Name
        {
            get { return GameManager.Instance.ServerConfig.GameName; }
            set { GameManager.Instance.ServerConfig.GameName = value; }
        }

        private static IPAddress address;

        /// <summary>
        /// Gets the public-facing IP address of the server, if known
        /// </summary>
        public IPAddress Address
        {
            get
            {
                try
                {
                    if (address != null)
                    {
                        try
                        {
                            if (Utility.ValidateIPv4(GameManager.Instance.ServerConfig.BoundIP) && !Utility.IsLocalIP(GameManager.Instance.ServerConfig.BoundIP))
                            {
                                IPAddress.TryParse(GameManager.Instance.ServerConfig.BoundIP, out address);
#if DEBUG
                                Interface.Oxide.LogWarning($"IP address from command-line: {address}");
#endif
                            }
                            else
                            {
                                uint ip = Steamworks.SteamGameServer.GetPublicIP();
                                if (ip > 0)
                                {
                                    string publicIp = string.Concat(ip >> 24 & 255, ".", ip >> 16 & 255, ".", ip >> 8 & 255, ".", ip & 255);
                                    IPAddress.TryParse(publicIp, out address);
#if DEBUG
                                    Interface.Oxide.LogWarning($"IP address from Steam query: {address}");
#endif
                                }
                            }
                        }
                        catch
                        {
                            WebClient webClient = new WebClient();
                            IPAddress.TryParse(webClient.DownloadString("http://api.ipify.org"), out address);
#if DEBUG
                            Interface.Oxide.LogWarning($"IP address from external API: {address}");
#endif
                        }
                    }

                    return address;
                }
                catch (Exception ex)
                {
                    RemoteLogger.Exception("Couldn't get server IP address", ex);
                    return new IPAddress(0);
                }
            }
        }

        /// <summary>
        /// Gets the public-facing network port of the server, if known
        /// </summary>
        public ushort Port => (ushort)GameManager.Instance.ServerConfig.Port;

        /// <summary>
        /// Gets the version or build number of the server
        /// </summary>
        public string Version => GameManager.Instance.Version.ToString();

        /// <summary>
        /// Gets the network protocol version of the server
        /// </summary>
        public string Protocol => GameManager.PROTOCOL_VERSION.ToString();

        /// <summary>
        /// Gets the language set by the server
        /// </summary>
        public CultureInfo Language => CultureInfo.InstalledUICulture;

        /// <summary>
        /// Gets the total of players currently on the server
        /// </summary>
        public int Players => GameManager.Instance.GetPlayerCount();

        /// <summary>
        /// Gets/sets the maximum players allowed on the server
        /// </summary>
        public int MaxPlayers
        {
            get { return GameManager.Instance.ServerConfig.MaxPlayers; }
            set { GameManager.Instance.ServerConfig.MaxPlayers = value; }
        }

        /// <summary>
        /// Gets/sets the current in-game time on the server
        /// </summary>
        public DateTime Time
        {
            get
            {
                GameTime time = TimeManager.Instance.GetCurrentGameTime();
                return Convert.ToDateTime($"{time.Hour}:{time.Minute}:{Math.Floor(time.Second)}");
            }
            set
            {
                double currentOffset = TimeManager.Instance.GetCurrentGameTime().offset;
                int daysPassed = TimeManager.Instance.GetCurrentGameTime().Day + 1;
                double newOffset = 86400 * daysPassed - currentOffset + value.TimeOfDay.TotalSeconds;
                TimeManager.Instance.InitialTimeOffset += (float)newOffset;
            }
        }

        /// <summary>
        /// Gets information on the currently loaded save file
        /// </summary>
        public SaveInfo SaveInfo => null;

        #endregion Information

        #region Administration

        /// <summary>
        /// Bans the player for the specified reason and duration
        /// </summary>
        /// <param name="id"></param>
        /// <param name="reason"></param>
        /// <param name="duration"></param>
        public void Ban(string id, string reason, TimeSpan duration = default(TimeSpan))
        {
            if (!IsBanned(id))
            {
                Server.Ban(id, reason, duration);
            }
        }

        /// <summary>
        /// Gets the amount of time remaining on the player's ban
        /// </summary>
        /// <param name="id"></param>
        public TimeSpan BanTimeRemaining(string id) => TimeSpan.MaxValue;

        /// <summary>
        /// Gets if the player is banned
        /// </summary>
        /// <param name="id"></param>
        public bool IsBanned(string id) => Server.IsBanned(id);

        /// <summary>
        /// Saves the server and any related information
        /// </summary>
        public void Save() => Command("saveserver");

        /// <summary>
        /// Unbans the player
        /// </summary>
        /// <param name="id"></param>
        public void Unban(string id)
        {
            if (IsBanned(id))
            {
                Server.Unban(id);
            }
        }

        #endregion Administration

        #region Chat and Commands

        /// <summary>
        /// Broadcasts the specified chat message and prefix to all players
        /// </summary>
        /// <param name="message"></param>
        /// <param name="prefix"></param>
        /// <param name="args"></param>
        public void Broadcast(string message, string prefix, params object[] args) => Server.Broadcast(message, prefix, args);

        /// <summary>
        /// Broadcasts the specified chat message to all players
        /// </summary>
        /// <param name="message"></param>
        public void Broadcast(string message) => Broadcast(message, null);

        /// <summary>
        /// Runs the specified server command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args"></param>
        public void Command(string command, params object[] args) => Server.Command(command, args);

        #endregion Chat and Commands
    }
}
