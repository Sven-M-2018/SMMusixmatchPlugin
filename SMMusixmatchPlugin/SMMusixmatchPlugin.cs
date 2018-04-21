using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using ImPluginEngine.Abstractions;
using ImPluginEngine.Abstractions.Entities;
using ImPluginEngine.Abstractions.Interfaces;
using ImPluginEngine.Helpers;
using ImPluginEngine.Types;

namespace SMMusixmatchPlugin
{
    public class SMMusixmatchPlugin : IPlugin, ILyrics
    {
        public string Name => "SMMusixmatch";
        public string Version => "1.0.0";

        public async Task GetLyrics(PluginLyricsInput input, CancellationToken ct, Action<PluginLyricsResult> updateAction)
        {
            String url = string.Format("https://www.musixmatch.com/search/{0}+{1}/tracks", HttpUtility.UrlEncode(input.Artist), HttpUtility.UrlEncode(input.Title));
            var client = new HttpClient();
            String web = string.Empty;
            try
            {
                var response = await client.GetAsync(url, ct);
                var data = await response.Content.ReadAsByteArrayAsync();
                web = Encoding.UTF8.GetString(data);
            }
            catch (HttpRequestException)
            {
                return;
            }
            Regex SearchRegex = new Regex(@"<a class=""title"" href=""(?'url'[^""]+)""><span>(?'title'[^<]+)</span></a></h2><h3 class=""media-card-subtitle""><span class=""artist-field""><span><a class=""artist"" href=""[^""]+"">(?'artist'[^<]+)</a></span></span></h3></div></div><meta content=", RegexOptions.Compiled);
            MatchCollection matches = SearchRegex.Matches(web);
            foreach (Match match in matches)
            {
                var result = new PluginLyricsResult();
                result.Artist = match.Groups["artist"].Value;
                result.Title = match.Groups["title"].Value;
                result.FoundByPlugin = string.Format("{0} v{1}", Name, Version);
                result.Lyrics = await DownloadLyrics(string.Format("https://www.musixmatch.com{0}", match.Groups["url"].Value), ct);
                if (result.Lyrics.StartsWith(string.Format("<p>{0}</p>\n<p>{1}</p>\n", input.Artist, input.Title)))
                {
                    result.Lyrics = result.Lyrics.Remove(3, string.Format("{0}</p>\n<p>{1}</p>\n", input.Artist, input.Title).Length);
                }
                else if (result.Lyrics.StartsWith(string.Format("<p>{0}</p>\n<p>{1}</p>\n", input.Title, input.Artist)))
                {
                    result.Lyrics = result.Lyrics.Remove(3, string.Format("<p>{0}</p>\n<p>{1}</p>\n", input.Title, input.Artist).Length);
                }
                updateAction(result);
            }
        }
        private async Task<String> DownloadLyrics(String url, CancellationToken ct)
        {
            var client = new HttpClient();
            string lyrics = string.Empty;
            string web;
            try
            {
                var response = await client.GetAsync(url, ct);
                var data = await response.Content.ReadAsByteArrayAsync();
                web = Encoding.UTF8.GetString(data);
            }
            catch (HttpRequestException)
            {
                return lyrics;
            }
            if (web.Contains(@"<h2 class=""mxm-empty__title"">Instrumental</h2>"))
            {
                return "<p>[Instrumental]</p>\n<p><i><sub>powered by Musixmatch</sub></i></p>";
            }
            Regex LyricsRegex = new Regex(@"<p class=""mxm-lyrics__content "">(?'lyrics'.*)<div></div><div><div id="""" class=""lyrics-report", RegexOptions.Compiled);
            var match = LyricsRegex.Match(web.Replace("\r\n", "<br/>").Replace("\r", "<br/>").Replace("\n", "<br/>"));
            if (match.Success)
            {
                lyrics = CleanLyrics(match.Groups["lyrics"].Value);
            }
            return lyrics;
        }

        private static string CleanLyrics(String lyrics)
        {
            lyrics = Regex.Replace(lyrics, @"</p><div>.*<p class=""mxm-lyrics__content "">", "<br/>", RegexOptions.IgnoreCase);
            lyrics = lyrics.Replace("</p>", "").Replace("</div>", "").Replace("</span>", "");
            lyrics = Regex.Replace(lyrics, @"<a href=[^>]+>", "", RegexOptions.IgnoreCase);
            lyrics = lyrics.Replace("</a>", "");
            lyrics = lyrics.Replace("<br/><br/>", "</p>\n<p>").Replace("<br/>", "<br/>\n");
            lyrics = lyrics.Replace("´", "'").Replace("`", "'").Replace("’", "'").Replace("‘", "'");
            lyrics = lyrics.Replace("…", "...").Replace(" ...", "...");
            lyrics = lyrics.Replace("<p><br/>\n", "<p>\n");
            lyrics = Regex.Replace(lyrics, @"\s+<br/>", "<br/>", RegexOptions.IgnoreCase);
            lyrics = Regex.Replace(lyrics, @"\s+<p/>", "<p/>", RegexOptions.IgnoreCase);
            return "<p>" + lyrics.Trim() + "</p>\n<p><i><sub>powered by Musixmatch</sub></i></p>";
        }
    }
}