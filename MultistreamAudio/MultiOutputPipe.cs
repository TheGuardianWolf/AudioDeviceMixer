using AudioPipe.Audio;
using AudioPipe.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;

namespace MultistreamAudio
{
    /// <summary>
    /// Pipes audio from one audio device to another.
    /// </summary>
    public class MultiOutputAudioPipe : IDisposable
    {
        /// <summary>
        /// Default pipe latency in milliseconds.
        /// </summary>
        public const int DefaultLatency = 10;

        /// <summary>
        /// Minimum pipe latency in milliseconds.
        /// </summary>
        public const int MinLatency = 2;

        private WasapiLoopbackCapture inputCapture;
        private MultiChannelList<BufferedWaveProvider> waveBuffers;
        private MultiChannelList<MultiplexingWaveProvider> waveMultiplexers;

        /// <summary>
        /// To detect redundant <see cref="Dispose()"/> calls.
        /// </summary>
        private bool isDisposed;

        private bool previouslyMuted;
        private bool muteInputWhenPiped;
        private MultiChannelList<WasapiOut> outputDevices;

        /// <summary>
        /// Initializes a new instance of the <see cref="Pipe"/> class.
        /// </summary>
        /// <param name="input">Audio device from which to capture audio.</param>
        /// <param name="outputs">Audio devices to which the captured audio should be output.</param>
        /// <param name="latency">Latency of the pipe in milliseconds. </param>
        public MultiOutputAudioPipe(MMDevice input, MultiChannelList<MMDevice> outputs, int latency = DefaultLatency)
        {
            foreach (var output in outputs)
            {
                if (DeviceService.Equals(input, output))
                {
                    throw new ArgumentException($"{nameof(input)} and {nameof(output)} cannot both be {input.FriendlyName}");
                }
            }
            
            if (latency < MinLatency)
            {
                throw new ArgumentException("Latency is too low.", nameof(latency));
            }

            InputDevice = input;
            OutputDevices = outputs;

            try
            {
                inputCapture = new WasapiLoopbackCapture(InputDevice);
            }
            catch (COMException ex)
            {
                throw new PipeInitException(ex.HResult, InputDevice);
            }

            waveBuffers = new MultiChannelList<BufferedWaveProvider>(OutputDevices.Select(device => new BufferedWaveProvider(inputCapture.WaveFormat)).ToList());
            waveMultiplexers = new MultiChannelList<MultiplexingWaveProvider>(waveBuffers.Select(waveBuffer => new MultiplexingWaveProvider(new BufferedWaveProvider[] { waveBuffer }, 1)));

            waveMultiplexers.RightDevice.ConnectInputToOutput(0, 0);
            waveMultiplexers.LeftDevice.ConnectInputToOutput(1, 0);

            inputCapture.DataAvailable += (sender, e) =>
            {
                waveBuffers.AsParallel().ForAll(buffer => buffer.AddSamples(e.Buffer, 0, e.BytesRecorded));
            };

            try
            {
                outputDevices = new MultiChannelList<WasapiOut>(OutputDevices.AsParallel().Select(device => new WasapiOut(device, AudioClientShareMode.Shared, true, latency)).ToList());
                outputDevices.Zip(waveMultiplexers, (device, multiplexer) => new { Device=device, Multiplexer=multiplexer }).AsParallel().ForAll(z => z.Device.Init(z.Multiplexer));
            }
            catch (COMException ex)
            {
                throw new PipeInitException(ex.HResult, null);
            }
        }

        /// <summary>
        /// Gets the device from which audio is being captured.
        /// </summary>
        public MMDevice InputDevice { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the input device will be muted
        /// when the pipe is active.
        /// </summary>
        public bool MuteInputWhenPiped
        {
            get => muteInputWhenPiped;
            set
            {
                muteInputWhenPiped = value;
                if (PlaybackState == PlaybackState.Playing)
                {
                    InputDevice.AudioEndpointVolume.Mute = muteInputWhenPiped;
                }
            }
        }

        /// <summary>
        /// Gets the devices to which the captured audio is piped.
        /// </summary>
        public MultiChannelList<MMDevice> OutputDevices { get; }

        /// <summary>
        /// Gets the playback state of the pipe.
        /// </summary>
        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

        /// <inheritdoc/>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing).
            Dispose(true);
        }

        /// <summary>
        /// Begins piping audio from <see cref="InputDevice"/> to <see cref="OutputDevice"/>.
        /// Does nothing if the pipe is already started.
        /// </summary>
        public void Start()
        {
            if (isDisposed)
            {
                return;
            }

            if (PlaybackState != PlaybackState.Playing)
            {
                inputCapture.StartRecording();
                outputDevices.AsParallel().ForAll(device => device.Play());
                previouslyMuted = InputDevice.AudioEndpointVolume.Mute;
                InputDevice.AudioEndpointVolume.Mute = MuteInputWhenPiped;
                PlaybackState = PlaybackState.Playing;
            }
        }

        /// <summary>
        /// Stops piping audio from <see cref="InputDevice"/> to <see cref="OutputDevice"/>.
        /// Does nothing if the pipe is already stopped.
        /// </summary>
        public void Stop()
        {
            if (isDisposed)
            {
                return;
            }

            if (PlaybackState == PlaybackState.Playing)
            {
                outputDevices.AsParallel().ForAll(device => device.Stop());
                inputCapture.StopRecording();
                InputDevice.AudioEndpointVolume.Mute = previouslyMuted;
                PlaybackState = PlaybackState.Stopped;
            }
        }

        /// <summary>
        /// Frees managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing">Indicates whether the method was invoked by <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    Stop();

                    inputCapture?.Dispose();
                    inputCapture = null;

                    outputDevices.AsParallel().ForAll(device => device.Dispose());
                    outputDevices.Clear();
                }

                isDisposed = true;
            }
        }
    }
}
