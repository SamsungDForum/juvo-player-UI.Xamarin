/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using Nito.AsyncEx;
using UI.Common;
using System.Reactive.Subjects;
using JuvoPlayer;
using Nito.AsyncEx.Synchronous;
using Window = ElmSharp.Window;
using JuvoPlayer.Drms;
using Player;

namespace PlayerService
{
    public class PlayerServiceImpl : IPlayerService
    {
        private readonly AsyncContextThread _playerThread = new AsyncContextThread();
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private Window _window;
        private IPlayer _player;

        public TimeSpan Duration => _player?.Duration ?? TimeSpan.Zero;
        public TimeSpan CurrentPosition => _player?.Position ?? TimeSpan.Zero;
        public bool IsSeekingSupported => true;
        public PlayerState State => _player?.State ?? PlayerState.None;
        public string CurrentCueText => null;

        private readonly ReplaySubject<PlayerState> _playerStateReplaySubject = new ReplaySubject<PlayerState>(1);
        private readonly Subject<string> _errorSubject = new Subject<string>();
        private readonly Subject<int> _bufferingSubject = new Subject<int>();
        private IDisposable _playerEventSubscription;

        private readonly CancellationTokenSource _playerServiceCts = new CancellationTokenSource();

        public void Pause()
        {
            Logger.LogEnter();

            _playerThread.Factory.StartNew(async () =>
            {
                try
                {
                    await _player.Pause();
                }
                catch (Exception e)
                {
                    _errorSubject.OnNext($"{e.GetType()} {e.Message}");
                }
            }).Wait(_playerServiceCts.Token);

            Logger.LogExit();
        }

        public Task SeekTo(TimeSpan to)
        {
            Logger.LogEnter();

            Task tsk = _playerThread.Factory.StartNew(async () =>
            {
                try
                {
                    await _player.Seek(to);
                }
                catch (Exception e)
                {
                    _errorSubject.OnNext($"{e.GetType()} {e.Message}");
                }
            }).WaitAndUnwrapException(_playerServiceCts.Token);

            Logger.LogExit();
            return tsk;
        }

        public Task ChangeActiveStream(StreamDescription streamDescription)
        {
            Logger.LogEnter();
            Logger.LogExit();
            return Task.FromResult<object>(null);
        }

        public void DeactivateStream(StreamType streamType)
        {
            Logger.LogEnter();
            Logger.LogExit();
        }

        public List<StreamDescription> GetStreamsDescription(StreamType streamType)
        {
            Logger.LogEnter();
            Logger.LogExit();
            return Enumerable.Empty<StreamDescription>().ToList();
        }

        public async Task SetSourceInternal(ClipDefinition clip)
        {
            try
            {
                Logger.Info("Building player");
                IPlayer player = BuildDashPlayer(clip);

                Logger.Info("Preparing player");
                await player.Prepare();

                _player = player;

                PlayerStatePusher();
                _playerEventSubscription = _player.OnEvent().Subscribe(OnEvent, SynchronizationContext.Current);
            }
            catch (Exception e)
            {
                _errorSubject.OnNext(e.ToString());
            }

            async void PlayerStatePusher()
            {
                Logger.Info("PlayerState pump started");
                try
                {
                    PlayerState current = PlayerState.None;
                    while (!_playerServiceCts.IsCancellationRequested)
                    {
                        PlayerState next = _player.State;
                        if (next == current)
                        {
                            await Task.Delay(200, _playerServiceCts.Token);
                            continue;
                        }

                        _playerStateReplaySubject.OnNext(next);
                        current = next;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Ignore.
                }

                Logger.Info("PlayerState pump stopped. Completing PlayerState observable.");
                _playerStateReplaySubject.OnCompleted();
            }
        }

        public void SetSource(ClipDefinition clip)
        {
            Logger.LogEnter(clip.Url);

            _playerThread.Factory.StartNew(async () =>
            {
                try
                {
                    await SetSourceInternal(clip);
                }
                catch (Exception e)
                {
                    _errorSubject.OnNext($"{e.GetType()} {e.Message}");
                }
            }).Wait(_playerServiceCts.Token);

            Logger.LogExit(clip.Url);
        }

        public void Start()
        {
            Logger.LogEnter();

            _playerThread.Factory.StartNew(() =>
            {
                try
                {
                    PlayerState current = _player.State;
                    switch (_player.State)
                    {
                        case PlayerState.Playing:
                            _player.Pause();
                            break;
                        case PlayerState.Ready:
                        case PlayerState.Paused:
                            _player.Play();
                            break;
                        default:
                            Logger.Warn($"Cannot play/pause in state: {current}");
                            break;
                    }
                }
                catch (Exception e)
                {
                    _errorSubject.OnNext($"{e.GetType()} {e.Message}");
                }
            }).Wait(_playerServiceCts.Token);

            Logger.LogExit();
        }

        public void Stop()
        {
            Logger.LogEnter();

            // This one... we don't won't to abort when cancelling.
            _playerThread.Factory.StartNew(async () => await TerminatePlayer()).Wait();

            Logger.LogExit();
        }

        public void Suspend()
        {
            Logger.LogEnter();
            throw new NotImplementedException();
            Logger.LogExit();
        }

        public Task Resume()
        {
            Logger.LogEnter();
            throw new NotImplementedException();
            Logger.LogExit();
        }

        public IObservable<PlayerState> StateChanged()
        {
            return _playerStateReplaySubject
                .Publish()
                .RefCount();
        }

        public IObservable<string> PlaybackError()
        {
            return _errorSubject
                .Publish()
                .RefCount();
        }

        public IObservable<int> BufferingProgress()
        {
            return _bufferingSubject
                .Publish()
                .RefCount();
        }

        public void SetWindow(Window window)
        {
            _window = window;
        }

        private IPlayer BuildDashPlayer(ClipDefinition clip, Configuration configuration = default)
        {
            DashPlayerBuilder builder = new DashPlayerBuilder()
                .SetWindow(new JuvoPlayer.Platforms.Tizen.Window(_window))
                .SetMpdUri(clip.Url)
                .SetConfiguration(configuration);

            if (clip.DRMDatas != null)
            {
                DrmDescription drmInfo = clip.DRMDatas.FirstOrDefault();
                builder = builder
                    .SetKeySystem(SchemeToKeySystem(drmInfo.Scheme))
                    .SetDrmSessionHandler(new YoutubeDrmSessionHandler(
                        drmInfo.LicenceUrl,
                        drmInfo.KeyRequestProperties));
            }

            return builder.Build();

            string SchemeToKeySystem(in string scheme)
            {
                switch (scheme)
                {
                    case "playready":
                        return "com.microsoft.playready";
                    case "widevine":
                        return "com.widevine.alpha";
                    default:
                        return scheme;
                }
            }
        }

        private async Task TerminatePlayer()
        {
            Logger.Info();
            _playerServiceCts.Cancel();
            _playerEventSubscription?.Dispose();

            if (_player != null)
            {
                Logger.Info("Disposing player");
                try
                {
                    await _player.DisposeAsync();
                }
                catch (Exception e)
                {
                    Logger.Warn($"Ignoring exception: {e}");
                }
                _player = null;
            }
        }

        private void OnEvent(IEvent ev)
        {
            Logger.Info(ev.ToString());

            switch (ev)
            {
                case EosEvent eos:
                    // EOS will arrive before content end.
                    break;
                case BufferingEvent buf:
                    _bufferingSubject.OnNext(buf.IsBuffering ? 0 : 100);
                    break;
            }

        }

        public void Dispose()
        {
            Logger.Info();
            Stop();

            _errorSubject.OnCompleted();
            _errorSubject.Dispose();
            _playerStateReplaySubject.OnCompleted();
            _playerStateReplaySubject.Dispose();
            _playerServiceCts.Dispose();

            _playerThread.Join();
        }
    }
}

