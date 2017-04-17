using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using ICSharpCode.SharpZipLib.BZip2;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Alpacka.Meta.AddOnService;

namespace Alpacka.Meta
{
    public class ProjectFeed
    {
        private static readonly string COMPLETE_URL = "http://clientupdate-v6.cursecdn.com/feed/addons/432/v10/complete.json.bz2"; 
        private static readonly string COMPLETE_URL_TIMESTAMP = "http://clientupdate-v6.cursecdn.com/feed/addons/432/v10/complete.json.bz2.txt";
        private static readonly string HOURLY_URL = "http://clientupdate-v6.cursecdn.com/feed/addons/432/v10/hourly.json.bz2"; 
        private static readonly string HOURLY_URL_TIMESTAMP = "http://clientupdate-v6.cursecdn.com/feed/addons/432/v10/hourly.json.bz2.txt";
        private static readonly JsonSerializerSettings settings = new JsonSerializerSettings {
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Error,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            // NullValueHandling = NullValueHandling.Ignore,
        };
        private static readonly string cache = Path.Combine(Constants.CachePath, "curse");
        private static HttpClient client = new HttpClient();
        
        public static async Task<ProjectList> GetComplete(bool filter = true)
        {
            var completeFile = Path.Combine(cache, "complete.json");
            var completeFileTimestamp = Path.Combine(cache, "complete.txt");
            // read timestamp
            var timestamp = await client.GetStringAsync(COMPLETE_URL_TIMESTAMP);
            String uncompressedString = null;
            if (File.Exists(completeFileTimestamp) && File.Exists(completeFile)) {
                var localTimestamp = File.ReadAllText(completeFileTimestamp);
                if (localTimestamp == timestamp) // if complete.json exists, read it
                    uncompressedString = File.ReadAllText(completeFile);
            }
            
            // download and decompress
            if (uncompressedString == null) {
                uncompressedString = await uncompress(COMPLETE_URL, completeFile, "complete");
            }
            
            var allProjects = JsonConvert.DeserializeObject<ProjectList>(uncompressedString, settings);
            
            if(filter) {
                Console.WriteLine($"Filtering projects, please wait... old count: {allProjects.Data.Count()}");
                allProjects.Data = allProjects.Data.Where(a => a.PackageType == PackageTypes.Mod).ToList();
                Console.WriteLine($"Finished filtering projects, new count: {allProjects.Data.Count()}");
            }
            
            Directory.CreateDirectory(Path.GetDirectoryName(completeFileTimestamp));
            File.WriteAllText(completeFileTimestamp, allProjects.Timestamp.ToString());
            
            return allProjects;
        }
        
        
        public static async Task<ProjectList> GetHourly(bool filter = true)
        {
            var hourlyFile = Path.Combine(cache, "hourly.json");
            var hourlyFileTimestamp = Path.Combine(cache, "hourly.txt");
            // read timestamp
            var timestamp = await client.GetStringAsync(HOURLY_URL_TIMESTAMP);
            String uncompressedString = null;
            if (File.Exists(hourlyFileTimestamp) && File.Exists(hourlyFile)) {
                var localTimestamp = File.ReadAllText(hourlyFileTimestamp);
                if (localTimestamp == timestamp) // if complete.json exists, read it
                    uncompressedString = File.ReadAllText(hourlyFile);
            }
            
            // download and decompress
            if (uncompressedString == null) {
                uncompressedString = await uncompress(HOURLY_URL, hourlyFile, "hourly");
            }
            
            var allProjects = JsonConvert.DeserializeObject<ProjectList>(uncompressedString, settings);
            
            if(filter) {
                Console.WriteLine($"Filtering projects, please wait... old count: {allProjects.Data.Count()}");
                allProjects.Data = allProjects.Data.Where(a => a.PackageType == PackageTypes.Mod).ToList();
                Console.WriteLine($"Finished filtering projects, new count: {allProjects.Data.Count()}");
            }
            
            Directory.CreateDirectory(Path.GetDirectoryName(hourlyFileTimestamp));
            File.WriteAllText(hourlyFileTimestamp, allProjects.Timestamp.ToString());
            
            return allProjects;
        }
        
        public static async Task<ProjectList> GetLocalComplete(string directory, bool filter = true)
        {
            var compressedFile = Path.Combine(directory, "complete.json.bz2");
            String uncompressedString = null;
            if (File.Exists(compressedFile)) {
                //decompress
                using (var filestream = File.OpenRead(compressedFile))
                using (var target = new MemoryStream()) {
                    BZip2.Decompress(filestream, target, true);
                    uncompressedString = Encoding.UTF8.GetString(target.ToArray());
                }
            } else {
                //fallback
                return await GetComplete();
            }
            
            var allProjects = JsonConvert.DeserializeObject<ProjectList>(uncompressedString, settings);
            
            if(filter) {
                Console.WriteLine($"Filtering projects, please wait... old count: {allProjects.Data.Count()}");
                allProjects.Data = allProjects.Data.Where(a => a.PackageType == PackageTypes.Mod).ToList();
                Console.WriteLine($"Finished filtering projects, new count: {allProjects.Data.Count()}");
            }
            
            return allProjects;
        }
        
        private static async Task<string> uncompress(string url, string file, string mode = "")
        {
            string uncompressedString;
            Console.WriteLine($"Downloading { mode ?? "" } project list from Curse. This could take a while ..."); // TODO: verbose logging
            using (var stream = await client.GetStreamAsync(url))
            using (var target = new MemoryStream()) {
                BZip2.Decompress(stream, target, true);
                uncompressedString = Encoding.UTF8.GetString(target.ToArray());
            }
            // save to cache
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, uncompressedString);
            return uncompressedString;
        }
        
        public static void SaveLocalComplete(ProjectList allProjects, string directory, bool compressed = true, bool uncompressed = true)
        {
            var json_string = JsonConvert.SerializeObject(allProjects, settings);
            
            var currentFile = Path.Combine(cache, "current.json");
            var completeFile = Path.Combine(directory, "complete.json");
            var compressedFile = Path.Combine(directory, "complete.json.bz2");
            
            //uncompressed
            if(uncompressed) {
                File.WriteAllText(completeFile, json_string);
            }
            File.WriteAllText(currentFile, json_string);
            
            //compressed
            if(compressed) {
                using(var fileOutStream = File.OpenWrite(compressedFile)) 
                {
                    byte[] byteArray = Encoding.ASCII.GetBytes( json_string );
                    MemoryStream stream = new MemoryStream( byteArray );
                    BZip2.Compress(stream, fileOutStream, true, 4096);
                }
            }
        }
    }
    
    
    public class ProjectList
    {
        public long Timestamp { get; set; }
        public List<AddOn> Data { get; set; }
        
        public ProjectList merge(ProjectList patch)
        {
            var newlist = this.Data.Where(s => !patch.Data.Any(p => p.Id == s.Id)).ToList();
            newlist.AddRange(patch.Data);
            this.Data = newlist.OrderBy(o => o.Id).ToList();
            var newProjectList = new ProjectList 
            {
                Timestamp = patch.Timestamp,
                Data = newlist.OrderBy(o => o.Id).ToList()
            };
            return newProjectList;
        }
    }
}