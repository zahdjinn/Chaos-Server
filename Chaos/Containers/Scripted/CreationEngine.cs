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
    internal delegate bool ActivationDelegate(Client client, Server server, ActivationContext context); //maybe put target and prompt in spell? or in client??
    internal delegate Item ItemCreationDelegate(uint count);
    internal delegate Skill SkillCreationDelegate();
    internal delegate Spell SpellCreationDelegate();

    /// <summary>
    /// Object containing methods, objects, and properties for the easy creation and activation of PanelObjects.
    /// </summary>
    internal sealed class CreationEngine
    {
        private readonly Dictionary<string, ItemCreationDelegate> Items;
        private readonly Dictionary<string, SkillCreationDelegate> Skills;
        private readonly Dictionary<string, SpellCreationDelegate> Spells;
        private readonly Dictionary<string, ActivationDelegate> Effects;

        internal CreationEngine()
        {
            Items = new Dictionary<string, ItemCreationDelegate>(StringComparer.CurrentCultureIgnoreCase);
            Spells = new Dictionary<string, SpellCreationDelegate>(StringComparer.CurrentCultureIgnoreCase);
            Skills = new Dictionary<string, SkillCreationDelegate>(StringComparer.CurrentCultureIgnoreCase);
            Effects = new Dictionary<string, ActivationDelegate>(StringComparer.CurrentCultureIgnoreCase);


            #region Items
            AddItem("Admin Trinket", AdminTrinket, DialogItem);
            AddItem("Test Item", TestItem, NormalItem);
            AddItem("Test Male Equipment", TestMaleEquipment, EquipItem);
            AddItem("Test Female Equipment", TestFemaleEquipment, EquipItem);
            AddItem("Test Weapon", TestWeapon, EquipItem);
            AddItem("Male Tattered Robes", MaleTatteredRobes, EquipItem);
            AddItem("Female Tattered Robes", FemaleTatteredRobes, EquipItem);
            AddItem("Winiphira Ticket", WiniphiraTicket, NormalItem);
            #endregion

            #region Skills
            AddSkill("Attack", Attack, NormalSkill);
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
            AddSpell("Fireball", Fireball, WorldSpell1);
            #endregion
        }

        #region Interface
        internal ActivationDelegate GetEffect(string itemName) => Effects.ContainsKey(itemName) ? Effects[itemName] : Effects["NormalObj"];
        internal GroundObject CreateGold(Client client, uint amount, Point groundPoint) => new GroundObject((client.User.Location.MapID, groundPoint), GetGoldSprite(amount), amount);
        internal Item CreateItem(string name) => Items.ContainsKey(name) ? Items[name](1) : null;
        internal IEnumerable<Item> CreateItems(string name, uint count)
        {
            if (Items.TryGetValue(name, out ItemCreationDelegate builder))
            {
                Item item = builder(count);

                if (item.Stackable)
                    yield return item;
                else
                    for (int i = 0; i < count; i++)
                        yield return builder(1);
            }
            else
                yield break;
        }
        private byte GetGoldSprite(uint amount) => (byte)(amount >= 5000 ? 140
                : amount >= 1000 ? 141
                : amount >= 500 ? 142
                : amount >= 100 ? 137
                : amount > 1 ? 138
                : 139);
        internal Skill CreateSkill(string name) => Skills.ContainsKey(name) ? Skills[name]() : null;
        internal Spell CreateSpell(string name) => Spells.ContainsKey(name) ? Spells[name]() : null;
        private void AddItem(string name, ItemCreationDelegate itemCreationDelegate, ActivationDelegate activationDelegate)
        {
            Items.Add(name, itemCreationDelegate);
            Effects.Add(name, activationDelegate);
        }
        private void AddSkill(string name, SkillCreationDelegate skillCreationDelegate, ActivationDelegate activationDelegate)
        {
            Skills.Add(name, skillCreationDelegate);
            Effects.Add(name, activationDelegate);
        }
        private void AddSpell(string name, SpellCreationDelegate spellCreationDelegate, ActivationDelegate activationDelegate)
        {
            Spells.Add(name, spellCreationDelegate);
            Effects.Add(name, activationDelegate);
        }
        #endregion

        #region Defaults
        private bool NormalItem(Client client, Server server, ActivationContext context)
        {
            var item = context.Invoker as Item;

            if (client.User.Exchange?.IsActive == true)
            {
                client.User.Exchange.AddItem(client.User, item.Slot);
                return true;
            }
            return false;
        }
        private bool EquipItem(Client client, Server server, ActivationContext context)
        {
            var item = context.Invoker as Item;

            if (client.User.Exchange?.IsActive == true)
            {
                client.User.Exchange.AddItem(client.User, item.Slot);
                return true;
            }

            if (!item.Gender.HasFlag(client.User.Gender))
            {
                client.SendServerMessage(ServerMessageType.ActiveMessage, "This item does not fit you.");
                return false;
            }

            byte oldSlot = item.Slot;
            if(client.User.Equipment.TryEquip(item, out Item outItem))
            {
                client.User.Inventory.TryRemove(oldSlot);

                if(outItem != null)
                    client.Enqueue(ServerPackets.RemoveEquipment(outItem.EquipmentSlot));

                client.Enqueue(ServerPackets.RemoveItem(oldSlot));
                client.Enqueue(ServerPackets.AddEquipment(item));

                if (outItem != null && client.User.Inventory.AddToNextSlot(outItem))
                    client.Enqueue(ServerPackets.AddItem(outItem));

                foreach (User user in client.User.Map.ObjectsVisibleFrom(client.User.Point, true).OfType<User>())
                    user.Client.Enqueue(ServerPackets.DisplayUser(client.User));

                return true;
            }
            return false;
        }
        private bool DialogItem(Client client, Server server, ActivationContext context)
        {
            var item = context.Invoker as Item;
            client.SendDialog(item, Game.Dialogs[item.NextDialogId]);
            return true;
        }
        private bool NormalSkill(Client client, Server server, ActivationContext context)
        {
            var skill = context.Invoker as Skill;
            List<Creature> targets = Targeting.TargetsFromType(client, Point.None, skill.TargetType);
            int amount = skill.BaseDamage + (client.User.Attributes.CurrentStr * 250);

            foreach (Creature c in targets)
                Game.Assert.Damage(c, amount);

            Game.Assert.Activate(client, skill, targets, null, StatUpdateType.None, true, false, false);
            return true;
        }
        private bool NormalSpell(Client client, Server server, ActivationContext context)
        {
            var spell = context.Invoker as Spell;
            var targets = new List<Creature>() { context.Target };
            int amount = spell.BaseDamage < 0 ?
                spell.BaseDamage - (client.User.Attributes.CurrentInt * 500) :
                spell.BaseDamage + (client.User.Attributes.CurrentInt * 500);

            targets.AddRange(Targeting.TargetsFromType(client, context.Target.Point, spell.TargetType));

            foreach (Creature c in targets)
                Game.Assert.Damage(c, amount);

            Game.Assert.Activate(client, spell, targets, null, StatUpdateType.None, true);
            return targets.Count > 0;
        }
        private bool PersistentSpell(Client client, Server server, ActivationContext context)
        {
            var spell = context.Invoker as Spell;
            var targets = new List<Creature>();

            if (!spell.UsersOnly || context.Target is User)
                targets.Add(context.Target);

            foreach (Creature creature in Targeting.TargetsFromType(client, context.Target.Point, spell.TargetType))
                if (!spell.UsersOnly || creature is User)
                    targets.Add(creature);

            foreach (User u in targets.OfType<User>().ToList())
            {
                Effect targetedEffect = spell.Effect.GetTargetedEffect(u.ID, client.User.ID);
                if (u.EffectsBar.TryAdd(targetedEffect))
                    u.Client.SendEffect(targetedEffect);
                else
                    targets.Remove(u);
            }

            foreach (Creature c in targets)
                Game.Assert.Damage(c, spell.BaseDamage);

            Game.Assert.Activate(client, spell, targets, null, StatUpdateType.Primary);
            return targets.Count > 0;
        }
        /// <summary>
        /// Applies a spell of any area-size to an enemy. Applies an effect to the world in the same area-size.
        /// </summary>
        private bool WorldSpell1(Client client, Server server, ActivationContext context)
        {
            var spell = context.Invoker as Spell;
            var targets = new List<Creature>();

            if (!spell.UsersOnly || context.Target is User)
                targets.Add(context.Target);

            foreach (Creature c in Targeting.TargetsFromType(client, context.Target.Point))
                if (!spell.UsersOnly || c is User)
                    targets.Add(c);

            foreach (Point point in Targeting.PointsFromType(client, context.Target.Point, spell.TargetType))
            {
                Effect targetedEffect = spell.Effect.GetTargetedEffect(point);
                client.User.Map.AddEffect(targetedEffect);
            }

            foreach (Creature c in targets)
                Game.Assert.Damage(c, spell.BaseDamage);

            Game.Assert.Activate(client, spell, targets, null, StatUpdateType.Primary, true);

            return targets.Count > 0;
        }
        /// <summary>
        /// Applies a spell to any enemy, single Target. Applies an effect to the world in any area-size.
        /// </summary>
        private bool WorldSpell2(Client client, Server server, ActivationContext context)
        {
            var spell = context.Invoker as Spell;
            var targets = new List<Creature>();

            if (!spell.UsersOnly || context.Target is User)
                targets.Add(context.Target);

            foreach (Point point in Targeting.PointsFromType(client, context.Target.Point, spell.TargetType))
            {
                Effect targetedEffect = spell.Effect.GetTargetedEffect(point);
                client.User.Map.AddEffect(targetedEffect);
            }

            foreach (Creature c in targets)
                Game.Assert.Damage(c, spell.BaseDamage);

            Game.Assert.Activate(client, spell, targets, null, StatUpdateType.Primary, true);

            return targets.Count > 0;
        }
        #endregion

        #region Items
        #region Default Items
        private Item AdminTrinket(uint count) => new Item(new ItemSprite(13709, 0), 0, "Admin Trinket", TimeSpan.Zero, 1, 1, Animation.None, TargetsType.None, true, BodyAnimation.None, 0, Effect.None, true);
        private Item TestItem(uint count) => new Item(new ItemSprite(1108, 0), 0, "Test Item", true, count, 1, false);
        private Item TestMaleEquipment(uint count) => new Item(new ItemSprite(11990, 1023), 0, "Test Male Equipment", EquipmentSlot.Armor, 10000, 10000, 5, Gender.Male, false);
        private Item TestFemaleEquipment(uint count) => new Item(new ItemSprite(11991, 1023), 0, "Test Female Equipment", EquipmentSlot.Armor, 10000, 10000, 5, Gender.Female, false);
        private Item TestWeapon(uint count) => new Item(new ItemSprite(3254, 186), 0, "Test Weapon", EquipmentSlot.Weapon, 10000, 10000, 5, Gender.Unisex, false);
        private Item MaleTatteredRobes(uint count) => new Item(new ItemSprite(1108, 208), 0, "Male Tattered Robes", EquipmentSlot.Armor, 10000, 10000, 2, Gender.Male, false);
        private Item FemaleTatteredRobes(uint count) => new Item(new ItemSprite(1109, 208), 0, "Female Tattered Robes", EquipmentSlot.Armor, 10000, 10000, 2, Gender.Female, false);

        private Item WiniphiraTicket(uint count) => new Item(new ItemSprite(12203, 0), 0, "Winiphira Ticket", true, count, 1, true);
        #endregion
        #region Scripted Items
        #endregion
        #endregion

        #region Skills
        #region Default Skills
        private Skill Attack() => new Skill(78, "Attack", TimeSpan.Zero, true, Animation.None, TargetsType.Front, BodyAnimation.Assail, 50000);
        private Skill Cleave() => new Skill(0, "Cleave", TimeSpan.Zero, true, new Animation(119, 0, 100), TargetsType.Cleave, BodyAnimation.Swipe, 50000);
        private Skill Stab() => new Skill(5, "Stab", TimeSpan.Zero, true, new Animation(207, 0, 100), TargetsType.Front, BodyAnimation.Stab, 50000);
        private Skill CycloneSlice() => new Skill(16, "Cyclone Slice", TimeSpan.FromSeconds(3), false, new Animation(165, 0, 100), TargetsType.Surround, BodyAnimation.HeavySwipe, 50000);
        #endregion

        #region Scripted Skills
        private Skill Reposition() => new Skill(29, "Reposition", new TimeSpan(0, 0, 10), false, Animation.None, TargetsType.Front, BodyAnimation.None, 0);
        private bool Reposition(Client client, Server server, ActivationContext context)
        {
            var skill = context.Invoker as Skill;
            List<Creature> targets = Targeting.TargetsFromType(client, Point.None, skill.TargetType);

            if (targets.Count > 0)
            {
                Creature target = targets[0];

                Point newPoint = target.Point.Offset(target.Direction.Reverse());

                if (client.User.Map.IsWalkable(newPoint) || client.User.Point == newPoint)
                {
                    client.User.Direction = target.Direction;
                    Game.Assert.Warp(client.User, new Warp(client.User.Location, (client.User.Map.Id, newPoint)));
                }

                Game.Assert.Activate(client, skill, targets, null, StatUpdateType.None);
                return true;
            }
            return false;
        }

        private Skill ShoulderCharge() => new Skill(49, "Shoulder Charge", new TimeSpan(0, 0, 5), false, new Animation(107, 0, 100), TargetsType.Front, BodyAnimation.None, 0);
        private bool ShoulderCharge(Client client, Server server, ActivationContext context)
        {
            var skill = context.Invoker as Skill;
            Point furthestPoint = client.User.Point;
            Point newPoint = client.User.Point;

            for (int i = 0; i < 3; i++)
            {
                newPoint = newPoint.Offset(client.User.Direction);
                if (client.User.Map.IsWalkable(newPoint))
                    furthestPoint = newPoint;
                else
                    break;
            }

            Game.Assert.Warp(client.User, new Warp(client.User.Location, (client.User.Map.Id, furthestPoint)));

            var points = new List<Point>();
            List<Creature> targets = Targeting.TargetsFromType(client, Point.None, skill.TargetType);

            if (targets.Count > 0)
            {
                Creature target = targets[0];
                furthestPoint = target.Point;
                newPoint = target.Point;


                points.Add(target.Point);
                for (int i = 0; i < 2; i++)
                {
                    newPoint = newPoint.Offset(client.User.Direction);
                    if (client.User.Map.IsWalkable(newPoint))
                    {
                        furthestPoint = newPoint;
                        points.Add(newPoint);
                    }
                    else
                        break;
                }

                Game.Assert.Warp(target, new Warp(target.Location, (target.Map.Id, furthestPoint)));
            }

            var sfx = new List<Animation>();

            foreach(Point point in points)
                sfx.Add(new Animation(point, 0, 0, 2, 0, 100));

            Game.Assert.Activate(client, skill, targets, sfx, StatUpdateType.None);
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
            new Effect(0, 0, 0, 0, 0, 0, 0, 50000, 0, 1000, TimeSpan.FromSeconds(5), false, new Animation(13, 0, 250)));
        #endregion

        #region Scripted Spells
        private Spell Blink() => new Spell(164, "Blink", SpellType.NoTarget, string.Empty, 1, new TimeSpan(0, 0, 30), new Animation(91, 0, 100), TargetsType.None, true, BodyAnimation.WizardCast);
        private bool Blink(Client client, Server server, ActivationContext context)
        {
            var spell = context.Invoker as Spell;
            var targets = new List<Creature>() { context.Target };
            
            var ani = new Animation(96, 0, 100);
            var eff = new Effect(1800, new TimeSpan(0, 0, 10), false, ani);

            if (!client.User.HasFlag(UserState.UsedBlink) || DateTime.UtcNow.Subtract(spell.LastUse).TotalSeconds > 10 || client.User.BlinkSpot.MapID != client.User.Map.Id)
            {
                spell.CooldownReduction += 100;
                client.User.AddFlag(UserState.UsedBlink);
                client.User.BlinkSpot = client.User.Location;

                client.User.Map.AddEffect(eff.GetTargetedEffect(client.User.BlinkSpot.Point));
                return false;
            }
            else
            {
                Game.Assert.Warp(client.User, new Warp(client.User.Location, client.User.BlinkSpot));
                client.User.RemoveFlag(UserState.UsedBlink);

                //remove effect
                client.User.Map.RemoveEffect(eff.GetTargetedEffect(client.User.BlinkSpot.Point));
            }

            spell.CooldownReduction -= 100;
            Game.Assert.Activate(client, spell, targets, null, StatUpdateType.None);
            return true;
        }
        private Spell ReturnHome() => new Spell(56, "Return Home", SpellType.NoTarget, string.Empty, 1, new TimeSpan(0, 0, 1), new Animation(91, 0, 100), TargetsType.None, true, BodyAnimation.WizardCast);
        private bool ReturnHome(Client client, Server server, ActivationContext context)
        {
            var spell = context.Invoker as Spell;
            var targets = new List<Creature>() { context.Target };

            Game.Assert.Warp(context.Target, Warp.Home(context.Target as User));
            Game.Assert.Activate(client, spell, targets, null, StatUpdateType.None);
            return true;
        }
        private Spell AdminCreate() => new Spell(139, "Admin Create", SpellType.Prompt, "<Type> <Name>:<Amount>", 0, TimeSpan.Zero, new Animation(78, 0, 50), TargetsType.None, true, BodyAnimation.HandsUp);
        private bool AdminCreate(Client client, Server server, ActivationContext context)
        {
            var spell = context.Invoker as Spell;
            var targets = new List<Creature>() { context.Target };

            Match m;
            if ((m = Regex.Match(context.Prompt, @"^(item|skill|spell) (\w+(?: \w+)*)(?::(\d+))?$", RegexOptions.IgnoreCase)).Success)
            {
                string type = m.Groups[1].Value.ToLower();
                string key = m.Groups[2].Value;

                if (!uint.TryParse(m.Groups[3].Value, out uint amount))
                    amount = 1;

                switch (type)
                {
                    case "item":
                        var newItems = CreateItems(key, amount).ToList();

                        if(newItems.Count > 0)
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

                Game.Assert.Activate(client, spell, targets, null, StatUpdateType.None);
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
