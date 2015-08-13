﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Manager;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Bot
{
    static class Program
    {
        private static Request Request = new Request();

        static void Main(string[] args)
        {
            const string url = "http://osu.ppy.sh/forum/ucp.php?mode=login";
            var wr = Request.Create(url, true);
            if (string.IsNullOrEmpty(Settings.Session))
            {
                using (var sw = new StreamWriter(wr.GetRequestStream()))
                {
                    sw.Write(string.Format("login=login&username={0}&password={1}&autologin=on",
                        Uri.EscapeDataString(Settings.OsuId), Uri.EscapeDataString(Settings.OsuPw)));
                }
            }
            else
            {
                Request.AddCookie(Settings.SessionKey, Settings.Session);
            }
            using (var rp = (HttpWebResponse) wr.GetResponse())
            {
                if (rp.Cookies["last_login"] == null)
                {
                    Settings.Session = "";
                    Console.WriteLine("login failed");
                    Main(args);
                    return;
                }
            }
            Settings.Session = Request.GetCookie(Settings.SessionKey);

            if (args.Length > 0)
            {
                if (args[0] == "/?")
                {
                    Console.WriteLine();
                    Console.WriteLine(Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location) + " (SetID[l][s])*");
                    Console.WriteLine();
                    Console.WriteLine("\tl\t내려받기를 건너뛰고 로컬에 있는 맵셋 파일 사용");
                    Console.WriteLine("\ts\tsynced 열을 유지하면서 데이터베이스 갱신");
                    Console.WriteLine();
                    return;
                }

                foreach (Match arg in Regex.Matches(string.Join(" ", args), @"(\d+)([^\s]*)"))
                {
                    var a = false;
                    var b = false;
                    foreach (var ch in arg.Groups[2].Value)
                    {
                        switch (ch)
                        {
                            case 'l':
                            a = true;
                            break;
                            case 's':
                            b = true;
                            break;
                        }
                    }
                    DateTime lastUpdate;
                    Sync(Set.GetByAPI(Convert.ToInt32(arg.Groups[1].Value), out lastUpdate), a, b);
                }
                return;
            }
        }

        private static void Sync(Set set, bool skipDownload = false, bool keepSyncedTime = false)
        {
            Log.Flag = set.ID;

            var started = DateTime.Now;
            try
            {
                if (!skipDownload)
                {
                    Download(set.ID, out started);
                }

                var local = Set.GetByLocal(set.ID);
                set.Title = local.Title;
                set.TitleUnicode = local.TitleUnicode;
                set.Artist = local.Artist;
                set.ArtistUnicode = local.ArtistUnicode;
                set.Creator = local.Creator;
                set.Tags = local.Tags;
                foreach (var i in set.Beatmaps)
                {
                    var k = local.Beatmaps.Find(j => j.Name == i.Name);
                    if (k != null)
                    {
                        i.Mode = k.Mode;
                        i.HPDrainRate = k.HPDrainRate;
                        i.CircleSize = k.CircleSize;
                        i.OverallDifficulty = k.OverallDifficulty;
                        i.ApproachRate = k.ApproachRate;
                        i.BPM = k.BPM;
                        i.Length = k.Length;
                    }
                    else
                    {
                        Log.WriteLine("missing beatmap: " + i.ID + " " + i.Name);
                    }
                }

                Register(set, keepSyncedTime ? new DateTime(0) : started);
            }
            catch (Exception e)
            {
                Log.WriteLine("error occured\r\n" + e.GetBaseException());
                Settings.LastCheckTime = Settings.LastCheckTime;
            }
        }
    }
}
