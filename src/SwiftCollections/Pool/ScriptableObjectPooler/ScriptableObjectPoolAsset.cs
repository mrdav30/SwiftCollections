using System.Collections.Generic;
using UnityEngine;

namespace SwiftCollections.Pool
{
    [CreateAssetMenu(menuName = "Utilities/ScriptableObjectPooler")]
    public class ScriptableObjectPoolAsset : ScriptableObject
    {
        public ScriptableObjectPool[] pools;
        [System.NonSerialized]
        Dictionary<string, ScriptableObjectPool> poolDict = new Dictionary<string, ScriptableObjectPool>();
        [System.NonSerialized]
        public Transform parentTransform;

        public void Init()
        {
            parentTransform = new GameObject("Scriptable Object Pool").transform;

            for (int i = 0; i < pools.Length; i++)
            {
                poolDict.Add(pools[i].poolName, pools[i]);

                if (pools[i].prewarm)
                {
                    pools[i].PrewarmObject(parentTransform);
                }
            }
        }

        public GameObject GetObject(string id)
        {
            poolDict.TryGetValue(id, out ScriptableObjectPool value);

            return value.GetObject(parentTransform);
        }
    }
}

