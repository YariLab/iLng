using System;
using System.IO;
using System.Media;
using System.Threading;

namespace iLang
{
    internal sealed class SoundNotifier : IDisposable
    {
        private SoundPlayer _en;
        private SoundPlayer _ua;
        private SoundPlayer _other;
        private int _volumePercent = 100;
        private int _busy;

        public SoundNotifier()
        {
            RebuildPlayers(100);
        }

        public int VolumePercent
        {
            get { return _volumePercent; }
            set
            {
                int normalized = AppSettings.NormalizeVolume(value);
                if (normalized == _volumePercent && _en != null)
                {
                    return;
                }

                _volumePercent = normalized;
                RebuildPlayers(_volumePercent);
            }
        }

        public void Play(LayoutInfo info)
        {
            if (_volumePercent <= 0)
            {
                return;
            }

            if (Interlocked.Exchange(ref _busy, 1) == 1)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    SoundPlayer player;
                    if (info.LangId == 0x0409)
                    {
                        player = _en;
                    }
                    else if (info.LangId == 0x0422)
                    {
                        player = _ua;
                    }
                    else
                    {
                        player = _other;
                    }

                    if (player != null)
                    {
                        player.PlaySync();
                    }
                }
                catch
                {
                    // Ignore audio device issues; OSD still works.
                }
                finally
                {
                    Interlocked.Exchange(ref _busy, 0);
                }
            });
        }

        public void Dispose()
        {
            DisposePlayers();
        }

        private void RebuildPlayers(int volumePercent)
        {
            DisposePlayers();
            if (volumePercent <= 0)
            {
                return;
            }

            double gain = volumePercent / 100.0;
            _en = CreatePlayer(920, 70, gain);
            _ua = CreatePlayer(620, 90, gain);
            _other = CreatePlayer(760, 60, gain);
        }

        private static SoundPlayer CreatePlayer(int frequencyHz, int durationMs, double gain)
        {
            var player = new SoundPlayer(new MemoryStream(MakeTone(frequencyHz, durationMs, gain)));
            player.Load();
            return player;
        }

        private void DisposePlayers()
        {
            if (_en != null)
            {
                _en.Dispose();
                _en = null;
            }
            if (_ua != null)
            {
                _ua.Dispose();
                _ua = null;
            }
            if (_other != null)
            {
                _other.Dispose();
                _other = null;
            }
        }

        private static byte[] MakeTone(int frequencyHz, int durationMs, double gain)
        {
            const int sampleRate = 22050;
            int sampleCount = Math.Max(1, sampleRate * durationMs / 1000);
            int dataSize = sampleCount * 2;
            byte[] wav = new byte[44 + dataSize];

            WriteAscii(wav, 0, "RIFF");
            WriteInt32(wav, 4, 36 + dataSize);
            WriteAscii(wav, 8, "WAVE");
            WriteAscii(wav, 12, "fmt ");
            WriteInt32(wav, 16, 16);
            WriteInt16(wav, 20, 1);
            WriteInt16(wav, 22, 1);
            WriteInt32(wav, 24, sampleRate);
            WriteInt32(wav, 28, sampleRate * 2);
            WriteInt16(wav, 32, 2);
            WriteInt16(wav, 34, 16);
            WriteAscii(wav, 36, "data");
            WriteInt32(wav, 40, dataSize);

            double fadeSamples = Math.Min(sampleCount / 4.0, sampleRate * 0.012);
            double amplitude = 12000.0 * Math.Max(0.0, Math.Min(1.0, gain));
            for (int i = 0; i < sampleCount; i++)
            {
                double t = (double)i / sampleRate;
                double envelope = 1.0;
                if (i < fadeSamples)
                {
                    envelope = i / fadeSamples;
                }
                else if (i > sampleCount - fadeSamples)
                {
                    envelope = (sampleCount - i) / fadeSamples;
                }

                short sample = (short)(Math.Sin(2.0 * Math.PI * frequencyHz * t) * amplitude * envelope);
                int offset = 44 + i * 2;
                wav[offset] = (byte)(sample & 0xFF);
                wav[offset + 1] = (byte)((sample >> 8) & 0xFF);
            }

            return wav;
        }

        private static void WriteAscii(byte[] buffer, int offset, string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                buffer[offset + i] = (byte)text[i];
            }
        }

        private static void WriteInt16(byte[] buffer, int offset, short value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
