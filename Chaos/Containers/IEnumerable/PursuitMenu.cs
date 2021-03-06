﻿// ****************************************************************************
// This file belongs to the Chaos-Server project.
// 
// This project is free and open-source, provided that any alterations or
// modifications to any portions of this project adhere to the
// Affero General Public License (Version 3).
// 
// A copy of the AGPLv3 can be found in the project directory.
// You may also find a copy at <https://www.gnu.org/licenses/agpl-3.0.html>
// ****************************************************************************

using System.Collections;
using System.Collections.Generic;

namespace Chaos
{
    internal sealed class PursuitMenu : IEnumerable<PursuitMenuItem>
    {
        private readonly List<PursuitMenuItem> Pursuits;

        internal PursuitMenuItem this[int index] => Pursuits[index];
        internal int Count => Pursuits.Count;

        public IEnumerator<PursuitMenuItem> GetEnumerator() => Pursuits.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Base constructor for an enumerable object of PursuitMenuItem. Represents the pursuit menu of a merchant menu.
        /// </summary>
        /// <param name="pursuits">A list of objects that each contain a pursuitId paired with it's display text for the menu.</param>
        internal PursuitMenu(List<PursuitMenuItem> pursuits)
        {
            Pursuits = pursuits;
        }
    }
}
