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
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;
using UI.Common;
using UI.Common.Logger;

namespace PlayerService
{
    internal class PlayerHarness : IDisposable
    {
        private IPlayer _player;
        private IDisposable _playerEventSubscription;

        public event Action<IEvent> EventHandler;

        private static string SchemeToKeySystem(string scheme)
        {
            using (Log.Scope())
            {
                switch (scheme)
                {
                    case "playready":
                        return "com.microsoft.playready";
                    case "widevine":
                        return "com.widevine.alpha";
                    case null:
                        return null;
                    default:
                        Log.Warn($"No conversion defined for scheme {scheme}");
                        return scheme;
                }
            }
        }

        private async Task PrepareWithCancellation()
        {
            using (Log.Scope($"Have player: {_player != default}"))
            {
                using (var cts = new CancellationTokenSource())
                {
                    var errorTask = _player
                        .OnEvent()
                        .FirstAsync(ev => ev is ExceptionEvent)
                        .Select(ev => (ev as ExceptionEvent).Exception)
                        .ToTask(cts.Token);
                    var prepareTask = _player.Prepare();
                    var firstCompleted = await Task.WhenAny(errorTask, prepareTask);
                    if (firstCompleted == errorTask)
                    {
                        Log.Error("Prepare completed with error: " + errorTask.Result);
                        cts.Cancel();
                        throw errorTask.Result;
                    }

                    Log.Info("Prepare completed without errors");
                }
            }
        }

        private void SubscribePlayerEvents()
        {
            using (Log.Scope())
                _playerEventSubscription = _player.OnEvent().Subscribe(OnEvent);
        }

        private void UnsubscribePlayerEvents()
        {
            using (Log.Scope($"Have subscription: {_playerEventSubscription != default}"))
            {
                _playerEventSubscription?.Dispose();
                _playerEventSubscription = default;
                EventHandler = default;
            }
        }

        private void OnEvent(IEvent ev)
        {
            using (Log.Scope(ev.ToString()))
                EventHandler?.Invoke(ev);
        }

        private static Task<IPlayer> BuildDashPlayer(IWindow window,
            string url,
            DrmDescription drmInfo = default,
            Configuration configuration = default)
        {
            using (Log.Scope(url))
            {
                try
                {
                    return Task.FromResult(
                        new DashPlayerBuilder()
                            .SetWindow(window)
                            .SetMpdUri(url)
                            .SetConfiguration(configuration)
                            .SetKeySystem(SchemeToKeySystem(drmInfo?.Scheme))
                            .SetDrmSessionHandler(
                                drmInfo == default
                                    ? default
                                    : new YoutubeDrmSessionHandler(
                                        drmInfo.LicenceUrl,
                                        drmInfo.KeyRequestProperties))
                            .Build());
                }
                catch (Exception e)
                {
                    return Task.FromException<IPlayer>(e);
                }
            }
        }

        public TimeSpan Duration => _player?.Duration ?? default;
        public TimeSpan CurrentPosition => _player?.Position ?? default;
        public PlayerState State => _player?.State ?? default;
        public bool IsSeekingSupported => true;

        public async Task SeekTo(TimeSpan seekTo)
        {
            using (Log.Scope(seekTo.ToString()))
                await _player.Seek(seekTo);
        }

        public Task Play()
        {
            PlayerState currentState;
            using (Log.Scope((currentState = _player.State).ToString()))
            {
                if (currentState == PlayerState.Ready || currentState == PlayerState.Paused)
                {
                    try
                    {
                        _player.Play();
                    }
                    catch (Exception e)
                    {
                        return Task.FromException(e);
                    }
                }
                else
                {
                    Log.Warn("Unexpected player state. IPlayer.Play() not called");
                }

                return Task.CompletedTask;
            }
        }

        public async Task Pause()
        {
            PlayerState currentState;
            using (Log.Scope((currentState = _player.State).ToString()))
            {
                if (currentState == PlayerState.Playing)
                {
                    await _player.Pause();
                }
                else
                {
                    Log.Warn("Unexpected player state. IPlayer.Pause() not called");
                }
            }
        }

        public static async Task<PlayerHarness> Create(IWindow window,
            string url,
            DrmDescription drmInfo = default,
            Configuration configuration = default)
        {
            using (Log.Scope(url))
            {
                var harness = new PlayerHarness();
                harness._player = await BuildDashPlayer(window, url, drmInfo, configuration);
                return harness;
            }
        }

        public async Task Prepare(TimeSpan timeIndex = default)
        {
            using (Log.Scope(timeIndex.ToString()))
            {
                SubscribePlayerEvents();

                await PrepareWithCancellation();
                if (timeIndex != default)
                    await SeekTo(timeIndex);
            }
        }

        public async Task Stop()
        {
            using (Log.Scope($"Have player: {_player != default}"))
            {
                UnsubscribePlayerEvents();

                if (_player != default)
                {
                    try
                    {
                        await _player.DisposeAsync();
                    }
                    catch (Exception e)
                    {
                        Log.Warn($"Ignoring IPlayer.DisposeAsync() exception: {e}");
                    }

                    _player = default;
                }
            }
        }

        public Task<IEnumerable<StreamDescription>> GetAvailableTracks()
        {
            using (Log.Scope())
            {
                try
                {
                    return Task.FromResult(_player.GetSelectedStreamGroups().ToStreamDescription());
                }
                catch (Exception e)
                {
                    return Task.FromException<IEnumerable<StreamDescription>>(e);
                }
            }
        }

        public async Task SetActiveTrack(StreamDescription track)
        {
            using (Log.Scope())
            {
                var (groups, selectors) = _player.GetSelectedStreamGroups();
                selectors[track.GroupIndex] = track.GetSelector();
                await _player.SetStreamGroups(groups, selectors);
            }
        }

        public void Dispose()
        {
            async void StopAction() => await Stop();

            using (Log.Scope())
                new Task(StopAction).RunSynchronously();
        }
    }
}