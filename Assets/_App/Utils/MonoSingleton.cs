using UnityEngine;

namespace _App
{
    public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        [Header("Singleton")] [SerializeField] private bool _isPersistent;

        public static T Instance;

        protected virtual void Awake()
        {
            if (ReferenceEquals(Instance, null))
            {
                Instance = this as T;
            }
            else if (Instance != this as T)
            {
                Destroy(gameObject);

                return;
            }

            if (_isPersistent)
            {
                DontDestroyOnLoad(Instance);
            }
        }

        protected virtual void OnDestroy()
        {
            if (_isPersistent == false)
            {
                Instance = null;
            }
        }
    }
}