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
using Newtonsoft.Json;
using System.Linq;
using System;

namespace Chaos
{
    public sealed class Map
    {
        private readonly object Sync = new object();
        private readonly Dictionary<int, WorldObject> Objects;
        private readonly List<Effect> Effects;

        public Point[,] Points { get; }
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
        public Map(ushort id, byte sizeX, byte sizeY, MapFlags flags, string name, sbyte music)
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
            Objects = new Dictionary<int, WorldObject>();
            Doors = new Dictionary<Point, Door>();
            Effects = new List<Effect>();
        }

        /// <summary>
        /// Json constructor for an object representing an in-game map. Only the Id is serialized. The map is then fetched from a pre-populated list from the world.
        /// </summary>
        /// <param name="id"></param>
        [JsonConstructor]
        internal Map(ushort id)
        {
            Id = id;
        }

        /// <summary>
        /// Loads the tile data from file for the map.
        /// </summary>
        /// <param name="path"></param>
        internal void LoadData(string path)
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
                        Points[x, y] = (x, y);
                        Tiles[Points[x, y]] = new Tile((ushort)(data[index++] | (data[index++] << 8)), (ushort)(data[index++] | (data[index++] << 8)), (ushort)(data[index++] | (data[index++] << 8)));

                        if (CONSTANTS.DOOR_SPRITES.Contains(Tiles[Points[x, y]].LeftForeground))
                            Doors[Points[x, y]] = new Door((Id, Points[x, y]), true, true);
                        else if (CONSTANTS.DOOR_SPRITES.Contains(Tiles[Points[x, y]].RightForeground))
                            Doors[Points[x, y]] = new Door((Id, Points[x, y]), true, false);
                    }
            }

            CheckSum = Crypto.Generate16(Data);
        }
        #endregion

        #region WorldObjects
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

                //get all objects that would be visible to this object and sort them
                foreach (VisibleObject obj in vObject.Map.ObjectsVisibleFrom(vObject))
                    if (obj is User aUser)
                        usersToSend.Add(aUser);
                    else
                        itemMonsterToSend.Add(obj);

                //if this object is a user
                if (vObject is User tUser)
                {
                    tUser.Client.Enqueue(ServerPackets.MapChangePending());     //send pending map change
                    tUser.Client.Enqueue(ServerPackets.MapInfo(tUser.Map));      //send map info
                    tUser.Client.Enqueue(ServerPackets.Location(tUser.Point));   //send location

                    foreach (User u2s in usersToSend)
                    {
                        tUser.Client.Enqueue(ServerPackets.DisplayUser(u2s));   //send it all the users
                        u2s.Client.Enqueue(ServerPackets.DisplayUser(tUser));   //send all the users this user as well
                    }

                    tUser.Client.Enqueue(ServerPackets.DisplayItemMonster(itemMonsterToSend.ToArray()));    //send it all the items, monsters, and merchants
                    tUser.Client.Enqueue(ServerPackets.Door(tUser.Map.DoorsVisibleFrom(tUser).ToArray()));     //send the user all nearby doors
                    tUser.Client.Enqueue(ServerPackets.MapChangeComplete());    //send it mapchangecomplete
                    tUser.Client.Enqueue(ServerPackets.MapLoadComplete());      //send it maploadcomplete
                    tUser.Client.Enqueue(ServerPackets.DisplayUser(tUser));      //send it itself

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
        internal void AddObjects(List<VisibleObject> vObjects, Point point)
        {
            if (vObjects.Count() == 0) return;

            lock (Sync)
            {
                //change location of each object and add each item to the map
                foreach (VisibleObject vObj in vObjects)
                {
                    vObj.Location = (Id, point);
                    Objects.Add(vObj.ID, vObj);
                }

                //send all the visible users these objects
                foreach (User user in ObjectsVisibleFrom(vObjects.First()).OfType<User>())
                    user.Client.Enqueue(ServerPackets.DisplayItemMonster(vObjects.ToArray()));
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
                foreach (User user in ObjectsVisibleFrom(vObject).OfType<User>())
                    user.Client.Enqueue(ServerPackets.RemoveObject(vObject));

                if (!skipRemove)
                    vObject.Location = Location.None;
            }
        }

        /// <summary>
        /// Synchronously retrieves all objects the given object can see.
        /// </summary>
        /// <param name="vObject">Object to base from.</param>
        /// <param name="include">Whether or not to include the base object.</param>
        /// <param name="distance">Optional distance from the object to retrieve from.</param>
        internal IEnumerable<VisibleObject> ObjectsVisibleFrom(VisibleObject vObject, bool include = false, byte distance = 13)
        {
            lock (Sync)
                foreach (VisibleObject visibleObject in Objects.Values.OfType<VisibleObject>().Where(obj => obj.Point.Distance(vObject.Point) <= distance && (include ? true : vObject != obj)))
                    yield return visibleObject;
        }

        /// <summary>
        /// Synchronously retrieves all objects visible from a given point.
        /// </summary>
        /// <param name="point">The point of origin.</param>
        /// <param name="include">Whether or not to include the origin point.</param>
        /// <param name="distance">Optional distance from the point to retreive from.</param>
        /// <returns></returns>
        internal IEnumerable<VisibleObject> ObjectsVisibleFrom(Point point, bool include = false, byte distance = 13)
        {
            lock (Sync)
                foreach (VisibleObject visibleObject in Objects.Values.OfType<VisibleObject>().Where(obj => obj.Point.Distance(point) <= distance && (include ? true : obj.Point != point)))
                    yield return visibleObject;
        }

        /// <summary>
        /// Attempts to synchronously retreive a user by searching through the objects for the given name.
        /// </summary>
        /// <param name="name">The name of the user to search for.</param>
        /// <param name="user">Reference to the user to set.</param>
        internal bool TryGet(string name, out User user)
        {
            user = null;

            lock (Sync)
                user = Objects.Values.FirstOrDefault(obj => obj.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)) as User;

            return user != null;
        }

        /// <summary>
        /// Attempts to synchronously retreive an object by searching through the objects for the given id.
        /// </summary>
        /// <param name="id">The id of the object to search for.</param>
        /// <param name="obj">Reference to the object to set.</param>
        internal bool TryGet<T>(int id, out T obj) where T : class
        {
            obj = null;

            lock (Sync)
                if (Objects.TryGetValue(id, out WorldObject wObj))
                    obj = wObj as T;

            return obj != null;
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

        /// <summary>
        /// Synchronously retrieves all effects visible from the creature.
        /// </summary>
        /// <param name="creature">The creature to base the search from.</param>
        internal IEnumerable<Effect> EffectsVisibleFrom(Creature creature)
        {
            lock (Sync)
                foreach (Effect eff in Effects.Where(e => e.Animation.TargetPoint.Distance(creature.Point) < 13))
                    yield return eff;
        }

        internal void ApplyPersistantEffects()
        {
            //lock the map
            lock (Sync)
            {
                foreach (Creature creature in GetAllObjects<Creature>()) //for each creature C on the map
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
                            foreach (User user in creature.Map.ObjectsVisibleFrom(creature, true).OfType<User>()) //for each user within sight, including itself if it is a user
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

                    foreach (Effect effect in EffectsVisibleFrom(creature).ToList())
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

        #region MapObjects
        /// <summary>
        /// Synchronously retrieves all doors visible from the user.
        /// </summary>
        /// <param name="user">The user to base from.</param>
        internal IEnumerable<Door> DoorsVisibleFrom(User user)
        {
            lock (Sync)
                foreach (Door door in Doors.Values.Where(door => user.WithinRange(door.Point)))
                    yield return door;
        }

        internal void ToggleDoor(Door door)
        {
            lock (Sync)
            {
                var doors = new List<Door>() { door };

                //for each surrounding point from the door
                foreach (Point p in Targeting.GetCardinalPoints(door.Point))
                    //if it's also a door
                    if (TryGet(p, out Door tDoor))
                    {
                        //add it
                        doors.Add(tDoor);

                        //if this 2nd door has another door 1 space in the same direction, we can break.
                        if (TryGet(p.Offset(p.Relation(p)), out Door eDoor))
                        {
                            //add that door as well
                            doors.Add(eDoor);
                            break;
                        }
                    }

                foreach (Door d in doors)
                    if (d.Toggle())
                        foreach (User user in ObjectsVisibleFrom(d.Point).OfType<User>())
                            user.Client.Enqueue(ServerPackets.Door(d));
            }
        }
        #endregion

        #region All Objects
        /// <summary>
        /// Attempts to synchronously retreive an object by searching through the maps for the given point.
        /// </summary>
        /// <param name="p">The point of the object to search for.</param>
        /// <param name="obj">Reference to the object to set.</param>
        internal bool TryGet<T>(Point p, out T obj) where T : class
        {
            lock (Sync)
            {
                Type tType = typeof(T);

                obj = VisibleObject.TypeRef.IsAssignableFrom(tType) ? Objects.Values.OfType<VisibleObject>().FirstOrDefault(tObj => tObj.Point == p) as T
                    : Door.TypeRef.IsAssignableFrom(tType) ? (Doors.TryGetValue(p, out Door outDoor) ? outDoor as T : null)
                    : Warp.TypeRef.IsAssignableFrom(tType) ? (Warps.TryGetValue(p, out Warp outWarp) ? outWarp as T : null)
                    : WorldMap.TypeRef.IsAssignableFrom(tType) ? (WorldMaps.TryGetValue(p, out WorldMap outWorldMap) ? outWorldMap as T : null)
                    : Effect.TypeRef.IsAssignableFrom(tType) ? Effects.FirstOrDefault(e => e.Animation.TargetPoint == p) as T 
                    : null;
            }

            return obj != null;
        }

        /// <summary>
        /// Attempts to synchronously retreive all objects of a given type on a given map.
        /// Only use on guaranteed safe code, or where performance is critical.
        /// </summary>
        /// <typeparam name="T">The type of object to return.</typeparam>
        /// <param name="map">The map to return from.</param>
        internal IEnumerable<T> GetAllObjects<T>()
        {
            lock (Sync)
            {
                Type tType = typeof(T);
                IEnumerable<T> enumerable;

                if (VisibleObject.TypeRef.IsAssignableFrom(tType))
                    enumerable = Objects.Values.OfType<T>();
                else if (Door.TypeRef.IsAssignableFrom(tType))
                    enumerable = Doors.Values.OfType<T>();
                else if (Warp.TypeRef.IsAssignableFrom(tType))
                    enumerable = Warps.Values.OfType<T>();
                else if (WorldMap.TypeRef.IsAssignableFrom(tType))
                    enumerable = WorldMaps.Values.OfType<T>();
                else if (Effect.TypeRef.IsAssignableFrom(tType))
                    enumerable = Effects.OfType<T>();
                else
                    yield break;

                foreach (T tObj in enumerable)
                    yield return tObj;
            }
        }

        internal IEnumerable<T> ObjectsVisibleFrom<T>(Point point, bool include = false, int distance = 13)
        {
            lock (Sync)
            {
                Type tType = typeof(T);
                IEnumerable<T> enumerable;

                if (VisibleObject.TypeRef.IsAssignableFrom(tType))
                    enumerable = Objects.Values.OfType<VisibleObject>().Where(obj => obj.Point.Distance(point) < distance).OfType<T>();
                else if (Door.TypeRef.IsAssignableFrom(tType))
                    enumerable = Doors.Values.OfType<T>();
                else if (Warp.TypeRef.IsAssignableFrom(tType))
                    enumerable = Warps.Values.OfType<T>();
                else if (WorldMap.TypeRef.IsAssignableFrom(tType))
                    enumerable = WorldMaps.Values.OfType<T>();
                else if (Effect.TypeRef.IsAssignableFrom(tType))
                    enumerable = Effects.OfType<T>();
                else
                    yield break;

                foreach (T tObj in enumerable)
                    yield return tObj;
            }
        }

        /// <summary>
        /// Attempts to synchronously retreive all objects of a given type on a given map. Bases off an instanced list.
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
                    enumerable = Objects.Values.ToList().OfType<T>();
                else if (Door.TypeRef.IsAssignableFrom(tType))
                    enumerable = Doors.Values.ToList().OfType<T>();
                else if (Warp.TypeRef.IsAssignableFrom(tType))
                    enumerable = Warps.Values.ToList().OfType<T>();
                else if (WorldMap.TypeRef.IsAssignableFrom(tType))
                    enumerable = WorldMaps.Values.ToList().OfType<T>();
                else if (Effect.TypeRef.IsAssignableFrom(tType))
                    enumerable = Effects.ToList().OfType<T>();
                else
                    yield break;

                foreach (T tObj in enumerable)
                    yield return tObj;
            }
        }
        #endregion

        #region Walking
        /// <summary>
        /// Checks if a point is within the bounds of the map.
        /// </summary>
        internal bool WithinMap(Point p) => p.X >= 0 && p.Y >= 0 && p.X < SizeX && p.Y < SizeY;

        /// <summary>
        /// Checks if a point is within the bounds of the map, or is a wall.
        /// </summary>
        internal bool IsWall(Point p) => !WithinMap(p) || (Doors.Keys.Contains(p) ? Doors[p].Closed : Tiles[p].IsWall);

        /// <summary>
        /// Checks if a given point is within the bounds of the map, is a wall, or has a monster, door, or other object already on it.
        /// </summary>
        internal bool IsWalkable(Point p)
        {
            lock (Sync)
                return !IsWall(p) && !Objects.Values.OfType<Creature>().Any(creature => creature.Type != CreatureType.WalkThrough && creature.Point == p);
        }

        /// <summary>
        /// Synchronously moves a character in a given direction, handling on-screen information and walking packets.
        /// </summary>
        /// <param name="client">The client who's user is walking.</param>
        /// <param name="direction">The direction to walk.</param>
        internal void Walk(Client client, Direction direction)
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

                var visibleBefore = ObjectsVisibleFrom(client.User).ToList();
                var doorsBefore = DoorsVisibleFrom(client.User).ToList();
                client.User.Location = (Id, startPoint.Offset(direction));
                var visibleAfter = ObjectsVisibleFrom(client.User).ToList();
                var itemMonster = new List<VisibleObject>().ToList();
                var doorsAfter = DoorsVisibleFrom(client.User).ToList();
                var doors = doorsAfter.Except(doorsBefore).ToList();

                //send ourselves the walk
                client.Enqueue(ServerPackets.ClientWalk(direction, client.User.Point));

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
                if (TryGet(client.User.Point, out Warp warp))
                    Game.Assert.Warp(client.User, warp);

                //check collisions with worldmaps
                if (TryGet(client.User.Point, out WorldMap worldMap))
                {
                    RemoveObject(client.User, true);
                    client.Enqueue(ServerPackets.WorldMap(worldMap));
                }
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
                    foreach (VisibleObject obj in ObjectsVisibleFrom(client.User))
                        if (obj is User user)
                        {
                            client.Enqueue(ServerPackets.DisplayUser(user));
                            user.Client.Enqueue(ServerPackets.DisplayUser(client.User));
                        }
                        else
                            itemMonsterToSend.Add(obj);

                    client.Enqueue(ServerPackets.DisplayItemMonster(itemMonsterToSend.ToArray()));
                    client.Enqueue(ServerPackets.Door(DoorsVisibleFrom(client.User).ToArray()));
                    client.Enqueue(ServerPackets.MapLoadComplete());
                    client.Enqueue(ServerPackets.DisplayUser(client.User));
                    client.Enqueue(ServerPackets.RefreshResponse());
                }
            }
        }
        #endregion
    }
}
