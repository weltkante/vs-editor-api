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

namespace Microsoft.VisualStudio.Language.Intellisense.Implementation
{
    [Export(typeof(IAsyncCompletionBroker))]
    internal class AsyncCompletionBroker : IAsyncCompletionBroker
    {
        [Import]
        private IGuardedOperations GuardedOperations;

        [Import]
        private JoinableTaskContext JoinableTaskContext;

        [Import]
        private IContentTypeRegistryService ContentTypeRegistryService;

        [ImportMany]
        private IEnumerable<Lazy<ICompletionPresenterProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> UnorderedPresenterProviders;

        [ImportMany]
        private IEnumerable<Lazy<IAsyncCompletionItemSourceProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> UnorderedCompletionItemSourceProviders;

        [ImportMany]
        private IEnumerable<Lazy<IAsyncCompletionServiceProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> UnorderedCompletionServiceProviders;

        // Used for telemetry
        [Import(AllowDefault = true)]
        private ILoggingServiceInternal Logger;

        // Used for legacy telemetry
        [Import(AllowDefault = true)]
        private ITextDocumentFactoryService TextDocumentFactoryService;

        private IList<Lazy<ICompletionPresenterProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> _orderedPresenterProviders;
        private IList<Lazy<ICompletionPresenterProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> OrderedPresenterProviders
            => _orderedPresenterProviders ?? (_orderedPresenterProviders = Orderer.Order(UnorderedPresenterProviders));

        private IList<Lazy<IAsyncCompletionItemSourceProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> _orderedCompletionItemSourceProviders;
        private IList<Lazy<IAsyncCompletionItemSourceProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> OrderedCompletionItemSourceProviders
            => _orderedCompletionItemSourceProviders ?? (_orderedCompletionItemSourceProviders = Orderer.Order(UnorderedCompletionItemSourceProviders));

        private IList<Lazy<IAsyncCompletionServiceProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> _orderedCompletionServiceProviders;
        private IList<Lazy<IAsyncCompletionServiceProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> OrderedCompletionServiceProviders
            => _orderedCompletionServiceProviders ?? (_orderedCompletionServiceProviders = Orderer.Order(UnorderedCompletionServiceProviders));

        private ImmutableDictionary<IContentType, ImmutableSortedSet<char>> _commitCharacters = ImmutableDictionary<IContentType, ImmutableSortedSet<char>>.Empty;
        private ImmutableDictionary<IContentType, ImmutableArray<IAsyncCompletionItemSourceProvider>> _cachedCompletionItemSourceProviders = ImmutableDictionary<IContentType, ImmutableArray<IAsyncCompletionItemSourceProvider>>.Empty;
        private ImmutableDictionary<IContentType, ImmutableArray<IAsyncCompletionServiceProvider>> _cachedCompletionServiceProviders = ImmutableDictionary<IContentType, ImmutableArray<IAsyncCompletionServiceProvider>>.Empty;
        private ImmutableDictionary<IContentType, ICompletionPresenterProvider> _cachedPresenterProviders = ImmutableDictionary<IContentType, ICompletionPresenterProvider>.Empty;
        private bool firstRun = true; // used only for diagnostics
        private bool _firstInvocationReported; // used for "time to code"
        private StableContentTypeComparer _contentTypeComparer;
        private const string IsCompletionAvailableProperty = "IsCompletionAvailable";

        private Dictionary<IContentType, bool> FeatureAvailabilityByContentType = new Dictionary<IContentType, bool>();

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

            featureIsAvailable = UnorderedCompletionItemSourceProviders
                    .Any(n => n.Metadata.ContentTypes.Any(ct => contentType.IsOfType(ct)));
            featureIsAvailable &= UnorderedCompletionServiceProviders
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

            var sourcesWithData = MetadataUtilities<IAsyncCompletionItemSourceProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>.GetBuffersAndImports(textView, triggerLocation, GetCompletionItemSourceProviders);

            // Obtain applicable span, potential commit chars and mapping of source to buffer
            SnapshotSpan? applicableSpan = null;
            var potentialCommitCharsBuilder = ImmutableArray.CreateBuilder<char>();
            var sourcesWithLocations = new List<(IAsyncCompletionItemSource, SnapshotPoint)>();
            foreach (var sourceWithData in sourcesWithData)
            {
                var sourceProvider = GuardedOperations.InstantiateExtension(this, sourceWithData.import);
                var source = GuardedOperations.CallExtensionPoint(
                    errorSource: sourceProvider,
                    call: () => sourceProvider.GetOrCreate(textView),
                    valueOnThrow: null);

                if (source == null)
                    continue;

                var candidateSpan = GuardedOperations.CallExtensionPoint(
                    errorSource: source,
                    call: () =>
                    {
                        potentialCommitCharsBuilder.AddRange(source.GetPotentialCommitCharacters());
                        sourcesWithLocations.Add((source, sourceWithData.point));
                        return source.ShouldTriggerCompletion(typedChar, sourceWithData.point);
                    },
                    valueOnThrow: null);

                // Assume that sources are ordered. If this source is the first one to provide span, map it to the view's top buffer and use it for completion,
                if (applicableSpan == null && candidateSpan.HasValue)
                {
                    var mappingSpan = textView.BufferGraph.CreateMappingSpan(candidateSpan.Value, SpanTrackingMode.EdgeInclusive);
                    applicableSpan = mappingSpan.GetSpans(textView.TextBuffer)[0];
                }
            }

            if (!applicableSpan.HasValue)
                return null;

            if (_contentTypeComparer == null)
                _contentTypeComparer = new StableContentTypeComparer(ContentTypeRegistryService);

            var servicesWithLocations = MetadataUtilities<IAsyncCompletionServiceProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>.GetOrderedBuffersAndImports(textView, triggerLocation, GetServiceProviders, _contentTypeComparer);
            var bestServiceWithData = servicesWithLocations.FirstOrDefault();
            var serviceProvider = GuardedOperations.InstantiateExtension(this, bestServiceWithData.import);
            var service = GuardedOperations.CallExtensionPoint(serviceProvider, () => serviceProvider.GetOrCreate(textView), null);
            if (service == null)
            {
                // This should never happen because we provide a default and IsCompletionFeatureAvailable would have returned false 
                throw new InvalidOperationException("No completion services not found. Completion will be unavailable.");
            }

            var presentationProvidersWithLocations = MetadataUtilities<ICompletionPresenterProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>.GetOrderedBuffersAndImports(textView, triggerLocation, GetPresenters, _contentTypeComparer);
            var bestPresentationProviderWithLocation = presentationProvidersWithLocations.FirstOrDefault();
            var presenterProvider = GuardedOperations.InstantiateExtension(this, bestPresentationProviderWithLocation.import);

            if (firstRun)
            {
                System.Diagnostics.Debug.Assert(presenterProvider != null, $"No instance of {nameof(ICompletionPresenterProvider)} is loaded. Completion will work without the UI.");
                firstRun = false;
            }
            var telemetry = GetOrCreateTelemetry(textView);

            session = new AsyncCompletionSession(applicableSpan.Value, potentialCommitCharsBuilder.ToImmutable(), JoinableTaskContext, presenterProvider, sourcesWithLocations, service, this, textView, telemetry, GuardedOperations);
            textView.Properties.AddProperty(typeof(IAsyncCompletionSession), session);
            textView.Closed += TextView_Closed;

            // Additionally, emulate the legacy completion telemetry
            EmulateLegacyCompletionTelemetry(bestServiceWithData.buffer?.ContentType, textView);

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

        private IReadOnlyList<Lazy<IAsyncCompletionItemSourceProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> GetCompletionItemSourceProviders(IContentType contentType, ITextViewRoleSet textViewRoles)
        {
            return OrderedCompletionItemSourceProviders.Where(n => n.Metadata.ContentTypes.Any(c => contentType.IsOfType(c)) && (n.Metadata.TextViewRoles == null || textViewRoles.ContainsAny(n.Metadata.TextViewRoles))).ToList();
        }
        private IReadOnlyList<Lazy<IAsyncCompletionServiceProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> GetServiceProviders(IContentType contentType, ITextViewRoleSet textViewRoles)
        {
            return OrderedCompletionServiceProviders.Where(n => n.Metadata.ContentTypes.Any(c => contentType.IsOfType(c)) && (n.Metadata.TextViewRoles == null || textViewRoles.ContainsAny(n.Metadata.TextViewRoles))).OrderBy(n => n.Metadata.ContentTypes, _contentTypeComparer).ToList();
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

        internal string GetItemSourceName(IAsyncCompletionItemSource source) => OrderedCompletionItemSourceProviders.FirstOrDefault(n => n.IsValueCreated && n.Value == source)?.Metadata.Name ?? string.Empty;
        internal string GetCompletionServiceName(IAsyncCompletionService service) => OrderedCompletionServiceProviders.FirstOrDefault(n => n.IsValueCreated && n.Value == service)?.Metadata.Name ?? string.Empty;
        internal string GetCompletionPresenterProviderName(ICompletionPresenterProvider provider) => OrderedPresenterProviders.FirstOrDefault(n => n.IsValueCreated && n.Value == provider)?.Metadata.Name ?? string.Empty;

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
