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
using System.IO;
using System.Linq;
using System;

namespace Chaos
{
    public sealed class Map
    {
        private readonly object Sync = new object();
        private readonly Dictionary<int, VisibleObject> Objects;
        private readonly List<Effect> Effects;
        private readonly Pathfinder Pathfinder;

        internal Point[,] Points { get; }
        internal Dictionary<Point, Tile> Tiles { get; }
        internal byte[] Data { get; private set; }
        internal ushort CheckSum { get; private set; }

        public Dictionary<Point, Door> Doors { get; }
        public Dictionary<Point, Warp> Warps { get; }
        public Dictionary<Point, WorldMap> WorldMaps { get; }
        public ushort Id { get; }
        public byte SizeX { get; set; }
        public byte SizeY { get; set; }
        public MapFlags Flags { get; set; }
        public string Name { get; set; }
        public sbyte Music { get; set; }

        public Point this[int x, int y] => Points[x, y];
        /// <summary>
        /// Checks if the map has a certain flag.
        /// </summary>
        internal bool HasFlag(MapFlags flag) => Flags.HasFlag(flag);
        public override string ToString() => $@"ID: {Id} | NAME: {Name} | SIZE_X: {SizeX} | SIZE_Y: {SizeY}";

        #region Constructor / Data
        /// <summary>
        /// Master constructor for an object representing an in-game map.
        /// </summary>
        public Map(ushort id, byte sizeX, byte sizeY, MapFlags flags, string name, sbyte music, BinaryReader reader = null)
        {
            Id = id;
            SizeX = sizeX;
            SizeY = sizeY;
            Flags = flags;
            Name = name;
            Points = new Point[sizeX, sizeY];
            Tiles = new Dictionary<Point, Tile>();
            Warps = new Dictionary<Point, Warp>();
            WorldMaps = new Dictionary<Point, WorldMap>();
            Objects = new Dictionary<int, VisibleObject>();
            Doors = new Dictionary<Point, Door>();
            Effects = new List<Effect>();

            if (reader != null)
            {
                Initialize($@"{Paths.MapFiles}lod{Id}.map", reader);
                Pathfinder = new Pathfinder(this);
            }
        }

        /// <summary>
        /// Loads the tile data from file for the map.
        /// </summary>
        /// <param name="path"></param>
        private void Initialize(string path, BinaryReader reader)
        {
            if (File.Exists(path))
            {
                byte[] data = File.ReadAllBytes(path);
                Tiles.Clear();
                Data = data;

                int index = 0;
                for (ushort y = 0; y < SizeY; y++)
                    for (ushort x = 0; x < SizeX; x++)
                    {
                        Point point = (x, y);

                        Points[x, y] = point;
                        Tiles[Points[x, y]] = new Tile((ushort)(data[index++] | (data[index++] << 8)), (ushort)(data[index++] | (data[index++] << 8)), (ushort)(data[index++] | (data[index++] << 8)));

                        if (CONSTANTS.DOOR_SPRITES.Contains(Tiles[Points[x, y]].LeftForeground))
                            Doors[point] = new Door((Id, point), true, true);
                        else if (CONSTANTS.DOOR_SPRITES.Contains(Tiles[Points[x, y]].RightForeground))
                            Doors[point] = new Door((Id, Points[x, y]), true, false);
                    }

                CheckSum = Crypto.Generate16(Data);
            }

            //unused index byte for future use
            reader.ReadByte();

            //load warps
            ushort warpCount = reader.ReadUInt16();
            var doorPoints = GetLockedInstance<Door>().Select(d => d.Point).ToList();

            for (int i = 0; i < warpCount; i++)
            {
                byte sourceX = reader.ReadByte();
                byte sourceY = reader.ReadByte();
                ushort targetMapId = reader.ReadUInt16();
                byte targetX = reader.ReadByte();
                byte targetY = reader.ReadByte();
                bool shouldDisplay = !doorPoints.Contains((sourceX, sourceY));
                var warp = new Warp(Id, sourceX, sourceY, targetMapId, targetX, targetY);
                Warps[(sourceX, sourceY)] = warp;

                if (shouldDisplay)
                    AddEffect(new Effect(4500, TimeSpan.Zero, false, new Animation((sourceX, sourceY), 96, 250)));
            }

            var wmapEff = new Effect(1250, TimeSpan.Zero, false, new Animation(214, 0, 250));

            //load worldmaps for this map
            byte wMapCount = reader.ReadByte();
            for (int i = 0; i < wMapCount; i++)
            {
                byte x = reader.ReadByte();
                byte y = reader.ReadByte();
                uint checkSum = reader.ReadUInt32();
                if(Game.World.WorldMaps.TryGetValue(checkSum, out WorldMap worldMap))
                {
                    WorldMaps[Points[x, y]] = worldMap;
                    AddEffect(new Effect(1250, TimeSpan.Zero, false, new Animation((x, y), 214, 250)));
                }
            }
        }
        #endregion

        /// <summary>
        /// Attempts to synchronously retreive all objects of a given type on the map. LOCKED INSTANCED.
        /// </summary>
        /// <typeparam name="T">The type of object to return.</typeparam>
        /// <param name="map">The map to return from.</param>
        internal IEnumerable<T> GetLockedInstance<T>()
        {
            lock (Sync)
            {
                Type tType = typeof(T);
                IEnumerable<T> enumerable;

                if (VisibleObject.TypeRef.IsAssignableFrom(tType))
                    enumerable = Objects.Values.OfType<T>().ToList();
                else if (Door.TypeRef.IsAssignableFrom(tType))
                    enumerable = Doors.Values.OfType<T>().ToList();
                else if (Warp.TypeRef.IsAssignableFrom(tType))
                    enumerable = Warps.Values.OfType<T>().ToList();
                else if (typeof(KeyValuePair<Point, WorldMap>).IsAssignableFrom(tType))
                    enumerable = WorldMaps.OfType<T>().ToList();
                else if (Effect.TypeRef.IsAssignableFrom(tType))
                    enumerable = Effects.OfType<T>().ToList();
                else
                    yield break;

                foreach (T tObj in enumerable)
                    yield return tObj;
            }
        }

        #region MapObjects
        /// <summary>
        /// Synchronously adds a single object to a map. Sends and sets all relevant data.
        /// </summary>
        /// <param name="vObject">Any visible object.</param>
        /// <param name="point">The point you want to add it to.</param>
        internal void AddObject(VisibleObject vObject, Point point)
        {
            if (vObject == null) return;

            lock (Sync)
            {
                //change location of the object and add it to the map
                vObject.Location = (Id, point);
                Objects.Add(vObject.ID, vObject);

                var itemMonsterToSend = new List<VisibleObject>();
                var usersToSend = new List<User>();

                //get all objects that would be visible to this object and handle them
                foreach (VisibleObject obj in vObject.Map.ObjectsVisibleFrom(point, true).OfType<VisibleObject>())
                    if (obj is User aUser && obj != vObject)
                        usersToSend.Add(aUser);
                    else
                        itemMonsterToSend.Add(obj);

                //if this object is a user
                if (vObject is User tUser)
                {
                    tUser.Client.Enqueue(ServerPackets.MapChangePending());      //send pending map change
                    tUser.Client.Enqueue(ServerPackets.MapInfo(tUser.Map));      //send map info
                    tUser.Client.Enqueue(ServerPackets.Location(tUser.Point));   //send location

                    foreach (User u2s in usersToSend)
                    {
                        tUser.Client.Enqueue(ServerPackets.DisplayUser(u2s));   //send it all the users
                        u2s.Client.Enqueue(ServerPackets.DisplayUser(tUser));   //send all the users this user as well
                        u2s.Client.Enqueue(ServerPackets.ServerMessage(ServerMessageType.AdminMessage, "{=c" + tUser.Name + ", a " + tUser.BaseClass + ", has logged into Cogadh."));
                    }

                    tUser.Client.Enqueue(ServerPackets.DisplayItemMonster(itemMonsterToSend.ToArray()));    //send it all the items, monsters, and merchants
                    tUser.Client.Enqueue(ServerPackets.Door(tUser.Map.DoorsVisibleFrom(point).ToArray()));     //send the user all nearby doors
                    tUser.Client.Enqueue(ServerPackets.MapChangeComplete());    //send it mapchangecomplete
                    tUser.Client.Enqueue(ServerPackets.MapLoadComplete());      //send it maploadcomplete
                    tUser.Client.Enqueue(ServerPackets.DisplayUser(tUser));     //send it itself

                    tUser.AnimationHistory.Clear();
                }
                else //if this object isnt a user
                    foreach (User u2s in usersToSend)
                        u2s.Client.Enqueue(ServerPackets.DisplayItemMonster(vObject)); //send all the visible users this object
            }
        }

        /// <summary>
        /// Synchronously adds many objects to the map. NON-USERS ONLY!
        /// </summary>
        /// <param name="vObjects">Any non-user visibleobject</param>
        /// <param name="point">The point you want to add it to.</param>
        internal void AddObjects(List<VisibleObject> vObjects)
        {
            if (vObjects.Count() == 0) return;

            lock (Sync)
            {
                //change location of each object and add each item to the map
                foreach (VisibleObject vObj in vObjects)
                    Objects.Add(vObj.ID, vObj);

                //send all the visible users these objects
                foreach (User user in GetLockedInstance<User>())
                    user.Client.Enqueue(ServerPackets.DisplayItemMonster(vObjects.Where(obj => obj.WithinRange(user)).ToArray()));
            }
        }

        /// <summary>
        /// Synchronously removes a single object from the map.
        /// </summary>
        /// <param name="vObject">Any visible object you want removed.</param>
        /// <param name="skipRemove">Whether or not they are stepping into a worldMap.</param>
        internal void RemoveObject(VisibleObject vObject, bool skipRemove = false)
        {
            if (vObject == null) return;

            lock (Sync)
            {
                Objects.Remove(vObject.ID);
                foreach (User user in ObjectsVisibleFrom(vObject.Point).OfType<User>())
                    user.Client.Enqueue(ServerPackets.RemoveObject(vObject));

                if (!skipRemove)
                    vObject.Location = Location.None;
            }
        }

        /// <summary>
        /// Attempts to synchronously get an object that matches given conditions and return it.
        /// </summary>
        /// <typeparam name="T">The type of object to return. MUST INHERIT FROM VISIBLEOBJECT.</typeparam>
        /// <param name="predicate">The conditions to be met.</param>
        /// <param name="obj"></param>
        internal bool TryGetObject<T>(Func<T, bool> predicate, out T obj)
        {
            lock (Sync)
            {
                obj = Objects.Values.OfType<T>().FirstOrDefault(predicate);

                return obj != null;
            }
        }
        #endregion

        #region VisibleFromPoint
        /// <summary>
        /// Synchronously retrieves all objects visible from a given point.
        /// </summary>
        /// <param name="point">The point of origin.</param>
        /// <param name="include">Whether or not to include the origin point.</param>
        /// <param name="distance">Optional distance from the point to retreive from.</param>
        /// <returns></returns>
        internal IEnumerable<MapObject> ObjectsVisibleFrom(Point point, bool include = false, byte distance = 13)
        {
            lock (Sync)
                foreach (MapObject visibleObject in Objects.Values.Where(obj => obj.Point.Distance(point) <= distance && (include ? true : obj.Point != point)))
                    yield return visibleObject;
        }

        /// <summary>
        /// Synchronously retrieves all effects visible from the creature.
        /// </summary>
        /// <param name="creature">The creature to base the search from.</param>
        internal IEnumerable<Effect> EffectsVisibleFrom(Point point)
        {
            lock (Sync)
                foreach (Effect eff in Effects.Where(e => e.Animation.TargetPoint.Distance(point) < 13))
                    yield return eff;
        }

        /// <summary>
        /// Synchronously retrieves all doors visible from the user.
        /// </summary>
        /// <param name="user">The user to base from.</param>
        internal IEnumerable<Door> DoorsVisibleFrom(Point point)
        {
            lock (Sync)
                foreach (Door door in Doors.Values.Where(door => point.Distance(door.Point) < 13))
                    yield return door;
        }
        #endregion

        #region Effects
        /// <summary>
        /// Synchronously adds an effect to the map.
        /// </summary>
        /// <param name="effect">The effect to add.</param>
        internal void AddEffect(Effect effect)
        {
            lock (Sync)
                Effects.Add(effect);
        }

        /// <summary>
        /// Synchronously removes a single effect from the map.
        /// </summary>
        /// <param name="effect">The effect to remove.</param>
        internal void RemoveEffect(Effect effect)
        {
            lock (Sync)
                Effects.Remove(effect);
        }

        internal void ApplyPersistantEffects()
        {
            //lock the map
            lock (Sync)
            {
                foreach (Creature creature in GetLockedInstance<Creature>()) //for each creature C on the map
                {
                    var user1 = creature as User;
                    foreach (Effect effect in creature.EffectsBar.ToList()) //for each effect on that creature's bar
                    {
                        int index = effect.Animation.GetHashCode(); //get it's animation's index
                        if (effect.RemainingDurationMS() == 0) //if the effect is expired
                        {
                            creature.EffectsBar.TryRemove(effect); //remove the effect from the creature
                            user1?.Client.SendEffect(effect); //if it's a user, update the bar
                        }
                        else if (!creature.AnimationHistory.ContainsKey(index) || DateTime.UtcNow.Subtract(creature.AnimationHistory[index]).TotalMilliseconds > effect.AnimationDelay) //if the effect is not expired, and need to be updated
                        {
                            creature.AnimationHistory[effect.Animation.GetHashCode()] = DateTime.UtcNow; //update the animation history
                            foreach (User user in creature.Map.ObjectsVisibleFrom(creature.Point, true).OfType<User>()) //for each user within sight, including itself if it is a user
                            {
                                if (user == user1) //if this user is the creature
                                    user.Client.SendEffect(effect); //update the bar

                                user.Client.SendAnimation(effect.Animation); //send this animation to all visible users
                            }

                            if (effect.CurrentHPMod != 0 || effect.CurrentMPMod != 0)
                            {
                                Game.Assert.Damage(creature, effect.CurrentHPMod, true); //apply damage to the creature
                                Game.Assert.Damage(creature, effect.CurrentMPMod, true, true);
                                user1?.Client.SendAttributes(StatUpdateType.Vitality);
                            }
                        }
                    }

                    foreach (Effect effect in EffectsVisibleFrom(creature.Point).ToList())
                    {
                        int index = effect.Animation.GetHashCode();
                        if (effect.Duration != TimeSpan.Zero && effect.RemainingDurationMS() == 0)
                            RemoveEffect(effect);
                        else if (!creature.WorldAnimationHistory.ContainsKey(index) || DateTime.UtcNow.Subtract(creature.WorldAnimationHistory[index]).TotalMilliseconds > effect.AnimationDelay)
                        {
                            if (user1 != null)
                            {
                                user1.WorldAnimationHistory[index] = DateTime.UtcNow;
                                user1?.Client.SendAnimation(effect.Animation);
                            }

                            if (effect.Animation.TargetPoint == creature.Point)
                            {
                                Game.Assert.Damage(creature, effect.CurrentHPMod);
                                Game.Assert.Damage(creature, effect.CurrentMPMod);
                            }
                        }
                    }

                }
            }
        }
        #endregion

        #region Doors
        internal void ToggleDoor(Point point)
        {
            lock (Sync)
            {
                if (Doors.TryGetValue(point, out Door tDoor))
                {
                    var doors = new List<Door>() { tDoor };

                    //for each surrounding point from the door
                    foreach (Point p in Targeting.GetCardinalPoints(tDoor.Point))
                        //if it's also a door
                        if (Doors.TryGetValue(p, out Door door2))
                        {
                            //add it
                            doors.Add(door2);

                            //if this 2nd door has another door 1 space in the same direction, we can break.
                            if (Doors.TryGetValue(p.Offset(p.Relation(tDoor.Point)), out Door door3))
                            {
                                //add that door as well
                                doors.Add(door3);
                                break;
                            }
                        }

                    var doorToSend = new List<Door>();

                    foreach (Door door in doors)
                        if (door.Toggle())
                            doorToSend.Add(door);

                    if (doorToSend.Count > 0)
                        foreach (User user in ObjectsVisibleFrom(tDoor.Point).OfType<User>())
                            user.Client.Enqueue(ServerPackets.Door(doorToSend.ToArray()));
                }
            }
        }
        #endregion

        #region Walking
        /// <summary>
        /// Checks if a point is within the bounds of the map.
        /// </summary>
        internal bool WithinMap(Point point) => point.X >= 0 && point.Y >= 0 && point.X < SizeX && point.Y < SizeY;

        /// <summary>
        /// Checks if a point is within the bounds of the map, or is a wall.
        /// </summary>
        internal bool IsWall(Point point) => !WithinMap(point) || (Doors.Keys.Contains(point) ? Doors[point].Closed : Tiles[point].IsWall);

        /// <summary>
        /// Checks if a given point is within the bounds of the map, is a wall, or has a monster, door, or other object already on it.
        /// </summary>
        internal bool IsWalkable(Point point)
        {
            lock (Sync)
                return !IsWall(point) && !Objects.Values.OfType<Creature>().Any(creature => creature.Type != CreatureType.WalkThrough && creature.Point == point);
        }

        /// <summary>
        /// Synchronously moves a character in a given direction, handling on-screen information and walking packets.
        /// </summary>
        /// <param name="client">The client who's user is walking.</param>
        /// <param name="direction">The direction to walk.</param>
        internal void ClientWalk(Client client, Direction direction)
        {
            lock (Sync)
            {
                //plus the stepcount
                client.StepCount++;
                client.User.Direction = direction;

                if (!Objects.ContainsKey(client.User.ID))
                    return;

                Point startPoint = client.User.Point;

                //check if we can actually walk to the spot
                if ((!client.User.IsAdmin && !IsWalkable(client.User.Point.Offset(direction))) || !WithinMap(client.User.Point.Offset(direction)))
                {
                    //if no, set their location back to what it was and return
                    Refresh(client, true);
                    return;
                }

                var visibleBefore = ObjectsVisibleFrom(client.User.Point, true).Where(obj => obj != client.User).ToList();
                var doorsBefore = DoorsVisibleFrom(client.User.Point).ToList();
                client.User.Location = (Id, startPoint.Offset(direction));
                var visibleAfter = ObjectsVisibleFrom(client.User.Point, true).Where(obj => obj != client.User).ToList();
                var itemMonster = new List<VisibleObject>().ToList();
                var doorsAfter = DoorsVisibleFrom(client.User.Point).ToList();
                var doors = doorsAfter.Except(doorsBefore).ToList();

                //send ourselves the walk
                client.Enqueue(ServerPackets.ConfirmClientWalk(direction, client.User.Point));

                //for all the things that will go off screen, remove them from the before list, our screen, and remove us from their screen(if theyre a user)
                foreach (VisibleObject obj in visibleBefore.Except(visibleAfter).ToList())
                {
                    (obj as User)?.Client.Enqueue(ServerPackets.RemoveObject(client.User));
                    client.Enqueue(ServerPackets.RemoveObject(obj));
                    visibleBefore.Remove(obj);
                }

                //send the remaining users in the before list our walk
                foreach (User user in visibleBefore.OfType<User>())
                    user.Client.Enqueue(ServerPackets.CreatureWalk(client.User.ID, startPoint, direction));

                //for all the things that just came onto screen, display to eachother if it's a user, otherwise add it to itemMonster
                foreach (VisibleObject obj in visibleAfter.Except(visibleBefore))
                {
                    if (obj is User user)
                    {
                        user.Client.Enqueue(ServerPackets.DisplayUser(client.User));
                        client.Enqueue(ServerPackets.DisplayUser(user));
                    }
                    else
                        itemMonster.Add(obj);
                }

                //if itemmonster isnt empty, send everything in it to us
                if (itemMonster.Count > 0)
                    client.Enqueue(ServerPackets.DisplayItemMonster(itemMonster.ToArray()));

                //if doors isnt empty, send everything in it to us
                if (doors.Count > 0)
                    client.Enqueue(ServerPackets.Door(doors.ToArray()));

                //check collisions with warps
                if (Warps.TryGetValue(client.User.Point, out Warp warp))
                    Game.Assert.Warp(client.User, warp);

                //check collisions with worldmaps
                if (WorldMaps.TryGetValue(client.User.Point, out WorldMap worldMap))
                {
                    RemoveObject(client.User, true);
                    client.Enqueue(ServerPackets.WorldMap(worldMap));
                }
            }
        }

        internal void MonsterWalk(Monster monster, Direction direction)
        {
            lock (Sync)
            {
                Point startPoint = monster.Point;
                Point nextPoint = startPoint.Offset(direction);

                //get all users before and after the location change
                var visibleBefore = ObjectsVisibleFrom(monster.Point).OfType<User>().ToList();
                monster.Location = (Id, nextPoint);
                var visibleAfter = ObjectsVisibleFrom(monster.Point).OfType<User>().ToList();

                //for all users that are going off screen, remove this monster from their screen
                foreach (User user in visibleBefore.Except(visibleAfter).ToList())
                {
                    user.Client.Enqueue(ServerPackets.RemoveObject(monster));
                    visibleBefore.Remove(user);
                }

                //for all users coming on screen, add this user to their screen
                foreach (User user in visibleAfter.Except(visibleBefore))
                    user.Client.Enqueue(ServerPackets.DisplayItemMonster(monster));

                //for everything that will see us walk, send the walk
                foreach (User user in visibleBefore)
                    user.Client.Enqueue(ServerPackets.CreatureWalk(monster.ID, startPoint, direction));
            }
        }

        /// <summary>
        /// Resends all the current information for the given user.
        /// </summary>
        /// <param name="user">The client to refresh.</param>
        internal void Refresh(Client client, bool byPassTimer = false)
        {
            if (client == null || (!byPassTimer && DateTime.UtcNow.Subtract(client.LastRefresh).TotalMilliseconds < CONSTANTS.REFRESH_DELAY_MS))
                return;
            else
                client.LastRefresh = DateTime.UtcNow;

            lock (Sync)
            {
                if (Warps.TryGetValue(client.User.Point, out Warp outWarp))
                    Game.Assert.Warp(client.User, outWarp);
                else if (WorldMaps.TryGetValue(client.User.Point, out WorldMap outWorldMap))
                {
                    RemoveObject(client.User, true);
                    client.Enqueue(ServerPackets.WorldMap(outWorldMap));
                }
                else
                {
                    client.Enqueue(ServerPackets.MapInfo(this));
                    client.Enqueue(ServerPackets.Location(client.User.Point));
                    client.SendAttributes(StatUpdateType.Full);
                    var itemMonsterToSend = new List<VisibleObject>();

                    //get all objects that would be visible to this object and send this user to them / send them to this user
                    foreach (VisibleObject obj in ObjectsVisibleFrom(client.User.Point))
                        if (obj is User user)
                        {
                            client.Enqueue(ServerPackets.DisplayUser(user));
                            user.Client.Enqueue(ServerPackets.DisplayUser(client.User));
                        }
                        else
                            itemMonsterToSend.Add(obj);

                    client.Enqueue(ServerPackets.DisplayItemMonster(itemMonsterToSend.ToArray()));
                    client.Enqueue(ServerPackets.Door(DoorsVisibleFrom(client.User.Point).ToArray()));
                    client.Enqueue(ServerPackets.MapLoadComplete());
                    client.Enqueue(ServerPackets.DisplayUser(client.User));
                    client.Enqueue(ServerPackets.RefreshResponse());
                }
            }
        }
        #endregion
    }
}
