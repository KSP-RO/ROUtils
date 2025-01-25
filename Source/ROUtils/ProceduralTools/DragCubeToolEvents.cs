using UnityEngine.SceneManagement;

namespace ROUtils
{
    internal class DragCubeToolEvents : HostedSingleton
    {
        public DragCubeToolEvents(SingletonHost host) : base(host)
        {
        }

        public override void Awake()
        {
            SceneManager.sceneLoaded += SceneLoaded;
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            DragCubeTool.ClearStaticState();
        }
    }
}
