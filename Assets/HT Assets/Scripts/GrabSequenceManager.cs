using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[System.Serializable]
public class SocketAudioSequence
{
    public string socketName;
    public List<int> audioIndices;
}

public class GrabSequenceManager : MonoBehaviour
{
    [Header("UI Settings")]
    public GameObject completionPanel;   // ✅ Assign a UI panel prefab in Inspector

    [Header("Audio Controller")]
    public StepsAudioController stepsAudioController;

    [Header("Audio Step Mapping (Multiple Clips per Socket)")]
    public List<SocketAudioSequence> socketAudioSequences = new List<SocketAudioSequence>();

    [Header("Grabbable Objects (in order)")]
    public List<XRGrabInteractable> grabObjects;

    [Header("Sockets (match order with grabbable objects)")]
    public List<XRSocketInteractor> sockets;

    [Header("Snap Check Settings")]
    public float positionThreshold = 0.01f;
    public float rotationThreshold = 5f;
    public float snapCheckDelay = 0.2f;

    [Header("Reset Settings")]
    public float resetSpeed = 3f;

    private Vector3[] originalPositions;
    private Quaternion[] originalRotations;

    // ✅ Track whether each object has been snapped
    private bool[] objectSnapped;

    void Start()
    {
        originalPositions = new Vector3[grabObjects.Count];
        originalRotations = new Quaternion[grabObjects.Count];
        objectSnapped = new bool[grabObjects.Count];

        for (int i = 0; i < grabObjects.Count; i++)
        {
            originalPositions[i] = grabObjects[i].transform.position;
            originalRotations[i] = grabObjects[i].transform.rotation;

            // Enable only the first object
            bool isFirst = (i == 0);
            var rb = grabObjects[i].GetComponent<Rigidbody>();
            var outline = grabObjects[i].GetComponent<Outline>();

            grabObjects[i].enabled = isFirst;
            if (rb) rb.isKinematic = !isFirst;
            if (outline) outline.enabled = isFirst;

            int indexCopy = i;
            grabObjects[i].selectExited.AddListener((args) => OnObjectReleased(indexCopy, args));
        }

        for (int i = 0; i < sockets.Count; i++)
        {
            int index = i;
            sockets[i].selectEntered.AddListener((args) => OnObjectPlaced(index, args));
        }
    }

    // Called when grabbed object released (even if near socket)
    private void OnObjectReleased(int index, SelectExitEventArgs args)
    {
        StartCoroutine(CheckAfterRelease(index, grabObjects[index]));
    }

    private IEnumerator CheckAfterRelease(int index, XRGrabInteractable releasedObj)
    {
        yield return new WaitForSeconds(0.3f); // wait for socket to catch it

        // ✅ If snapped correctly, do not reset
        if (objectSnapped[index])
        {
            Debug.Log($"✅ {releasedObj.name} snapped - skipping reset");
            yield break;
        }

        // Check if still floating or released away
        bool isInSocket = false;
        foreach (var socket in sockets)
        {
            if (socket.hasSelection && socket.firstInteractableSelected == releasedObj)
            {
                isInSocket = true;
                break;
            }
        }

        if (!isInSocket)
        {
            Debug.Log($"🔁 {releasedObj.name} released away — resetting...");
            StartCoroutine(ResetObjectPosition(index, releasedObj));
        }
    }

    // Called when object successfully enters a socket
    private void OnObjectPlaced(int index, SelectEnterEventArgs args)
    {
        var placedObject = args.interactableObject.transform.GetComponent<XRGrabInteractable>();

        if (placedObject == grabObjects[index])
        {
            objectSnapped[index] = true; // ✅ Mark snapped
            StartCoroutine(VerifyAndProceedAfterDelay(index, placedObject));
        }
        else
        {
            Debug.LogWarning($"❌ Wrong object placed in {sockets[index].name}");
        }
    }

    private IEnumerator VerifyAndProceedAfterDelay(int index, XRGrabInteractable placedObject)
    {
        yield return new WaitForSeconds(snapCheckDelay);

        if (IsSnappedCorrectly(sockets[index], placedObject))
        {
            Debug.Log($"✅ {placedObject.name} snapped correctly in {sockets[index].name}");

            // Play audio sequence
            if (stepsAudioController != null &&
                socketAudioSequences != null &&
                index < socketAudioSequences.Count &&
                socketAudioSequences[index].audioIndices.Count > 0)
            {
                StartCoroutine(PlayAudioSequence(socketAudioSequences[index].audioIndices));
            }

            // Lock and hide outline
            placedObject.enabled = false;
            var rb = placedObject.GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;
            var outline = placedObject.GetComponent<Outline>();
            if (outline) outline.enabled = false;

            // Enable next object
            if (index + 1 < grabObjects.Count)
            {
                var next = grabObjects[index + 1];
                next.enabled = true;
                if (next.GetComponent<Rigidbody>()) next.GetComponent<Rigidbody>().isKinematic = false;
                if (next.GetComponent<Outline>()) next.GetComponent<Outline>().enabled = true;
                Debug.Log($"➡️ Next object enabled: {next.name}");
            }
            else
            {
                Debug.Log("🎉 All objects placed correctly!");
                CheckCompletion();  // ✅ Call UI open check
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ {placedObject.name} not perfectly aligned in {sockets[index].name}");
            objectSnapped[index] = false; // ❌ not snapped anymore
            StartCoroutine(ResetObjectPosition(index, placedObject));
        }
    }

    private bool IsSnappedCorrectly(XRSocketInteractor socket, XRGrabInteractable placedObject)
    {
        Transform attach = socket.attachTransform ? socket.attachTransform : socket.transform;
        float posDiff = Vector3.Distance(placedObject.transform.position, attach.position);
        float rotDiff = Quaternion.Angle(placedObject.transform.rotation, attach.rotation);

        return posDiff <= positionThreshold && rotDiff <= rotationThreshold;
    }

    private IEnumerator ResetObjectPosition(int index, XRGrabInteractable obj)
    {
        var rb = obj.GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;

        Vector3 startPos = obj.transform.position;
        Quaternion startRot = obj.transform.rotation;
        Vector3 targetPos = originalPositions[index];
        Quaternion targetRot = originalRotations[index];

        float elapsed = 0f;
        float duration = 1f / resetSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            obj.transform.position = Vector3.Lerp(startPos, targetPos, t);
            obj.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        obj.transform.position = targetPos;
        obj.transform.rotation = targetRot;

        if (rb) rb.isKinematic = false;
        Debug.Log($"🔁 {obj.name} reset to original position");
    }

    private IEnumerator PlayAudioSequence(List<int> sequence)
    {
        foreach (int clipIndex in sequence)
        {
            if (stepsAudioController != null)
            {
                stepsAudioController.PlayStepSound(clipIndex);
                yield return new WaitForSeconds(stepsAudioController.GetClipLength(clipIndex));
            }
        }
    }
    // Check if all sockets are filled
    private void CheckCompletion()
    {
        foreach (bool snapped in objectSnapped)
        {
            if (!snapped)
                return; // Still missing some
        }

        Debug.Log("🎯 All sockets filled — showing completion UI!");
        if (completionPanel != null)
        {
            completionPanel.SetActive(true);  // ✅ Show panel
        }
    }
    private void OnDestroy()
    {
        foreach (var socket in sockets)
            socket.selectEntered.RemoveAllListeners();
    }
}
