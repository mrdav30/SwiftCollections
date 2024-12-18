using UnityEngine;

namespace SwiftCollections.Pool
{
    public static class ScriptableObjectPooler
    {
        static ScriptableObjectPoolAsset _poolAsset;

        public static ScriptableObjectPoolAsset OPooler
        {
            get
            {
                if(_poolAsset == null)
                {
                    _poolAsset = Resources.Load("ScriptableObjectPooler") as ScriptableObjectPoolAsset;
                    _poolAsset.Init();
                }

                return _poolAsset;
            }
        }

        public static GameObject GetObject(string id)
        {
            return OPooler.GetObject(id);
        }

        public static Transform GetParentTransform()
        {
            return _poolAsset.parentTransform;
        }
    }
}

