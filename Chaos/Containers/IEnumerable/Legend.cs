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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Chaos
{
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class Legend : IEnumerable<LegendMark>
    {
        private readonly object Sync = new object();

        [JsonProperty]
        private readonly List<LegendMark> Marks;

        internal byte Count => (byte)Marks.Count;

        public IEnumerator<LegendMark> GetEnumerator()
        {
            lock (Sync)
                using (IEnumerator<LegendMark> safeEnum = Marks.GetEnumerator())
                    while (safeEnum.MoveNext())
                        yield return safeEnum.Current;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Default constructor for an enumerable object of LegendMark. Represent's a new user's legend.
        /// </summary>
        internal Legend()
        {
            Marks = new List<LegendMark>();
        }

        /// <summary>
        /// Json & Master constructor for an enumerable object of LegendMark. Represents an existing user's legend.
        /// </summary>
        [JsonConstructor]
        internal Legend(List<LegendMark> marks)
        {
            Marks = marks;
        }

        /// <summary>
        /// Synchronously retreives the legend mark at key location.
        /// </summary>
        /// <param name="key">Key of the legend mark you want returned.</param>
        internal LegendMark this[string key]
        {
            get
            {
                lock (Sync)
                    return Marks.FirstOrDefault(mark => mark.Key.Equals(key, StringComparison.CurrentCultureIgnoreCase));
            }
        }

        /// <summary>
        /// Synchronously adds or replaces an old legend mark at the mark's key location.
        /// </summary>
        /// <param name="mark">Mark to add or replace.</param>
        internal void Add(LegendMark mark)
        {
            lock (Sync)
            {
                LegendMark existingMark = this[mark.Key];

                if (existingMark != null)
                {
                    existingMark.Added = GameTime.Now;
                    existingMark.Count++;
                }
                else
                    Marks.Add(mark);
            }
        }
        /// <summary>
        /// Attempts to synchronously remove the legend mark at key location.
        /// </summary>
        /// <param name="key">Key of the mark to remove.</param>
        internal bool TryRemove(string key)
        {
            lock (Sync)
                return Marks.RemoveAll(m => m.Key.Equals(key, StringComparison.CurrentCultureIgnoreCase)) != 0;
        }

        /// <summary>
        /// Synchronously retreives the legend mark at key location.
        /// </summary>
        /// <param name="key">Key of the mark to check exists.</param>
        /// <returns>Legend Mark Key Exist</returns>
        internal bool Contains(string key)
        {
            lock (Sync)
                return Marks.Any(m => m.Key.Contains(key));
        }
    }
}
