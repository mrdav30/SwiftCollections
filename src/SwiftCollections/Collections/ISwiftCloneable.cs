using System.Collections.Generic;

namespace SwiftCollections
{
	public interface ISwiftCloneable<T>
	{
        /// <summary>
        /// Clones the entire <see cref="ISwiftCloneable{T}"/> into a new target <see cref="ICollection{T}"/>, 
        /// ensuring that the target list is an exact copy. Clears the target list first 
        /// to match the structure and state of the source list exactly.
        /// </summary>
        void CloneTo(ICollection<T> output);
    }
}