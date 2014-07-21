using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using MongoDB.Bson;
using MongoDB.Driver.Builders;
using log4net;
using MongoDB.Driver;
using System.Configuration;
using AlchemyAPI;

namespace TwitterAnalyzer
{

    public static class ServiceHelper
    {
        public static IDictionary<string,string> ReadTextSentiment(string text, ILog logger)
        {
            var wordMoods = new Dictionary<string,string>();

            var connectionString = "mongodb://localhost";
            var client = new MongoClient(connectionString);
            var server = client.GetServer();
            try
            {
                string collName = ConfigurationManager.AppSettings["CollectionName"];
                if (string.IsNullOrEmpty(collName))
                    collName = "words";
                var database = server.GetDatabase("wcat"); // "wcat" is the name 
                if (!database.CollectionExists(collName))
                {
                    database.CreateCollection(collName);
                }
                var collection = database.GetCollection(collName);
                if (collection != null)
                {                    
                    var emptyspaces = new char[] {' ', '\t', '\r', '\n'};
                    var specials = new char[]
                        {
                            '#', '"', '|', '@', '£', '$', '&', '(', ')', '[', ']', '=', '+', '-', '*', '^', '<', '>',
                            '~', '°'
                        };
                    var alpha_separators = new char[] {'\''};
                    var punctuation = new char[] {'.', ',', ';', ':', '!', '?'};

                    // deal with percentages
                    ManagePercentages(text, logger, emptyspaces, specials, wordMoods);

                    // deal with words                    
                    foreach (var sentence in text.Split(punctuation))
                    {
                        bool denial = false;
                        foreach (var item in sentence.Split(emptyspaces))
                        {
                            string unit = item.Trim().Trim(specials).ToLower();
                            // is an empty string?
                            if (string.IsNullOrEmpty(unit)) continue;
                            // is a number?
                            float num;
                            if (float.TryParse(unit, out num)) continue;
                            // is a denial?
                            if (unit == "non" || unit == "not")
                            {
                                denial = !denial;
                            }
                            else
                            {
                                // split by ' and -
                                foreach (var atom in unit.Split(alpha_separators))
                                {
                                    IMongoQuery query = Query.EQ("word", atom);
                                    var docs = collection.Find(query);
                                    foreach (var doc in docs)
                                    {
                                        BsonElement elem;
                                        if (doc.TryGetElement("mood", out elem))
                                        {
                                            string mood = (string) elem.Value;
                                            if (denial)
                                            {
                                                mood = (mood == "negative" ? "positive" : "negative");
                                                denial = false;
                                            }
                                            logger.DebugFormat("word:{0}, mood:{1}", atom, mood);
                                            wordMoods[atom] = mood;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                }
            }
            catch (MongoConnectionException mce)
            {
                logger.Error(mce);
            }
            return wordMoods;
        }


        public static string BuildResult(string text, ILog logger)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentNullException("text");

            string result = BuildContent("0", "");
            try
            {
                IList<string> urls;
                text = SkipHttpLinks(text, out urls);

                bool skip;
                string skipHttpLinks = ConfigurationManager.AppSettings["SkipHttpLinks"];
                if (string.IsNullOrEmpty(skipHttpLinks))
                    skip = true;
                else
                    skip = bool.Parse(skipHttpLinks);

                if (!skip && urls.Count > 0)
                {
                    string url = urls.First();

                    logger.DebugFormat("Creating AlchemyAPI object on: {0}", url);
                    // Create an AlchemyAPI object.
                    AlchemyAPI.AlchemyAPI alchemyObj = new AlchemyAPI.AlchemyAPI();
                    // Load an API key from disk.
                    string apiKey = Properties.Resources.api_key;
                    alchemyObj.SetAPIKey(apiKey);

                    AlchemyAPI_CombinedDataParams prms = new AlchemyAPI_CombinedDataParams();
                    //prms.Extractions = CombinedExtract.Title | CombinedExtract.Author | CombinedExtract.Entity;
                    prms.Extractions = CombinedExtract.Title | CombinedExtract.Author | CombinedExtract.DocSentiment;

                    string xml = alchemyObj.URLGetCombinedData(url, prms);
                    if (!string.IsNullOrEmpty(xml))
                    {
                        logger.InfoFormat("Got Combined Data as XML: {0}", xml);

                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(xml);
                        // get docSentiment type
                        XmlElement root = xmlDoc.DocumentElement;

                        string errorMessage = "Error reading Sentiment type by API call.";
                        try
                        {
                            XmlNode docSentimentType = root.SelectSingleNode("/results/docSentiment/type");
                            string sentimentType = docSentimentType.InnerText;
                            result = BuildContent("1", sentimentType);
                        }
                        catch(Exception ex)
                        {
                            errorMessage = "An error occurred: Unable to access XmlNode /results/docSentiment/type";
                            logger.Error(errorMessage, ex);
                        }                        
                    }
                }
                else
                {
                    var wordMoods = ReadTextSentiment(text, logger);
                    if (wordMoods.Any())
                    {
                        if (wordMoods.Values.Any(w => w == "negative"))
                        {
                            result = BuildContent("1", "negative");
                        }
                        else if (wordMoods.Values.Any(w => w == "positive"))
                        {
                            result = BuildContent("1", "positive");
                        }
                        else
                        {
                            result = BuildContent("1", "neutral");
                        }
                    }
                }
                logger.DebugFormat("result:{0}", result);
            }
            catch (Exception exc)
            {
                logger.ErrorFormat("exception in BuildResult: {0}", exc);
            }

            return result;
        }

        private static string BuildContent(string status, string sentiment)
        {
            //{"output":{"status":1,"result":"negative"}}
            string content = "{\"output\":{" + String.Format("\"status\":{0},\"result\":\"{1}\"",
                status, sentiment) + "}}";
            return content;
        }

        
        private static void ManagePercentages(string text, ILog logger, char[] emptyspaces, 
                                              char[] specials, Dictionary<string, string> wordMoods)
        {            
            foreach (var item in text.Split(emptyspaces))
            {
                string unit = item.Trim().Trim(new char[] { ';', ':', '!', '?' }).Trim(specials).ToLower();

                float num;
                // is a percentage?
                if (unit.EndsWith("%"))
                {
                    if (float.TryParse(unit.Trim(new char[] {'%'}), out num))
                    {
                        string mood = Math.Sign(num) < 0 ? "negative" : "positive";
                        logger.DebugFormat("num:{0}, mood:{1}", unit, mood);
                        wordMoods[unit] = mood;
                    }
                }
            }
        }

        private static string SkipHttpLinks(string text, out IList<string> urls)
        {
            urls = new List<string>();
            if (!string.IsNullOrEmpty(text))
            {
                int startIndex = 0;
                do
                {
                    startIndex = text.IndexOf("http", StringComparison.CurrentCulture);
                    if (startIndex >= 0)
                    {
                        int count = 1, endIndex = startIndex;
                        // skip hypertext links
                        while (endIndex < text.Length && text[endIndex++] != ' ')
                            count++;
                        // keep url for further use
                        string url = text.Substring(startIndex, count - 1);
                        urls.Add(url);
                        // remove url from text
                        text = text.Remove(startIndex, count - 1);

                        // read html                    
                        //WebClient client = new WebClient();
                        //String htmlCode = client.DownloadString(url);
                    }
                } while (startIndex >= 0 && startIndex < text.Length);
            }
            return text;
        }
    }
}
