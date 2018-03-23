using System;
using Microsoft.VisualStudio.Core.Imaging;

namespace Microsoft.VisualStudio.Core.Imaging
{
    /// <summary>
    /// Unique identifier for Visual Studio image asset, with extra properties used in accessibility scenarios.
    /// </summary>
    public struct AccessibleImageId
    {
        /// <summary>
        /// Identifier for Visual Studio image asset.
        /// </summary>
        public ImageId ImageId { get; }

        /// <summary>
        /// The <see cref="Guid"/> identifying the group to which this image belongs.
        /// </summary>
        public Guid Guid => ImageId.Guid;

        /// <summary>
        /// The <see cref="int"/> identifying the particular image from the group that this id maps to.
        /// </summary>
        public int Id => ImageId.Id;

        /// <summary>
        /// Localized description of the image
        /// </summary>
        public string AutomationName { get; }

        /// <summary>
        /// Creates a new instance of AccessibleImage
        /// </summary>
        /// <param name="guid">The <see cref="Guid"/> identifying the group to which this image belongs</param>
        /// <param name="id">The <see cref="int"/> identifying the particular image from the group that this id maps to</param>
        /// <param name="automationName">Localized description of the image</param>
        public AccessibleImageId(Guid guid, int id, string automationName)
            : this(new ImageId(guid, id), automationName)
        {
        }

        /// <summary>
        /// Creates a new instance of AccessibleImage
        /// </summary>
        /// <param name="imageId">The <see cref="ImageId"/> identifying the Visual Studio image asset</param>
        /// <param name="automationName">Localized description of the image</param>
        public AccessibleImageId(ImageId imageId, string automationName)
        {
            this.ImageId = imageId;
            AutomationName = automationName;
        }
    }
}
