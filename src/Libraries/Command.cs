﻿using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Game.Hurtworld.Libraries
{
    /// <summary>
    /// A library containing functions for adding console and chat commands
    /// </summary>
    public class Command : Library
    {
        public override bool IsGlobal => false;

        private struct PluginCallback
        {
            public readonly Plugin Plugin;
            public readonly string Name;

            public PluginCallback(Plugin plugin, string name)
            {
                Plugin = plugin;
                Name = name;
            }
        }

        private class ConsoleCommand
        {
            public readonly string Name;
            public readonly Plugin Plugin;
            public readonly string CallbackName;

            public ConsoleCommand(string name, Plugin plugin, string callback)
            {
                Name = name;
                Plugin = plugin;
                CallbackName = callback;
            }
        }

        private class ChatCommand
        {
            public readonly string Name;
            public readonly Plugin Plugin;
            public readonly string CallbackName;

            public ChatCommand(string name, Plugin plugin, string callback)
            {
                Name = name;
                Plugin = plugin;
                CallbackName = callback;
            }
        }

        // All chat commands that plugins have registered
        private readonly Dictionary<string, ChatCommand> chatCommands;

        // All console commands that plugins have registered
        private readonly Dictionary<string, ConsoleCommand> consoleCommands;

        // A reference to the plugin removed callback
        private readonly Dictionary<Plugin, Event.Callback<Plugin, PluginManager>> pluginRemovedFromManager;

        /// <summary>
        /// Initializes a new instance of the Command class
        /// </summary>
        public Command()
        {
            chatCommands = new Dictionary<string, ChatCommand>();
            consoleCommands = new Dictionary<string, ConsoleCommand>();
            pluginRemovedFromManager = new Dictionary<Plugin, Event.Callback<Plugin, PluginManager>>();
        }

        /// <summary>
        /// Adds a chat command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="plugin"></param>
        /// <param name="callbackName"></param>
        [LibraryFunction("AddChatCommand")]
        public void AddChatCommand(string command, Plugin plugin, string callbackName)
        {
            string commandName = command.ToLowerInvariant();
            if (chatCommands.TryGetValue(commandName, out ChatCommand cmd))
            {
                string previousPluginName = cmd.Plugin?.Name ?? "an unknown plugin";
                string newPluginName = plugin?.Name ?? "An unknown plugin";
                string msg = $"{newPluginName} has replaced the '{commandName}' chat command previously registered by {previousPluginName}";
                Interface.Oxide.LogWarning(msg);
            }
            cmd = new ChatCommand(commandName, plugin, callbackName);

            // Add the new command to collections
            chatCommands[commandName] = cmd;

            // Hook the unload event
            if (plugin != null && !pluginRemovedFromManager.ContainsKey(plugin))
            {
                pluginRemovedFromManager[plugin] = plugin.OnRemovedFromManager.Add(plugin_OnRemovedFromManager);
            }
        }

        /// <summary>
        /// Adds a console command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="plugin"></param>
        /// <param name="callbackName"></param>
        [LibraryFunction("AddConsoleCommand")]
        public void AddConsoleCommand(string command, Plugin plugin, string callbackName)
        {
            string commandName = command.ToLowerInvariant();
            if (consoleCommands.TryGetValue(commandName, out ConsoleCommand cmd))
            {
                string previousPluginName = cmd.Plugin?.Name ?? "an unknown plugin";
                string newPluginName = plugin?.Name ?? "An unknown plugin";
                string msg = $"{newPluginName} has replaced the '{commandName}' console command previously registered by {previousPluginName}";
                Interface.Oxide.LogWarning(msg);
            }
            cmd = new ConsoleCommand(commandName, plugin, callbackName);

            // Add the new command to collections
            consoleCommands[commandName] = cmd;

            // Hook the unload event
            if (plugin != null && !pluginRemovedFromManager.ContainsKey(plugin))
            {
                pluginRemovedFromManager[plugin] = plugin.OnRemovedFromManager.Add(plugin_OnRemovedFromManager);
            }
        }

        /// <summary>
        /// Handles the specified chat command
        /// </summary>
        /// <param name="session"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        internal bool HandleChatCommand(PlayerSession session, string command, string[] args)
        {
            if (!chatCommands.TryGetValue(command.ToLowerInvariant(), out ChatCommand cmd))
            {
                return false;
            }

            cmd.Plugin.CallHook(cmd.CallbackName, session, command, args);

            return true;
        }

        /// <summary>
        /// Handles the specified console command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        internal object HandleConsoleCommand(string command, string[] args)
        {
            if (!consoleCommands.TryGetValue(command.ToLowerInvariant(), out ConsoleCommand cmd))
            {
                return null;
            }

            cmd.Plugin.CallHook(cmd.CallbackName, command, args);

            return true;
        }

        /// <summary>
        /// Called when a plugin has been removed from manager
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="manager"></param>
        private void plugin_OnRemovedFromManager(Plugin sender, PluginManager manager)
        {
            // Remove all console commands which were registered by the plugin
            foreach (ConsoleCommand cmd in consoleCommands.Values.Where(c => c.Plugin == sender).ToArray())
            {
                consoleCommands.Remove(cmd.Name);
            }

            // Remove all chat commands which were registered by the plugin
            foreach (ChatCommand cmd in chatCommands.Values.Where(c => c.Plugin == sender).ToArray())
            {
                chatCommands.Remove(cmd.Name);
            }

            // Unhook the event
            if (pluginRemovedFromManager.TryGetValue(sender, out Event.Callback<Plugin, PluginManager> callback))
            {
                callback.Remove();
                pluginRemovedFromManager.Remove(sender);
            }
        }

        /*/// <summary>
        /// Checks if a command can be overridden
        /// </summary>
        /// <param name="command"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool CanOverrideCommand(string command, string type)
        {
            string[] split = command.Split('.');
            string parent = split.Length >= 2 ? split[0].Trim() : "global";
            string name = split.Length >= 2 ? string.Join(".", split.Skip(1).ToArray()) : split[0].Trim();
            string fullname = $"{parent}.{name}";

            HurtworldCommandSystem.RegisteredCommand cmd;
            if (HurtworldCore.Covalence.CommandSystem.registeredCommands.TryGetValue(command, out cmd))
                if (cmd.Source.IsCorePlugin)
                    return false;

            if (type == "chat")
            {
                ChatCommand chatCommand;
                if (chatCommands.TryGetValue(command, out chatCommand))
                    if (chatCommand.Plugin.IsCorePlugin)
                        return false;
            }
            else if (type == "console")
            {
                ConsoleCommand consoleCommand;
                if (consoleCommands.TryGetValue(parent == "global" ? name : fullname, out consoleCommand))
                    if (consoleCommand.PluginCallbacks[0].Plugin.IsCorePlugin)
                        return false;
            }

            return !HurtworldCore.RestrictedCommands.Contains(command) && !HurtworldCore.RestrictedCommands.Contains(fullname);
        }*/
    }
}
