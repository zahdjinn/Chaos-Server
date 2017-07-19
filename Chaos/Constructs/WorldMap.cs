﻿using System.Collections.Generic;
using System.IO;

namespace Chaos
{
    internal sealed class WorldMap
    {
        internal string Field { get; set; }
        internal List<WorldMapNode> Nodes { get; }

        internal WorldMap(string field, params WorldMapNode[] nodes)
        {
            Field = field;
            Nodes = new List<WorldMapNode>(nodes);
        }

        internal WorldMap()
        {

        }

        internal uint GetCrc32()
        {
            byte[] buffer;
            MemoryStream memoryStream = new MemoryStream();
            using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
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
                buffer = memoryStream.ToArray();
            }
            return CRC32.Calculate(buffer);
        }
    }
}
