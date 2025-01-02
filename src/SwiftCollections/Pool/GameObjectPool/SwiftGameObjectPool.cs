#if UNITY_EDITOR

using System;
using UnityEngine;

namespace SwiftCollections.Pool
{
    /// <summary>
    /// Manages a pool of GameObjects for efficient reuse, reducing memory allocation overhead in Unity.
    /// </summary>
    [Serializable]
    public class SwiftGameObjectPool : IDisposable
    {
        #region Unity Inspector Properties

        /// <summary>
        /// Name of the pool used for identification.
        /// </summary>
        [Tooltip("Name of the pool used for identification.")]
        [SerializeField]
        private string _poolName;
        public string PoolName => _poolName;

        /// <summary>
        /// The prefab to be instantiated and pooled.
        /// </summary>
        [Tooltip("The prefab to be instantiated and pooled.")]
        [SerializeField]
        private GameObject _prefab;
        public GameObject Prefab => _prefab;

        /// <summary>
        /// The maximum number of objects the pool can manage.
        /// </summary>
        [Tooltip("The maximum number of objects the pool can manage.")]
        [SerializeField]
        private int _budget;
        public int Budget => _budget;

        /// <summary>
        /// Whether the pool should be prewarmed (instantiating objects up to the budget at initialization).
        /// </summary>
        [Tooltip("Whether the pool should be prewarmed (instantiating objects up to the budget at initialization).")]
        [SerializeField]
        private bool _prewarm;
        public bool Prewarm => _prewarm;

        #endregion

        [NonSerialized]
        private readonly SwiftList<GameObject> _createdObjects;
        public SwiftList<GameObject> CreatedObjects => _createdObjects;

        [NonSerialized]
        private int _index;

        public SwiftGameObjectPool()
        {
            _createdObjects = new SwiftList<GameObject>();
        }

        public SwiftGameObjectPool(string poolName, GameObject prefab, int budget, bool prewarm = false): this()
        {
            _poolName = poolName;
            _prefab = prefab;
            _budget = budget;
            _prewarm = prewarm;
        }

        /// <summary>
        /// Rents a GameObject from the pool. If the pool is not full, a new object is instantiated.
        /// </summary>
        /// <param name="parent">The parent transform for the GameObject.</param>
        /// <returns>A pooled GameObject instance.</returns>
        public GameObject GetObject(Transform parent)
        {
            if (_prefab == null) ThrowHelper.ThrowInvalidOperationException("Prefab not assigned.");

            GameObject retVal;
            if (_createdObjects.Count < _budget)
            {
                GameObject go = UnityEngine.Object.Instantiate(_prefab);
                go.transform.parent = parent;
                _createdObjects.Add(go);
                retVal = go;
            }
            else
            {
                retVal = _createdObjects[_index];
                _index = (_index + 1) % _createdObjects.Count;
            }

            retVal.SetActive(false);

            return retVal;
        }

        /// <summary>
        /// Prewarms the pool by instantiating objects up to the budget.
        /// </summary>
        /// <param name="parent">The parent transform for the prewarmed objects.</param>
        public void PrewarmObject(Transform parent)
        {
            if (_createdObjects.Count > 0) return; // Prevent duplicate prewarming

            for (int i = 0; i < _budget; i++)
            {
                GameObject go = UnityEngine.Object.Instantiate(_prefab);
                go.SetActive(false);
                go.transform.parent = parent;
                _createdObjects.Add(go);
            }
        }

        /// <summary>
        /// Disposes the pool, destroying all managed GameObjects.
        /// </summary>
        public void Dispose()
        {
            foreach (var obj in _createdObjects)
            {
                if (obj != null)
                    UnityEngine.Object.Destroy(obj);
            }
            _createdObjects.Clear();
        }
    }
}
#endif
