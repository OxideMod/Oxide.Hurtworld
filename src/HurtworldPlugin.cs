using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Hurtworld.Libraries;
using System.Reflection;

namespace Oxide.Plugins
{
    public abstract class HurtworldPlugin : CSharpPlugin
    {
        protected Command cmd = Interface.Oxide.GetLibrary<Command>();
        protected Hurtworld hurt = Interface.Oxide.GetLibrary<Hurtworld>("Hurt");
        protected Item Item = Interface.Oxide.GetLibrary<Item>();
        protected Player Player = Interface.Oxide.GetLibrary<Player>();
        protected Server Server = Interface.Oxide.GetLibrary<Server>();

        public override void HandleAddedToManager(PluginManager manager)
        {
            foreach (MethodInfo method in GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                object[] attributes = method.GetCustomAttributes(typeof(ConsoleCommandAttribute), true);
                if (attributes.Length > 0)
                {
                    ConsoleCommandAttribute attribute = attributes[0] as ConsoleCommandAttribute;
                    cmd.AddConsoleCommand(attribute?.Command, this, method.Name);
                    continue;
                }

                attributes = method.GetCustomAttributes(typeof(ChatCommandAttribute), true);
                if (attributes.Length > 0)
                {
                    ChatCommandAttribute attribute = attributes[0] as ChatCommandAttribute;
                    cmd.AddChatCommand(attribute?.Command, this, method.Name);
                }
            }

            base.HandleAddedToManager(manager);
        }
    }
}
