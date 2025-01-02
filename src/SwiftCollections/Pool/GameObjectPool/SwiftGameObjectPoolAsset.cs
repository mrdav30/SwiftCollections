#if UNITY_EDITOR

using System;
using UnityEngine;

namespace SwiftCollections.Pool
{
    /// <summary>
    /// A ScriptableObject that manages multiple <see cref="SwiftGameObjectPool"/> instances.
    /// </summary>
    [CreateAssetMenu(menuName = "Utilities/ScriptableObjectPooler")]
    public class SwiftGameObjectPoolAsset : ScriptableObject, IDisposable
    {
        #region Unity Inspector Properties

        /// <summary>
        /// Array of object pools managed by this asset.
        /// </summary>
        [Tooltip("Array of object pools managed by this asset.")]
        [SerializeField]
        private SwiftGameObjectPool[] _pools;

        #endregion

        [NonSerialized]
        private Transform _parentTransform;
        public Transform ParentTransform => _parentTransform;

        [NonSerialized]
        private SwiftDictionary<string, SwiftGameObjectPool> _poolDict;

        public SwiftGameObjectPoolAsset(SwiftGameObjectPool[] pools)
        {
            _pools = pools;
        }

        /// <summary>
        /// Initializes the object pool asset, creating the parent transform and prewarming pools as needed.
        /// </summary>
        public void Init()
        {
            if (_pools == null || _pools.Length == 0)
                ThrowHelper.ThrowInvalidOperationException("Pools array is not initialized or empty.");

            _parentTransform = new GameObject("Scriptable Object Pool").transform;
            _poolDict = new SwiftDictionary<string, SwiftGameObjectPool>(_pools.Length);

            foreach (var pool in _pools)
            {
                _poolDict.Add(pool.PoolName, pool);

                if (pool.Prewarm)
                    pool.PrewarmObject(_parentTransform);
            }
        }

        /// <summary>
        /// Gets an object from the specified pool by ID.
        /// </summary>
        /// <param name="id">The pool name.</param>
        /// <returns>A pooled GameObject instance.</returns>
        public GameObject GetObject(string id)
        {
            if (_poolDict == null || !_poolDict.TryGetValue(id, out var pool))
                return ThrowHelper.ThrowInvalidOperationException<GameObject>($"Pool with ID '{id}' not found.");

            return pool.GetObject(_parentTransform);
        }

        /// <summary>
        /// Disposes all pools and their resources.
        /// </summary>
        public void Dispose()
        {
            foreach (var pool in _pools)
                pool.Dispose();

            if (_parentTransform != null)
                UnityEngine.Object.Destroy(_parentTransform.gameObject);
        }
    }
}
#endif
