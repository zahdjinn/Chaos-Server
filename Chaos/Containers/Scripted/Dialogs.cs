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
    /// <summary>
    /// Object containing all in-game dialogs.
    /// </summary>
    internal sealed class Dialogs
    {
        private readonly ImmutableDictionary<ushort, Dialog> DialogDic;

        internal Dialog this[ushort dialogId] => DialogDic[dialogId];

        internal Dialogs()
        {
            //these will use dialog id to get the dialog
            var DialogList = new List<Dialog>()
            {
                CloseDialog(PursuitIds.None),
                ItemOrMerchantMenuDialog(PursuitIds.None, 1, "What would you like to do?",
                    new DialogMenu(
                        new List<DialogMenuItem>()
                        {
                            new DialogMenuItem(2, "Teleport"),
                            new DialogMenuItem(3, "Summon User"),
                            new DialogMenuItem(4, "Summon All"),
                            new DialogMenuItem(5, "Kill User"),
                            new DialogMenuItem(0, "Revive Yourself", PursuitIds.ReviveSelf),
                            new DialogMenuItem(8, "Revive User"),
                            new DialogMenuItem(16, "Force Give"),
                        })),
                TextEntryDialog(PursuitIds.Teleport, 2,
                    "To where? \"mapid x y\" or \"characterName\"\r" +
                    "MAP LIST:\r" +
                    $@"{string.Join("\r", Game.World.Maps.Select(map => $@"{map.Key.ToString()}: {map.Value.Name}"))} ", 12, 0),
                TextEntryDialog(PursuitIds.SummonUser, 3, "Who would you like to summon?", 12, 0),
                ItemOrMerchantMenuDialog(PursuitIds.SummonAll, 4, "This will summon everyone, are you sure?",
                    new DialogMenu(
                        new List<DialogMenuItem>()
                        {
                            new DialogMenuItem(0, "Yes"),
                            new DialogMenuItem(ushort.MaxValue, "No")
                        })),
                TextEntryDialog(PursuitIds.KillUser, 5, "Who would you like to kill?", 12, 0),
                ItemOrMerchantMenuDialog(PursuitIds.LouresCitizenship, 6, "Would you like to be a citizen?",
                    new DialogMenu(
                        new List<DialogMenuItem>()
                        {
                            new DialogMenuItem(0, "Yes"),
                            new DialogMenuItem(ushort.MaxValue, "No")
                        })),
                NormalDialog(PursuitIds.None, 7, false, false, "Need a room, do you? Please, help yourself upstairs.", 0),
                TextEntryDialog(PursuitIds.ReviveUser, 8, "Who would you like to revive?", 12, 0),
                TextEntryDialog(PursuitIds.ForceGive, 16, "Give an Item Name, and User Name pair... \"Item Name:User Name\"", 30, 0),

                //Tutorial
                NormalDialog(PursuitIds.None, 9, false, true, "Hello! I can assist you in reallocating stats. This is a permanent process and others will be able to see that you have modified your soul. One Winiphira ticket is the cost.", 10),
                ItemOrMerchantMenuDialog(PursuitIds.None, 10, "Please select a stat to modify! This process costs one Winphira ticket.",
                    new DialogMenu(
                        new List<DialogMenuItem>()
                        {
                            new DialogMenuItem(11, "STR"),
                        })),
                 ItemOrMerchantMenuDialog(PursuitIds.None, 11, "Please select a stat to modify STR to..",
                    new DialogMenu(
                        new List<DialogMenuItem>()
                        {
                            new DialogMenuItem(12, "Int"),
                        })),
                 ItemOrMerchantMenuDialog(PursuitIds.None, 12, "All sales are FINAL! Do you accept?",
                    new DialogMenu(
                        new List<DialogMenuItem>()
                        {
                            new DialogMenuItem(18, "Yes."),
                            new DialogMenuItem(19, "No."),
                        })),
                 NormalDialog(PursuitIds.STRtoINT, 18, true, true, "It will be so.", 9),
                 NormalDialog(PursuitIds.None, 19, false, false, "Then leave. The door will be at your backside."),
            };

            foreach(Dialog d in DialogList)
                DialogList[d.NextDialogId].PreviousDialogId = d.Id;

            DialogDic = ImmutableDictionary.CreateRange(DialogList.ToDictionary(d => d.Id, d => d));
        }

        /// <summary>
        /// Creates a standard dialog with or without buttons
        /// </summary>
        /// <param name="nextDialogId">The dialog that comes after this one if you press next.</param>
        internal Dialog NormalDialog(PursuitIds pursuitId, ushort dialogId, bool prevBtn, bool nextBtn, string message, ushort nextDialogId = 0) =>
            new Dialog(DialogType.Normal, pursuitId, dialogId, prevBtn, nextBtn, message, null, nextDialogId, null, 0);

        /// <summary>
        /// Creates a dialog that has a menu on it that leads to other dialogs or effects
        /// </summary>
        /// <param name="menu">List of dialog options, and the dialog id they each lead to.</param>
        internal Dialog ItemOrMerchantMenuDialog(PursuitIds pursuitId, ushort dialogId, string message, DialogMenu menu) =>
            new Dialog(DialogType.ItemMenu, pursuitId, dialogId, false, false, message, menu, 0, null, 0);

        /// <summary>
        /// Creates a dialog that has a menu on it that leads to other dialogs or effects
        /// </summary>
        /// <param name="menu">List of dialog options, and the dialog id they each lead to.</param>
        internal Dialog UserMenuDialog(PursuitIds pursuitId, ushort dialogId, string message, DialogMenu menu) =>
            new Dialog(DialogType.CreatureMenu, pursuitId, dialogId, false, false, message, menu, 0, null, 0);

        /// <summary>
        /// Creates a dialog that allows text entry
        /// </summary>
        /// <param name="maxCharacters">Maximum number of input characters to accept.</param>
        /// <param name="nextDialogId">Dialog id that comes after this dialog.</param>
        internal Dialog TextEntryDialog(PursuitIds pursuitId, ushort dialogId, string message, ushort maxCharacters, ushort nextDialogId) =>
            new Dialog(DialogType.TextEntry, pursuitId, dialogId, false, false, message, null, nextDialogId, null, maxCharacters);

        /// <summary>
        /// Creates a dialog that ends the dialog sequence.
        /// </summary>
        /// <param name="pursuitId">The pursuit to activate as you end the sequence.</param>
        /// <returns></returns>
        internal Dialog CloseDialog(PursuitIds pursuitId = PursuitIds.None) =>
            new Dialog(DialogType.CloseDialog, pursuitId, 0, false, false, string.Empty, null, 0, null, 0);

        /// <summary>
        /// Returns the effect delegate for the given pursuit ID
        /// </summary>
        internal PursuitDelegate ActivatePursuit(PursuitIds pid) => Pursuits.Activate(pid);
    }
}