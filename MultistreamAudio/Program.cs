using AudioPipe.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace MultistreamAudio
{
    class Program
    {
        static void Main(string[] args)
        {
            var exitEvent = new ManualResetEvent(false);
            

            var outputDevices = DeviceService.GetOutputDevices().ToList();
            var defaultOutputDevice = DeviceService.DefaultPlaybackDevice;
            Console.WriteLine("Devices available:");
            for (var i = 0; i < outputDevices.Count; i++)
            {
                var device = outputDevices[i];
                if (device.ID != defaultOutputDevice.ID)
                {
                    Console.WriteLine($"{i + 1} - {device.FriendlyName}");
                }
            }

            Console.WriteLine("Enter the id of the output device to assign as the left speaker.");
            var leftDeviceIndex = int.Parse(Console.ReadLine()) - 1;
            var leftDevice = outputDevices[leftDeviceIndex];

            Console.WriteLine("Enter the id of the output device to assign as the right speaker.");
            var rightDeviceIndex = int.Parse(Console.ReadLine()) - 1;
            var rightDevice = outputDevices[rightDeviceIndex];

            Console.WriteLine("Connecting processing pipeline...");
            var pipe = new MultiOutputAudioPipe(
                defaultOutputDevice, 
                new MultiChannelList<MMDevice>(
                    leftDevice,
                    rightDevice
                )
            );
            pipe.MuteInputWhenPiped = true;

            Console.CancelKeyPress += (sender, eventArgs) => {
                eventArgs.Cancel = true;
                exitEvent.Set();
                pipe.Stop();
                Console.WriteLine("Stopped pipeline.");
            };
            AppDomain.CurrentDomain.ProcessExit += new EventHandler((sender, eventArgs) =>
            {
                exitEvent.Set();
                pipe.Stop();
                Console.WriteLine("Stopped pipeline.");
            });

            Console.WriteLine("Pipeline connected.");
            pipe.Start();
            Console.WriteLine("Pipeline started.");

            exitEvent.WaitOne();
        }
    }
}
