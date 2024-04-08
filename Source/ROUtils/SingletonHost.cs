using UnityEngine;
using System.Collections.Generic;
using System;

namespace ROUtils
{
    public abstract class HostedSingleton
    {
        protected MonoBehaviour _host = null;
        public MonoBehaviour Host => _host;

        protected static HostedSingleton _instance = null;
        public static HostedSingleton Instance => _instance;

        public HostedSingleton(SingletonHost host)
        {
            _host = host;
            _instance = this;
        }
        
        
        public virtual void Awake() { }

        public virtual void Start() { }

        [Obsolete("Will never actually get destroyed. Remove at a later date.")]
        public virtual void OnDestroy() { }
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class SingletonHost : MonoBehaviour
    {
        private List<HostedSingleton> _singletons = new List<HostedSingleton>();

        public void Awake()
        {
            DontDestroyOnLoad(this);

            Debug.Log("SingletonHost Awake - registering events and running processes");


            List <Type> singletonTypes = KSPUtils.GetAllLoadedTypes<HostedSingleton>();
            foreach (var t in singletonTypes)
            {
                HostedSingleton s = (HostedSingleton)Activator.CreateInstance(t, new System.Object[] { this });
                _singletons.Add(s);
            }
            string logstr = $"Found and added {_singletons.Count} singletons:";
            foreach (var s in singletonTypes)
                logstr += "\n" + s.FullName;
            Debug.Log(logstr);

            foreach (var s in _singletons)
            {
                try
                {
                    s.Awake();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception awaking {s.GetType()}: {e}");
                }
            }
        }

        public void Start()
        {
            foreach (var s in _singletons)
            {
                try
                {
                    s.Start();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception starting {s.GetType()}: {e}");
                }
            }
        }
    }
}
