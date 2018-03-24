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

using System;
using System.CodeDom;
using System.Configuration;
using System.IO;
using System.Text;

namespace Chaos
{
    public static class Paths
    {
        //Get Current Directoy 
        private static readonly string SolutionDir = Directory.GetCurrentDirectory();
        //Path of settings.txt
        public static string Path = $@"{SolutionDir}\settings.txt";
        //Read settings.txt
        public static string[] ReadSettings = File.ReadAllLines(Path, Encoding.UTF8);
        //primary directory, change to your own~
        public static string BaseDir = ReadSettings[0];
        //dark ages directory
        public static string DarkAgesDir = ReadSettings[1];
        //dark ages executable
        public static string DarkAgesExe => $@"{DarkAgesDir}Darkages.exe";
        //dynamic host name, change to your own~
        public static string HostName = ReadSettings[2];
        //redis config string (host, port)
        public static string RedisConfig = ReadSettings[3];

        public static string LogFiles => $@"{BaseDir}logs\";
        public static string MetaFiles => $@"{BaseDir}metafiles\";
        public static string MapFiles => $@"{BaseDir}maps\";


    }
}
