using System.IO;
using UnityEditor;
using UnityEngine;

// Cuts a piece out of an imported sound and saves it as a new .wav:
// - Loop pieces get a crossfaded seam (the bit after the end is blended
//   into the start), so they loop with no click or jump.
// - One-shots (crossfade 0) just get a short fade-out.
// Tools > Audio > Make Game Sound Pieces rebuilds every piece the game uses
// (the train ride, helicopter, truck, siren and horn loops, the jet fly-by and the single gunshot).
public static class AudioLoopSlicer
{
    private const string Src = "Assets/Audio/import/";
    private const string Out = "Assets/Audio/Pieces/";

    [MenuItem("Tools/Audio/Make Game Sound Pieces")]
    public static void MakeAll()
    {
        Directory.CreateDirectory(Out);
        Slice(Src + "freesound_community-inside-train-start-cruise-and-stop-19100.mp3", 100f, 220f, 3f, true, Out + "Train_Cruise_Loop.wav");
        Slice(Src + "u_xg7ssi08yr-jet-plane-fly-by-369631.mp3", 0f, 14f, 0f, true, Out + "Jet_FlyBy.wav"); // one-shot, loud part in the middle
        Slice(Src + "dragon-studio-helicopter-sound-8d-372463.mp3", 3f, 5.8f, 0.4f, true, Out + "Helicopter_Loop.wav");
        Slice(Src + "freesound_community-truck-passing-2-27104.mp3", 5f, 14f, 1f, true, Out + "Truck_Loop.wav");
        Slice(Src + "dragon-studio-police-siren-397963.mp3", 0.6f, 3.4f, 0.3f, true, Out + "Siren_Loop.wav");
        Slice(Src + "scottishperson-sound-effect-machine-gun-333524.mp3", 5.975f, 6.26f, 0f, true, Out + "Gun_Shot.wav");
        Slice(Src + "dragon-studio-car-honk-386166.mp3", 0.2f, 0.7f, 0.08f, true, Out + "Horn_Loop.wav");
        AssetDatabase.Refresh();
        Debug.Log("Audio pieces rebuilt in " + Out);
    }

    // start/end in seconds. crossfade > 0 = seamless loop, 0 = one-shot with a fade-out.
    public static void Slice(string srcPath, float start, float end, float crossfade, bool mono, string outPath)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(srcPath);
        if (clip == null) { Debug.LogError("AudioLoopSlicer: missing " + srcPath); return; }
        clip.LoadAudioData();
        int ch = clip.channels, hz = clip.frequency;
        var all = new float[clip.samples * ch];
        clip.GetData(all, 0);

        int s0 = Mathf.Clamp(Mathf.RoundToInt(start * hz), 0, clip.samples - 1);
        int len = Mathf.Clamp(Mathf.RoundToInt((end - start) * hz), 1, clip.samples - s0);
        int xf = Mathf.Min(Mathf.RoundToInt(crossfade * hz), clip.samples - s0 - len, len / 2);
        int outCh = mono ? 1 : ch;
        var data = new float[len * outCh];

        for (int i = 0; i < len; i++)
        {
            for (int c = 0; c < outCh; c++)
            {
                float v = Sample(all, ch, s0 + i, mono ? -1 : c);
                if (xf > 0 && i < xf)
                {
                    // blend the audio just past the end into the start: the loop seam disappears
                    float t = i / (float)xf;
                    v = v * t + Sample(all, ch, s0 + len + i, mono ? -1 : c) * (1f - t);
                }
                data[i * outCh + c] = v;
            }
        }

        if (crossfade <= 0f)
        {
            int fade = Mathf.Min(len, Mathf.RoundToInt(0.04f * hz)), fin = Mathf.Min(len, Mathf.RoundToInt(0.002f * hz));
            for (int i = 0; i < fade; i++) for (int c = 0; c < outCh; c++) data[(len - 1 - i) * outCh + c] *= i / (float)fade;
            for (int i = 0; i < fin; i++) for (int c = 0; c < outCh; c++) data[i * outCh + c] *= i / (float)fin;
        }

        WriteWav(outPath, data, outCh, hz);
    }

    // c = -1: average of all channels (mono)
    private static float Sample(float[] all, int ch, int frame, int c)
    {
        int i = frame * ch;
        if (i + ch > all.Length) return 0f;
        if (c >= 0) return all[i + c];
        float s = 0f;
        for (int k = 0; k < ch; k++) s += all[i + k];
        return s / ch;
    }

    private static void WriteWav(string path, float[] data, int channels, int hz)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int bytes = data.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)channels);
            w.Write(hz); w.Write(hz * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
            foreach (float f in data) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(f * 32767f), -32768, 32767));
        }
    }
}
