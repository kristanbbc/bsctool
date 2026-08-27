using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using NLog.Targets.ElasticSearch;


namespace BBC.BSC.Tool
{
    public class Logging
    {
        public Logger Logger;


        public Logger InitLogger()
        {

            return Logger;
        }
        public Logging()
        {
            try
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

                // Note: internal-only, unauthenticated HTTP endpoint. This is only reachable from
                // the internal network (e.g. via Zscaler on Intune-managed devices); if it becomes
                // unreachable or slow, the async wrapper below prevents it from blocking the app.
                elasticSearchTarget.Uri = @"http://3gbbmdbels1000:9200";

                // Wrap in an async target so a slow/unreachable ElasticSearch endpoint can never
                // block the calling thread. Bounded queue with OverflowAction.Discard avoids
                // unbounded memory growth if the endpoint is unreachable for an extended period.
                var asyncElasticSearchTarget = new AsyncTargetWrapper(elasticSearchTarget)
                {
                    QueueLimit = 5000,
                    OverflowAction = AsyncTargetWrapperOverflowAction.Discard,
                    TimeToSleepBetweenBatches = 0
                };

                config.AddRule(LogLevel.Info, LogLevel.Fatal, asyncElasticSearchTarget);

                config.AddRule(LogLevel.Trace, LogLevel.Fatal, consoleTarget);

                Logger = LogManager.GetCurrentClassLogger();
                LogManager.Configuration = config;
            }
            catch
            {
                // Logging must never prevent the application from starting - fall back to a
                // logger with no configured targets rather than throwing.
                Logger = LogManager.GetCurrentClassLogger();
            }
        }


    }
}
