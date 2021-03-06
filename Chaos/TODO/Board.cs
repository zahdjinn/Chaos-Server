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

using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;

namespace Chaos
{
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class Board : IEnumerable<Post>
    {
        private readonly object Sync = new object();
        [JsonIgnore]
        private readonly List<Post> Messages;

        internal int Count => Messages.Count;
        internal Post this[byte postNum] => (postNum < Count) ? Messages[postNum] : default;

        public IEnumerator<Post> GetEnumerator()
        {
            lock (Sync)
                using (IEnumerator<Post> safeEnum = Messages.GetEnumerator())
                    while (safeEnum.MoveNext())
                        yield return safeEnum.Current;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Json & Master constructor for an enumerable object of Post. Represents a board, or list of posts.
        /// </summary>
        /// <param name="messages">A list of posts contained in the board.</param>
        [JsonConstructor]
        internal Board(List<Post> messages = null)
        {
            Messages = messages ?? new List<Post>();
        }

        /// <summary>
        /// Synchronously adds a post to the board.
        /// </summary>
        /// <param name="post">The post to add.</param>
        internal void AddPost(Post post)
        {
            lock (Sync)
                Messages[post.PostId] = post;
        }

        /// <summary>
        /// Synchronously removes a post from the board.
        /// </summary>
        /// <param name="PostNum">The index of the post to be removed.</param>
        internal bool RemovePost(int PostNum)
        {
            lock (Sync)
            {
                if (PostNum < Count)
                {
                    Messages.RemoveAt(PostNum);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Synchronously reverses the posts, so that the newest one is on the top, for use in the packet.
        /// </summary>
        internal IEnumerable<Post> Reverse()
        {
            lock (Sync)
                for (int i = Count - 1; i != 0; i--)
                    yield return Messages[i];
        }
    }
}
