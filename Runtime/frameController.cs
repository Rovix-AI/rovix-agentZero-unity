using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Text;

public class frameController : MonoBehaviour
{
    private int currentStep = 0;
    private int lastEventStep = 0;
    private int eventInterval = 600; // FixedUpdate steps between events
    
    private string pythonServerUrl = "http://localhost:8000";
    // For testing with external endpoint, use:
    // private string pythonServerUrl = "https://fefjmoggmwawzhlkwzai667hx3dlbg08h.oast.fun";
    
    // State tracking: only send event when actions are executed
    private bool actionsExecuted = true; // Start as true so first event can be sent

    void Awake()
    {
        Application.runInBackground = true;
        // Prevent this GameObject from being destroyed on scene changes
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        actionsExecuted = true; // Start ready to send first event
        Debug.Log("[FrameController] Initialized - ready to send events");
    }

    void FixedUpdate()
    {
        currentStep++;
        
        // Only send event if interval reached AND actions from previous event are executed
        if (currentStep - lastEventStep >= eventInterval && actionsExecuted)
        {
            SendEventToPython();
        }
    }

    private void SendEventToPython()
    {
        // Mark that we're waiting for actions to be executed
        actionsExecuted = false;
        lastEventStep = currentStep;
        
        int currentFrame = Time.frameCount;
        
        // Build simplified JSON payload (no screenshot data)
        string jsonPayload = $@"{{
            ""current_step"": {currentStep},
            ""current_frame"": {currentFrame}
        }}";
        
        Debug.Log($"[AI] Sending event to Python server at step {currentStep}, frame {currentFrame}");
        Debug.Log($"[AI] Payload: {jsonPayload}");
        
        // Start coroutine to send HTTP POST
        StartCoroutine(SendPostRequest(jsonPayload));
    }
    
    private IEnumerator SendPostRequest(string jsonData)
    {
        string url = $"{pythonServerUrl}/ai/on-pause";
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            // Allow insecure HTTP connections for testing
            request.certificateHandler = new AcceptAllCertificatesHandler();
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[AI] ✅ Event sent successfully: {request.downloadHandler.text}");
            }
            else
            {
                Debug.LogError($"[AI] ❌ Event send failed: {request.error}");
                // On failure, mark actions as executed so we can try again next interval
                actionsExecuted = true;
            }
        }
    }
    
    // Certificate handler to allow insecure connections (for testing only!)
    private class AcceptAllCertificatesHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true;
        }
    }
    
    /// <summary>
    /// Called by Python server via AltTester after actions are executed.
    /// This marks actions as executed, allowing the next event to be sent.
    /// </summary>
    public void MarkActionsExecuted()
    {
        actionsExecuted = true;
        Debug.Log($"[AI] Actions marked as executed at step {currentStep}, frame {Time.frameCount}");
    }
    
    public int GetCurrentStep()
    {
        return currentStep;
    }
    
    public int GetCurrentFrame()
    {
        return Time.frameCount;
    }
    
    public void SetEventInterval(int interval)
    {
        eventInterval = interval;
        Debug.Log($"[FrameController] Event interval set to {interval} steps");
    }
    
    void OnDestroy()
    {
        Debug.Log("[FrameController] Destroyed");
    }
}