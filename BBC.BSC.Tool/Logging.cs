using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.ElasticSearch;


namespace BBC.BSC.Tool
{
    public class Logging
    {
        public Logger logger;


        public Logger initLogger()
        {

            return logger;
        }
        public Logging()
        {
            //init
            var config = new LoggingConfiguration();
            var consoleTarget = new ColoredConsoleTarget
            {
                // ReSharper disable once StringLiteralTypo
                Layout = "${time} ${pad:padding=3:inner=${threadid}} ${message} ${exception:format=tostring}"
            };
            var elasticSearchTarget = new ElasticSearchTarget
            {
                Index = "bsctool1",
                IncludeAllProperties = true
            };
            elasticSearchTarget.Fields.Add(new Field { Name = "user", Layout = "${windows-identity:userName=True:domain=False}" });
            elasticSearchTarget.Fields.Add(new Field { Name = "host", Layout = "${machinename}" });
            elasticSearchTarget.Fields.Add(new Field { Name = "thread", Layout = "${threadid}" });
            elasticSearchTarget.Fields.Add(new Field { Name = "threadname", Layout = "${threadname}" });
            elasticSearchTarget.Fields.Add(new Field { Name = "version", Layout = "${assembly-version}" });
#if DEBUG
            elasticSearchTarget.Fields.Add(new Field { Name = "build", Layout = "DEBUG" });

#else
            elasticSearchTarget.Fields.Add(new NLog.Targets.ElasticSearch.Field() { Name = "build", Layout = "RELEASE" });

#endif

            elasticSearchTarget.Layout = "${message} ${exception:format=tostring}";

            elasticSearchTarget.Uri = @"http://3gbbmdbels1000:9200";
            config.AddRule(LogLevel.Info, LogLevel.Fatal, elasticSearchTarget);

            config.AddRule(LogLevel.Trace, LogLevel.Fatal, consoleTarget);

             logger = LogManager.GetCurrentClassLogger();
            LogManager.Configuration = config;

        }


    }
}
