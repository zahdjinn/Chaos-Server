// ****************************************************************************
// This file belongs to the Chaos-Server project.
// 
// This project is free and open-source, provided that any alterations or
// modifications to any portions of this project adhere to the
// Affero General Public License (Version 3).
// 
// A copy of the AGPLv3 can be found in the project directory.
// You may also find a copy at <https://www.gnu.org/licenses/agpl-3.0.html>
// ****************************************************************************

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Chaos
{
    internal delegate void PursuitDelegate(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null);
    /// <summary>
    /// Object containing all in-game persuits for dialogs, and the methods for their effects.
    /// </summary>
    #pragma warning disable IDE0022
    internal static class Pursuits
    {
        //these will use pursuit id to get the effect
        private static readonly ImmutableDictionary<PursuitIds, PursuitDelegate> PursuitList = ImmutableDictionary.CreateRange(new Dictionary<PursuitIds, PursuitDelegate>()
        {
            { PursuitIds.None, new PursuitDelegate(None) },
            { PursuitIds.ReviveSelf, new PursuitDelegate(ReviveSelf) },
            { PursuitIds.ReviveUser, new PursuitDelegate(ReviveUser) },
            { PursuitIds.Teleport, new PursuitDelegate(Teleport) },
            { PursuitIds.SummonUser, new PursuitDelegate(SummonUser) },
            { PursuitIds.SummonAll, new PursuitDelegate(SummonAll) },
            { PursuitIds.KillUser, new PursuitDelegate(KillUser) },
            { PursuitIds.LouresCitizenship, new PursuitDelegate(LouresCitizenship) },
            { PursuitIds.BecomeWarrior, new PursuitDelegate(BecomeWarrior) },
            { PursuitIds.BecomeWizard, new PursuitDelegate(BecomeWizard) },
            { PursuitIds.BecomePriest, new PursuitDelegate(BecomePriest) },
            { PursuitIds.BecomeMonk, new PursuitDelegate(BecomeMonk) },
            { PursuitIds.BecomeRogue, new PursuitDelegate(BecomeRogue) },
            { PursuitIds.GiveTatteredRobe, new PursuitDelegate(GiveTatteredRobe) },
            { PursuitIds.STRtoINT, new PursuitDelegate(STRtoINT) },
        });

        internal static PursuitDelegate Activate(PursuitIds pid) => PursuitList[pid];

        #region PursuitEffects
        private static void None(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null) { }

        private static void STRtoINT(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null)
        {
            if (client.User.Inventory.Contains("Winiphira Ticket"))
            {
                if (client.User.Legend.Contains("modStats") && client.User.Legend["modStats"].Count == 0)
                {
                    client.User.Legend.Add(new LegendMark(GameTime.Now, "Modified spirit using Winiphira", "modStats", MarkIcon.Heart, MarkColor.LightGreen));
                }
                if (client.User.Legend["modStats"].Count >= 10)
                {
                    client.SendServerMessage(ServerMessageType.OrangeBar1, @"You have the maximum stat modifications allowed.");
                }
                if (client.User.Legend["modStats"].Count < 10)
                {
                    if (client.User.Attributes.BaseStr > 1)
                    {
                        client.User.Inventory["Winiphira Ticket"].Count--;
                        client.User.Attributes.BaseStr--;
                        client.User.Attributes.BaseInt++;
                        client.SendAttributes(StatUpdateType.Primary);
                        client.SendServerMessage(ServerMessageType.AdminMessage, @"You feel weaker yet understand more.");
                    }
                    else if (client.User.Attributes.BaseStr == 1)
                        client.SendServerMessage(ServerMessageType.OrangeBar1, @"Unacceptable. Doing so would make your spirit leave the body.");
                    else if (client.User.Attributes.BaseInt >= 180)
                        client.SendServerMessage(ServerMessageType.OrangeBar1, @"Unacceptable. Your mind cannot comprehend more than it has.");
                }
            }
            else
                client.SendServerMessage(ServerMessageType.OrangeBar1, @"Come back when you have what I asked for.");
        }

        private static void GiveTatteredRobe(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null)
        {
            /*
            if (!client.User.HasFlag(Quest.MaribelRobes) && client.User.Gender == Gender.Male)
            {
                Game.CreationEngine.GiveItem(client, server, "Male Tattered Robes", 1);
                client.User.AddFlag(Quest.MaribelRobes);
            }
            else if (!client.User.HasFlag(Quest.MaribelRobes) && client.User.Gender == Gender.Female)
            {
                Game.CreationEngine.GiveItem(client, server, "Female Tattered Robes", 1);
                client.User.AddFlag(Quest.MaribelRobes);
            }
            else
                client.SendServerMessage(ServerMessageType.AdminMessage, @"I've already given you robes.");
                */

            if(!client.User.HasFlag(Quest.MaribelRobes))
            {
                Item item = Game.CreationEngine.CreateItem($@"{((client.User.Gender == Gender.Female) ? "Female" : "Male")} Tattered Robes");

                if (client.User.Inventory.AddToNextSlot(item))
                {
                    client.User.AddFlag(Quest.MaribelRobes);
                    client.Enqueue(ServerPackets.AddItem(item));
                }
            }
            else
            {
            }
        }

        private static void ReviveSelf(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null)
        {
            Game.Assert.ReviveUser(client.User);
        }

        private static void ReviveUser(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null)
        {
            if (server.TryGetUser(userInput, out User user))
                Game.Assert.ReviveUser(user);
            else
                client.SendServerMessage(ServerMessageType.Whisper, @"Invalid name.");
        }

        private static void Teleport(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null)
        {
            if (Location.TryParse(userInput, out Location warpLoc))
                Game.Assert.Warp(client.User, new Warp(client.User.Location, warpLoc));
            else if (server.TryGetUser(userInput, out User user))
                Game.Assert.Warp(client.User, new Warp(client.User.Location, user.Location));
            else
                client.SendServerMessage(ServerMessageType.Whisper, @"Invalid format. ""mapId xCord yCord"" or ""characterName""");
        }

        private static void SummonUser(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null)
        {
            if (server.TryGetUser(userInput, out User user))
                Game.Assert.Warp(user, new Warp(user.Location, client.User.Location));
            else
                client.SendServerMessage(ServerMessageType.Whisper, @"Invalid name.");
        }

        private static void SummonAll(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null)
        {
            foreach (User user in server.WorldClients.Where(c => c.User != client.User).Select(c => c.User))
                Game.Assert.Warp(user, new Warp(user.Location, client.User.Location));
        }

        private static void KillUser(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null)
        {
            if (server.TryGetUser(userInput, out User user))
                Game.Assert.Damage(user, int.MaxValue, true);
            else
                client.SendServerMessage(ServerMessageType.Whisper, @"Invalid name.");
        }

        private static void LouresCitizenship(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null)
        {
            client.User.Nation = Nation.Loures;
        }

        private static void ForceGive(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null)
        {
            string[] args = userInput.Split(':');
            Item item = client.User.Inventory[args[0]];

            if (item == null)
                client.SendServerMessage(ServerMessageType.ActiveMessage, $"You do not own {args[0]}.");
            else if (server.TryGetUser(args[1], out User user))
            {
                Item result;
                if (item.Count > 1)
                {
                    result = item.Split(1);
                    client.Enqueue(ServerPackets.AddItem(item));
                }
                else if (client.User.Inventory.TryGetRemove(item.Slot, out Item oItem))
                {
                    user.Inventory.TryAdd(oItem);
                    user.Client.Enqueue(ServerPackets.AddItem(oItem));
                }
            }
            else
                client.SendServerMessage(ServerMessageType.ActiveMessage, $"{args[1]} cannot be found.");
        }

        private static void BecomeWarrior(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null)
        {
            client.User.BaseClass = BaseClass.Warrior;
            client.User.ClearSkillsSpells();

            client.User.SkillBook.AddToNextSlot(Game.CreationEngine.CreateSkill("Cleave"));
            client.User.SkillBook.AddToNextSlot(Game.CreationEngine.CreateSkill("Reposition"));
            client.User.SkillBook.AddToNextSlot(Game.CreationEngine.CreateSkill("Shoulder Charge"));

            foreach (Skill skill in client.User.SkillBook)
                client.Enqueue(ServerPackets.AddSkill(skill));

            client.Enqueue(ServerPackets.DisplayDialog(client.ActiveObject, Game.Dialogs.CloseDialog()));
        }

        private static void BecomeWizard(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null)
        {
            client.User.BaseClass = BaseClass.Wizard;
            client.User.ClearSkillsSpells();

            client.Enqueue(ServerPackets.DisplayDialog(client.ActiveObject, Game.Dialogs.CloseDialog()));
        }

        private static void BecomePriest(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null)
        {
            client.User.BaseClass = BaseClass.Priest;
            client.User.ClearSkillsSpells();

            client.Enqueue(ServerPackets.DisplayDialog(client.ActiveObject, Game.Dialogs.CloseDialog()));
        }

        private static void BecomeMonk(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null)
        {
            client.User.BaseClass = BaseClass.Monk;
            client.User.ClearSkillsSpells();

            client.Enqueue(ServerPackets.DisplayDialog(client.ActiveObject, Game.Dialogs.CloseDialog()));
        }

        private static void BecomeRogue(Client client, Server server, bool closing = false, byte menuOption = 0, string userInput = null)
        {
            client.User.BaseClass = BaseClass.Rogue;
            client.User.ClearSkillsSpells();

            client.Enqueue(ServerPackets.DisplayDialog(client.ActiveObject, Game.Dialogs.CloseDialog()));
        }
        #endregion

    }
}
