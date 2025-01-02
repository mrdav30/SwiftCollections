#if UNITY_EDITOR

using System;
using UnityEngine;

namespace SwiftCollections.Pool
{
    /// <summary>
    /// A singleton-based manager for accessing and managing pooled GameObjects through a <see cref="SwiftGameObjectPoolAsset"/>.
    /// </summary>
    public class SwiftGameObjectPoolManager
    {
        /// <summary>
        /// A lazily initialized singleton instance of the pooler.
        /// </summary>
        private static readonly Lazy<SwiftGameObjectPoolManager> _instance =
            new Lazy<SwiftGameObjectPoolManager>(() => new SwiftGameObjectPoolManager());

        /// <summary>
        /// Gets the shared singleton instance of the object pooler.
        /// </summary>
        public static SwiftGameObjectPoolManager Shared => _instance.Value;

        private readonly SwiftGameObjectPoolAsset _poolAsset;

        private SwiftGameObjectPoolManager()
        {
            _poolAsset = Resources.Load<SwiftGameObjectPoolAsset>("SwiftGameObjectPoolAsset");
            if (_poolAsset == null) ThrowHelper.ThrowInvalidOperationException("SwiftGameObjectPoolAsset asset not found in Resources.");
            _poolAsset.Init();
        }

        /// <summary>
        /// Gets a pooled GameObject by ID.
        /// </summary>
        /// <param name="id">The pool name.</param>
        /// <returns>A pooled GameObject instance.</returns>
        public GameObject GetObject(string id)
        {
            return _poolAsset.GetObject(id);
        }

        /// <summary>
        /// Gets the parent transform managing all pooled objects.
        /// </summary>
        /// <returns>The parent transform.</returns>
        public Transform GetParentTransform()
        {
            return _poolAsset.ParentTransform;
        }
    }
}
#endif
