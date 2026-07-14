using UnityEngine;

namespace MochiPark.Runtime.Views
{
    public static class UIInstanceResolver
    {
        public static T GetOrCreate<T>(
            ref T cachedInstance,
            GameObject prefab)
            where T : Component
        {
            if (cachedInstance != null)
            {
                return cachedInstance;
            }

            T[] sceneInstances = Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (sceneInstances.Length > 0)
            {
                cachedInstance = sceneInstances[0];
                ActivateInstance(cachedInstance);
                if (sceneInstances.Length > 1)
                {
                    Debug.LogWarning(
                        $"Found {sceneInstances.Length} instances of {typeof(T).Name}; using '{cachedInstance.name}' and avoiding a duplicate UI prefab spawn.",
                        cachedInstance);
                }

                return cachedInstance;
            }

            if (prefab == null)
            {
                Debug.LogError($"Cannot create {typeof(T).Name} because the configured UI prefab is null.");
                return null;
            }

            T prefabComponent = prefab.GetComponentInChildren<T>(true);
            if (prefabComponent == null)
            {
                Debug.LogError(
                    $"Cannot create {typeof(T).Name} because UI prefab '{prefab.name}' does not contain that component.",
                    prefab);
                return null;
            }

            GameObject instance = Object.Instantiate(prefab);
            instance.name = prefab.name;
            instance.SetActive(true);
            cachedInstance = instance.GetComponentInChildren<T>(true);
            if (cachedInstance == null)
            {
                Debug.LogError(
                    $"Instantiated UI prefab '{prefab.name}', but the instance does not contain {typeof(T).Name}.",
                    instance);
                Object.Destroy(instance);
            }

            ActivateInstance(cachedInstance);
            return cachedInstance;
        }

        private static void ActivateInstance<T>(T instance)
            where T : Component
        {
            if (instance != null)
            {
                instance.gameObject.SetActive(true);
            }
        }
    }
}
