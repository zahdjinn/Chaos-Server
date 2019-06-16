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

using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Chaos
{
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class Panel<T> : IEnumerable<T> where T : PanelObject
    {
        private readonly object Sync = new object();

        [JsonProperty]
        private readonly PanelType Type;
        [JsonProperty]
        private readonly T[] Objects;
        private readonly byte[] Invalid;
        private readonly byte Length;
        private readonly int TotalSlots;

        internal T this[EquipmentSlot slot] => this[(byte)slot];
        internal T this[byte slot] => TryGetObject(slot, out T outObj) ? outObj : null;
        internal T this[string name] => this.FirstOrDefault(obj => obj.Name == name);

        /// <summary>
        /// Synchronously checks of the panel is full.
        /// </summary>
        internal int AvailableSlots => TotalSlots - this.Count();

        public IEnumerator<T> GetEnumerator()
        {
            lock (Sync)
                using (IEnumerator<T> safeEnum = Objects.GetEnumerable().GetEnumerator())
                    while (safeEnum.MoveNext())
                        if (safeEnum.Current != null && Valid(safeEnum.Current.Slot))
                            yield return safeEnum.Current;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Base constructor for an object representing an in-game panel. Skill/Spell/Inventory/Equipment
        /// </summary>
        /// <param name="length">The number of objects that can fit in the panel.</param>
        internal Panel(PanelType type)
        {
            Type = type;

            switch(type)
            {
                case PanelType.SkillBook:
                case PanelType.SpellBook:
                    Length = 89;
                    Invalid = new byte[] { 36, 72 };
                    break;
                case PanelType.Inventory:
                    Length = 59;
                    Invalid = new byte[] { };
                    break;
                case PanelType.Equipment:
                    Length = 18;
                    Invalid = new byte[] { };
                    break;
            }


            Objects = new T[Length + 1];

            for (int i = 1; i <= Length; i++) //create slots +1 (so we can index without 0-based)
                if (Valid((byte)i))
                    Objects[i] = null;

            TotalSlots = Objects.Count(obj => obj == null);
        }

        /// <summary>
        /// Json & Master constructor for an object representing an in-game panel.
        /// </summary>
        [JsonConstructor]
        private Panel(PanelType type, T[] objects)
        {
            Type = type;

            switch (type)
            {
                case PanelType.SkillBook:
                case PanelType.SpellBook:
                    Length = 89;
                    Invalid = new byte[] { 36, 72 };
                    break;
                case PanelType.Inventory:
                    Length = 59;
                    Invalid = new byte[] { };
                    break;
                case PanelType.Equipment:
                    Length = 18;
                    Invalid = new byte[] { };
                    break;
            }

            Objects = objects;
            TotalSlots = Objects.Count(obj => obj == null);
        }

        /// <summary>
        /// Checks whether the given slot is valid.
        /// </summary>
        /// <param name="slot"></param>
        private bool Valid(byte slot) => slot > 0 && !Invalid.Contains(slot) && slot <= Length;

        /// <summary>
        /// Synchronously checks if the panel contains an object.
        /// </summary>
        /// <param name="obj">Object to check if the panel contains</param>
        internal bool Contains(T obj)
        {
            lock (Sync)
                return Objects[obj.Slot].Equals(obj);
        }

        internal bool Contains(string key)
        {
            lock (Sync)
                return Objects.Any(m => m.Name.Contains(key));
        }

        /// <summary>
        /// Attempts to synchronously add a stackable item.
        /// </summary>
        /// <param name="item">Object to try adding.</param>
        internal bool TryAddStack(T item)
        {
            lock (Sync)
            {
                if (Type == PanelType.Inventory && item is Item tItem && tItem.Stackable)
                {
                    //get an existing item
                    //if it exists, increase the count
                    if (Objects.FirstOrDefault(obj => tItem.Equals(obj)) is Item existingItem)
                    {
                        if (tItem.Count + existingItem.Count > CONSTANTS.ITEM_STACK_MAX)
                            return false;

                        tItem.Slot = existingItem.Slot;
                        tItem.Count += existingItem.Count;
                        tItem.LastUse = existingItem.LastUse;
                        Objects[existingItem.Slot] = item;
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Attempts to synchronously equip the given item. Sends an item out if an item was replaced in the slot.
        /// </summary>
        /// <param name="item">The item to try equiping.</param>
        /// <param name="outItem">The object you were previously wearing in that slot.</param>
        internal bool TryEquip(T item, out T outItem)
        {
            lock (Sync)
            {
                outItem = null;

                if (Type == PanelType.Equipment && item is Item tItem)
                {
                    //get the items desired slot
                    byte slot = (byte)tItem.EquipmentSlot;

                    //the only failure path is if the slot is occupied and fails to get/remove, otherwise success
                    if (!(Objects[slot] != null && !TryGetRemove((byte)tItem.EquipmentSlot, out outItem)))
                    {
                        tItem.Slot = slot;
                        return TryAdd(item);
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Attempts to synchronously add an object.
        /// </summary>
        /// <param name="obj">Object to add.</param>
        internal bool TryAdd(T obj)
        {
            lock (Sync)
            {
                //if adding as a stack fails
                if (!TryAddStack(obj)) //try adding normally
                    if(Valid(obj.Slot) && Objects[obj.Slot] == null)
                        Objects[obj.Slot] = obj;

                //return true if successfully placed
                return Objects[obj.Slot] == obj;
            }
        }

        /// <summary>
        /// Attempts to synchronously add an object to the next available slot. Handles stackable items.
        /// </summary>
        /// <param name="obj">Object to add</param>
        internal bool AddToNextSlot(T obj)
        {
            lock (Sync)
            {
                byte slot = obj.Slot;
                //get the first valid & empty slot
                for (byte i = 1; i <= Length; i++)
                    if (Objects[i] == null && Valid(i))
                    {
                        obj.Slot = i;
                        break;
                    }

                //try to add it as a stackable, then normally
                if (TryAdd(obj))
                    return true;
                else
                    obj.Slot = slot;

                return false;
                
            }
        }

        /// <summary>
        /// Attempts to synchronously remove an object from the panel. Sets the value to null.
        /// </summary>
        /// <param name="slot">Slot to remove.</param>
        internal bool TryRemove(byte slot)
        {
            lock (Sync)
            {
                if (Valid(slot))
                    Objects[slot] = null;

                return Objects[slot] == null;
            }
        }

        /// <summary>
        /// Attempts to synchronously return a reference to an existing object.
        /// </summary>
        /// <param name="slot">Slot to retreive from.</param>
        /// <param name="obj">Obj reference to set.</param>
        internal bool TryGetObject(byte slot, out T obj)
        {
            lock (Sync)
            {
                if (!Valid(slot))
                {
                    obj = null;
                    return false;
                }

                obj = Objects[slot];
                return Valid(slot);
            }
        }

        /// <summary>
        /// Attempts to synchronously remove an object and return it.
        /// </summary>
        /// <param name="slot">Slot to remove.</param>
        /// <param name="obj">Return object if successful.</param>
        internal bool TryGetRemove(byte slot, out T obj)
        {
            lock (Sync)
                return TryGetObject(slot, out obj) && TryRemove(slot);
        }

        /// <summary>
        /// Attempts to synchronously swap two items slots.
        /// If it fails, items will be put back.
        /// </summary>
        /// <param name="slot1">First slot to swap.</param>
        /// <param name="slot2">Second slot to swap.</param>
        internal bool TrySwap(byte slot1, byte slot2)
        {
            //if either slot is invalid, false
            if (!Valid(slot1) || !Valid(slot2))
                return false;

            lock (Sync)
            {
                T obj1 = Objects[slot1];
                T obj2 = Objects[slot2];

                if (obj1 != null)
                    obj1.Slot = slot2;
                if (obj2 != null)
                    obj2.Slot = slot1;

                Objects[slot1] = obj2;
                Objects[slot2] = obj1;

                return true;
            }
        }
    }
}
