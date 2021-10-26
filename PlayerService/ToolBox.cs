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

using System.Collections.Generic;
using JuvoPlayer.Common;
using UI.Common;
using UI.Common.Logger;

namespace PlayerService
{
    internal static class PlayerServiceToolBox
    {
        public const int ThroughputSelection = -1;
        public const string ThroughputDescription = "Auto";
        public const string ThroughputId = @"\ō͡≡o˞̶";

        public static string ToStringDescription(this Format format)
        {
            string description = string.Empty;

            if (!string.IsNullOrEmpty(format.Language))
                description += format.Language;

            if (format.Width.HasValue && format.Height.HasValue)
                description += " " + format.Width + "x" + format.Height;
            else if (format.ChannelCount.HasValue)
                description += " " + format.ChannelCount + " Ch.";

            if (format.Bitrate.HasValue)
                description += " " + (int)(format.Bitrate / 1000) + " kbps";

            return description.Trim();
        }

        public static IEnumerable<StreamDescription> ToStreamDescription(this (StreamGroup[] groups, IStreamSelector[] selectors) grouping)
        {
            var descriptions = new List<StreamDescription>();
            var (groups, selectors) = grouping;
            var groupCount = groups.Length;

            for (var g = 0; g < groupCount; g++)
            {
                var streams = groups[g].Streams;
                var streamCount = streams.Count;

                if (streamCount > 0)
                {
                    var groupStreamType = groups[g].ContentType.ToStreamType();

                    for (var f = 0; f < streamCount; f++)
                    {
                        descriptions.Add(new StreamDescription
                        {
                            Default = false,
                            FormatIndex = f,
                            GroupIndex = g,
                            Description = streams[f].Format.ToStringDescription(),
                            Id = streams[f].Format.Id,
                            StreamType = groupStreamType
                        });
                    }

                    switch (groupStreamType)
                    {
                        case StreamType.Video when streamCount > 1:
                            // Add 'Auto' option if multiple video streams exist.
                            descriptions.Add(new StreamDescription
                            {
                                // Mark as default if selector is throughput.
                                Default = selectors[g] is ThroughputHistoryStreamSelector,
                                Description = ThroughputDescription,
                                FormatIndex = ThroughputSelection,
                                GroupIndex = g,
                                Id = ThroughputId,
                                StreamType = StreamType.Video
                            });
                            break;

                        case StreamType.Video:
                            // One video stream.
                            descriptions[0].Default = true;
                            break;

                        case StreamType.Audio:
                            // Default audio = audio.streamcount - 1
                            descriptions[streamCount - 1].Default = true;
                            break;
                    }
                }
            }

            return descriptions;
        }

        public static IStreamSelector GetSelector(this StreamDescription track)
        {
            bool isAuto;
            using (Log.Scope($"Is auto selection: {isAuto = track.FormatIndex == ThroughputSelection}"))
                return isAuto
                    ? (IStreamSelector)new ThroughputHistoryStreamSelector(new ThroughputHistory())
                    : (IStreamSelector)new FixedStreamSelector(track.FormatIndex);
        }

        public static IEnumerable<StreamGroup> DumpStreamGroups(this IEnumerable<StreamGroup> groups)
        {
            using (Log.Scope())
            {
                foreach (var group in groups)
                {
                    Log.Debug($"Group: {group.ContentType} Entries: {group.Streams.Count}");
                    group.Streams.DumpStreamInfo();
                }

                return groups;
            }
        }

        public static IEnumerable<StreamDescription> DumpStreamDescriptions(this IEnumerable<StreamDescription> descriptions, string message = default)
        {
            using (Log.Scope(message))
            {
                foreach (var description in descriptions)
                    Log.Debug($"Stream: {description}");
                return descriptions;
            }
        }

        public static void DumpFormat(this Format format)
        {
            Log.Debug($"Id: {format.Id}");
            Log.Debug($"\tLabel: '{format.Label}'");
            Log.Debug($"\tSelection Flags: '{format.SelectionFlags}'");
            Log.Debug($"\tRole Flags: '{format.RoleFlags}'");
            Log.Debug($"\tBitrate: '{format.Bitrate}'");
            Log.Debug($"\tCodecs: '{format.Codecs}'");
            Log.Debug($"\tContainer MimeType: '{format.ContainerMimeType}'");
            Log.Debug($"\tSample MimeType: '{format.SampleMimeType}'");
            Log.Debug($"\tWxH: '{format.Width}x{format.Height}'");
            Log.Debug($"\tFrame Rate:'{format.FrameRate}'");
            Log.Debug($"\tSample Rate: '{format.SampleRate}'");
            Log.Debug($"\tChannel Count: '{format.ChannelCount}'");
            Log.Debug($"\tSample Rate: '{format.SampleRate}'");
            Log.Debug($"\tLanguage: '{format.Language}'");
            Log.Debug($"\tAccessibility Channel: '{format.AccessibilityChannel}'");
        }

        public static IEnumerable<StreamInfo> DumpStreamInfo(this IEnumerable<StreamInfo> streamInfos)
        {
            foreach (var info in streamInfos)
                info.Format.DumpFormat();

            return streamInfos;
        }
    }
}