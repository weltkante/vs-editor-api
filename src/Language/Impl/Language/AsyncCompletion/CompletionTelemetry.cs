using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text.Utilities;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Implementation
{
    internal class CompletionSessionTelemetry
    {
        private readonly CompletionTelemetryHost _telemetryHost;

        // Names of parts that participated in completion
        internal string ItemManagerName { get; private set; }
        internal string PresenterProviderName { get; private set; }
        internal string CommitManagerName { get; private set; }

        // "Processing" is work done by IAsyncCompletionItemManager
        internal long InitialProcessingDuration { get; private set; }
        internal long TotalProcessingDuration { get; private set; }
        internal int TotalProcessingCount { get; private set; }

        // "Rendering" is work done on UI thread by ICompletionPresenter
        internal long InitialRenderingDuration { get; private set; }
        internal long TotalRenderingDuration { get; private set; }
        internal int TotalRenderingCount { get; private set; }

        // "Commit" is work done on UI thread by IAsyncCompletionCommitManager
        internal long CommitDuration { get; private set; }

        // Additional parameters related to work done by IAsyncCompletionItemManager
        internal bool UserEverScrolled { get; private set; }
        internal bool UserEverSetFilters { get; private set; }
        internal int FinalItemCount { get; private set; }
        internal int NumberOfKeystrokes { get; private set; }

        public CompletionSessionTelemetry(CompletionTelemetryHost telemetryHost)
        {
            _telemetryHost = telemetryHost;
        }

        internal void RecordProcessing(long processingTime, int itemCount)
        {
            if (TotalProcessingCount == 0)
            {
                InitialProcessingDuration = processingTime;
            }
            else
            {
                TotalProcessingDuration += processingTime;
                FinalItemCount = itemCount;
            }
            TotalProcessingCount++;
        }

        internal void RecordRendering(long processingTime)
        {
            if (TotalRenderingCount == 0)
                InitialRenderingDuration = processingTime;
            TotalRenderingCount++;
            TotalRenderingDuration += processingTime;
        }

        internal void RecordScrolling()
        {
            UserEverScrolled = true;
        }

        internal void RecordChangingFilters()
        {
            UserEverSetFilters = true;
        }

        internal void RecordKeystroke()
        {
            NumberOfKeystrokes++;
        }

        internal void RecordCommittedSaveAndForget(long commitDuration,
            IAsyncCompletionItemManager itemManager,
            ICompletionPresenterProvider presenterProvider,
            IAsyncCompletionCommitManager manager)
        {
            ItemManagerName = CompletionTelemetryHost.GetItemManagerName(itemManager);
            PresenterProviderName = CompletionTelemetryHost.GetPresenterProviderName(presenterProvider);
            CommitManagerName = CompletionTelemetryHost.GetCommitManagerName(manager);
            CommitDuration = commitDuration;
            _telemetryHost.Add(this);
        }
    }

    internal class CompletionTelemetryHost
    {
        private class AggregateCommitManagerData
        {
            internal long TotalCommitTime;

            /// <summary>
            /// This value is used to calculate averages
            /// </summary>
            internal long CommitCount;
        }

        private class AggregateItemManagerData
        {
            internal long InitialProcessTime;
            internal long TotalProcessTime;

            /// <summary>
            /// This value is used to calculate average processing time. One session may have multiple processing operations.
            /// </summary>
            internal int ProcessCount;
            internal int TotalKeystrokes;
            internal int UserEverScrolled;
            internal int UserEverSetFilters;
            internal int FinalItemCount;

            /// <summary>
            /// This value is used to calculate averages
            /// </summary>
            internal int DataCount;
        }

        private class AggregatePresenterData
        {
            internal long InitialRenderTime;
            internal long TotalRenderTime;

            /// <summary>
            /// This value is used to calculate averages
            /// </summary>
            internal int RenderCount;
        }

        Dictionary<string, AggregateCommitManagerData> CommitManagerData = new Dictionary<string, AggregateCommitManagerData>(2);
        Dictionary<string, AggregateItemManagerData> ItemManagerData = new Dictionary<string, AggregateItemManagerData>(8);
        Dictionary<string, AggregatePresenterData> PresenterData = new Dictionary<string, AggregatePresenterData>(3);

        private readonly ILoggingServiceInternal _logger;
        private readonly AsyncCompletionBroker _broker;

        public CompletionTelemetryHost(ILoggingServiceInternal logger, AsyncCompletionBroker broker)
        {
            _logger = logger;
            _broker = broker;
        }

        internal static string GetSourceName(IAsyncCompletionSource source) => source?.GetType().ToString() ?? string.Empty;
        internal static string GetCommitManagerName(IAsyncCompletionCommitManager commitManager) => commitManager?.GetType().ToString() ?? string.Empty;
        internal static string GetItemManagerName(IAsyncCompletionItemManager itemManager) => itemManager?.GetType().ToString() ?? string.Empty;
        internal static string GetPresenterProviderName(ICompletionPresenterProvider provider) => provider?.GetType().ToString() ?? string.Empty;

        /// <summary>
        /// Adds data from <see cref="CompletionSessionTelemetry" /> to appropriate buckets.
        /// </summary>
        /// <param name=""></param>
        internal void Add(CompletionSessionTelemetry telemetry)
        {
            if (_logger == null)
                return;

            var presenterKey = telemetry.PresenterProviderName;
            if (!PresenterData.ContainsKey(presenterKey))
                PresenterData[presenterKey] = new AggregatePresenterData();
            var aggregatePresenterData = PresenterData[presenterKey];

            var itemManagerKey = telemetry.ItemManagerName;
            if (!ItemManagerData.ContainsKey(itemManagerKey))
                ItemManagerData[itemManagerKey] = new AggregateItemManagerData();
            var aggregateItemManagerData = ItemManagerData[itemManagerKey];

            var commitKey = telemetry.CommitManagerName;
            if (!CommitManagerData.ContainsKey(commitKey))
                CommitManagerData[commitKey] = new AggregateCommitManagerData();
            var aggregateCommitManagerData = CommitManagerData[commitKey];

            aggregatePresenterData.InitialRenderTime += telemetry.InitialRenderingDuration;
            aggregatePresenterData.TotalRenderTime += telemetry.TotalRenderingDuration;
            aggregatePresenterData.RenderCount += telemetry.TotalRenderingCount;

            aggregateItemManagerData.InitialProcessTime += telemetry.InitialProcessingDuration;
            aggregateItemManagerData.TotalProcessTime += telemetry.TotalProcessingDuration;
            aggregateItemManagerData.ProcessCount += telemetry.TotalProcessingCount;
            aggregateItemManagerData.TotalKeystrokes += telemetry.NumberOfKeystrokes;
            aggregateItemManagerData.UserEverScrolled += telemetry.UserEverScrolled ? 1 : 0;
            aggregateItemManagerData.UserEverSetFilters += telemetry.UserEverSetFilters ? 1 : 0;
            aggregateItemManagerData.FinalItemCount += telemetry.FinalItemCount;
            aggregateItemManagerData.DataCount++;

            aggregateCommitManagerData.TotalCommitTime += telemetry.CommitDuration;
            aggregateCommitManagerData.CommitCount++;
        }

        /// <summary>
        /// Sends batch of collected data.
        /// </summary>
        internal void Send()
        {
            if (_logger == null)
                return;

            foreach (var data in ItemManagerData)
            {
                if (data.Value.DataCount == 0)
                    continue;
                if (data.Value.ProcessCount == 0)
                    continue;

                _logger.PostEvent(TelemetryEventType.Operation,
                    ServiceEventName,
                    TelemetryResult.Success,
                    (ServiceName, data.Key),
                    (ServiceAverageFinalItemCount, data.Value.FinalItemCount / data.Value.DataCount),
                    (ServiceAverageInitialProcessTime, data.Value.InitialProcessTime / data.Value.DataCount),
                    (ServiceAverageFilterTime, data.Value.TotalProcessTime / data.Value.ProcessCount),
                    (ServiceAverageKeystrokeCount, data.Value.TotalKeystrokes / data.Value.DataCount),
                    (ServiceAverageScrolled, data.Value.UserEverScrolled / data.Value.DataCount),
                    (ServiceAverageSetFilters, data.Value.UserEverSetFilters / data.Value.DataCount)
                );
            }

            foreach (var data in CommitManagerData)
            {
                if (data.Value.CommitCount == 0)
                    continue;

                _logger.PostEvent(TelemetryEventType.Operation,
                    CommitManagerEventName,
                    TelemetryResult.Success,
                    (CommitManagerName, data.Key),
                    (CommitManagerAverageCommitDuration, data.Value.TotalCommitTime / data.Value.CommitCount)
                );
            }

            foreach (var data in PresenterData)
            {
                if (data.Value.RenderCount == 0)
                    continue;

                _logger.PostEvent(TelemetryEventType.Operation,
                    PresenterEventName,
                    TelemetryResult.Success,
                    (PresenterName, data.Key),
                    (PresenterAverageInitialRendering, data.Value.InitialRenderTime / data.Value.RenderCount),
                    (PresenterAverageRendering, data.Value.TotalRenderTime / data.Value.RenderCount)
                );
            }
        }

        // Property and event names
        internal const string PresenterEventName = "VS/Editor/Completion/PresenterData";
        internal const string PresenterName = "Property.Rendering.Name";
        internal const string PresenterAverageInitialRendering = "Property.Rendering.InitialDuration";
        internal const string PresenterAverageRendering = "Property.Rendering.AnyDuration";

        internal const string ServiceEventName = "VS/Editor/Completion/ServiceData";
        internal const string ServiceName = "Property.Service.Name";
        internal const string ServiceAverageFinalItemCount = "Property.Service.FinalItemCount";
        internal const string ServiceAverageInitialProcessTime = "Property.Service.InitialDuration";
        internal const string ServiceAverageFilterTime = "Property.Service.AnyDuration";
        internal const string ServiceAverageKeystrokeCount = "Property.Service.KeystrokeCount";
        internal const string ServiceAverageScrolled = "Property.Service.Scrolled";
        internal const string ServiceAverageSetFilters = "Property.Service.SetFilters";

        internal const string CommitManagerEventName = "VS/Editor/Completion/SourceData";
        internal const string CommitManagerName = "Property.CommitManager.Name";
        internal const string CommitManagerAverageCommitDuration = "Property.Commit.Duration";
    }
}
