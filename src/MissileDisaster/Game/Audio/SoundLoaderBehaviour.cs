using System.Collections;
using System.IO;
using UnityEngine;

namespace MissileDisaster.Game.Audio
{
    /// <summary>
    /// Coroutine host that loads the files in Sounds through WWW and registers them with
    /// SoundLibrary. Main thread.
    /// It lives on a hidden DontDestroyOnLoad GameObject and loads every file exactly once.
    /// </summary>
    public class SoundLoaderBehaviour : MonoBehaviour
    {
        private string _modDir;

        public void Begin(string modDir)
        {
            _modDir = modDir;
            StartCoroutine(LoadAll());
        }

        private IEnumerator LoadAll()
        {
            string folder = Path.Combine(_modDir, ModConfig.SoundsFolderName);
            for (int i = 0; i < SoundLibrary.FileNames.Length; i++)
            {
                string name = SoundLibrary.FileNames[i];
                // CS runs Unity 5.6, which cannot decode mp3 at runtime - AudioType.MPEG simply
                // yields null - so these are WAV.
                string path = Path.Combine(folder, name + ".wav");
                if (!File.Exists(path))
                {
                    ModConfig.LogError("SoundLoader: file not found " + path);
                    continue;
                }

                string url = "file:///" + path.Replace("\\", "/");
                WWW www = new WWW(url);
                yield return www;

                if (!string.IsNullOrEmpty(www.error))
                {
                    ModConfig.LogError("SoundLoader: load failed " + name + " : " + www.error);
                    continue;
                }

                AudioClip clip = null;
                try { clip = www.GetAudioClip(false, false, AudioType.WAV); }
                catch (System.Exception e) { ModConfig.LogError("SoundLoader: decode failed " + name + " : " + e); }
                if (clip == null)
                {
                    ModConfig.LogError("SoundLoader: GetAudioClip returned null " + name);
                    continue;
                }

                // Wait for the asynchronous decode to finish, for at most five seconds.
                float t = 0f;
                while (clip.loadState == AudioDataLoadState.Loading && t < 5f)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (clip.loadState == AudioDataLoadState.Failed)
                {
                    ModConfig.LogError("SoundLoader: load state is Failed " + name);
                    continue;
                }

                clip.name = name;
                SoundLibrary.Register(name, clip);
                ModConfig.Log("SoundLoader: loaded " + name + " (" + clip.length.ToString("0.0") + "s)");
            }
        }
    }
}
