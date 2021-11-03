/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2021, Samsung Electronics Co., Ltd
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
using System.Threading.Tasks;
using Nito.AsyncEx;
using UI.Common;
using UI.Common.Logger;
using System.Reactive.Subjects;
using System.Threading;
using JuvoPlayer.Common;
using Window = ElmSharp.Window;

namespace PlayerService
{
    public class PlayerServiceImpl : IPlayerService
    {
        private const int ReplayEventBufferSize = 1;

        private readonly AsyncContextThread _playerThread;
        private readonly ReplaySubject<string> _errorSubject;
        private readonly ReplaySubject<PlayerState> _stateSubject;
        private readonly ReplaySubject<int> _bufferingSubject;
        private PlayerState _publishedState;
        private PlayerHarness _player;

        private Window _window;
        private ClipDefinition _currentClip;
        private TimeSpan _suspendTimeIndex;

        private void PublishPlayerState()
        {
            using (Log.Scope($"Current: {_publishedState} -> Next: {_publishedState = _player.State}"))
                _stateSubject.OnNext(_publishedState);
        }

        private void OnEvent(JuvoPlayer.Common.IEvent ev)
        {
            using (Log.Scope(ev.ToString()))
            {
                switch (ev)
                {
                    case EosEvent _:
                        Log.Info($"PlayerState: {State}@[{CurrentPosition} -> {Duration}]");
                        _ = Stop();
                        break;

                    case BufferingEvent buf:
                        Log.Info($"IsBuffering: {buf.IsBuffering}");
                        _bufferingSubject?.OnNext(buf.IsBuffering ? 0 : 100);
                        break;

                    case ExceptionEvent ex:
                        var errMsg = $"{ex.Exception.Message} {ex.Exception.InnerException?.Message}";
                        Log.Error(errMsg);
                        _errorSubject.OnNext(errMsg);
                        break;
                }
            }
        }

        private static async Task<PlayerHarness> CreateAndPrepare(Window window, ClipDefinition clip, Action<IEvent> handler, TimeSpan timeIndex = default)
        {
            using (Log.Scope(clip.Type + " " + clip.Title))
            {
                PlayerHarness player = default;
                try
                {
                    player = await PlayerHarness.Create(
                        new JuvoPlayer.Platforms.Tizen.ElmSharpWindow(window),
                        clip.Url,
                        clip.DRMDatas?.FirstOrDefault(),
                        new Configuration { StartTime = timeIndex });

                    player.EventHandler += handler;
                    await player.Prepare();
                    return player;
                }
                catch (Exception)
                    when (player != default)
                {
                    player.EventHandler -= handler;
                    player.Dispose();
                    throw;
                }
            }
        }

        private static readonly WaitCallback CompleteStateSubjectCb = state =>
        {
            using (Log.Scope())
            {
                // On _stateSubject completion, Xamarin UI starts a cascade of closures and shutdowns.
                ((PlayerServiceImpl)state)._stateSubject.OnCompleted();
            }
        };

        private void TerminateThread()
        {
            using (Log.Scope())
                _playerThread.Join();
        }

        public void Dispose()
        {
            async void SuspendAction() => await Suspend();

            using (Log.Scope())
            {
                new Task(SuspendAction).RunSynchronously();

                Log.Info("Completing subjects");
                _errorSubject.OnCompleted();
                // _stateSubject is completed in Stop().
                _bufferingSubject.OnCompleted();

                Log.Info("Disposing subjects");
                _errorSubject.Dispose();
                _stateSubject.Dispose();
                _bufferingSubject.Dispose();

                new Task(TerminateThread).RunSynchronously();
            }
        }

        public PlayerServiceImpl()
        {
            using (Log.Scope())
            {
                _playerThread = new AsyncContextThread();
                _errorSubject = new ReplaySubject<string>(ReplayEventBufferSize);
                _stateSubject = new ReplaySubject<PlayerState>(ReplayEventBufferSize);
                _bufferingSubject = new ReplaySubject<int>(ReplayEventBufferSize);
            }
        }

        public TimeSpan Duration => _player?.Duration ?? default;
        public TimeSpan CurrentPosition => _player?.CurrentPosition ?? default;
        public bool IsSeekingSupported => _player?.IsSeekingSupported ?? false;
        public PlayerState State => _publishedState;
        public string CurrentCueText => string.Empty;

        public async Task Pause()
        {
            using (Log.Scope())
            {
                await await _playerThread.ThreadJob(_player.Pause).ReportException(_errorSubject.OnNext);
                PublishPlayerState();
            }
        }

        public async Task SeekTo(TimeSpan to)
        {
            using (Log.Scope())
                await await _playerThread.ThreadJob(() => _player.SeekTo(to)).ReportException(_errorSubject.OnNext);
        }

        public async Task ChangeActiveStream(StreamDescription streamDescription)
        {
            using (Log.Scope(streamDescription.ToString()))
                await await _playerThread.ThreadJob(() => _player.SetActiveTrack(streamDescription)).ReportException(_errorSubject.OnNext);
        }

        public void DeactivateStream(StreamType streamType)
        {
            using (Log.Scope(streamType.ToString()))
                throw new NotImplementedException();
        }

        public async Task<List<StreamDescription>> GetStreamsDescription(StreamType streamType)
        {
            using (Log.Scope(streamType.ToString()))
            {
                var allTracks = await await _playerThread.ThreadJob(_player.GetAvailableTracks)
                    .ReportException(_errorSubject.OnNext);

                return allTracks.Where(entry => entry.StreamType == streamType).DumpStreamDescriptions(streamType.ToString()).ToList();
            }
        }

        public async Task SetSource(ClipDefinition clip)
        {
            using (Log.Scope())
            {
                _currentClip = clip;

                // Late assign started player. Xamarin UI is overly eager peeking at clip info.
                // If that happens prior to start completion... they'll be shroom clouds!
                _player = await await _playerThread
                    .ThreadJob(async () => await CreateAndPrepare(_window, clip, OnEvent))
                    .ReportException(_errorSubject.OnNext);

                PublishPlayerState();
            }
        }

        public async Task Start()
        {
            using (Log.Scope())
            {
                await await _playerThread.ThreadJob(_player.Play).ReportException(_errorSubject.OnNext);
                PublishPlayerState();
            }
        }

        public Task Stop()
        {
            using (Log.Scope())
            {
                // Escape from calling context.
                ThreadPool.UnsafeQueueUserWorkItem(CompleteStateSubjectCb, this);

                return Task.CompletedTask;
            }
        }

        public async Task Suspend()
        {
            using (Log.Scope(_player == default ? "No player" : string.Empty))
            {
                if (_player == default)
                    return;

                _suspendTimeIndex = CurrentPosition;
                Log.Info($"Suspend time index {_suspendTimeIndex}");

                await _playerThread.ThreadJob(_player.Dispose).ReportException(_errorSubject.OnNext);

                _player = default;
            }
        }

        public async Task Resume()
        {
            using (Log.Scope(_player != default ? "Unexpected player" : string.Empty))
            {
                _player = await await _playerThread.ThreadJob(
                    async () =>
                    {
                        var player = await CreateAndPrepare(_window, _currentClip, OnEvent, _suspendTimeIndex);
                        await player.Play();
                        return player;
                    }).ReportException(_errorSubject.OnNext);
            }
        }

        public IObservable<PlayerState> StateChanged()
        {
            using (Log.Scope())
                return _stateSubject
                    .Publish()
                    .RefCount();
        }

        public IObservable<string> PlaybackError()
        {
            using (Log.Scope())
                return _errorSubject
                    .Publish()
                    .RefCount();
        }

        public IObservable<int> BufferingProgress()
        {
            using (Log.Scope())
                return _bufferingSubject
                    .Publish()
                    .RefCount();
        }

        public void SetWindow(Window window)
        {
            using (Log.Scope(window.ToString()))
                _window = window;
        }
    }
}