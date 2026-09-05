using UnityEngine;
using System;
using System.Collections.Generic;

[System.Serializable]
public class SFXAudioClips
{
    public string soundId;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0f, 1f)] public float pitch = 1f;
}

public class SFXAudioManager : MonoBehaviour
{
    public static SFXAudioManager Instance { get; private set; }

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip defaultButtonClick;
    [SerializeField] private SFXAudioClips[] customButtonSounds;
    private Dictionary<string, SFXAudioClips> soundIdMap;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        soundIdMap = new Dictionary<string, SFXAudioClips>();
        if (customButtonSounds != null)
        {
            foreach (var sound in customButtonSounds)
            {
                if (!string.IsNullOrEmpty(sound.soundId) && sound.clip != null)
                {
                    if (!soundIdMap.ContainsKey(sound.soundId))
                    {
                        soundIdMap[sound.soundId] = sound;
                    }
                    else
                    {
                        Debug.LogWarning($"Duplicate sound ID '{sound.soundId}' found in AudioManager!");
                    }
                }
            }
        }

        if (defaultButtonClick == null)
            defaultButtonClick = ResolveDefaultButtonClick();
    }

    private AudioClip ResolveDefaultButtonClick()
    {
        if (soundIdMap != null && soundIdMap.TryGetValue("TickButton", out var tick) && tick.clip != null)
            return tick.clip;

        if (customButtonSounds == null)
            return null;

        foreach (var sound in customButtonSounds)
        {
            if (sound != null && sound.clip != null)
                return sound.clip;
        }

        return null;
    }

    public void PlaySound(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, volume);
    }

    public void PlayDefaultButtonSound()
    {
        PlaySound(defaultButtonClick);
    }

    public void PlayCustomSound(int soundIndex)
    {
        if (soundIndex >= 0 && soundIndex < customButtonSounds.Length)
        {
            PlaySound(customButtonSounds[soundIndex].clip);
        }
        else
        {
            Debug.LogWarning($"Sound Index '{soundIndex}' not found in AudioManager!");
        }
    }

    public void PlayCustomSound(string soundId)
    {
        if (string.IsNullOrEmpty(soundId)) return;

        if (soundIdMap.TryGetValue(soundId, out SFXAudioClips SfxClip))
        {
            PlaySound(SfxClip.clip, SfxClip.volume, SfxClip.pitch);
        }
        else
        {
            Debug.LogWarning($"Sound ID '{soundId}' not found in AudioManager!");
        }
    }
}