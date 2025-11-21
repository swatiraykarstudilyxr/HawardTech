//StepsAudioController : MonoBehaviour

using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class StepsAudioController : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip[] stepClips;

    private bool[] hasPlayed;
    private Queue<int> clipQueue = new Queue<int>();
    private bool isPlaying = false;

    private void Start()
    {
        if (stepClips == null || stepClips.Length == 0)
        {
            Debug.LogWarning("Step clips not assigned.");
            return;
        }

        hasPlayed = new bool[stepClips.Length];

        // Play first 4 clips automatically at Start
        int count = Mathf.Min(4, stepClips.Length);
        for (int i = 0; i < count; i++)
        {
            if (stepClips[i] != null)
                EnqueueClip(i); // marks as played and queues clip
        }
    }

    /// <summary>
    /// Call this to play a clip by index. Will only play if not already played.
    /// </summary>
    public void PlayStepSound(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= stepClips.Length)
        {
            Debug.LogWarning("Invalid step index");
            return;
        }

        if (stepClips[stepIndex] == null)
        {
            Debug.LogWarning($"Step clip {stepIndex} is null.");
            return;
        }

        EnqueueClip(stepIndex);
    }

    /// <summary>
    /// Enqueues a clip for playback if it hasn't been played yet.
    /// </summary>
    private void EnqueueClip(int index)
    {
        if (hasPlayed[index])
        {
            Debug.Log($"Step clip {index} already played, skipping.");
            return;
        }

        hasPlayed[index] = true; // mark as played
        clipQueue.Enqueue(index);

        if (!isPlaying)
            StartCoroutine(ProcessQueue());
    }

    /// <summary>
    /// Plays queued clips sequentially.
    /// </summary>
    private IEnumerator ProcessQueue()
    {
        isPlaying = true;

        while (clipQueue.Count > 0)
        {
            int index = clipQueue.Dequeue();
            AudioClip clip = stepClips[index];

            if (clip == null)
                continue;

            audioSource.clip = clip;
            audioSource.Play();

            yield return new WaitForSeconds(clip.length);
        }

        isPlaying = false;
    }
    public float GetClipLength(int index)
    {
        if (stepClips == null || index < 0 || index >= stepClips.Length || stepClips[index] == null)
            return 0f;

        return stepClips[index].length;
    }
}