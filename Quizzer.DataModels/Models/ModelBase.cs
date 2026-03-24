using System;
using System.ComponentModel.DataAnnotations;

namespace Quizzer.DataModels.Models
{
    public abstract class ModelBase
    {
        [Key]
        public Guid Id { get; set; }

        public string Designation { get; set; } = string.Empty;

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }

    public interface ICloneWithoutReferences<out TEntity>
    {
        TEntity CloneWithoutReferences(bool copyIdentity = false);
    }

    public abstract class ModelBase<TEntity> : ModelBase, ICloneWithoutReferences<TEntity>
    where TEntity : ModelBase
    {
        public abstract TEntity CloneWithoutReferences(bool copyIdentity = true);

        protected void CopyBaseValuesTo(TEntity target, bool copyIdentity = true)
        {
            if (copyIdentity)
            {
                target.Id = Id;
                target.RowVersion = RowVersion?.ToArray();
            }

            target.Designation = Designation;
        }
    }
}