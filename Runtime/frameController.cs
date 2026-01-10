using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using StackExchange.Redis;
using System;
using System.Text;

public class frameController : MonoBehaviour
{
    private enum FrameState
    {
        Running,
        Paused
    }
    
    private FrameState currentState = FrameState.Running;
    
    private int currentStep = 0;
    private int lastPauseStep = 0;
    private int pauseDuration = 100;
    
    private string pythonServerUrl = "http://localhost:8000";
    // For testing with external endpoint, use:
    // private string pythonServerUrl = "https://fefjmoggmwawzhlkwzai667hx3dlbg08h.oast.fun";
    
    // Track screenshots captured during current action
    private int actionStartFrame = 0;
    private List<int> capturedFramesDuringAction = new List<int>();
    
    // Redis configuration
    private ConnectionMultiplexer redis;
    private IDatabase redisDb;
    private string keyPrefix;
    private float screenshotInterval = 0.2f; // 5 fps
    private float lastScreenshotTime = 0f;
    
    // Threading
    private Queue<ScreenshotData> screenshotQueue = new Queue<ScreenshotData>();
    private Thread redisThread;
    private bool isRunning = true;
    
    // Screenshot settings - Balanced resolution for AI vision
    private int screenshotWidth = 640;
    private int screenshotHeight = 480;
    private RenderTexture renderTexture;
    private Texture2D screenshot;
    private Camera mainCamera;
    
    private struct ScreenshotData
    {
        public byte[] rawPixels;  // Raw RGB24 data
        public int frameNumber;
        public int width;
        public int height;
    }

    void Awake()
    {
        Application.runInBackground = true;
        // Prevent this GameObject from being destroyed on scene changes
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        currentState = FrameState.Running;
        keyPrefix = $"screenshot_{Guid.NewGuid().ToString("N").Substring(0, 8)}_";
        
        // Setup camera and render texture
        mainCamera = Camera.main;
        renderTexture = new RenderTexture(screenshotWidth, screenshotHeight, 24);
        screenshot = new Texture2D(screenshotWidth, screenshotHeight, TextureFormat.RGB24, false);
        
        // Connect to Redis
        try
        {
            redis = ConnectionMultiplexer.Connect("127.0.0.1:6379,abortConnect=false");
            redisDb = redis.GetDatabase();
            Debug.Log($"[Redis] Connected. Key prefix: {keyPrefix}");
            
            // Start background thread for Redis operations
            redisThread = new Thread(RedisWorker);
            redisThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Redis] Failed to connect: {e.Message}");
        }
    }

    void Update()
    {
        // Capture screenshots at reduced rate
        if (redisDb != null && redis.IsConnected && Time.time - lastScreenshotTime >= screenshotInterval)
        {
            lastScreenshotTime = Time.time;
            CaptureScreenshotFast();
        }
    }

    void FixedUpdate()
    {
        Debug.Log($"[FrameController] Inside FixedUpdate and current step is {currentStep}");
        if (Time.timeScale > 0 && currentState == FrameState.Paused)
        {
            currentState = FrameState.Running;
        }
        
        if (currentState == FrameState.Paused)
            return;
            
        currentStep++;
        
        if (currentStep - lastPauseStep >= pauseDuration)
        {
            Pause();
        }
    }

    private void Pause()
    {
        Debug.Log($"[FrameController] Pausing at step {currentStep}");
        Time.timeScale = 0;
        currentState = FrameState.Paused;
        int actionEndFrame = Time.frameCount;
        
        // Wait for Redis queue to drain (give it time to write pending screenshots)
        int waitCount = 0;
        while (screenshotQueue.Count > 0 && waitCount < 50)
        {
            Thread.Sleep(10);
            waitCount++;
        }
        
        lastPauseStep = currentStep;
        
        Debug.Log($"[AI] Paused at physics step {currentStep}, frame {actionEndFrame}");
        Debug.Log($"[AI] Captured {capturedFramesDuringAction.Count} screenshots during action");
        
        NotifyPythonServer(actionStartFrame, actionEndFrame);
        Debug.Log($"[FrameController] Notified Python server");
    }

    private void NotifyPythonServer(int startFrame, int endFrame)
    {
        // Get list of captured frames (already in chronological order)
        List<int> frames;
        lock (capturedFramesDuringAction)
        {
            frames = new List<int>(capturedFramesDuringAction);
        }
        
        if (frames.Count == 0)
        {
            Debug.LogWarning("[AI] No screenshots captured during this action!");
        }
        
        // Sort frames to ensure chronological order
        frames.Sort();
        int startFrameKey = -1;
        int endFrameKey = -1;
        
        if (frames.Count > 0) {
            startFrameKey = frames[0];
            endFrameKey = frames[frames.Count - 1];
        }
        
        // Get first and last screenshot keys
        string startScreenshotKey = $"{keyPrefix}{startFrameKey}";
        string endScreenshotKey = $"{keyPrefix}{endFrameKey}";
        
        Debug.Log($"[AI] Start screenshot: {startScreenshotKey}");
        Debug.Log($"[AI] End screenshot: {endScreenshotKey}");
        
        // Build available_frames JSON array
        string availableFramesJson = "[" + string.Join(",", frames) + "]";
        
        // Build JSON payload
        string jsonPayload = $@"{{
            ""current_step"": {currentStep},
            ""start_frame"": {startFrame},
            ""end_frame"": {endFrame},
            ""start_screenshot"": ""{startScreenshotKey}"",
            ""end_screenshot"": ""{endScreenshotKey}"",
            ""key_prefix"": ""{keyPrefix}"",
            ""available_frames"": {availableFramesJson}
        }}";
        
        Debug.Log($"[AI] Sending POST to {pythonServerUrl}/ai/on-pause");
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
                Debug.Log($"[AI] ✅ POST successful: {request.downloadHandler.text}");
            }
            else
            {
                Debug.LogError($"[AI] ❌ POST failed: {request.error}");
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
    
    // FAST screenshot capture - only main thread operations here
    private void CaptureScreenshotFast()
    {
        if (mainCamera == null) return;
        
        try
        {
            // Render to small RenderTexture
            RenderTexture currentRT = RenderTexture.active;
            mainCamera.targetTexture = renderTexture;
            mainCamera.Render();
            
            // Read pixels from RenderTexture
            RenderTexture.active = renderTexture;
            screenshot.ReadPixels(new Rect(0, 0, screenshotWidth, screenshotHeight), 0, 0);
            screenshot.Apply();
            
            // Restore camera
            mainCamera.targetTexture = null;
            RenderTexture.active = currentRT;
            
            // Get RAW pixel data (NO ENCODING - this is the key!)
            // This is very fast - just a memory copy
            byte[] rawPixels = screenshot.GetRawTextureData();
            
            // Queue for background thread to handle Redis
            lock (screenshotQueue)
            {
                screenshotQueue.Enqueue(new ScreenshotData 
                { 
                    rawPixels = rawPixels,
                    frameNumber = Time.frameCount,
                    width = screenshotWidth,
                    height = screenshotHeight
                });
                
                // Prevent queue from growing too large if Redis is slow
                if (screenshotQueue.Count > 30)
                {
                    screenshotQueue.Dequeue(); // Drop oldest frame
                    Debug.LogWarning("[Redis] Queue overflow, dropping frame");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Capture] Failed: {e.Message}");
        }
    }
    
    // Background thread - handles ALL Redis operations
    private void RedisWorker()
    {
        Debug.Log("[Redis] Worker thread started");
        
        while (isRunning)
        {
            ScreenshotData data = default;
            bool hasData = false;
            
            // Get data from queue
            lock (screenshotQueue)
            {
                if (screenshotQueue.Count > 0)
                {
                    data = screenshotQueue.Dequeue();
                    hasData = true;
                }
            }
            
            if (hasData)
            {
                try
                {
                    // Store RAW pixels directly in Redis
                    // No encoding needed!
                    string redisKey = $"{keyPrefix}{data.frameNumber}";
                    
                    // Store metadata too so Python knows dimensions
                    string metaKey = $"{keyPrefix}{data.frameNumber}_meta";
                    string metadata = $"{data.width}x{data.height}";
                    
                    redisDb.StringSet(redisKey, data.rawPixels, flags: CommandFlags.FireAndForget);
                    redisDb.StringSet(metaKey, metadata, flags: CommandFlags.FireAndForget);
                    
                    // Optional: Set expiration to avoid filling up Redis
                    redisDb.KeyExpire(redisKey, TimeSpan.FromMinutes(5));
                    redisDb.KeyExpire(metaKey, TimeSpan.FromMinutes(5));
                    
                    // Track this frame was successfully written
                    lock (capturedFramesDuringAction)
                    {
                        capturedFramesDuringAction.Add(data.frameNumber);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Redis] Store failed: {e.Message}");
                }
            }
            else
            {
                // Small sleep when queue is empty
                Thread.Sleep(5);
            }
        }
        
        Debug.Log("[Redis] Worker thread stopped");
    }

    public void Resume()
    {
        actionStartFrame = Time.frameCount;
        capturedFramesDuringAction.Clear();
        
        Time.timeScale = 1;
        currentState = FrameState.Running;
        Debug.Log($"[AI] Resumed at step {currentStep}, frame {actionStartFrame}");
    }
    
    public int GetCurrentStep()
    {
        return currentStep;
    }
    
    public int GetCurrentFrame()
    {
        return Time.frameCount;
    }
    
    public void SetPauseDuration(int duration)
    {
        pauseDuration = duration;
    }
    
    void OnDestroy()
    {
        // Stop background thread
        isRunning = false;
        
        if (redisThread != null && redisThread.IsAlive)
        {
            redisThread.Join(2000); // Wait up to 2 seconds
        }
        
        // Cleanup
        if (redis != null)
        {
            redis.Close();
            Debug.Log("[Redis] Connection closed");
        }
        
        if (renderTexture != null)
        {
            renderTexture.Release();
        }
        
        if (screenshot != null)
        {
            Destroy(screenshot);
        }
    }
}
