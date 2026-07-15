using System.Collections;
using System.IO;
using UnityEngine;

namespace MissileDisaster.Game.Audio
{
    /// <summary>
    /// Sounds/*.mp3 を WWW で読み込んで SoundLibrary に登録するコルーチンホスト（メインスレッド）。
    /// DontDestroyOnLoad の隠し GameObject に付与され、1回だけ全ファイルを読み込む。
    /// MP3 のランタイムデコードは WWW.GetAudioClip(AudioType.MPEG) を使う。
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
                // CS の Unity 5.6 は実行時MP3デコード非対応(AudioType.MPEGがnull)のため WAV を読む。
                string path = Path.Combine(folder, name + ".wav");
                if (!File.Exists(path))
                {
                    ModConfig.LogError("SoundLoader: ファイルなし " + path);
                    continue;
                }

                string url = "file:///" + path.Replace("\\", "/");
                WWW www = new WWW(url);
                yield return www;

                if (!string.IsNullOrEmpty(www.error))
                {
                    ModConfig.LogError("SoundLoader: 読込失敗 " + name + " : " + www.error);
                    continue;
                }

                AudioClip clip = null;
                try { clip = www.GetAudioClip(false, false, AudioType.WAV); }
                catch (System.Exception e) { ModConfig.LogError("SoundLoader: デコード失敗 " + name + " : " + e); }
                if (clip == null)
                {
                    ModConfig.LogError("SoundLoader: GetAudioClip が null " + name);
                    continue;
                }

                // 非同期デコードの完了を待つ（最大5秒）。
                float t = 0f;
                while (clip.loadState == AudioDataLoadState.Loading && t < 5f)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (clip.loadState == AudioDataLoadState.Failed)
                {
                    ModConfig.LogError("SoundLoader: ロード状態=Failed " + name);
                    continue;
                }

                clip.name = name;
                SoundLibrary.Register(name, clip);
                ModConfig.Log("SoundLoader: 読込完了 " + name + " (" + clip.length.ToString("0.0") + "s)");
            }
        }
    }
}
