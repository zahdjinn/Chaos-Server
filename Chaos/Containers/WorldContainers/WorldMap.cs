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
using System.IO;
using System;

namespace Chaos
{
    public sealed class WorldMap
    {
        internal static readonly Type TypeRef = typeof(WorldMap);

        public List<WorldMapNode> Nodes { get; }
        public string Field { get; private set; }


        /// <summary>
        /// Base constructor for an object representing the field, or world map.
        /// </summary>
        /// <param name="field">The name of the field, or world map.</param>
        /// <param name="nodes">A lost of nodes, each containing a destination mapID & point pair, and a point on the map for it to be displayed.</param>
        public WorldMap(string field, params WorldMapNode[] nodes)
        {
            Field = field;
            Nodes = nodes.ToList();
        }

        /// <summary>
        /// Gets the checksum of the worldmap, based on it's nodes.
        /// </summary>
        /// <returns></returns>
        public uint CheckSum
        {
            get
            {
                var memoryStream = new MemoryStream();
                using (var binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write((byte)Nodes.Count);
                    foreach (WorldMapNode worldMapNode in Nodes)
                    {
                        binaryWriter.Write(worldMapNode.Position.X);
                        binaryWriter.Write(worldMapNode.Position.Y);
                        binaryWriter.Write(worldMapNode.Name);
                        binaryWriter.Write(worldMapNode.MapId);
                    }
                    binaryWriter.Flush();
                    return Crypto.Generate32(memoryStream.ToArray());
                }
            }
        }
    }
}
