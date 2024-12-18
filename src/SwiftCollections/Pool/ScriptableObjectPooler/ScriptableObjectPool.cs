using System.Collections.Generic;
using UnityEngine;

namespace SwiftCollections.Pool
{
    [System.Serializable]
    public class ScriptableObjectPool
    {
        public string poolName;
        public GameObject prefab;
        public int budget;
        public bool prewarm;

        [System.NonSerialized]
        List<GameObject> createdObjects = new List<GameObject>();
        [System.NonSerialized]
        int index;
        public GameObject GetObject(Transform parent)
        {
            GameObject retVal;
            if (createdObjects.Count < budget)
            {
                GameObject go = Object.Instantiate(prefab);
                go.transform.parent = parent;
                createdObjects.Add(go);
                retVal = go;
            }
            else
            {
                retVal = createdObjects[index];
                index++;
                if (index > createdObjects.Count - 1)
                {
                    index = 0;
                }
            }

            retVal.SetActive(false);

            return retVal;
        }

        public void PrewarmObject(Transform parent)
        {
            for (int i = 0; i < budget; i++)
            {
                GameObject go = Object.Instantiate(prefab);
                go.SetActive(false);
                go.transform.parent = parent;
                createdObjects.Add(go);
            }
        }
    }
}
