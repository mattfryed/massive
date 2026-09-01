using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    public class PlayerVFXHub : MonoBehaviour
    {
        [SerializeField] private Transform modulesRoot;

        private readonly Dictionary<Type, Component> _cache = new();

        private void Awake()
        {
            if (!modulesRoot)
            {
                var go = new GameObject("VFXModules");
                go.transform.SetParent(transform, false);
                modulesRoot = go.transform;
            }
        }

        public T GetOrCreate<T>(GameObject prefab = null) where T : Component
        {
            var key = typeof(T);
            if (_cache.TryGetValue(key, out var existing) && existing)
                return (T)existing;

            T module;

            if (prefab != null)
            {
                var inst = Instantiate(prefab, modulesRoot);
                module = inst.GetComponent<T>();
                if (!module) module = inst.AddComponent<T>();
            }
            else
            {
                var go = new GameObject(key.Name);
                go.transform.SetParent(modulesRoot, false);
                module = go.AddComponent<T>();
            }

            _cache[key] = module;
            return module;
        }

        public bool TryGet<T>(out T module) where T : Component
        {
            if (_cache.TryGetValue(typeof(T), out var c) && c)
            {
                module = (T)c;
                return true;
            }
            module = null;
            return false;
        }

        public void Disable<T>() where T : Component
        {
            if (_cache.TryGetValue(typeof(T), out var c) && c)
                c.gameObject.SetActive(false);
        }

        public void DestroyModule<T>() where T : Component
        {
            if (_cache.TryGetValue(typeof(T), out var c) && c)
                Destroy(c.gameObject);

            _cache.Remove(typeof(T));
        }
    }
}
