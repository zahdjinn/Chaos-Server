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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Chaos
{
    internal delegate bool OnUseDelegate(Client client, Server server, PanelObject obj = null, Creature target = null, string prompt = null);
    internal delegate Item ItemCreationDelegate(int count);
    internal delegate Skill SkillCreationDelegate();
    internal delegate Spell SpellCreationDelegate();
    internal class CreationEngine
    {
        private Dictionary<string, ItemCreationDelegate> Items { get; }
        private Dictionary<string, SkillCreationDelegate> Skills { get; }
        private Dictionary<string, SpellCreationDelegate> Spells { get; }
        private Dictionary<string, OnUseDelegate> Effects { get; }
        internal CreationEngine()
        {
            Items = new Dictionary<string, ItemCreationDelegate>(StringComparer.CurrentCultureIgnoreCase);
            Spells = new Dictionary<string, SpellCreationDelegate>(StringComparer.CurrentCultureIgnoreCase);
            Skills = new Dictionary<string, SkillCreationDelegate>(StringComparer.CurrentCultureIgnoreCase);
            Effects = new Dictionary<string, OnUseDelegate>(StringComparer.CurrentCultureIgnoreCase);


            #region Items
            AddItem("Admin Trinket", AdminTrinket, DialogItem);
            AddItem("Test Item", TestItem, NormalItem);
            AddItem("Test Male Equipment", TestMaleEquipment, EquipItem);
            AddItem("Test Female Equipment", TestFemaleEquipment, EquipItem);
            AddItem("Test Weapon", TestWeapon, EquipItem);
            AddItem("Male Tattered Robes", MaleTatteredRobes, EquipItem);
            AddItem("Female Tattered Robes", FemaleTatteredRobes, EquipItem);
            #endregion

            #region Skills
            AddSkill("Test Skill 1", TestSkill1, NormalSkill);
            AddSkill("Cleave", Cleave, NormalSkill);
            AddSkill("Reposition", Reposition, Reposition);
            AddSkill("Shoulder Charge", ShoulderCharge, ShoulderCharge);
            #endregion

            #region Spells
            AddSpell("Mend", Mend, NormalSpell);
            AddSpell("Heal", Heal, NormalSpell);
            AddSpell("Srad Tut", SradTut, NormalSpell);
            AddSpell("Blink", Blink, Blink);
            AddSpell("Return Home", ReturnHome, ReturnHome);
            AddSpell("Admin Create", AdminCreate, AdminCreate);
            AddSpell("Admin Buff", AdminBuff, PersistentSpell);
            AddSpell("Test HOT", TestHOT, PersistentSpell);
            AddSpell("Fireball", Fireball, PersistenWorldSpell);
            #endregion
        }

        #region Interface
        internal OnUseDelegate GetEffect(string itemName) => Effects.ContainsKey(itemName) ? Effects[itemName] : Effects["NormalObj"];
        internal Gold CreateGold(Client client, uint amount, Point groundPoint) => new Gold(GetGoldSprite(amount), groundPoint, client.User.Map, amount);
        internal Item CreateItem(string name) => Items.ContainsKey(name) ? Items[name](1) : null;
        internal IEnumerable<Item> CreateItems(string name, int count)
        {
            Item item;

            if (!Items.ContainsKey(name))
                yield break;
            else if ((item = Items[name](count)).Stackable)
                yield return item;
            else
                for (int i = 0; i < count; i++)
                    yield return Items[name](1);
        }
        private byte GetGoldSprite(uint amount)
        {
            if (amount >= 5000)
                return 140;
            else if (amount >= 1000)
                return 141;
            else if (amount >= 500)
                return 142;
            else if (amount >= 100)
                return 137;
            else if (amount > 1)
                return 138;
            else
                return 139;
        }
        internal Skill CreateSkill(string name) => Skills.ContainsKey(name) ? Skills[name]() : null;
        internal Spell CreateSpell(string name) => Spells.ContainsKey(name) ? Spells[name]() : null;
        private void AddItem(string name, ItemCreationDelegate itemCreationDelegate, OnUseDelegate onUseDelegate)
        {
            Items.Add(name, itemCreationDelegate);
            Effects.Add(name, onUseDelegate);
        }
        private void AddSkill(string name, SkillCreationDelegate skillCreationDelegate, OnUseDelegate onUseDelegate)
        {
            Skills.Add(name, skillCreationDelegate);
            Effects.Add(name, onUseDelegate);
        }
        private void AddSpell(string name, SpellCreationDelegate spellCreationDelegate, OnUseDelegate onUseDelegate)
        {
            Spells.Add(name, spellCreationDelegate);
            Effects.Add(name, onUseDelegate);
        }
        #endregion

        #region Defaults
        private bool NormalItem(Client client, Server server, PanelObject obj = null, Creature target = null, string prompt = null)
        {
            Item item = obj as Item;

            if (client.User.Exchange?.IsActive == true)
            {
                client.User.Exchange.AddItem(client.User, item.Slot);
                return true;
            }
            return false;
        }
        private bool EquipItem(Client client, Server server, PanelObject obj = null, Creature target = null, string prompt = null)
        {
            Item item = obj as Item;

            if (client.User.Exchange?.IsActive == true)
            {
                client.User.Exchange.AddItem(client.User, item.Slot);
                return true;
            }

            if (!item.Gender.HasFlag(client.User.Gender))
            {
                client.SendServerMessage(ServerMessageType.ActiveMessage, "This item does not fit you.");
                return true;
            }

            Item outItem;
            byte oldSlot = item.Slot;
            if(client.User.Equipment.TryEquip(item, out outItem))
            {
                client.User.Inventory.TryRemove(oldSlot);

                if(outItem != null)
                    client.Enqueue(ServerPackets.RemoveEquipment(outItem.EquipmentSlot));

                client.Enqueue(ServerPackets.RemoveItem(oldSlot));
                client.Enqueue(ServerPackets.AddEquipment(item));

                if (outItem != null && client.User.Inventory.AddToNextSlot(outItem))
                    client.Enqueue(ServerPackets.AddItem(outItem));

                foreach (User user in client.User.Map.ObjectsVisibleFrom(client.User, true).OfType<User>())
                    user.Client.Enqueue(ServerPackets.DisplayUser(client.User));

                return true;
            }
            return false;
        }
        private bool DialogItem(Client client, Server server, PanelObject obj = null, Creature target = null, string prompt = null)
        {
            Item item = obj as Item;
            client.SendDialog(item, Game.Dialogs[item.NextDialogId]);
            return true;
        }
        private bool NormalSkill(Client client, Server server, PanelObject obj = null, Creature target = null, string prompt = null)
        {
            Skill skill = obj as Skill;
            List<Creature> targets = Game.Extensions.GetTargetsFromType(client, Point.None, skill.TargetType);
            int amount = skill.BaseDamage + client.User.Attributes.CurrentStr * 250;

            foreach (Creature c in targets)
                Game.Extensions.ApplyDamage(c, amount);

            Game.Extensions.ApplyActivation(client, skill, targets, null, StatUpdateType.None, true, false, false);
            return true;
        }
        private bool NormalSpell(Client client, Server server, PanelObject obj = null, Creature target = null, string prompt = null)
        {
            Spell spell = obj as Spell;
            List<Creature> targets = new List<Creature>() { target };
            int amount = spell.BaseDamage < 0 ?
                spell.BaseDamage - client.User.Attributes.CurrentInt * 500 :
                spell.BaseDamage + client.User.Attributes.CurrentInt * 500;

            targets.AddRange(Game.Extensions.GetTargetsFromType(client, target.Point, spell.TargetType));

            foreach (Creature c in targets)
                Game.Extensions.ApplyDamage(c, amount);

            Game.Extensions.ApplyActivation(client, spell, targets, null, StatUpdateType.None, true);
            return true;
        }
        private bool PersistentSpell(Client client, Server server, PanelObject obj = null, Creature target = null, string prompt = null)
        {
            Spell spell = obj as Spell;
            User user = target as User;
            List<Creature> targets = new List<Creature>();

            if (!spell.UsersOnly || target is User)
                targets.Add(target);

            foreach (Creature creature in Game.Extensions.GetTargetsFromType(client, target.Point, spell.TargetType))
                if (!spell.UsersOnly || creature is User)
                    targets.Add(creature);

            foreach (User u in targets.OfType<User>().ToList())
            {
                Effect targetedEffect = spell.Effect.GetTargetedEffect(u.Id, client.User.Id);
                if (u.EffectsBar.TryAdd(targetedEffect))
                    u.Client.SendEffect(targetedEffect);
                else
                    targets.Remove(u);
            }

            if (targets.Count > 0)
            {
                foreach (Creature c in targets)
                    Game.Extensions.ApplyDamage(c, spell.BaseDamage);

                Game.Extensions.ApplyActivation(client, spell, targets.OfType<Creature>().ToList(), null, StatUpdateType.Primary);
                return true;
            }
            return false;
        }
        private bool PersistenWorldSpell(Client client, Server server, PanelObject obj = null, Creature target = null, string prompt = null)
        {
            Spell spell = obj as Spell;
            User user = target as User;
            List<Creature> targets = new List<Creature>();

            if (!spell.UsersOnly || target is User)
                targets.Add(target);

            foreach (Point point in Game.Extensions.GetPointsFromType(client, target.Point, spell.TargetType))
            {
                Effect targetedEffect = spell.Effect.GetTargetedEffect(point);
                client.User.Map.AddEffect(targetedEffect);
            }

            if (targets.Count > 0)
            {
                foreach (Creature c in targets)
                    Game.Extensions.ApplyDamage(c, spell.BaseDamage);

                Game.Extensions.ApplyActivation(client, spell, targets.OfType<Creature>().ToList(), null, StatUpdateType.Primary, true);
                return true;
            }
            return false;
        }
        #endregion

        #region Items
        #region Default Items
        private Item AdminTrinket(int count) => new Item(new ItemSprite(13709, 0), 0, "Admin Trinket", TimeSpan.Zero, 1, 1, Animation.None, TargetsType.None, true, BodyAnimation.None, 0, Effect.None, true);
        private Item TestItem(int count) => new Item(new ItemSprite(1108, 0), 0, "Test Item", true, count, 1, false);
        private Item TestMaleEquipment(int count) => new Item(new ItemSprite(11990, 1023), 0, "Test Male Equipment", EquipmentSlot.Armor, 10000, 10000, 5, Gender.Male, false);
        private Item TestFemaleEquipment(int count) => new Item(new ItemSprite(11991, 1023), 0, "Test Female Equipment", EquipmentSlot.Armor, 10000, 10000, 5, Gender.Female, false);
        private Item TestWeapon(int count) => new Item(new ItemSprite(3254, 186), 0, "Test Weapon", EquipmentSlot.Weapon, 10000, 10000, 5, Gender.Unisex, false);
        private Item MaleTatteredRobes(int count) => new Item(new ItemSprite(1108, 208), 0, "Male Tattered Robes", EquipmentSlot.Armor, 10000, 10000, 2, Gender.Male, false);
        private Item FemaleTatteredRobes(int count) => new Item(new ItemSprite(1109, 208), 0, "Female Tattered Robes", EquipmentSlot.Armor, 10000, 10000, 2, Gender.Female, false);
        #endregion
        #region Scripted Items
        #endregion
        #endregion

        #region Skills
        #region Default Skills
        private Skill TestSkill1() => new Skill(78, "Test Skill 1", TimeSpan.Zero, true, Animation.None, TargetsType.Front, BodyAnimation.Assail, 50000);
        private Skill Cleave() => new Skill(16, "Cleave", TimeSpan.Zero, true, new Animation(119, 0, 100), TargetsType.Cleave, BodyAnimation.Swipe, 50000);
        #endregion

        #region Scripted Skills
        private Skill Reposition() => new Skill(29, "Reposition", new TimeSpan(0, 0, 10), false, Animation.None, TargetsType.Front, BodyAnimation.None, 0);
        private bool Reposition(Client client, Server server, PanelObject obj = null, Creature target = null, string prompt = null)
        {
            Skill skill = obj as Skill;
            List<Creature> targets = Game.Extensions.GetTargetsFromType(client, Point.None, skill.TargetType);

            if (targets.Count > 0)
            {
                target = targets[0];

                Point newPoint = target.Point.NewOffset(DirectionExtensions.Reverse(target.Direction));

                if (client.User.Map.IsWalkable(newPoint) || client.User.Point == newPoint)
                {
                    Game.Extensions.WarpObj(client.User, new Warp(client.User.Location, new Location(client.User.Map.Id, newPoint)));
                    client.User.Direction = target.Direction;
                }

                Game.Extensions.ApplyActivation(client, skill, targets, null, StatUpdateType.None);
                return true;
            }
            return false;
        }

        private Skill ShoulderCharge() => new Skill(49, "Shoulder Charge", new TimeSpan(0, 0, 5), false, new Animation(107, 0, 100), TargetsType.Front, BodyAnimation.None, 0);
        private bool ShoulderCharge(Client client, Server server, PanelObject obj = null, Creature target = null, string prompt = null)
        {
            Skill skill = obj as Skill;
            Point furthestPoint = client.User.Point;
            Point newPoint = client.User.Point;

            for (int i = 0; i < 3; i++)
            {
                newPoint.Offset(client.User.Direction);
                if (client.User.Map.IsWalkable(newPoint))
                    furthestPoint = newPoint;
                else
                    break;
            }

            Game.Extensions.WarpObj(client.User, new Warp(client.User.Location, new Location(client.User.Map.Id, furthestPoint)));

            List<Point> points = new List<Point>();
            List<Creature> targets = Game.Extensions.GetTargetsFromType(client, Point.None, skill.TargetType);

            if (targets.Count > 0)
            {
                target = targets[0];
                furthestPoint = target.Point;
                newPoint = target.Point;


                points.Add(target.Point);
                for (int i = 0; i < 2; i++)
                {
                    newPoint.Offset(client.User.Direction);
                    if (client.User.Map.IsWalkable(newPoint))
                    {
                        furthestPoint = newPoint;
                        points.Add(newPoint);
                    }
                    else
                        break;
                }

                Game.Extensions.WarpObj(target, new Warp(target.Location, new Location(client.User.Map.Id, furthestPoint)));
            }

            List<Animation> sfx = new List<Animation>();

            foreach(Point point in points)
                sfx.Add(new Animation(point, 0, 0, 2, 0, 100));

            Game.Extensions.ApplyActivation(client, skill, targets, sfx, StatUpdateType.None);
            return true;
        }
        #endregion
        #endregion

        #region Spells
        #region Default Spells
        private Spell Mend() => new Spell(118, "Mend", SpellType.Targeted, string.Empty, 1, TimeSpan.Zero, new Animation(4, 0, 100), TargetsType.None, true, BodyAnimation.HandsUp, -10000);
        private Spell Heal() => new Spell(21, "Heal", SpellType.Targeted, string.Empty, 1, TimeSpan.FromSeconds(2), new Animation(157, 0, 100), TargetsType.None, true, BodyAnimation.HandsUp, -100000);
        private Spell SradTut() => new Spell(39, "Srad Tut", SpellType.Targeted, string.Empty, 1, TimeSpan.FromSeconds(2), new Animation(217, 0, 100), TargetsType.None, false, BodyAnimation.HandsUp, 100000);
        private Spell AdminBuff() => new Spell(1, "Admin Buff", SpellType.Targeted, string.Empty, 1, TimeSpan.FromSeconds(20), new Animation(189, 0, 100), TargetsType.None, true, BodyAnimation.HandsUp, 0,
            new Effect(sbyte.MaxValue, sbyte.MaxValue, sbyte.MaxValue, sbyte.MaxValue, sbyte.MaxValue, 1333337, 1333337, 0, 0, 2000, new TimeSpan(0, 5, 0), true, Animation.None));
        private Spell TestHOT() => new Spell(127, "Test HOT", SpellType.Targeted, string.Empty, 0, TimeSpan.Zero, new Animation(187, 0, 100), TargetsType.None, true, BodyAnimation.HandsUp, -25000,
            new Effect(0, 0, 0, 0, 0, 0, 0, -25000, 0, 1000, new TimeSpan(0, 0, 20), true));
        private Spell Fireball() => new Spell(39, "Fireball", SpellType.Targeted, string.Empty, 1, TimeSpan.FromSeconds(5), new Animation(138, 102, 150), TargetsType.Cluster2, false, BodyAnimation.WizardCast, 100000,
            new Effect(0, 0, 0, 0, 0, 0, 0, 50000, 0, 500, TimeSpan.FromSeconds(5), false, new Animation(211, 0, 100)));
        #endregion

        #region Scripted Spells
        private Spell Blink() => new Spell(164, "Blink", SpellType.NoTarget, string.Empty, 1, new TimeSpan(0, 0, 30), new Animation(91, 0, 100), TargetsType.None, true, BodyAnimation.WizardCast);
        private bool Blink(Client client, Server server, PanelObject obj = null, Creature target = null, string prompt = null)
        {
            Spell spell = obj as Spell;
            List<Creature> targets = new List<Creature>() { target };
            Animation ani = new Animation(client.User.BlinkSpot.Point, 96, 100);
            Effect eff = new Effect(1800, new TimeSpan(0, 0, 10), false, ani);

            if (!client.User.HasFlag(UserState.UsedBlink) || DateTime.UtcNow.Subtract(spell.LastUse).TotalSeconds > 10 || client.User.BlinkSpot.MapId != client.User.Map.Id)
            {
                spell.CooldownReduction += 1f;
                client.User.AddFlag(UserState.UsedBlink);
                client.User.BlinkSpot = client.User.Location;

                ani = new Animation(client.User.BlinkSpot.Point, 96, 100);
                eff = new Effect(1800, new TimeSpan(0, 0, 10), false, ani);

                lock (client.User.Map.Sync)
                    client.User.Map.AddEffect(eff);
                return false;
            }
            else
            {
                Game.Extensions.WarpObj(client.User, new Warp(client.User.Location, client.User.BlinkSpot));
                client.User.RemoveFlag(UserState.UsedBlink);

                //remove effect
                lock (client.User.Map.Sync)
                    client.User.Map.RemoveEffect(eff);
            }

            spell.CooldownReduction -= 1f;
            Game.Extensions.ApplyActivation(client, spell, targets, null, StatUpdateType.None);
            return true;
        }
        private Spell ReturnHome() => new Spell(56, "Return Home", SpellType.NoTarget, string.Empty, 1, new TimeSpan(0, 0, 1), new Animation(91, 0, 100), TargetsType.None, true, BodyAnimation.WizardCast);
        private bool ReturnHome(Client client, Server server, PanelObject obj = null, Creature target = null, string prompt = null)
        {
            Spell spell = obj as Spell;
            List<Creature> targets = new List<Creature>() { target };

            switch (client.User.Nation)
            {
                case Nation.None:
                    Game.Extensions.WarpObj(client.User, new Warp(client.User.Location, CONSTANTS.NO_NATION_LOCATION));
                    break;
                case Nation.Suomi:
                    Game.Extensions.WarpObj(client.User, new Warp(client.User.Location, CONSTANTS.SUOMI_LOCATION));
                    break;
                case Nation.Loures:
                    Game.Extensions.WarpObj(client.User, new Warp(client.User.Location, CONSTANTS.LOURES_LOCATION));
                    break;
                case Nation.Mileth:
                    Game.Extensions.WarpObj(client.User, new Warp(client.User.Location, CONSTANTS.MILETH_LOCATION));
                    break;
                case Nation.Tagor:
                    Game.Extensions.WarpObj(client.User, new Warp(client.User.Location, CONSTANTS.TAGOR_LOCATION));
                    break;
                case Nation.Rucesion:
                    Game.Extensions.WarpObj(client.User, new Warp(client.User.Location, CONSTANTS.RUCESION_LOCATION));
                    break;
                case Nation.Noes:
                    Game.Extensions.WarpObj(client.User, new Warp(client.User.Location, CONSTANTS.NOES_LOCATION));
                    break;
            }
            Game.Extensions.ApplyActivation(client, spell, targets, null, StatUpdateType.None);
            return true;
        }
        private Spell AdminCreate() => new Spell(139, "Admin Create", SpellType.Prompt, "<Type> <Name>:<Amount>", 0, TimeSpan.Zero, new Animation(78, 0, 50), TargetsType.None, true, BodyAnimation.HandsUp);
        private bool AdminCreate(Client client, Server server, PanelObject obj = null, Creature target = null, string prompt = null)
        {
            Spell spell = obj as Spell;
            List<Creature> targets = new List<Creature>() { target };

            Match m;
            if ((m = Regex.Match(prompt, @"^(item|skill|spell) (\w+(?: \w+)*)(?::(\d+))?$", RegexOptions.IgnoreCase)).Success)
            {
                string type = m.Groups[1].Value.ToLower();
                string key = m.Groups[2].Value;
                int amount = 1;

                if (!int.TryParse(m.Groups[3].Value, out amount))
                    amount = 1;

                switch (type)
                {
                    case "item":
                        List<Item> newItems;
                        if ((newItems = CreateItems(key, amount).ToList()) != null && newItems.Count > 0)
                        {
                            foreach (Item i in newItems)
                                if (client.User.Inventory.AddToNextSlot(i))
                                    client.Enqueue(ServerPackets.AddItem(i));
                        }
                        else
                        {
                            client.SendServerMessage(ServerMessageType.AdminMessage, "Object doesn't exist.");
                            return false;
                        }
                        break;
                    case "skill":
                        Skill newSkill;
                        if ((newSkill = CreateSkill(key)) != null && client.User.SkillBook.AddToNextSlot(newSkill))
                            client.Enqueue(ServerPackets.AddSkill(newSkill));
                        else
                        {
                            client.SendServerMessage(ServerMessageType.AdminMessage, "Object doesn't exist.");
                            return false;
                        }
                        break;
                    case "spell":
                        Spell newSpell;
                        if ((newSpell = CreateSpell(key)) != null && client.User.SpellBook.AddToNextSlot(newSpell))
                            client.Enqueue(ServerPackets.AddSpell(newSpell));
                        else
                        {
                            client.SendServerMessage(ServerMessageType.AdminMessage, "Object doesn't exist.");
                            return false;
                        }
                        break;
                }

                Game.Extensions.ApplyActivation(client, spell, targets, null, StatUpdateType.None);
                return true;
            }
            else
                client.SendServerMessage(ServerMessageType.AdminMessage, "Incorrect syntax.");

            return false;
        }
        #endregion
        #endregion
    }
}
