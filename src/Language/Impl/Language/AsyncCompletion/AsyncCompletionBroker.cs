using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Utilities;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Implementation
{
    [Export(typeof(IAsyncCompletionBroker))]
    internal class AsyncCompletionBroker : IAsyncCompletionBroker
    {
        [Import]
        private IGuardedOperations GuardedOperations;

        [Import(AllowDefault = true)]
        private JoinableTaskContext JoinableTaskContext;

        [Import]
        private IContentTypeRegistryService ContentTypeRegistryService;

        [ImportMany]
        private IEnumerable<Lazy<IAsyncCompletionSourceProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> UnorderedCompletionSourceProviders;

        [ImportMany]
        private IEnumerable<Lazy<IAsyncCompletionItemManagerProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> UnorderedCompletionItemManagerProviders;

        [ImportMany]
        private IEnumerable<Lazy<IAsyncCompletionCommitManagerProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> UnorderedCompletionCommitManagerProviders;

        [ImportMany]
        private IEnumerable<Lazy<ICompletionPresenterProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> UnorderedPresenterProviders;

        // Used for telemetry
        [Import(AllowDefault = true)]
        private ILoggingServiceInternal Logger;

        // Used for legacy telemetry
        [Import(AllowDefault = true)]
        private ITextDocumentFactoryService TextDocumentFactoryService;

        private IList<Lazy<IAsyncCompletionSourceProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> _orderedCompletionSourceProviders;
        private IList<Lazy<IAsyncCompletionSourceProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> OrderedCompletionSourceProviders
            => _orderedCompletionSourceProviders ?? (_orderedCompletionSourceProviders = Orderer.Order(UnorderedCompletionSourceProviders));

        private IList<Lazy<IAsyncCompletionItemManagerProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> _orderedCompletionItemManagerProviders;
        private IList<Lazy<IAsyncCompletionItemManagerProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> OrderedCompletionItemManagerProviders
            => _orderedCompletionItemManagerProviders ?? (_orderedCompletionItemManagerProviders = Orderer.Order(UnorderedCompletionItemManagerProviders));

        private IList<Lazy<IAsyncCompletionCommitManagerProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> _orderedCompletionCommitManagerProviders;
        private IList<Lazy<IAsyncCompletionCommitManagerProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> OrderedCompletionCommitManagerProviders
            => _orderedCompletionCommitManagerProviders ?? (_orderedCompletionCommitManagerProviders = Orderer.Order(UnorderedCompletionCommitManagerProviders));

        private IList<Lazy<ICompletionPresenterProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> _orderedPresenterProviders;
        private IList<Lazy<ICompletionPresenterProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> OrderedPresenterProviders
            => _orderedPresenterProviders ?? (_orderedPresenterProviders = Orderer.Order(UnorderedPresenterProviders));

        private bool firstRun = true; // used only for diagnostics
        private bool _firstInvocationReported; // used for "time to code"
        private StableContentTypeComparer _contentTypeComparer;

        private const string IsCompletionAvailableProperty = "IsCompletionAvailable";
        private Dictionary<IContentType, bool> FeatureAvailabilityByContentType = new Dictionary<IContentType, bool>();

        public event EventHandler<CompletionTriggeredEventArgs> CompletionTriggered;

        bool IAsyncCompletionBroker.IsCompletionActive(ITextView textView)
        {
            return textView.Properties.ContainsProperty(typeof(IAsyncCompletionSession));
        }

        bool IAsyncCompletionBroker.IsCompletionSupported(IContentType contentType)
        {
            bool featureIsAvailable;
            if (FeatureAvailabilityByContentType.TryGetValue(contentType, out featureIsAvailable))
            {
                return featureIsAvailable;
            }

            featureIsAvailable = UnorderedCompletionSourceProviders
                    .Any(n => n.Metadata.ContentTypes.Any(ct => contentType.IsOfType(ct)));
            featureIsAvailable &= UnorderedCompletionItemManagerProviders
                    .Any(n => n.Metadata.ContentTypes.Any(ct => contentType.IsOfType(ct)));

            FeatureAvailabilityByContentType[contentType] = featureIsAvailable;
            return featureIsAvailable;
        }

        IAsyncCompletionSession IAsyncCompletionBroker.GetSession(ITextView textView)
        {
            if (textView.Properties.TryGetProperty(typeof(IAsyncCompletionSession), out IAsyncCompletionSession session))
            {
                return session;
            }
            return null;
        }

        IAsyncCompletionSession IAsyncCompletionBroker.TriggerCompletion(ITextView textView, SnapshotPoint triggerLocation, char typedChar)
        {
            var session = ((IAsyncCompletionBroker)this).GetSession(textView);
            if (session != null)
            {
                return session;
            }

            if (!JoinableTaskContext.IsOnMainThread)
                throw new InvalidOperationException($"This method must be callled on the UI thread.");

            var sourcesWithData = MetadataUtilities<IAsyncCompletionSourceProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>.GetBuffersAndImports(textView, triggerLocation, GetItemSourceProviders);
            var commitManagersWithData = MetadataUtilities<IAsyncCompletionCommitManagerProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>.GetBuffersAndImports(textView, triggerLocation, GetCommitManagerProviders);

            // Obtain potential commit characters
            var potentialCommitCharsBuilder = ImmutableArray.CreateBuilder<char>();
            var managersWithBuffers = new List<(IAsyncCompletionCommitManager, ITextBuffer)>(1);
            foreach (var managerWithData in commitManagersWithData)
            {
                var managerProvider = GuardedOperations.InstantiateExtension(this, managerWithData.import);
                var manager = GuardedOperations.CallExtensionPoint(
                    errorSource: managerProvider,
                    call: () => managerProvider.GetOrCreate(textView),
                    valueOnThrow: null);

                if (manager == null)
                    continue;

                GuardedOperations.CallExtensionPoint(
                    errorSource: manager,
                    call: () =>
                    {
                        var characters = manager.PotentialCommitCharacters;
                        potentialCommitCharsBuilder.AddRange(characters);
                    });
                managersWithBuffers.Add((manager, managerWithData.buffer));
            }

            // Obtain applicable span, potential commit chars and mapping of source to buffer
            SnapshotSpan applicableSpan = default(SnapshotSpan);
            bool applicableSpanExists = false;
            var sourcesWithLocations = new List<(IAsyncCompletionSource, SnapshotPoint)>(); // TODO: optimize this.
            foreach (var sourceWithData in sourcesWithData)
            {
                var sourceProvider = GuardedOperations.InstantiateExtension(this, sourceWithData.import);
                var source = GuardedOperations.CallExtensionPoint(
                    errorSource: sourceProvider,
                    call: () => sourceProvider.GetOrCreate(textView),
                    valueOnThrow: null);

                if (source == null)
                    continue;

                applicableSpanExists |= GuardedOperations.CallExtensionPoint(
                    errorSource: source,
                    call: () =>
                    {
                        sourcesWithLocations.Add((source, sourceWithData.point)); // We want to iterate through all sources
                        if (!applicableSpanExists) // Call this only once.
                            return source.TryGetApplicableSpan(typedChar, sourceWithData.point, out applicableSpan);
                        return false;
                    },
                    valueOnThrow: false);

                // Assume that sources are ordered. If this source is the first one to provide span, map it to the view's top buffer and use it for completion,
                if (applicableSpanExists)
                {
                    var mappingSpan = textView.BufferGraph.CreateMappingSpan(applicableSpan, SpanTrackingMode.EdgeInclusive);
                    applicableSpan = mappingSpan.GetSpans(textView.TextBuffer)[0];
                    break; // We have the span, there is no need to call any more sources.
                }
            }

            if (!applicableSpanExists)
                return null;

            if (_contentTypeComparer == null)
                _contentTypeComparer = new StableContentTypeComparer(ContentTypeRegistryService);

            var itemManagerProvidersWithData = MetadataUtilities<IAsyncCompletionItemManagerProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>.GetOrderedBuffersAndImports(textView, triggerLocation, GetItemManagerProviders, _contentTypeComparer);
            if (!itemManagerProvidersWithData.Any())
            {
                // This should never happen because we provide a default and IsCompletionFeatureAvailable would have returned false 
                throw new InvalidOperationException("No completion services not found. Completion will be unavailable.");
            }
            var bestItemManagerProvider = GuardedOperations.InstantiateExtension(this, itemManagerProvidersWithData.First().import);
            var itemManager = GuardedOperations.CallExtensionPoint(bestItemManagerProvider, () => bestItemManagerProvider.GetOrCreate(textView), null);

            var presenterProvidersWithData = MetadataUtilities<ICompletionPresenterProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>.GetOrderedBuffersAndImports(textView, triggerLocation, GetPresenters, _contentTypeComparer);
            ICompletionPresenterProvider presenterProvider = null;
            if (presenterProvidersWithData.Any())
                presenterProvider = GuardedOperations.InstantiateExtension(this, presenterProvidersWithData.First().import);

            if (firstRun)
            {
                System.Diagnostics.Debug.Assert(presenterProvider != null, $"No instance of {nameof(ICompletionPresenterProvider)} is loaded. Completion will work without the UI.");
                firstRun = false;
            }
            var telemetry = GetOrCreateTelemetry(textView);

            session = new AsyncCompletionSession(applicableSpan, potentialCommitCharsBuilder.ToImmutable(), JoinableTaskContext, presenterProvider, sourcesWithLocations, managersWithBuffers, itemManager, this, textView, telemetry, GuardedOperations);
            textView.Properties.AddProperty(typeof(IAsyncCompletionSession), session);
            textView.Closed += TextView_Closed;

            // Additionally, emulate the legacy completion telemetry
            EmulateLegacyCompletionTelemetry(itemManagerProvidersWithData.First().buffer?.ContentType, textView);

            GuardedOperations.RaiseEvent(this, CompletionTriggered, new CompletionTriggeredEventArgs(session, textView));

            return session;
        }

        /// <summary>
        /// This method is used by <see cref="IAsyncCompletionSession"/> to inform the broker that it should forget about the session.
        /// Invoked as a result of dismissing. This method does not dismiss the session!
        /// </summary>
        /// <param name="session">Session being dismissed</param>
        internal void ForgetSession(IAsyncCompletionSession session)
        {
            session.TextView.Properties.RemoveProperty(typeof(IAsyncCompletionSession));
        }

        private IReadOnlyList<Lazy<IAsyncCompletionSourceProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> GetItemSourceProviders(IContentType contentType, ITextViewRoleSet textViewRoles)
        {
            return OrderedCompletionSourceProviders.Where(n => n.Metadata.ContentTypes.Any(c => contentType.IsOfType(c)) && (n.Metadata.TextViewRoles == null || textViewRoles.ContainsAny(n.Metadata.TextViewRoles))).ToList();
        }

        private IReadOnlyList<Lazy<IAsyncCompletionItemManagerProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> GetItemManagerProviders(IContentType contentType, ITextViewRoleSet textViewRoles)
        {
            return OrderedCompletionItemManagerProviders.Where(n => n.Metadata.ContentTypes.Any(c => contentType.IsOfType(c)) && (n.Metadata.TextViewRoles == null || textViewRoles.ContainsAny(n.Metadata.TextViewRoles))).OrderBy(n => n.Metadata.ContentTypes, _contentTypeComparer).ToList();
        }

        private IReadOnlyList<Lazy<IAsyncCompletionCommitManagerProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> GetCommitManagerProviders(IContentType contentType, ITextViewRoleSet textViewRoles)
        {
            return OrderedCompletionCommitManagerProviders.Where(n => n.Metadata.ContentTypes.Any(c => contentType.IsOfType(c)) && (n.Metadata.TextViewRoles == null || textViewRoles.ContainsAny(n.Metadata.TextViewRoles))).ToList();
        }

        private IReadOnlyList<Lazy<ICompletionPresenterProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> GetPresenters(IContentType contentType, ITextViewRoleSet textViewRoles)
        {
            return OrderedPresenterProviders.Where(n => n.Metadata.ContentTypes.Any(c => contentType.IsOfType(c)) && (n.Metadata.TextViewRoles == null || textViewRoles.ContainsAny(n.Metadata.TextViewRoles))).OrderBy(n => n.Metadata.ContentTypes, _contentTypeComparer).ToList();
        }

        private void TextView_Closed(object sender, EventArgs e)
        {
            var view = (ITextView)sender;
            view.Closed -= TextView_Closed;
            ((IAsyncCompletionBroker)this).GetSession(view)?.Dismiss();
            try
            {
                SendTelemetry(view);
            }
            catch (Exception ex)
            {
                GuardedOperations.HandleException(this, ex);
            }
        }

        // Helper methods for telemetry:

        private CompletionTelemetryHost GetOrCreateTelemetry(ITextView textView)
        {
            if (textView.Properties.TryGetProperty(typeof(CompletionTelemetryHost), out CompletionTelemetryHost telemetry))
            {
                return telemetry;
            }
            else
            {
                var newTelemetry = new CompletionTelemetryHost(Logger, this);
                textView.Properties.AddProperty(typeof(CompletionTelemetryHost), newTelemetry);
                return newTelemetry;
            }
        }

        private void SendTelemetry(ITextView textView)
        {
            if (textView.Properties.TryGetProperty(typeof(CompletionTelemetryHost), out CompletionTelemetryHost telemetry))
            {
                telemetry.Send();
                textView.Properties.RemoveProperty(typeof(CompletionTelemetryHost));
            }
        }

        // Parity with legacy telemetry
        private void EmulateLegacyCompletionTelemetry(IContentType contentType, ITextView textView)
        {
            if (Logger == null || _firstInvocationReported)
                return;

            string GetFileExtension(ITextBuffer buffer)
            {
                var documentFactoryService = TextDocumentFactoryService;
                if (buffer != null && documentFactoryService != null)
                {
                    ITextDocument currentDocument = null;
                    documentFactoryService.TryGetTextDocument(buffer, out currentDocument);
                    if (currentDocument != null && currentDocument.FilePath != null)
                    {
                        return System.IO.Path.GetExtension(currentDocument.FilePath);
                    }
                }
                return null;
            }
            var fileExtension = GetFileExtension(textView.TextBuffer) ?? "Unknown";
            var reportedContentType = contentType?.ToString() ?? "Unknown";

            _firstInvocationReported = true;
            Logger.PostEvent(TelemetryEventType.Operation, "VS/Editor/IntellisenseFirstRun/Opened", TelemetryResult.Success,
                ("VS.Editor.IntellisenseFirstRun.Opened.ContentType", reportedContentType),
                ("VS.Editor.IntellisenseFirstRun.Opened.FileExtension", fileExtension));
        }
    }
}
