using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;

using Rock.Data;
using Rock.Model;

namespace com.shepherdchurch.ImageCashLetter.Model
{
    [Table( "_com_shepherdchurch_ImageCashLetter_FileFormat" )]
    [DataContract]
    public class ImageCashLetterFileFormat : Model<ImageCashLetterFileFormat>, IRockEntity
    {
        [MaxLength( 100 )]
        [Required( ErrorMessage = "Name is required" )]
        [DataMember( IsRequired = true )]
        public string Name { get; set; }

        [DataMember]
        public string Description { get; set; }

        [DataMember]
        public int? EntityTypeId { get; set; }

        [DataMember]
        public bool IsActive { get; set; }

        [DataMember]
        public string FileNameTemplate { get; set; }

        #region Virtual Properties

        [LavaInclude]
        public virtual EntityType EntityType { get; set; }

        #endregion
    }

    #region Entity Configuration

    /// <summary>
    /// Campus Configuration class.
    /// </summary>
    public partial class ImageCashLetterFileFormatConfiguration : EntityTypeConfiguration<ImageCashLetterFileFormat>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageCashLetterFileFormat"/> class.
        /// </summary>
        public ImageCashLetterFileFormatConfiguration()
        {
            this.HasRequired( f => f.EntityType )
                .WithMany()
                .HasForeignKey( f => f.EntityTypeId )
                .WillCascadeOnDelete( false );

            this.HasEntitySetName( "ImageCashLetterFileFormat" );
        }
    }

    #endregion
}
