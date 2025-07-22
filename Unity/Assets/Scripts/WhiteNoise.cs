using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]

public class WhiteNoise : MonoBehaviour
{
    [Range(0f, 1f)]
    public float volume = 0.1f;
    public bool playOnStart = true;
    public int sampleRate = 44100;
    public float durationSeconds = 10000f;

    private AudioSource audioSource;
    private AudioClip noiseClip;
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.volume = volume;
        audioSource.playOnAwake = false;
        GenerateWhiteNoise();
        if (playOnStart)
            audioSource.Play();
    }

    private void OnValidate()
    {
        if (audioSource != null)
            audioSource.volume = volume;
    }

    void GenerateWhiteNoise()
    {
        int samples = Mathf.CeilToInt(sampleRate * durationSeconds);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
            data[i] = Random.Range(-1f, 1f);
        noiseClip = AudioClip.Create("WhiteNoise", samples, 1, sampleRate, false);
        noiseClip.SetData(data, 0);
        audioSource.clip = noiseClip;
    }

    public void PlayNoise() { audioSource.Play(); }
    public void StopNoise() { audioSource.Stop(); }

}
