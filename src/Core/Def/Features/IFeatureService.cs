using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.VisualStudio.Utilities.Features
{
    /// <summary>
    /// Service that keeps track of <see cref="IFeatureController"/>'s requests to disable a feature.
    /// When multiple <see cref="IFeatureController"/>s disable a feature and one <see cref="IFeatureController"/>
    /// enables it back, it will not interfere with other disable requests, and feature will ultimately remain disabled.
    /// Features are implemented by exporting <see cref="FeatureDefinition"/> and grouped using <see cref="BaseDefinitionAttribute"/>.
    /// Grouping allows alike features to be disabling at once.
    /// Grouping also relieves <see cref="IFeatureController"/> from changing code when new feature of appropriate category is introduced.
    /// Standard editor feature names are available in <see cref="PredefinedEditorFeatureNames"/>.
    /// </summary>
    /// <example>
    /// // In an exported MEF part:
    /// [Import]
    /// IFeatureService FeatureService;
    /// // Also have a reference to <see cref="IFeatureController"/>:
    /// IFeatureController MyFeatureController;
    /// // Interact with the <see cref="IFeatureService"/>:
    /// FeatureService.Disable(PredefinedEditorFeatureNames.Popup, MyFeatureController);
    /// FeatureService.IsEnabled(PredefinedEditorFeatureNames.Completion); // returns false, because Popup is a base definition of Completion
    /// </example>
    public interface IFeatureService
    {
        /// <summary>
        /// Checks if feature is enabled. By default, every feature is enabled.
        /// </summary>
        /// <param name="featureName">Name of the feature</param>
        /// <returns>False if there are any disable requests. True otherwise.</returns>
        bool IsEnabled(string featureName);

        /// <summary>
        /// Disables a feature.
        /// </summary>
        /// <param name="featureName">Name of the feature to disable</param>
        /// <param name="controller">Object that uniquely identifies the caller. Must be the same as <see cref="IFeatureController"/> passed to <see cref="Enable(string, IFeatureController)"/></param>
        void Disable(string featureName, IFeatureController controller);

        /// <summary>
        /// Cancels the request to disable a feature.
        /// If another <see cref="IFeatureController"/> disabled this feature or its group, the feature remains disabled.
        /// </summary>
        /// <param name="featureName">Name of previously disabled feature</param>
        /// <param name="controller">Object that uniquely identifies the caller. Must be the same as <see cref="IFeatureController"/> passed to <see cref="Disable(string, IFeatureController)"/></param>
        void Enable(string featureName, IFeatureController controller);

        /// <summary>
        /// Gets all <see cref="IFeatureController"/>s that disable a particular feature or one of its base groups.
        /// </summary>
        /// <param name="featureName">Name of a feature</param>
        /// <returns>Enumerations of references to <see cref="IFeatureController"/>s</returns>
        IEnumerable<IFeatureController> GetDisablingControllers(string featureName);
    }
}
