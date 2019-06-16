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
using System.Linq;

namespace Chaos
{
    /// <summary>
    /// Enumerable object containing all in-game merchants.
    /// </summary>
    internal sealed class Merchants
    {
        private readonly Dictionary<string, Merchant> MerchantDic;
        //access merchant by name
        internal Merchant this[string name] => MerchantDic[name];
        internal List<Merchant> GetMerchants => MerchantDic.Values.ToList();

        internal Merchants()
        {
            var MerchantList = new List<Merchant>()
            {
                new Merchant("Deliope", (5031, 15, 10), 61, Direction.South, 0,
                    new MerchantMenu("Are you dead, scrub?", MenuType.Menu,
                        new PursuitMenu(
                            new List<PursuitMenuItem>()
                            {
                                new PursuitMenuItem(PursuitIds.ReviveSelf, "Revive")
                            }))),
                new Merchant("Celeste", (17500, 3, 16), 57, Direction.South, 0,
                    new MerchantMenu("I like giant cock.", MenuType.Dialog,
                        new DialogMenu(
                            new List<DialogMenuItem>()
                            {
                                new DialogMenuItem(0, "Revive Self", PursuitIds.ReviveSelf),
                                new DialogMenuItem(3, "Summon User"),
                                new DialogMenuItem(4, "Summon All"),
                                new DialogMenuItem(6, "Loures Citizenship"),
                                new DialogMenuItem(5, "Kill User"),
                                new DialogMenuItem(2, "Teleport")
                            }))),
                new Merchant("Frank The Great", (17501, 5, 2), 34, Direction.East, 7),
                new Merchant("Maribel", (18007, 2, 11), 447, Direction.East, 9),
                //new Merchant("Markus", 364, new Point(2, 4), Game.World.Maps[18002], Direction.East, 15),
                new Merchant("Am I Pretty?", (8984, 10, 8), 627, Direction.South, 0,
                    new MerchantMenu("Do it. Tell me I'm beautiful.", MenuType.Menu,
                        new PursuitMenu(
                            new List<PursuitMenuItem>()
                            {
                                new PursuitMenuItem(PursuitIds.BecomeWarrior, "Become a warrior"),
                                new PursuitMenuItem(PursuitIds.BecomeWizard, "Become a wizard"),
                                new PursuitMenuItem(PursuitIds.BecomePriest, "Become a priest"),
                                new PursuitMenuItem(PursuitIds.BecomeMonk, "Become a monk"),
                                new PursuitMenuItem(PursuitIds.BecomeRogue, "Become a rogue")
                            }))),
            };

            MerchantDic = MerchantList.ToDictionary(m => m.Name, m => m);
        }
    }
}
