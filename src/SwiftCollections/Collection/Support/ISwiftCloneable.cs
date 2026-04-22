using System.Collections.Generic;

namespace SwiftCollections;

/// <summary>
/// Defines a method that copies all elements from the current instance to a specified collection, replacing its
/// contents with an exact clone of the source.
/// </summary>
/// <remarks>
/// Implementations should ensure that the target collection is cleared before copying elements, so that
/// its contents match the source exactly. 
/// The cloning operation may depend on the semantics of the element type T; 
/// if T is a reference type, the method may perform a shallow or deep copy depending on the implementation.
/// </remarks>
/// <typeparam name="T">The type of elements contained in the collection to be cloned.</typeparam>
public interface ISwiftCloneable<T>
{
    /// <summary>
    /// Clones the entire <see cref="ISwiftCloneable{T}"/> into a new target <see cref="ICollection{T}"/>, 
    /// ensuring that the target list is an exact copy. Clears the target list first 
    /// to match the structure and state of the source list exactly.
    /// </summary>
    void CloneTo(ICollection<T> output);
}