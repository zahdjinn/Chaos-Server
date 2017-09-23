﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Chaos
{
    internal static class Game
    {
        private static readonly object Sync = new object();
        internal static CreationEngine CreationEngine { get; set; }
        internal static Merchants Merchants { get; set; }
        internal static Dialogs Dialogs { get; set; }
        internal static Server Server { get; set; }
        internal static World World { get; set; }
        internal static Extensions Extensions { get; set; }

        internal static void Set(Server server)
        {
            Server.WriteLog("Initializing game...");

            Server = server;
            World = new World(Server);
            World.Load();
            CreationEngine = new CreationEngine();
            Merchants = new Merchants();
            Dialogs = new Dialogs();
            Extensions = new Extensions(Server, World);
            World.Populate();
        }

        internal static void JoinServer(Client client)
        {
            client.Enqueue(Server.Packets.ConnectionInfo(Server.TableCheckSum, client.Crypto.Seed, client.Crypto.Key));
        }

        internal static void CreateChar1(Client client, string name, string password)
        {
            //checks if the name is 4-12 characters straight, if not... checks if there's a string 7-12 units long that has a space surrounced by at least 3 chars on each side.
            if (!Regex.Match(name, @"[a-zA-Z]{4,12}").Success && (!Regex.Match(name, @"[a-z A-Z]{7, 12}").Success || !Regex.Match(name, @"[a-zA-Z]{3} [a-zA-Z]{3}").Success))
                client.SendLoginMessage(LoginMessageType.Message, "Name must be 4-12 characters long, or a space surrounded by at least 3 characters on each side, up to 12 total.");
            //checks if the password is 4-8 units long
            else if (!Regex.Match(password, @".{4,8}").Success)
                client.SendLoginMessage(LoginMessageType.Message, "Password must be 4-8 units long.");
            //check if a user already exists with the given valid name
            else if(Server.DataBase.UserExists(name))
                client.SendLoginMessage(LoginMessageType.Message, "That name is taken.");
            else
            {   //otherwise set the client's new character fields so CreateChar1 can use the information and send a confirmation to the client
                client.CreateCharName = name;
                client.CreateCharPw = password;
                client.SendLoginMessage(LoginMessageType.Confirm);
            }
        }

        internal static void Login(Client client, string name, string password)
        {
            User user;
            //checks the userhash to see if the given name and password exist
            if (!Server.DataBase.CheckHash(name, Crypto.GetMD5Hash(password)))
                client.SendLoginMessage(LoginMessageType.Message, "Incorrect user name or password.");
            //checks to see if the user is currently logged on
            else if (Server.TryGetUser(name, out user))
            {
                client.SendLoginMessage(LoginMessageType.Message, "That character is already logged in.");
                user.Client.Disconnect();
            }
            else
            {   //otherwise, confirms the login, sends the login message, and redirects them to the world
                client.SendLoginMessage(LoginMessageType.Confirm);
                client.SendServerMessage(ServerMessageType.ActiveMessage, "Logging in to Chaos");
                client.Redirect(new Redirect(client, ServerType.World, name));
            }

        }

        internal static void CreateChar2(Client client, byte hairStyle, Gender gender, byte hairColor)
        {
            //if either is null, return
            if (string.IsNullOrEmpty(client.CreateCharName) || string.IsNullOrEmpty(client.CreateCharPw))
                return;

            //check the data given
            hairStyle = (byte)(hairStyle < 1 ? 1 : hairStyle > 17 ? 17 : hairStyle);
            hairColor = (byte)(hairColor > 13 ? 13 : hairColor < 0 ? 0 : hairColor);
            gender = gender != Gender.Male && gender != Gender.Female ? Gender.Male : gender;

            //create a new user, and it's display data
            User newUser = new User(client.CreateCharName, CONSTANTS.STARTING_LOCATION.Point, World.Maps[CONSTANTS.STARTING_LOCATION.MapId], Direction.South);
            DisplayData data = new DisplayData(newUser, hairStyle, hairColor, (BodySprite)((byte)gender * 16));
            newUser.DisplayData = data;

            //if the user is an admin character, apply godmode
            if (Server.Admins.Contains(newUser.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                newUser.IsAdmin = true;
                newUser.Attributes.BaseHP = 1333337;
                newUser.Attributes.BaseMP = 1333337;
                newUser.Attributes.CurrentHP = 1333337;
                newUser.Attributes.CurrentMP = 1333337;
                newUser.Attributes.BaseStr = 255;
                newUser.Attributes.BaseInt = 255;
                newUser.Attributes.BaseWis = 255;
                newUser.Attributes.BaseCon = 255;
                newUser.Attributes.BaseDex = 255;
                newUser.Titles.Add("Game Master");
                newUser.IsMaster = true;
                newUser.BaseClass = BaseClass.Admin;
                newUser.Guild = World.Guilds["Chaos Team"];
                newUser.Inventory.AddToNextSlot(CreationEngine.CreateItem("Admin Trinket"));
                newUser.Inventory.AddToNextSlot(CreationEngine.CreateItem("Test Item"));
                newUser.Inventory.AddToNextSlot(CreationEngine.CreateItem("Test Equipment"));
                newUser.SpellBook.AddToNextSlot(CreationEngine.CreateSpell("Mend"));
                newUser.Attributes.Gold += 500000000;
            }

            //try to save the new user to the database
            if (Server.DataBase.TryAddUser(newUser, client.CreateCharPw))
                client.SendLoginMessage(LoginMessageType.Confirm);
            else
                client.SendLoginMessage(LoginMessageType.Message, "Unable to create character. Possibly already exists???");
        }

        internal static void RequestMapData(Client client)
        {
            client.Enqueue(Server.Packets.MapData(client.User.Map));
        }

        internal static void Walk(Client client, Direction direction, int stepCount)
        {
            lock(client.User.Map.Sync)
            {
                //if the stepcount matches with what we have
                if (stepCount == client.StepCount)
                {
                    //plus the stepcount
                    client.StepCount++;
                    Point startPoint = client.User.Point;

                    //check if we can actually walk to the spot
                    if (!client.User.IsAdmin && !client.User.Map.IsWalkable(client.User.Point.Offsetter(direction)))
                    {
                        //if no, set their location back to what it was and return
                        World.Refresh(client, true);
                        return;
                    }

                    List<VisibleObject> visibleBefore = World.ObjectsVisibleFrom(client.User);
                    client.User.Point.Offset(direction);
                    List<VisibleObject> visibleAfter = World.ObjectsVisibleFrom(client.User);
                    List<VisibleObject> itemMonster = new List<VisibleObject>();

                    //send ourselves the walk
                    client.Enqueue(Server.Packets.ClientWalk(direction, client.User.Point));

                    //for all the things that will go off screen, remove them from the before list, our screen, and remove us from their screen(if theyre a user)
                    foreach (VisibleObject obj in visibleBefore.Except(visibleAfter, new WorldObjectComparer()).ToList())
                    {
                        (obj as User)?.Client.Enqueue(Server.Packets.RemoveObject(client.User));
                        client.Enqueue(Server.Packets.RemoveObject(obj));
                        visibleBefore.Remove(obj);
                    }

                    //send the remaining users in the before list our walk
                    foreach (User user in visibleBefore.OfType<User>())
                        user.Client.Enqueue(Server.Packets.CreatureWalk(client.User.Id, startPoint, direction));

                    //for all the things that just came onto screen, display to eachother if it's a user, otherwise add it to itemMonster
                    foreach (VisibleObject obj in visibleAfter.Except(visibleBefore, new WorldObjectComparer()))
                    {
                        User user = obj as User;

                        if (user != null)
                        {
                            user.Client.Enqueue(Server.Packets.DisplayUser(client.User));
                            client.Enqueue(Server.Packets.DisplayUser(user));
                        }
                        else
                            itemMonster.Add(obj);
                    }

                    //if itemmonster isnt empty, send everything in it to us
                    if (itemMonster.Count > 0)
                        client.Enqueue(Server.Packets.DisplayItemMonster(itemMonster.ToArray()));

                    //check collisions with warps
                    MapObject mapObj;
                    if (World.TryGetMapObject(client.User.Point, out mapObj, client.User.Map) && mapObj is Warp)
                        World.WarpUser(client.User, mapObj as Warp);

                    //check collisions with worldmaps
                    WorldMap worldMap;
                    if (World.TryGetWorldMap(client.User.Point, out worldMap, client.User.Map))
                    {
                        World.RemoveObjectFromMap(client.User, true);
                        client.Enqueue(Server.Packets.WorldMap(worldMap));
                    }
                }
            }
        }

        internal static void Pickup(Client client, byte slot, Point groundPoint)
        {
            //see if there's actually an item at the spot
            GroundItem groundItem;

            //if there's an item on the point
            if (World.TryGetGroundItem(groundPoint, out groundItem, client.User.Map))
            {
                if (groundItem.Point.Distance(client.User.Point) > CONSTANTS.PICKUP_RANGE)
                    return;

                if(groundItem is Gold)
                {
                    Gold gold = groundItem as Gold;
                    client.User.Attributes.Gold += gold.Amount;

                    client.SendAttributes(StatUpdateFlags.ExpGold);
                    World.RemoveObjectFromMap(groundItem);

                    return;
                }
                Item item = groundItem.Item;

                if (client.User.Attributes.CurrentWeight + item.Weight > client.User.Attributes.MaximumWeight)
                    client.SendServerMessage(ServerMessageType.ActiveMessage, $@"You need {item.Weight} available weight to carry this item.");
                else
                {
                    item.Slot = slot;
                    if (!client.User.Inventory.TryAdd(item))
                        return;

                    World.RemoveObjectFromMap(groundItem);
                    client.Enqueue(Server.Packets.AddItem(item));
                }
            }
        }

        internal static void Drop(Client client, byte slot, Point groundPoint, int count)
        {
            Map map = client.User.Map;

            //dont drop if too far, or on walls, warps, or doors
            if (count == 0 || groundPoint.Distance(client.User.Point) > CONSTANTS.DROP_RANGE || map.IsWall(groundPoint) || map.Warps.ContainsKey(groundPoint) || map.Doors.ContainsKey(groundPoint))
                return;

            Item item;
            
            //retreived the item
            if(client.User.Inventory.TryGet(slot, out item) && item != null)
            {
                //if we're trying to drop more than we have, return
                if (item.Count < count || item.AccountBound)
                    return;
                else //subtract the amount we're dropping
                    item.Count = item.Count - count;

                //get the grounditem associated with the item
                GroundItem groundItem = item.GroundItem(groundPoint, client.User.Map, count);

                if (item.Count > 0) //if we're suppose to still be carrying some of this item, update the count
                    client.Enqueue(Server.Packets.AddItem(item));
                else //otherwise remove the item
                {
                    if (client.User.Inventory.TryRemove(slot))
                    {
                        client.Enqueue(Server.Packets.RemoveItem(slot));
                        //subtract weight, and send a stat update
                        client.User.Attributes.CurrentWeight -= item.Weight;
                        client.Enqueue(Server.Packets.Attributes(client.User.IsAdmin, StatUpdateFlags.Primary, client.User.Attributes));
                    }
                    else
                        return;
                }

                //add the grounditem to the map
                World.AddObjectToMap(groundItem, new Location(groundItem.Map.Id, groundPoint));
            }
        }

        internal static void ExitClient(Client client, bool requestExit)
        {
            //client requests to exit first, you have to confirm
            if (requestExit)
                client.Enqueue(Server.Packets.ConfirmExit());
            else
                client.Redirect(new Redirect(client, ServerType.Login));
        }

        internal static void Ignore(Client client, IgnoreType type, string targetName)
        {
            switch(type)
            {
                //if theyre requesting the user list, send it 1 per line
                case IgnoreType.Request:
                    client.SendServerMessage(ServerMessageType.ScrollWindow, string.Join(Environment.NewLine, client.User.IgnoreList.ToArray()));
                    break;
                //add a user if it's not blank, and isnt already in the list
                case IgnoreType.AddUser:
                    if (string.IsNullOrEmpty(targetName))
                        client.SendServerMessage(ServerMessageType.ActiveMessage, "Blank never loses. He can't be ignored.");
                    else if (client.User.IgnoreList.TryAdd(targetName))
                        client.SendServerMessage(ServerMessageType.ActiveMessage, $@"{targetName} is already on the list.");
                    break;
                //remove a user if it's not blank, and is already in the list
                case IgnoreType.RemoveUser:
                    if (string.IsNullOrEmpty(targetName))
                        client.SendServerMessage(ServerMessageType.ActiveMessage, "Blank never loses. He can't be ignored.");
                    else if (client.User.IgnoreList.TryRemove(targetName))
                        client.SendServerMessage(ServerMessageType.ActiveMessage, $@"{targetName} is not on the list.");
                    break;
            }
        }

        internal static void PublicChat(Client client, PublicMessageType type, string message)
        {
            //normal messages are white, shouts are yellow
            switch(type)
            {
                case PublicMessageType.Chant:
                    break;
                case PublicMessageType.Normal:
                    message = $@"{client.User.Name}: {message}";
                    break;
                case PublicMessageType.Shout:
                    message = $@"{client.User.Name}: {{={(char)MessageColor.Yellow}{message}";
                    break;
            }
            
            List<VisibleObject> objects = new List<VisibleObject>();

            //normal messages display to everyone in 12 spaces, shouts 25
            if (type == PublicMessageType.Normal)
                objects = World.ObjectsVisibleFrom(client.User);
            else
                objects = World.ObjectsVisibleFrom(client.User, 25);
            objects.Add(client.User);

            //for each object within range
            foreach (var obj in objects)
            {
                //if it's a user
                if (obj is User)
                {
                    User user = obj as User;

                    //if we're not being ignored, send them the message
                    if (!user.IgnoreList.Contains(client.User.Name))
                        user.Client.Enqueue(Server.Packets.PublicChat(type, client.User.Id, message));
                }
                //if it's a monster
                else if (obj is Monster)
                {
                    //do things
                }
                //if it's a merchant
                else if (obj is Merchant)
                {
                    //do things
                }
            }
        }

        internal static void UseSpell(Client client, byte slot, int targetId, Point targetPoint)
        {
            Spell spell = client.User.SpellBook[slot];
            VisibleObject target;

            if (targetId == client.User.Id)
                spell.Activate(client, Server, client.User);
            else if (World.TryGetVisibleObject(targetId, out target, client.User.Map) && target.Point.Distance(targetPoint) < 5)
                spell.Activate(client, Server, target);
        }

        internal static void JoinClient(Client client, byte seed, byte[] key, string name, uint id)
        {
            //create a new crypto using the crypto information sent to us
            client.Crypto = new Crypto(seed, key, name);

            //if we're being redirected to the world
            if (client.ServerType == ServerType.World)
            {
                //retreive the user and resync it with this client
                Server.DataBase.GetUser(name).Resync(client);

                List<ServerPacket> packets = new List<ServerPacket>();

                //put all the necessary packets to log in in the list, and send them off
                foreach (Spell spell in client.User.SpellBook.Where(spell => spell != null))
                    packets.Add(Server.Packets.AddSpell(spell));
                foreach (Skill skill in client.User.SkillBook.Where(skill => skill != null))
                    packets.Add(Server.Packets.AddSkill(skill));
                foreach (Item item in client.User.Equipment.Where(equip => equip != null))
                    packets.Add(Server.Packets.AddEquipment(item));
                packets.Add(Server.Packets.Attributes(client.User.IsAdmin, StatUpdateFlags.Full, client.User.Attributes));
                foreach (Item item in client.User.Inventory.Where(item => item != null))
                    packets.Add(Server.Packets.AddItem(item));
                packets.Add(Server.Packets.LightLevel(Server.LightLevel));
                packets.Add(Server.Packets.UserId(client.User.Id, client.User.BaseClass));

                client.Enqueue(packets.ToArray());

                //add the user to the map that it's supposed to be on
                World.AddObjectToMap(client.User, client.User.Location);
                //request their profile picture and text so we can update it if they changed it
                client.Enqueue(Server.Packets.RequestPersonal());
            }
            //otherwise if theyre in the lobby, send them the notification
            else if(client.ServerType == ServerType.Lobby)
                client.Enqueue(Server.Packets.LobbyNotification(false, Server.LoginMessageCheckSum));
        }

        internal static void Turn(Client client, Direction direction)
        {
            //set the user's direction, and display the turn to everyone in range to see
            client.User.Direction = direction;
            foreach (User user in World.ObjectsVisibleFrom(client.User).OfType<User>())
                user.Client.Enqueue(Server.Packets.CreatureTurn(client.User.Id, direction));
        }

        internal static void SpaceBar(Client client)
        {
            List<ServerPacket> packets = new List<ServerPacket>();

            //cancel casting
            packets.Add(Server.Packets.CancelCasting());

            //use all basic skills (otherwise known as assails)
            foreach (Skill skill in client.User.SkillBook)
                if (skill.IsBasic)
                {
                    packets.Add(Server.Packets.AnimateUser(client.User.Id, skill.Animation, 100, false));

                    //damage checking, calculations, etc
                }

            client.Enqueue(packets.ToArray());
        }

        internal static void RequestWorldList(Client client)
        {
            client.Enqueue(Server.Packets.WorldList(Server.WorldClients.Select(cli => cli.User), client.User.Attributes.Level));
        }

        internal static void Whisper(Client client, string targetName, string message)
        {
            User targetUser;

            //if the user isnt online, tell them
            if (!Server.TryGetUser(targetName, out targetUser))
                client.SendServerMessage(ServerMessageType.Whisper, "That user is not online.");
            //otherwise, if the use is ignoring them, dont tell them. Make it seem like theyre succeeding, so they dont bother the person
            else if (targetUser.IgnoreList.Contains(client.User.Name))
                client.SendServerMessage(ServerMessageType.Whisper, $@"{targetName} >> {message}");
            //otherwise let them know if the target is on Do Not Disturb
            else if (targetUser.SocialStatus == SocialStatus.DoNotDisturb)
                client.SendServerMessage(ServerMessageType.Whisper, $@"{targetName} doesn't want to be bothered right now.");
            //otherwise send the whisper
            else
            {
                client.SendServerMessage(ServerMessageType.Whisper, $@"{targetName} >> {message}");
                targetUser.Client.SendServerMessage(ServerMessageType.Whisper, $@"{client.User.Name} << {message}");
            }
        }

        internal static void ToggleUserOption(Client client, UserOption option)
        {
            //request is for the whole list, send the whole thing
            if (option == UserOption.Request)
                client.SendServerMessage(ServerMessageType.UserOptions, client.User.UserOptions.ToString());
            else //otherwise send the single option they toggled
            {
                client.User.UserOptions.Toggle(option);
                client.SendServerMessage(ServerMessageType.UserOptions, client.User.UserOptions.ToString(option));
            }
        }

        internal static void UseItem(Client client, byte slot)
        {
            Item item = client.User.Inventory[slot];
            item.Activate(client, Server, item);
        }

        internal static void AnimateUser(Client client, byte index)
        {
            client.Enqueue(Server.Packets.AnimateUser(client.User.Id, index, 100));

            foreach (User user in World.ObjectsVisibleFrom(client.User).OfType<User>())
                user.Client.Enqueue(Server.Packets.AnimateUser(client.User.Id, index, 100));
        }

        internal static void DropGold(Client client, uint amount, Point groundPoint)
        {
            Map map = client.User.Map;
            //dont drop on walls, warps, or doors
            if (amount == 0 || groundPoint.Distance(client.User.Point) > CONSTANTS.DROP_RANGE || amount > client.User.Attributes.Gold || map.IsWall(groundPoint) || map.Warps.ContainsKey(groundPoint) || map.Doors.ContainsKey(groundPoint))
                return;

            client.User.Attributes.Gold -= amount;
            World.AddObjectToMap(CreationEngine.CreateGold(client, amount, groundPoint), new Location(map.Id, groundPoint));
            client.SendAttributes(StatUpdateFlags.ExpGold);
        }

        internal static void ChangePassword(Client client, string name, string currentPw, string newPw)
        {
            if (Server.DataBase.ChangePassword(name, currentPw, newPw))
                client.SendLoginMessage(LoginMessageType.Message, "Password successfully changed.");
        }

        internal static readonly object ExchangeLock = new object();
        internal static void DropItemOnCreature(Client client, byte inventorySlot, int targetId, byte count)
        {
            lock (ExchangeLock)
            {
                Item item;
                VisibleObject obj;

                if (World.TryGetVisibleObject(targetId, out obj, client.User.Map) && obj is Creature && client.User.Inventory.TryGetRemove(inventorySlot, out item) && item != null && obj.Point.Distance(client.User.Point) <= CONSTANTS.DROP_RANGE)
                {
                    if (obj is Monster)
                        (obj as Monster).Items.Add(item);
                    else if (obj is User)
                    {
                        User user = obj as User;
                        if (user == client.User)
                            return;

                        Exchange ex = new Exchange(client.User, user);
                        if (World.Exchanges.TryAdd(ex.ExchangeId, ex))
                            ex.Activate(item);

                        ex.AddItem(client.User, item);
                    }
                }
            }
        }

        internal static void DropGoldOnCreature(Client client, uint amount, int targetId)
        {
            lock (ExchangeLock)
            {
                VisibleObject obj;

                if (client.User.Attributes.Gold > amount && World.TryGetVisibleObject(targetId, out obj, client.User.Map) && obj is Creature && obj.Point.Distance(client.User.Point) <= CONSTANTS.DROP_RANGE)
                {
                    if (obj is Monster)
                    {
                        (obj as Monster).Gold += amount;
                        client.User.Attributes.Gold -= amount;
                    }
                    else if(obj is User)
                    {
                        User user = obj as User;
                        if (user == client.User)
                            return;

                        Exchange ex = new Exchange(client.User, user);
                        if (World.Exchanges.TryAdd(ex.ExchangeId, ex))
                            ex.Activate();

                        ex.SetGold(client.User, amount);
                    }
                }
            }
        }

        internal static void RequestProfile(Client client)
        {
            client.Enqueue(Server.Packets.ProfileSelf(client.User));
        }

        internal static readonly object GroupLock = new object();
        internal static void RequestGroup(Client client, GroupRequestType type, string targetName, GroupBox box)
        {
            lock (GroupLock)
            {
                User targetUser;

                switch (type)
                {
                    case GroupRequestType.Invite:
                        if (!World.TryGetUser(targetName, out targetUser, client.User.Map))                                                                                     //if target user doesnt exist
                            client.SendServerMessage(ServerMessageType.ActiveMessage, $@"{targetName} is not near.");
                        else if (targetUser.IgnoreList.Contains(client.User.Name))                                                     //if theyre on the ignore list, return
                            return;
                        else if (!client.User.UserOptions.Group)                                                                                                                //else if your grouping is turned off, let them know
                            client.SendServerMessage(ServerMessageType.ActiveMessage, $@"Grouping is disabled.");
                        else if (!targetUser.UserOptions.Group)                                                                                                                 //else if the targets grouping is turned off, let them know
                            client.SendServerMessage(ServerMessageType.ActiveMessage, $@"{targetName} is not accepting group invites.");
                        else if (client.User.Grouped)                                                                                                                           //else if this we are already in a group
                        {
                            if (client.User == targetUser)                                                                                                                          //and we're trying to group ourself
                            {
                                if (targetUser.Group.TryRemove(client.User))                                                                                                            //leave the group
                                    client.SendServerMessage(ServerMessageType.ActiveMessage, "You have left the group.");
                            }
                            else if (!targetUser.Grouped)                                                                                                                           //else if the target isnt grouped
                                targetUser.Client.Enqueue(Server.Packets.GroupRequest(GroupRequestType.Request, client.User.Name));
                            else if (targetUser.Group != client.User.Group)                                                                                                         //else if the target's group isnt the same as our group
                                client.SendServerMessage(ServerMessageType.ActiveMessage, $@"{targetName} is already in a group.");
                            else if (client.User.Group.Leader == client.User)                                                                                                       //else if we're the leader of the group
                            {
                                if (client.User.Group.TryRemove(targetUser, true))                                                                                                      //kick them from the group
                                    client.SendServerMessage(ServerMessageType.ActiveMessage, $@"You have kicked {targetName} from the group.");
                            }
                            else                                                                                                                                                    //else we cant kick them, just say theyre in our group
                                client.SendServerMessage(ServerMessageType.ActiveMessage, $@"{targetName} is already in your group.");
                        }
                        else                                                                                                                                                     //else we're not grouped
                        {
                            if (client.User == targetUser)                                                                                                                          //and we are trying to group ourself
                                client.SendServerMessage(ServerMessageType.ActiveMessage, "You can't form a group alone.");
                            else if (targetUser.Grouped)                                                                                                                            //else if target is grouped
                                client.SendServerMessage(ServerMessageType.ActiveMessage, $@"{targetName} is already in a group.");
                            else                                                                                                                                                    //else send them a group request
                                targetUser.Client.Enqueue(Server.Packets.GroupRequest(GroupRequestType.Request, client.User.Name));
                        }
                        break;
                    case GroupRequestType.Join:
                        if (!World.TryGetUser(targetName, out targetUser, client.User.Map))
                            client.SendServerMessage(ServerMessageType.ActiveMessage, $@"{targetName} is not near.");
                        else if (targetUser.IgnoreList.Contains(client.User.Name))
                            return;
                        else if (client.User.Grouped)
                            client.SendServerMessage(ServerMessageType.ActiveMessage, "You are already in a group.");
                        else
                        {
                            if (client.User == targetUser)
                                client.SendServerMessage(ServerMessageType.ActiveMessage, "You can't form a group alone.");
                            else if (targetUser.Grouped)
                                client.SendServerMessage(ServerMessageType.ActiveMessage, $@"{targetName} is already in a group.");
                            else
                            {
                                Group group = new Group(targetUser, client.User);
                                targetUser.Client.SendServerMessage(ServerMessageType.ActiveMessage, $@"You form a group with {client.User.Name}");
                                client.SendServerMessage(ServerMessageType.ActiveMessage, $@"You form a group with {targetName}");
                            }
                        }
                        break;
                    case GroupRequestType.Groupbox:

                        break;

                    case GroupRequestType.RemoveGroupBox:

                        break;
                }
            }
        }

        internal static void ToggleGroup(Client client)
        {
            //toggle the group useroption
            client.User.UserOptions.Toggle(UserOption.Group);

            //remove yourself from the group, if you're in one
            if (client.User.Grouped)
                if (client.User.Group.TryRemove(client.User))
                    client.User.Group = null;

            //send the profile so the group button will change, also send the useroption that was changed
            client.Enqueue(Server.Packets.ProfileSelf(client.User));
            client.SendServerMessage(ServerMessageType.UserOptions, client.User.UserOptions.ToString(UserOption.Group));
        }

        internal static void SwapSlot(Client client, Pane pane, byte origSlot, byte endSlot)
        {
            //attempt to swap the objects at origSlot and endSlot in the given pane
            switch (pane)
            {
                case Pane.Inventory:
                    if (!client.User.Inventory.TrySwap(origSlot, endSlot))
                        return;
                    break;
                case Pane.SkillBook:
                    if (!client.User.SkillBook.TrySwap(origSlot, endSlot))
                        return;
                    break;
                case Pane.SpellBook:
                    if (!client.User.SpellBook.TrySwap(origSlot, endSlot))
                        return;
                    break;
                default:
                    return;
            }

            //if it succeeds, update the user's panels
            client.Enqueue(Server.Packets.RemoveItem(origSlot));
            client.Enqueue(Server.Packets.RemoveItem(endSlot));

            //check for null, incase we were simply moving an item to an already empty slot
            if (client.User.Inventory[origSlot] != null)
                client.Enqueue(Server.Packets.AddItem(client.User.Inventory[origSlot]));
            if (client.User.Inventory[endSlot] != null)
                client.Enqueue(Server.Packets.AddItem(client.User.Inventory[endSlot]));
        }

        internal static void RequestRefresh(Client client)
        {
            World.Refresh(client);
        }

        internal static void RequestPursuit(Client client, GameObjectType objType, int objId, PursuitIds pursuitId, byte[] args)
        {
            VisibleObject obj;
            if(World.TryGetVisibleObject(objId, out obj, client.User.Map))
            {
                Merchant merchant = obj as Merchant;
                Pursuit p = (obj as Merchant).Menu[pursuitId];

                if (!client.User.WithinRange(merchant))
                    return;

                client.ActiveObject = obj;
                client.CurrentDialog = Dialogs[p.DialogId];

                //if dialog is null, or closeDialog & Activate is true
                //here we use the pursuit's ID, because we want to close the dialog and activate at the same time
                if (client.CurrentDialog == null || (client.CurrentDialog?.Type == DialogType.CloseDialog && Dialogs.ActivateEffect(p.PursuitId)(client, Server, client.CurrentDialog)))
                {
                    client.ActiveObject = null;
                    client.CurrentDialog = null;
                }

                client.Enqueue(Server.Packets.DisplayDialog(merchant, client.CurrentDialog));
            }
        }

        internal static void ReplyDialog(Client client, GameObjectType objType, int objId, PursuitIds pursuitId, ushort dialogId, DialogArgsType argsType, byte option, string userInput)
        {
            Dialog dialog = client.CurrentDialog;
            object effectArgs = new object();

            //if there's no active dialog or object, what are we replying to?
            //if the active object is no longer valid, cease the dialog
            if (client.CurrentDialog == null || client.ActiveObject == null ||
                (client.ActiveObject is Merchant && !(client.ActiveObject as Merchant).WithinRange(client.User)) ||
                (client.ActiveObject is Item && !client.User.Inventory.Contains(client.ActiveObject as Item)))
            {
                client.CurrentDialog = null;
                client.ActiveObject = null;
                client.Enqueue(Server.Packets.DisplayDialog(client.ActiveObject, client.CurrentDialog));
                return;
            }

            DialogOption opt = Enum.IsDefined(typeof(DialogOption), (dialogId - dialog.Id)) ? (DialogOption)(dialogId - dialog.Id) : DialogOption.Close;

            switch (opt)
            {
                case DialogOption.Previous:
                    client.CurrentDialog = client.CurrentDialog.Previous();
                    break;
                case DialogOption.Close:
                    client.ActiveObject = null;
                    client.CurrentDialog = null;
                    break;
                case DialogOption.Next:
                    switch(argsType)
                    {
                        case DialogArgsType.None:
                            client.CurrentDialog = client.CurrentDialog.Next();
                            break;
                        case DialogArgsType.MenuResponse:
                            client.CurrentDialog = client.CurrentDialog.Next(option);
                            effectArgs = option;
                            break;
                        case DialogArgsType.TextResponse:
                            client.CurrentDialog = client.CurrentDialog.Next();
                            effectArgs = userInput;
                            break;
                    }
                    //we use "dialog" here because we're closing the dialog, and we want to activate the effect of the dialog we were at as it closes
                    if (client.CurrentDialog == null || (client.CurrentDialog.Type == DialogType.CloseDialog && Dialogs.ActivateEffect((PursuitIds)dialog.PursuitId)(client, Server, effectArgs)))
                    {
                        client.ActiveObject = null;
                        client.CurrentDialog = null;
                    }
                    break;
            }

            client.Enqueue(Server.Packets.DisplayDialog(client.ActiveObject, client.CurrentDialog));
        }

        internal static void Boards()
        {
            throw new NotImplementedException();
        }

        internal static void UseSkill(Client client, byte slot)
        {
            throw new NotImplementedException();
        }

        internal static void ClickWorldMap(Client client, ushort mapId, Point point)
        {
            World.AddObjectToMap(client.User, new Location(mapId, point));
        }

        internal static void ClickObject(Client client, int objectId)
        {
            //if we're clicking ourself, send profileSelf
            if (objectId == client.User.Id)
                client.Enqueue(Server.Packets.ProfileSelf(client.User));
            else
            {   //otherwise, get the object we're clicking
                VisibleObject obj;
                if (World.TryGetVisibleObject(objectId, out obj, client.User.Map))
                {
                    //if it's a monster, display it's name
                    if (obj is Monster)
                        client.SendServerMessage(ServerMessageType.OrangeBar1, obj.Name);
                    //if it's a user, send us their profile
                    else if (obj is User)
                    {
                        client.Enqueue(Server.Packets.Profile(obj as User));
                    }
                    else if (obj is Merchant)
                    {
                        Merchant merchant = obj as Merchant;

                        if (merchant.ShouldDisplay)
                        {
                            client.ActiveObject = merchant;
                            client.Enqueue(Server.Packets.DisplayMenu(merchant));
                        }
                        else
                            merchant.LastClicked = DateTime.UtcNow;

                    }
                }
            }
        }

        internal static void ClickObject(Client client, Point clickPoint)
        {
            //dont allow this to be spammed
            if (DateTime.UtcNow.Subtract(client.LastClickObj).TotalSeconds < 1)
                return;
            else
                client.LastClickObj = DateTime.UtcNow;

            MapObject obj;

            //get the bottom map object at the point we're clicking
            if (World.TryGetMapObject(clickPoint, out obj, client.User.Map))
            {
                //if it's a door, toggle it
                if (obj is Door)
                {
                    (obj as Door).Toggle();
                    client.Enqueue(Server.Packets.Door(obj as Door));
                }
                //do things
            }
        }

        internal static void RemoveEquipment(Client client, EquipmentSlot slot)
        {
            Item item;

            //attempt to remove equipment at the given slot
            if (client.User.Equipment.TryGetRemove((byte)slot, out item))
            {
                //if it succeeds, display the item in the user's inventory, and remove it from the equipment panel
                client.User.Inventory.AddToNextSlot(item);
                client.Enqueue(Server.Packets.RemoveEquipment(slot));
                client.Enqueue(Server.Packets.AddItem(item));
                //set hp/mp?
                client.Enqueue(Server.Packets.Attributes(client.User.IsAdmin, StatUpdateFlags.Primary, client.User.Attributes));

                foreach (User user in World.ObjectsVisibleFrom(client.User).OfType<User>())
                    user.Client.Enqueue(Server.Packets.DisplayUser(client.User));

                client.Enqueue(Server.Packets.DisplayUser(client.User));
            }
        }

        internal static void KeepAlive(Client client, byte a, byte b)
        {
            client.Enqueue(Server.Packets.KeepAlive(a, b));
        }

        internal static void ChangeStat(Client client, Stat stat)
        {
            switch(stat)
            {
                case Stat.STR:
                    client.User.Attributes.BaseStr++;
                    break;
                case Stat.INT:
                    client.User.Attributes.BaseInt++;
                    break;
                case Stat.WIS:
                    client.User.Attributes.BaseWis++;
                    break;
                case Stat.CON:
                    client.User.Attributes.BaseCon++;
                    break;
                case Stat.DEX:
                    client.User.Attributes.BaseDex++;
                    break;
            }

            client.Enqueue(Server.Packets.Attributes(client.User.IsAdmin, StatUpdateFlags.Primary, client.User.Attributes));
        }

        internal static void Exchange(Client client, ExchangeType type, uint targetId = 0, uint amount = 0, byte slot = 0, byte count = 0)
        {
            throw new NotImplementedException();
        }

        internal static void RequestLoginMessage(bool send, Client client)
        {
            client.Enqueue(Server.Packets.LobbyNotification(send, 0, Server.LoginMessage));
        }

        internal static void BeginChant(Client client)
        {
            throw new NotImplementedException();
        }

        internal static void DisplayChant(Client client, string chant)
        {
            foreach (User user in World.ObjectsVisibleFrom(client.User).OfType<User>())
                user.Client.SendPublicMessage(PublicMessageType.Chant, client.User.Id, chant);

            client.SendPublicMessage(PublicMessageType.Chant, client.User.Id, chant);
        }

        internal static void Personal(Client client, byte[] portraitData, string profileMsg)
        {
            client.User.Personal = new Personal(portraitData, profileMsg);
        }

        internal static void RequestServerTable(Client client, bool request)
        {
            if (request)
                client.Enqueue(Server.Packets.ServerTable(Server.Table));
            else
                client.Redirect(new Redirect(client, ServerType.Lobby));
        }

        internal static void RequestHomepage(Client client)
        {
            client.Enqueue(Server.Packets.LobbyControls(3, @"http://www.darkages.com"));
        }

        internal static void SynchronizeTicks(Client client, TimeSpan serverTicks, TimeSpan clientTicks)
        {
            client.Enqueue(Server.Packets.SynchronizeTicks());
        }

        internal static void ChangeSoocialStatus(Client client, SocialStatus status)
        {
            client.User.SocialStatus = status;
        }

        internal static void RequestMetaFile(Client client, bool all)
        {
            client.Enqueue(Server.Packets.Metafile(all));
        }
    }
}
