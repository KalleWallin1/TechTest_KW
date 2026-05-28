using UnityEngine;
using UnityEngine.Video;

namespace TechnicalTask
{
    // Drop this on a GameObject that has a VideoPlayer component. Gives you a single-source
    // control over playback and frame scrubbing:
    //
    //   - Play / Pause from the inspector or context menu
    //   - Auto-loop
    //   - Tick "Scrub Mode" to freeze, then drag "Scrub Frame" to a specific video frame
    //
    // Live debug readout shows what the underlying VideoPlayer is actually doing.
    [RequireComponent(typeof(VideoPlayer))]
    public class VideoPlayerScrubber : MonoBehaviour
    {
        [Header("Playback")]
        [SerializeField] private bool  autoPlayOnAwake = true;
        [SerializeField] private bool  loop            = true;
        [SerializeField, Range(0.1f, 4f)] private float playbackSpeed = 1.0f;

        [Header("Scrub — tick to freeze, drag frame to seek")]
        [SerializeField] private bool  scrubMode;
        [Range(0, 250)] [SerializeField] private int scrubFrame;

        [Header("Runtime (read-only)")]
        [SerializeField] private string debugStatus = "uninitialized";
        [SerializeField] private bool   debugIsPrepared;
        [SerializeField] private bool   debugIsPlaying;
        [SerializeField] private long   debugCurrentFrame;
        [SerializeField] private long   debugFrameCount;
        [SerializeField] private double debugTime;

        private VideoPlayer videoPlayer;
        private bool        ready;

        private void Awake()
        {
            videoPlayer = GetComponent<VideoPlayer>();

            bool hasSource =
                (videoPlayer.source == VideoSource.VideoClip && videoPlayer.clip != null) ||
                (videoPlayer.source == VideoSource.Url && !string.IsNullOrEmpty(videoPlayer.url));
            if (!hasSource)
            {
                debugStatus = "no source — set Source/Clip on the VideoPlayer";
                Debug.LogError("VideoPlayerScrubber: VideoPlayer has no clip or URL.", this);
                return;
            }

            videoPlayer.playOnAwake       = false;
            videoPlayer.isLooping         = loop;
            videoPlayer.skipOnDrop        = true;
            videoPlayer.prepareCompleted += OnPrepared;
            videoPlayer.errorReceived    += OnError;
            videoPlayer.Prepare();
            debugStatus = "preparing";
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= OnPrepared;
                videoPlayer.errorReceived    -= OnError;
            }
        }

        private void OnPrepared(VideoPlayer vp)
        {
            ready       = true;
            debugStatus = "ready";
        }

        private void OnError(VideoPlayer vp, string msg)
        {
            debugStatus = "error: " + msg;
            Debug.LogError($"VideoPlayerScrubber: {msg}", this);
        }

        private void Update()
        {
            if (videoPlayer == null) return;

            debugIsPrepared   = videoPlayer.isPrepared;
            debugIsPlaying    = videoPlayer.isPlaying;
            debugCurrentFrame = videoPlayer.frame;
            debugFrameCount   = (long)videoPlayer.frameCount;
            debugTime         = videoPlayer.time;

            if (!ready) return;

            if (videoPlayer.isLooping != loop) videoPlayer.isLooping = loop;

            if (scrubMode)
            {
                if (videoPlayer.isPlaying) videoPlayer.Pause();

                long target  = Mathf.Max(0, scrubFrame);
                long maxF    = (long)videoPlayer.frameCount;
                if (maxF > 0 && target >= maxF) target = maxF - 1;

                if (videoPlayer.frame != target) videoPlayer.frame = target;
            }
            else
            {
                if (Mathf.Abs(videoPlayer.playbackSpeed - playbackSpeed) > 0.001f)
                    videoPlayer.playbackSpeed = playbackSpeed;

                if (!videoPlayer.isPlaying && (autoPlayOnAwake || wasUserPlay))
                {
                    videoPlayer.Play();
                }
            }
        }

        private bool wasUserPlay;

        [ContextMenu("Play")]
        public void Play()
        {
            scrubMode   = false;
            wasUserPlay = true;
            if (videoPlayer != null && ready) videoPlayer.Play();
        }

        [ContextMenu("Pause")]
        public void Pause()
        {
            wasUserPlay = false;
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Pause();
        }

        [ContextMenu("Restart")]
        public void Restart()
        {
            scrubMode   = false;
            wasUserPlay = true;
            if (videoPlayer != null && ready)
            {
                videoPlayer.time = 0;
                videoPlayer.Play();
            }
        }
    }
}
